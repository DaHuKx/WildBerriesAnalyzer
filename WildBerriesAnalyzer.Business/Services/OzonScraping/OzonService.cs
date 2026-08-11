using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
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
    public Task<List<WbProduct>> ParseProductsAsync(string name) =>
        SearchByNameAsync(name, _auth.SearchLimit);

    /// <inheritdoc />
    public async Task<List<WbProduct>> GetProductsForIdsAsync(IEnumerable<string> ids)
    {
        var skus = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => long.TryParse(id, out _))
            .Select(long.Parse)
            .Distinct()
            .ToList();

        if (skus.Count == 0)
        {
            return new List<WbProduct>();
        }

        var result = new List<WbProduct>(skus.Count);
        var delay = Math.Max(0, _auth.RequestDelayMs);

        foreach (var sku in skus)
        {
            var product = await GetByIdAsync(sku).ConfigureAwait(false);
            if (product is not null)
            {
                result.Add(product);
            }

            if (delay > 0)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        return result;
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

                existing.Rating = scraped.Rating;
                existing.ReviewRating = scraped.ReviewRating;
                existing.FeedBacksCount = scraped.FeedBacksCount;
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

            var page = await _client.FetchPageAsync(path, ct).ConfigureAwait(false);
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

        var page = await _client.FetchPageAsync($"/product/{sku}/", ct).ConfigureAwait(false);
        var product = OzonProductMapper.FromProductPage(page);

        if (product.IdInMarket <= 0 ||
            (product.PriceFromInit?.Price <= 0 && string.IsNullOrWhiteSpace(product.ImageUrl)))
        {
            return product.IdInMarket > 0 ? product : null;
        }

        return product;
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync() => await _client.DisposeAsync().ConfigureAwait(false);
}
