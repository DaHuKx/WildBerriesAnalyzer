using System.Collections.Concurrent;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Parsing;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping;

/// <summary>
/// Парсинг витрины Ozon → <see cref="WbProduct"/> (IParseService).
/// </summary>
public sealed class OzonService : IOzonService
{
    private readonly OzonScrapingAuthOptions _auth;
    private readonly IOzonComposerClient _client;

    public OzonService()
        : this(OzonScrapingAuthOptions.CreateDefault())
    {
    }

    public OzonService(OzonScrapingAuthOptions auth)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _client = auth.UseBrowser
            ? new OzonBrowserComposerClient(auth)
            : new OzonComposerClient(auth);
    }

    /// <inheritdoc />
    public Task WarmUpAsync(CancellationToken ct = default) =>
        _client.WarmUpAsync(ct);

    /// <inheritdoc />
    public async Task<List<string>> GetArticlesFromCartShareAsync(
        string shareToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shareToken))
        {
            throw new ArgumentException("shareToken пустой.", nameof(shareToken));
        }

        shareToken = shareToken.Trim();
        OzonComposerPage page;
        try
        {
            page = await _client.FetchCartSharePageAsync(shareToken, ct).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Не удалось загрузить общую корзину Ozon (антибот / сессия).",
                ex);
        }

        var skus = OzonWidgetParser.ParseCartShareSkus(page);
        if (skus.Count == 0)
        {
            throw new InvalidOperationException(
                "Общая корзина Ozon пуста или ссылка недействительна.");
        }

        var articles = skus
            .Select(sku => sku.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        Console.WriteLine($"[cart-share] articles: {string.Join(",", articles)}");
        return articles;
    }

    /// <inheritdoc />
    public Task<List<WbProduct>> ParseProductsAsync(string name) =>
        SearchByNameAsync(name, _auth.SearchLimit);

    /// <inheritdoc />
    public async Task<List<WbProduct>> GetProductsForIdsAsync(IEnumerable<string> ids)
    {
        var refs = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Select(id =>
                ProductHelper.TryNormalizeOzonProductRef(id, out var normalized)
                    ? normalized
                    : id)
            .Where(id =>
                long.TryParse(id, out _) ||
                ProductHelper.IsOzonProductUrl(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (refs.Count == 0)
        {
            return new List<WbProduct>();
        }

        var concurrency = Math.Clamp(_auth.ProductConcurrency, 1, 100);
        var result = new ConcurrentBag<WbProduct>();
        using var linkedCts = new CancellationTokenSource();
        var sessionFailed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = linkedCts.Token
        };

        try
        {
            await Parallel.ForEachAsync(refs, parallelOptions, async (productRef, ct) =>
            {
                try
                {
                    var product = await GetByProductRefAsync(productRef, ct).ConfigureAwait(false);
                    if (product is not null)
                    {
                        result.Add(product);
                    }
                    else
                    {
                        Console.WriteLine($"[ozon] product miss: {productRef}");
                    }

                    // Пауза только в однопоточном режиме.
                    if (concurrency == 1)
                    {
                        var delay = Math.Max(0, _auth.RequestDelayMs);
                        if (delay > 0)
                        {
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // отмена из-за session fail / внешнего ct
                }
                catch (Exception ex) when (IsFatalBrowserFailure(ex))
                {
                    if (Interlocked.Exchange(ref sessionFailed, 1) == 0)
                    {
                        linkedCts.Cancel();
                        if (result.IsEmpty)
                        {
                            throw;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine($"[ozon] product miss: {productRef}");
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (sessionFailed == 1 && !result.IsEmpty)
        {
            // частичный успех при падении сессии
        }

        if (sessionFailed == 1 && result.IsEmpty)
        {
            throw new HttpRequestException(
                "Ozon session failed while loading product batch (antibot / dead session).");
        }

        return result.ToList();
    }

    private static bool IsFatalBrowserFailure(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("session is dead", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("session marked dead", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("мёртв", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("Target closed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("has been closed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("Browser context is not ready", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ParseProductsPricesResult> ParseProductsPricesAsync(IEnumerable<WbProduct> products)
    {
        try
        {
            var productsList = products as IList<WbProduct> ?? products.ToList();
            if (productsList.Count == 0)
            {
                return new ParseProductsPricesResult();
            }

            var byMarketId = productsList
                .GroupBy(p => p.IdInMarket)
                .ToDictionary(g => g.Key, g => g.First());

            var scrapedProducts = await GetProductsForIdsAsync(
                    byMarketId.Keys.Select(id => id.ToString()))
                .ConfigureAwait(false);

            if (scrapedProducts.Count == 0)
            {
                return ParseProductsPricesResult.Failed(
                    "Ozon вернул пустой ответ по батчу товаров (возможны антибот / протухшая сессия).",
                    isAuthFailure: true);
            }

            var prices = new List<WbPrice>();
            var refreshed = new List<WbProduct>();

            foreach (var scraped in scrapedProducts)
            {
                if (!byMarketId.TryGetValue(scraped.IdInMarket, out var existing))
                {
                    continue;
                }

                if (scraped.Rating > 0)
                {
                    existing.Rating = scraped.Rating;
                    existing.ReviewRating = scraped.ReviewRating;
                }

                if (scraped.FeedBacksCount > 0)
                {
                    existing.FeedBacksCount = scraped.FeedBacksCount;
                }

                existing.IsAdult = scraped.IsAdult;
                existing.MarketType = MarketType.Ozon;
                refreshed.Add(existing);

                if (scraped.PriceFromInit is null)
                {
                    continue;
                }

                scraped.PriceFromInit.ProductId = existing.Id;
                prices.Add(scraped.PriceFromInit);
            }

            return new ParseProductsPricesResult
            {
                Success = true,
                Prices = prices,
                ProductsWithRefreshedMeta = refreshed
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return ParseProductsPricesResult.Failed(ex.Message, isAuthFailure: true);
        }
        catch (HttpRequestException ex)
        {
            var authLike = ex.Message.Contains("403", StringComparison.Ordinal) ||
                           ex.Message.Contains("307", StringComparison.Ordinal) ||
                           ex.Message.Contains("антибот", StringComparison.OrdinalIgnoreCase);
            return ParseProductsPricesResult.Failed(
                ex.Message,
                isAuthFailure: authLike,
                isNetworkFailure: !authLike);
        }
        catch (Exception ex)
        {
            return ParseProductsPricesResult.Failed(ex.Message);
        }
    }

    public async Task<List<WbProduct>> SearchByNameAsync(
        string query,
        int limit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("query is required", nameof(query));
        }

        if (limit <= 0)
        {
            return new List<WbProduct>();
        }

        var path = $"/search/?text={Uri.EscapeDataString(query.Trim())}&from_global=true";
        var collected = new List<WbProduct>();
        var seen = new HashSet<long>();
        var delay = Math.Max(0, _auth.RequestDelayMs);
        const int maxPages = 40;

        for (var pageIndex = 1; pageIndex <= maxPages && collected.Count < limit; pageIndex++)
        {
            ct.ThrowIfCancellationRequested();

            OzonComposerPage page;
            try
            {
                page = await _client.FetchPageAsync(path, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var detail = ex.GetBaseException().Message;
                throw new InvalidOperationException(
                    $"Не удалось загрузить выдачу Ozon: {detail}. " +
                    "Если в логе HTTP 407 — проверьте proxyUrl; если 403/0 tiles — обновите cookie через тот же прокси.",
                    ex);
            }

            var batch = OzonProductMapper.FromSearchPage(page, limit - collected.Count);

            foreach (var product in batch)
            {
                if (!seen.Add(product.IdInMarket))
                {
                    continue;
                }

                collected.Add(product);
                if (collected.Count >= limit)
                {
                    break;
                }
            }

            if (collected.Count >= limit)
            {
                break;
            }

            var next = OzonWidgetParser.GetNextSearchPagePath(page);
            if (string.IsNullOrWhiteSpace(next) ||
                string.Equals(next, path, StringComparison.Ordinal))
            {
                break;
            }

            path = next;
            if (delay > 0)
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        return collected;
    }

    public async Task<WbProduct?> GetByIdAsync(long sku, CancellationToken ct = default)
    {
        if (sku <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sku));
        }

        return await GetByProductRefAsync(
                sku.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Карточка по SKU или URL товара (в т.ч. короткие https://ozon.ru/t/…).
    /// Тот же алгоритм, что и обновление цен: навигация на страницу + разбор HTML/сети.
    /// </summary>
    public async Task<WbProduct?> GetByProductRefAsync(string productRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productRef))
        {
            throw new ArgumentException("productRef is required", nameof(productRef));
        }

        var page = long.TryParse(productRef.Trim(), out var sku)
            ? await _client.FetchProductPageAsync(sku, ct).ConfigureAwait(false)
            : await _client.FetchProductByUrlAsync(productRef.Trim(), ct).ConfigureAwait(false);

        var product = OzonProductMapper.FromProductPage(page);
        return product.IdInMarket > 0 ? product : null;
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync() => await _client.DisposeAsync().ConfigureAwait(false);
}
