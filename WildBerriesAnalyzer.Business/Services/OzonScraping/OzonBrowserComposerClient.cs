using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Parsing;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping;

/// <summary>
/// Composer-api через Chromium: один раз проходит Variti challenge, дальше fetch() из страницы.
/// </summary>
public sealed class OzonBrowserComposerClient : IOzonComposerClient
{
    private readonly OzonScrapingAuthOptions _auth;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _productSlots;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _challenged;

    /// <summary>
    /// После неудачного relaunch не крутим Chromium на каждом SKU — fail-fast до следующего WarmUp.
    /// </summary>
    private bool _sessionDead;
    private string? _sessionDeadReason;

    public OzonBrowserComposerClient(OzonScrapingAuthOptions auth)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        var slots = Math.Clamp(auth.ProductConcurrency, 1, 100);
        _productSlots = new SemaphoreSlim(slots, slots);
    }

    public async Task WarmUpAsync(CancellationToken ct = default)
    {
        _sessionDead = false;
        _sessionDeadReason = null;
        await EnsureReadyAsync(ct).ConfigureAwait(false);
    }

    public async Task<OzonComposerPage> FetchPageAsync(string sitePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sitePath))
        {
            throw new ArgumentException("sitePath is required", nameof(sitePath));
        }

        if (!sitePath.StartsWith('/'))
        {
            sitePath = "/" + sitePath;
        }

        if (_sessionDead)
        {
            throw new HttpRequestException(
                _sessionDeadReason ??
                "Ozon browser session is dead (antibot). WarmUp/cookie/proxy required.");
        }

        Exception? last = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await EnsureReadyAsync(ct).ConfigureAwait(false);

                if (IsSearchPath(sitePath))
                {
                    var fromHtml = await TryFetchSearchFromHtmlAsync(sitePath, ct).ConfigureAwait(false);
                    if (fromHtml is not null)
                    {
                        return fromHtml;
                    }

                    Console.WriteLine("[browser] HTML search empty — trying composer…");
                }

                try
                {
                    var json = await FetchJsonFromPageAsync(sitePath, ct).ConfigureAwait(false);
                    return DeserializePage(json);
                }
                catch (Exception ex) when (IsComposerForbidden(ex) && IsSearchPath(sitePath))
                {
                    throw new InvalidOperationException(
                        "Не удалось загрузить выдачу Ozon (антибот режет API и HTML). " +
                        "Обновите cookie в той же сессии, что и residential-прокси.",
                        ex);
                }
            }
            catch (Exception ex) when (attempt == 0 && IsHardSessionError(ex))
            {
                last = ex;
                Console.WriteLine($"[browser] session error, relaunch once: {ex.Message}");
                await ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt > 0 && IsHardSessionError(ex))
            {
                MarkSessionDead(ex.Message);
                await ShutdownAsync().ConfigureAwait(false);
                throw;
            }
        }

        if (last is not null && IsHardSessionError(last))
        {
            MarkSessionDead(last.Message);
        }

        throw last ?? new InvalidOperationException("Ozon browser fetch failed.");
    }

    public async Task<OzonComposerPage> FetchProductPageAsync(long sku, CancellationToken ct = default)
    {
        if (sku <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sku));
        }

        return await FetchProductByNavigationAsync(
                $"https://www.ozon.ru/product/{sku}/",
                expectedSku: sku,
                ct)
            .ConfigureAwait(false);
    }

    public async Task<OzonComposerPage> FetchProductByUrlAsync(string productUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl))
        {
            throw new ArgumentException("productUrl is required", nameof(productUrl));
        }

        var url = productUrl.Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url.TrimStart('/');
        }

        // ozon.ru → www.ozon.ru
        if (url.Contains("://ozon.ru/", StringComparison.OrdinalIgnoreCase))
        {
            url = url.Replace("://ozon.ru/", "://www.ozon.ru/", StringComparison.OrdinalIgnoreCase);
        }

        var expectedSku = OzonWidgetParser.SkuFromUrl(url);
        return await FetchProductByNavigationAsync(url, expectedSku, ct).ConfigureAwait(false);
    }

    public async Task<OzonComposerPage> FetchCartSharePageAsync(string shareToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shareToken))
        {
            throw new ArgumentException("shareToken is required", nameof(shareToken));
        }

        shareToken = shareToken.Trim();

        if (_sessionDead)
        {
            throw new HttpRequestException(
                _sessionDeadReason ??
                "Ozon browser session is dead (antibot). WarmUp/cookie/proxy required.");
        }

        // share-корзина не попадает в composer fetch с «прогретой» search-страницы — только после навигации.
        return await FetchCartShareByNavigationAsync(shareToken, ct).ConfigureAwait(false);
    }

    private async Task<OzonComposerPage> FetchCartShareByNavigationAsync(
        string shareToken,
        CancellationToken ct)
    {
        await EnsureReadyAsync(ct).ConfigureAwait(false);
        if (_context is null)
        {
            throw new InvalidOperationException("Browser context is not ready.");
        }

        var cartUrl = $"https://www.ozon.ru/cart?share={Uri.EscapeDataString(shareToken)}";
        var capturedJson = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var capturedResponses = new List<string>();
        IPage? worker = null;

        void OnResponse(object? sender, IResponse response)
        {
            _ = CaptureCartShareComposerResponseAsync(response, shareToken, capturedJson, capturedResponses);
        }

        try
        {
            worker = await _context.NewPageAsync().ConfigureAwait(false);
            worker.Response += OnResponse;

            var nav = await worker.GotoAsync(
                cartUrl,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 90_000
                }).ConfigureAwait(false);

            var status = (int?)nav?.Status;
            var title = await worker.TitleAsync().ConfigureAwait(false);
            if (IsBlockedTitle(title) || status is 403)
            {
                throw new HttpRequestException(
                    $"Ozon cart share page blocked (title=\"{title}\", http={status}).");
            }

            await worker.WaitForTimeoutAsync(2500).ConfigureAwait(false);

            try
            {
                await worker.WaitForSelectorAsync(
                    "[data-state], [data-widget], a[href*='/product/']",
                    new PageWaitForSelectorOptions { Timeout = 15_000 }).ConfigureAwait(false);
            }
            catch
            {
                // cart may render without stable selectors
            }

            await worker.WaitForTimeoutAsync(1500).ConfigureAwait(false);

            var composerSkus = await TryFetchCartShareSkusViaComposerAsync(worker, shareToken, ct)
                .ConfigureAwait(false);
            if (composerSkus.Count > 0)
            {
                Console.WriteLine($"[cart-share] post-nav composer skus={composerSkus.Count}");
                return BuildSyntheticCartSharePage(composerSkus);
            }

            composerSkus = await TryFetchCartShareViaActionEndpointsAsync(worker, shareToken, ct)
                .ConfigureAwait(false);
            if (composerSkus.Count > 0)
            {
                Console.WriteLine($"[cart-share] action endpoint skus={composerSkus.Count}");
                return BuildSyntheticCartSharePage(composerSkus);
            }

            await LogCartSharePageDiagnosticsAsync(worker, shareToken).ConfigureAwait(false);

            var htmlSkus = await TryExtractSkusNearShareTokenAsync(worker, shareToken).ConfigureAwait(false);
            if (htmlSkus.Count > 0)
            {
                Console.WriteLine($"[cart-share] HTML near share token skus={htmlSkus.Count}");
                return BuildSyntheticCartSharePage(htmlSkus);
            }

            var modalSkus = await TryExtractSkusFromOpenModalAsync(worker).ConfigureAwait(false);
            if (modalSkus.Count > 0)
            {
                Console.WriteLine($"[cart-share] openModal skus={modalSkus.Count}");
                return BuildSyntheticCartSharePage(modalSkus);
            }

            var shareTokenSkus = TryParseSkusFromShareResponses(capturedResponses, shareToken);
            if (shareTokenSkus.Count > 0)
            {
                Console.WriteLine($"[cart-share] share-token payload skus={shareTokenSkus.Count}");
                return BuildSyntheticCartSharePage(shareTokenSkus);
            }

            if (capturedJson.Task.IsCompletedSuccessfully)
            {
                var json = await capturedJson.Task.ConfigureAwait(false);
                var page = DeserializePage(json);
                if (OzonWidgetParser.ParseCartShareSkus(page).Count > 0)
                {
                    return page;
                }
            }
            else if (capturedResponses.Count > 0)
            {
                var capturedSkus = TrySelectBestSkusFromCapturedResponses(capturedResponses);
                if (capturedSkus.Count > 0)
                {
                    Console.WriteLine($"[cart-share] captured composer skus={capturedSkus.Count}");
                    return BuildSyntheticCartSharePage(capturedSkus);
                }

                OzonComposerPage? bestPage = null;
                var bestCount = int.MaxValue;

                foreach (var json in capturedResponses)
                {
                    try
                    {
                        var candidate = DeserializePage(json);
                        var count = OzonWidgetParser.ParseCartShareSkus(candidate).Count;
                        if (count > 0 && count < bestCount)
                        {
                            bestCount = count;
                            bestPage = candidate;
                        }
                    }
                    catch
                    {
                        // ignore broken composer payloads
                    }
                }

                if (bestPage is not null)
                {
                    return bestPage;
                }
            }

            var domSkus = await ExtractCartShareSkusFromDomAsync(worker).ConfigureAwait(false);
            var isAnonymous = await IsAnonymousCartShareSessionAsync(worker).ConfigureAwait(false);
            if (isAnonymous)
            {
                throw new InvalidOperationException(
                    "Общая корзина Ozon требует авторизованной сессии. " +
                    "Обновите cookie в ozon-scraping-auth.json (скопируйте из браузера, где вы залогинены на ozon.ru) " +
                    "и перезапустите Server.");
            }

            if (domSkus.Count == 0)
            {
                LogCartShareFailureDiagnostics(capturedResponses, shareToken);
                throw new InvalidOperationException(
                    "Не удалось извлечь товары из общей корзины Ozon (пустая ссылка или антибот).");
            }

            Console.WriteLine($"[cart-share] DOM skus={domSkus.Count}");
            return BuildSyntheticCartSharePage(domSkus);
        }
        finally
        {
            if (worker is not null)
            {
                worker.Response -= OnResponse;
                try
                {
                    await worker.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    private static OzonComposerPage BuildSyntheticCartSharePage(IReadOnlyList<long> domSkus)
    {
        var widgets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sku in domSkus)
        {
            widgets[$"cartItem-{sku}"] = JsonSerializer.Serialize(new
            {
                sku,
                link = $"https://www.ozon.ru/product/{sku}/"
            });
        }

        return new OzonComposerPage { WidgetStates = widgets };
    }

    private static async Task CaptureCartShareComposerResponseAsync(
        IResponse response,
        string shareToken,
        TaskCompletionSource<string> capturedJson,
        List<string> capturedResponses)
    {
        if (capturedJson.Task.IsCompleted)
        {
            return;
        }

        try
        {
            var url = response.Url;
            if (!url.Contains("composer-api", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (response.Status is < 200 or >= 300)
            {
                return;
            }

            var text = await response.TextAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text) ||
                !text.Contains("widgetStates", StringComparison.Ordinal))
            {
                return;
            }

            capturedResponses.Add(text);

            var encodedShare = Uri.EscapeDataString(shareToken);
            var isShareSpecific =
                url.Contains("share=", StringComparison.OrdinalIgnoreCase) ||
                url.Contains(encodedShare, StringComparison.OrdinalIgnoreCase) ||
                url.Contains("%2Fcart%3Fshare%3D", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("/cart", StringComparison.OrdinalIgnoreCase);

            if (isShareSpecific)
            {
                capturedJson.TrySetResult(text);
            }
        }
        catch
        {
            // ignore race / closed body
        }
    }

    private static async Task<List<long>> ExtractCartShareSkusFromDomAsync(IPage page)
    {
        try
        {
            var raw = await page.EvaluateAsync<string>(@"() => {
                const excluded = /tileGrid|skuShelf|searchResult|recommend|viewed|favorite|analog|banner|stories|webReview|webSale|tapTags|emptyCart|paginator|profileMenuAnonymous|horizontalMenu|verticalMenu|catalogMenu|navBar|Menu/i;
                const cartHint = /cart|split|share|basket|shared|rfbs/i;

                const hintBuckets = [];
                const otherBuckets = [];

                const addFromItems = (items, bucket) => {
                  if (!Array.isArray(items)) return;
                  for (const item of items) {
                    const rawSku = item?.sku?.id ?? item?.sku ?? item?.skuId ?? item?.offerId
                      ?? item?.productId ?? item?.itemId ?? item?.product?.id
                      ?? item?.cellTrackingInfo?.product?.id ?? item?.cellTrackingInfo?.sku;
                    const sku = Number(rawSku);
                    if (Number.isFinite(sku) && sku >= 10000000) {
                      bucket.add(sku);
                    }
                    const link = item?.action?.link || item?.link || '';
                    const m = String(link).match(/\/product\/(?:[^\/?#]*-)?(\d{8,})\/?/);
                    if (m) bucket.add(Number(m[1]));
                  }
                };

                const pushBucket = (id, bucket, target) => {
                  if (bucket.size > 0) {
                    target.push({ id, skus: [...bucket], hint: cartHint.test(id) });
                  }
                };

                for (const el of document.querySelectorAll('[data-state]')) {
                  const id = el.getAttribute('id') || el.getAttribute('data-widget') || '';
                  if (excluded.test(id)) continue;
                  try {
                    const state = JSON.parse(el.getAttribute('data-state') || '');
                    const bucket = new Set();
                    addFromItems(state.items, bucket);
                    if (Array.isArray(state.splits)) {
                      for (const split of state.splits) {
                        addFromItems(split.items, bucket);
                      }
                    }
                    if (Array.isArray(state.cartItems)) {
                      addFromItems(state.cartItems, bucket);
                    }
                    const target = cartHint.test(id) ? hintBuckets : otherBuckets;
                    pushBucket(id, bucket, target);
                  } catch { /* ignore */ }
                }

                for (const el of document.querySelectorAll('[data-widget]')) {
                  const widget = el.getAttribute('data-widget') || '';
                  if (excluded.test(widget)) continue;
                  const bucket = new Set();
                  for (const a of el.querySelectorAll('a[href*=""product/""]')) {
                    const href = a.getAttribute('href') || '';
                    const m = href.match(/\/product\/(?:[^\/?#]*-)?(\d{8,})\/?/);
                    if (m) bucket.add(Number(m[1]));
                  }
                  const target = cartHint.test(widget) ? hintBuckets : otherBuckets;
                  pushBucket(widget, bucket, target);
                }

                return JSON.stringify({ hintBuckets, otherBuckets });
            }").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("hintBuckets", out var hintEl) &&
                TrySelectDomBuckets(hintEl, out var hintSkus) &&
                hintSkus.Count > 0)
            {
                return hintSkus;
            }

            if (root.TryGetProperty("otherBuckets", out var otherEl) &&
                TrySelectDomBuckets(otherEl, out var otherSkus))
            {
                return otherSkus;
            }

            // legacy shape
            if (root.TryGetProperty("buckets", out var bucketsEl) &&
                TrySelectDomBuckets(bucketsEl, out var legacySkus))
            {
                return legacySkus;
            }

            return [];
        }
        catch
        {
            return [];
        }
    }

    private static bool TrySelectDomBuckets(JsonElement bucketsEl, out List<long> skus)
    {
        skus = [];
        if (bucketsEl.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var candidates = new List<(string WidgetKey, IReadOnlyCollection<long> Skus)>();
        foreach (var bucket in bucketsEl.EnumerateArray())
        {
            var id = bucket.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (!bucket.TryGetProperty("skus", out var skusEl) || skusEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var parsed = new List<long>();
            foreach (var skuEl in skusEl.EnumerateArray())
            {
                if (skuEl.TryGetInt64(out var sku) && IsPlausibleCartSku(sku))
                {
                    parsed.Add(sku);
                }
            }

            if (parsed.Count > 0)
            {
                candidates.Add((id, parsed));
            }
        }

        skus = OzonWidgetParser.SelectCartShareSkusFromBuckets(candidates);
        return skus.Count > 0;
    }

    private static List<long> TrySelectBestSkusFromCapturedResponses(IReadOnlyList<string> capturedResponses)
    {
        List<long> best = [];
        var bestCount = int.MaxValue;

        foreach (var json in capturedResponses)
        {
            try
            {
                var page = DeserializePage(json);
                var skus = OzonWidgetParser.ParseCartShareSkus(page);
                if (skus.Count > 0 && skus.Count < bestCount)
                {
                    bestCount = skus.Count;
                    best = skus;
                }
            }
            catch
            {
                // ignore
            }
        }

        return best;
    }

    private static List<long> TryParseSkusFromShareResponses(
        IReadOnlyList<string> capturedResponses,
        string shareToken)
    {
        if (capturedResponses.Count == 0 || string.IsNullOrWhiteSpace(shareToken))
        {
            return [];
        }

        List<long> best = [];
        var bestCount = int.MaxValue;

        foreach (var json in capturedResponses)
        {
            if (!json.Contains(shareToken, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var page = DeserializePage(json);
                var skus = OzonWidgetParser.ParseCartShareSkus(page);
                if (skus.Count > 0 && skus.Count < bestCount)
                {
                    bestCount = skus.Count;
                    best = skus;
                }
            }
            catch
            {
                // ignore
            }
        }

        return best;
    }

    private static bool IsPlausibleCartSku(long sku) =>
        sku is >= 10_000_000 and <= 999_999_999_999_999;

    private static async Task<List<long>> TryFetchCartShareSkusViaComposerAsync(
        IPage worker,
        string shareToken,
        CancellationToken ct)
    {
        _ = ct;
        var paths = new List<string>();

        try
        {
            var currentPath = new Uri(worker.Url).PathAndQuery;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                paths.Add(currentPath);
            }
        }
        catch
        {
            // ignore
        }

        paths.Add($"/cart?share={Uri.EscapeDataString(shareToken)}");

        List<long> best = [];
        var bestCount = int.MaxValue;

        foreach (var sitePath in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var skus = await FetchComposerSkusForSitePathAsync(worker, sitePath).ConfigureAwait(false);
            if (skus.Count > 0 && skus.Count < bestCount)
            {
                bestCount = skus.Count;
                best = skus;
            }
        }

        return best;
    }

    private static async Task<List<long>> FetchComposerSkusForSitePathAsync(IPage worker, string sitePath)
    {
        var apiUrl = "https://www.ozon.ru/api/composer-api.bx/page/json/v2?url=" +
                     Uri.EscapeDataString(sitePath);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    await worker.WaitForTimeoutAsync(1000).ConfigureAwait(false);
                }

                var resultJson = await worker.EvaluateAsync<string>(@"async (apiUrl) => {
                    const r = await fetch(apiUrl, {
                        headers: { accept: 'application/json' },
                        credentials: 'include',
                        referrer: location.href
                    });
                    const text = await r.text();
                    return JSON.stringify({ status: r.status, text });
                }", apiUrl).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(resultJson);
                var status = doc.RootElement.GetProperty("status").GetInt32();
                var text = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
                if (status is not (>= 200 and < 300) || string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine(
                        $"[cart-share] composer {sitePath} HTTP {status}, len={text.Length}");
                    continue;
                }

                var page = DeserializePage(text);
                var widgetKeys = page.WidgetStates?.Keys.ToList() ?? [];
                Console.WriteLine(
                    $"[cart-share] composer {sitePath} widget keys: {string.Join(", ", widgetKeys.Take(40))}");
                foreach (var (key, count) in OzonWidgetParser.DebugAllCartShareWidgets(page).Take(12))
                {
                    Console.WriteLine($"[cart-share] composer widget {key}: {count}");
                }

                var skus = OzonWidgetParser.ParseCartShareSkus(page);
                Console.WriteLine(
                    $"[cart-share] composer {sitePath} widgets={widgetKeys.Count}, skus={skus.Count}" +
                    (skus.Count > 0 ? $": {string.Join(",", skus)}" : string.Empty));
                if (skus.Count == 0)
                {
                    TryDumpCartShareComposer(text, sitePath);
                }
                else
                {
                    return skus;
                }
            }
            catch (Exception ex) when (
                ex.Message.Contains("Execution context was destroyed", StringComparison.OrdinalIgnoreCase) &&
                attempt < 2)
            {
                Console.WriteLine($"[cart-share] composer retry after navigation: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[cart-share] composer fetch failed ({sitePath}): {ex.Message}");
                break;
            }
        }

        return [];
    }

    private static async Task<List<long>> TryFetchCartShareViaActionEndpointsAsync(
        IPage worker,
        string shareToken,
        CancellationToken ct)
    {
        _ = ct;
        var encodedShare = Uri.EscapeDataString(shareToken);
        var encodedPath = Uri.EscapeDataString($"/cart?share={shareToken}");

        string[] composerUrls =
        [
            $"https://www.ozon.ru/api/composer-api.bx/page/json/v2?url={encodedPath}",
            $"https://www.ozon.ru/api/composer-api.bx/page/json/v1?url={encodedPath}",
            $"https://www.ozon.ru/api/composer-api.bx/page/json/v2?url={Uri.EscapeDataString($"/modal/sharedCart?share={shareToken}")}",
            $"https://www.ozon.ru/api/composer-api.bx/page/json/v2?url={Uri.EscapeDataString($"/cart/share/{shareToken}")}",
            $"https://www.ozon.ru/api/cart/share/{encodedShare}",
            $"https://www.ozon.ru/api/cart/v1/share/{encodedShare}"
        ];

        foreach (var apiUrl in composerUrls)
        {
            try
            {
                var resultJson = await worker.EvaluateAsync<string>(@"async (apiUrl) => {
                    const r = await fetch(apiUrl, {
                        headers: { accept: 'application/json' },
                        credentials: 'include',
                        referrer: location.href
                    });
                    const text = await r.text();
                    return JSON.stringify({ status: r.status, text: text.slice(0, 500000) });
                }", apiUrl).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(resultJson);
                var status = doc.RootElement.GetProperty("status").GetInt32();
                var text = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
                if (status is not (>= 200 and < 300) || string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (!text.Contains("widgetStates", StringComparison.Ordinal) &&
                    !text.Contains("\"sku\"", StringComparison.Ordinal) &&
                    !text.Contains("/product/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                List<long> skus;
                if (text.Contains("widgetStates", StringComparison.Ordinal))
                {
                    var page = DeserializePage(text);
                    skus = OzonWidgetParser.ParseCartShareSkus(page);
                }
                else
                {
                    skus = ExtractSkusFromLooseJson(text);
                }

                if (skus.Count > 0)
                {
                    Console.WriteLine($"[cart-share] endpoint hit {apiUrl} -> {skus.Count} skus");
                    return skus;
                }
            }
            catch
            {
                // try next endpoint
            }
        }

        return [];
    }

    private static List<long> ExtractSkusFromLooseJson(string json)
    {
        var skus = new HashSet<long>();
        foreach (Match match in CartShareLooseProductUrlRegex.Matches(json))
        {
            if (long.TryParse(match.Groups[1].Value, out var sku) && sku >= 10_000_000)
            {
                skus.Add(sku);
            }
        }

        foreach (Match match in CartShareLooseSkuRegex.Matches(json))
        {
            if (long.TryParse(match.Groups[1].Value, out var sku) && sku >= 10_000_000)
            {
                skus.Add(sku);
            }
        }

        return skus.OrderBy(x => x).ToList();
    }

    private static readonly Regex CartShareLooseSkuRegex = new(
        @"""(?:sku|skuId|offerId)""\s*:\s*""?(\d{8,16})""?",
        RegexOptions.Compiled);

    private static readonly Regex CartShareLooseProductUrlRegex = new(
        @"/product/(?:[^""\\/?#]*-)?(\d{8,16})/?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static async Task<bool> IsAnonymousCartShareSessionAsync(IPage worker)
    {
        try
        {
            return await worker.EvaluateAsync<bool>(@"() => {
                const hasAnonymous = !!document.querySelector('[data-widget=""profileMenuAnonymous""]');
                const hasEmptyCart = !!document.querySelector('[data-widget=""emptyCart""]');
                return hasAnonymous && hasEmptyCart;
            }").ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<List<long>> TryExtractSkusNearShareTokenAsync(IPage worker, string shareToken)
    {
        try
        {
            var raw = await worker.EvaluateAsync<string>(@"(shareToken) => {
                const html = document.body?.innerHTML || '';
                const idx = html.indexOf(shareToken);
                if (idx < 0) return '[]';
                const slice = html.slice(Math.max(0, idx - 12000), idx + 12000);
                const skus = new Set();
                const re = /-(\d{8,})\/?(?:\?|""|\\|\/)/g;
                let m;
                while ((m = re.exec(slice)) !== null) {
                  skus.add(Number(m[1]));
                }
                const reSku = /""sku""\s*:\s*(\d{8,16})/g;
                while ((m = reSku.exec(slice)) !== null) {
                  skus.add(Number(m[1]));
                }
                return JSON.stringify([...skus].filter(n => n >= 10000000));
            }", shareToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var skus = new List<long>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetInt64(out var sku) && IsPlausibleCartSku(sku))
                {
                    skus.Add(sku);
                }
            }

            return skus.Count <= 50 ? skus : [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task<List<long>> TryExtractSkusFromOpenModalAsync(IPage worker)
    {
        try
        {
            var raw = await worker.EvaluateAsync<string>(@"() => {
                const chunks = [];
                for (const el of document.querySelectorAll('[data-state]')) {
                  const id = el.id || '';
                  if (!/openModal|shared|share|cartSplit|split/i.test(id)) continue;
                  chunks.push(el.getAttribute('data-state') || '');
                }
                return JSON.stringify(chunks);
            }").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var skus = new HashSet<long>();
            foreach (var chunk in doc.RootElement.EnumerateArray())
            {
                var text = chunk.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foreach (var sku in ExtractSkusFromLooseJson(text))
                {
                    skus.Add(sku);
                }
            }

            var list = skus.OrderBy(x => x).ToList();
            return list.Count is > 0 and <= 50 ? list : [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task LogCartSharePageDiagnosticsAsync(IPage worker, string shareToken)
    {
        try
        {
            var raw = await worker.EvaluateAsync<string>(@"(shareToken) => JSON.stringify({
                url: location.href,
                title: document.title,
                hasShareToken: document.body?.innerHTML?.includes(shareToken) ?? false,
                stateIds: [...document.querySelectorAll('[data-state]')].map(e => e.id).filter(Boolean).slice(0, 30),
                widgets: [...document.querySelectorAll('[data-widget]')].map(e => e.getAttribute('data-widget')).filter(Boolean).slice(0, 30)
            })", shareToken).ConfigureAwait(false);

            Console.WriteLine($"[cart-share] page diag: {raw}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[cart-share] page diag failed: {ex.Message}");
        }
    }

    private static void LogCartShareFailureDiagnostics(IReadOnlyList<string> capturedResponses, string shareToken)
    {
        Console.WriteLine(
            $"[cart-share] diagnostics share={shareToken}, captured composer payloads={capturedResponses.Count}");

        for (var i = 0; i < capturedResponses.Count; i++)
        {
            try
            {
                var page = DeserializePage(capturedResponses[i]);
                var widgets = page.WidgetStates?.Count ?? 0;
                var skus = OzonWidgetParser.ParseCartShareSkus(page);
                Console.WriteLine($"[cart-share] payload #{i + 1}: widgets={widgets}, parsedSkus={skus.Count}");

                foreach (var (key, count) in OzonWidgetParser.DebugAllCartShareWidgets(page).Take(12))
                {
                    Console.WriteLine($"  widget {key}: {count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[cart-share] payload #{i + 1}: deserialize failed: {ex.Message}");
            }
        }
    }

    private async Task<OzonComposerPage> FetchProductByNavigationAsync(
        string productUrl,
        long? expectedSku,
        CancellationToken ct)
    {
        if (_sessionDead)
        {
            throw new HttpRequestException(
                _sessionDeadReason ??
                "Ozon browser session is dead (antibot). WarmUp/cookie/proxy required.");
        }

        await EnsureReadyAsync(ct).ConfigureAwait(false);
        await _productSlots.WaitAsync(ct).ConfigureAwait(false);
        IPage? worker = null;
        try
        {
            if (_context is null)
            {
                throw new InvalidOperationException("Browser context is not ready.");
            }

            worker = await _context.NewPageAsync().ConfigureAwait(false);
            return await NavigateProductAndParseAsync(productUrl, expectedSku, worker, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex.Message.Contains("Target closed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("has been closed", StringComparison.OrdinalIgnoreCase))
        {
            MarkSessionDead(ex.Message);
            throw;
        }
        finally
        {
            if (worker is not null)
            {
                try
                {
                    await worker.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
            }

            _productSlots.Release();
        }
    }

    private void MarkSessionDead(string reason)
    {
        _sessionDead = true;
        _sessionDeadReason =
            "Ozon antibot/composer 403 — сессия помечена мёртвой до следующего прогрева. " +
            reason;
        Console.WriteLine($"[browser] session marked dead: {reason}");
    }

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_sessionDead)
            {
                throw new HttpRequestException(
                    _sessionDeadReason ?? "Ozon browser session is dead.");
            }

            if (_page is not null && _challenged)
            {
                return;
            }

            await LaunchAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LaunchAsync(CancellationToken ct)
    {
        Console.WriteLine("[browser] launching Chromium (Playwright)…");
        var driverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        if (driverPath is not null && string.IsNullOrWhiteSpace(driverPath))
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
        }

        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);

        var launch = new BrowserTypeLaunchOptions
        {
            Headless = _auth.Headless,
            Channel = string.IsNullOrWhiteSpace(_auth.ChromeChannel) ? null : _auth.ChromeChannel.Trim(),
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--no-sandbox",
                "--disable-dev-shm-usage"
            ]
        };

        if (!string.IsNullOrWhiteSpace(_auth.ProxyUrl))
        {
            launch.Proxy = BuildPlaywrightProxy(_auth.ProxyUrl);
            Console.WriteLine($"[browser] proxy={MaskProxyUrl(_auth.ProxyUrl)}");
        }

        _browser = await _playwright.Chromium.LaunchAsync(launch).ConfigureAwait(false);

        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = _auth.UserAgent,
            Locale = "ru-RU",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["accept-language"] = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7"
            }
        };

        _context = await _browser.NewContextAsync(contextOptions).ConfigureAwait(false);

        if (_auth.HasCookie &&
            !_auth.Cookie.Contains("PASTE_OZON_COOKIE", StringComparison.OrdinalIgnoreCase))
        {
            var cookies = CookieHeaderParser.ToPlaywrightCookies(_auth.Cookie, "https://www.ozon.ru");
            if (cookies.Count > 0)
            {
                await _context.AddCookiesAsync(cookies).ConfigureAwait(false);
                Console.WriteLine(
                    $"[browser] injected cookies: {cookies.Count} (cookie header length={_auth.Cookie.Length})");
            }
        }

        _page = await _context.NewPageAsync().ConfigureAwait(false);
        Console.WriteLine("[browser] opening https://www.ozon.ru/ (anti-bot challenge)…");

        var response = await NavigateForAntibotAsync(
            _page,
            "https://www.ozon.ru/",
            initialTimeoutMs: 45_000,
            ct).ConfigureAwait(false);

        var title = await _page.TitleAsync().ConfigureAwait(false);
        var finalUrl = _page.Url;
        var status = (int?)response?.Status;
        Console.WriteLine($"[browser] title=\"{Truncate(title, 60)}\" url={finalUrl} http={status}");

        if (status == 407)
        {
            throw new InvalidOperationException(
                "Прокси отклонил авторизацию (HTTP 407). Проверьте login, password и port в proxyUrl — " +
                "они должны совпадать с расширением браузера, через которое открывается Ozon.");
        }

        if (IsBlockedTitle(title))
        {
            throw new InvalidOperationException(
                $"Антибот не пройден (title=\"{title}\", url={finalUrl}). " +
                "Обновите cookie после полной загрузки ozon.ru, поставьте headless=false или RU proxyUrl.");
        }

        if (IsSoftBlockedUrl(finalUrl) || status is 403 or 307)
        {
            Console.WriteLine(
                "[browser] soft antibot markers on homepage (__rr/abt_att/403) — checking composer…");
        }

        // Composer probe — желателен для поиска, но не блокируем PDP HTML-парсинг.
        try
        {
            await ProbeComposerAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[browser] composer probe failed (product HTML path still available): {ex.Message}");
        }

        // Держим вкладку на HTML-поиске — referrer/context лучше, чем голая главная с __rr.
        try
        {
            await NavigateForAntibotAsync(
                _page,
                "https://www.ozon.ru/search/?text=1&from_global=true",
                initialTimeoutMs: 30_000,
                ct).ConfigureAwait(false);
            await _page.WaitForTimeoutAsync(800).ConfigureAwait(false);
            Console.WriteLine("[browser] parked on search page");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[browser] park on search failed: {ex.Message}");
        }

        _challenged = true;
        _sessionDead = false;
        _sessionDeadReason = null;
        Console.WriteLine("[browser] challenge passed");
    }

    private async Task<IResponse?> NavigateForAntibotAsync(
        IPage page,
        string url,
        int initialTimeoutMs,
        CancellationToken ct)
    {
        IResponse? response = null;
        try
        {
            response = await page.GotoAsync(
                url,
                new PageGotoOptions
                {
                    // Variti-challenge часто не даёт domcontentloaded в headless — commit достаточно.
                    WaitUntil = WaitUntilState.Commit,
                    Timeout = initialTimeoutMs
                }).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[browser] navigation slow ({initialTimeoutMs}ms), waiting JS-challenge… url={url}");
        }

        ct.ThrowIfCancellationRequested();
        await page.WaitForTimeoutAsync(Math.Max(3000, _auth.ChallengeWaitMs)).ConfigureAwait(false);
        return response;
    }

    private static Proxy BuildPlaywrightProxy(string proxyUrl)
    {
        var trimmed = proxyUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return new Proxy { Server = trimmed };
        }

        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        var proxy = new Proxy { Server = $"{uri.Scheme}://{uri.Host}{port}" };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var colon = uri.UserInfo.IndexOf(':');
            if (colon >= 0)
            {
                proxy.Username = Uri.UnescapeDataString(uri.UserInfo[..colon]);
                proxy.Password = Uri.UnescapeDataString(uri.UserInfo[(colon + 1)..]);
            }
            else
            {
                proxy.Username = Uri.UnescapeDataString(uri.UserInfo);
            }
        }

        return proxy;
    }

    private static string MaskProxyUrl(string proxyUrl)
    {
        if (!Uri.TryCreate(proxyUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return proxyUrl;
        }

        return string.IsNullOrEmpty(uri.UserInfo)
            ? uri.GetLeftPart(UriPartial.Authority)
            : $"{uri.Scheme}://***:***@{uri.Host}:{uri.Port}";
    }

    private async Task ProbeComposerAsync(CancellationToken ct)
    {
        Console.WriteLine("[browser] probing composer via search…");
        try
        {
            var json = await FetchJsonFromPageAsync("/search/?text=тест&from_global=true", ct)
                .ConfigureAwait(false);
            _ = DeserializePage(json);
            Console.WriteLine("[browser] composer probe ok");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Composer-api недоступен после открытия главной (403/пустой JSON). " +
                "Сессия антибота недействительна — обновите abt_data/cookie или RU proxy. " +
                $"Детали: {ex.Message}",
                ex);
        }
    }

    private async Task<OzonComposerPage> NavigateProductAndParseAsync(
        string productUrl,
        long? expectedSku,
        IPage page,
        CancellationToken ct)
    {
        var capturedJson = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        long resolvedSku = expectedSku ?? 0;

        void OnResponse(object? sender, IResponse response)
        {
            _ = CaptureComposerResponseAsync(response, resolvedSku, capturedJson);
        }

        page.Response += OnResponse;
        try
        {
            var nav = await page.GotoAsync(
                productUrl,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 90_000
                }).ConfigureAwait(false);

            var status = (int?)nav?.Status;
            var title = await page.TitleAsync().ConfigureAwait(false);
            var finalUrl = page.Url;

            resolvedSku =
                expectedSku
                ?? OzonWidgetParser.SkuFromUrl(finalUrl)
                ?? 0;

            if (IsBlockedTitle(title) || status is 403)
            {
                throw new HttpRequestException(
                    $"Ozon product page blocked (title=\"{title}\", http={status}, url={finalUrl}).");
            }

            // Дождаться сети composer или HTML с ценой (+ рейтинг, если успеет).
            await page.WaitForTimeoutAsync(800).ConfigureAwait(false);

            // После редиректа короткой ссылки SKU часто появляется в URL.
            if (resolvedSku <= 0)
            {
                resolvedSku = OzonWidgetParser.SkuFromUrl(page.Url) ?? 0;
            }

            OzonComposerPage? lastDom = null;
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            waitCts.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                while (!capturedJson.Task.IsCompleted && !waitCts.IsCancellationRequested)
                {
                    if (resolvedSku <= 0)
                    {
                        resolvedSku = OzonWidgetParser.SkuFromUrl(page.Url) ?? 0;
                    }

                    var fromDom = await TryBuildPageFromDomAsync(page, resolvedSku).ConfigureAwait(false);
                    if (fromDom is not null)
                    {
                        lastDom = fromDom;
                        // Цена есть; если рейтинг уже тоже — можно возвращать сразу.
                        if (HasReviewMeta(fromDom))
                        {
                            return EnsureSkuOnPage(fromDom, resolvedSku, page.Url);
                        }
                    }

                    await Task.WhenAny(
                            capturedJson.Task,
                            Task.Delay(400, waitCts.Token))
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // timeout waiting for composer — fall through
            }

            if (capturedJson.Task.IsCompletedSuccessfully)
            {
                var json = await capturedJson.Task.ConfigureAwait(false);
                if (resolvedSku <= 0)
                {
                    resolvedSku = OzonWidgetParser.SkuFromUrl(page.Url) ?? 0;
                }

                return EnsureSkuOnPage(DeserializePage(json), resolvedSku, page.Url);
            }

            if (lastDom is not null)
            {
                if (resolvedSku <= 0)
                {
                    resolvedSku = OzonWidgetParser.SkuFromUrl(page.Url) ?? 0;
                }

                return EnsureSkuOnPage(lastDom, resolvedSku, page.Url);
            }

            if (resolvedSku <= 0)
            {
                resolvedSku = OzonWidgetParser.SkuFromUrl(page.Url) ?? 0;
            }

            var fallback = await TryBuildPageFromDomAsync(page, resolvedSku).ConfigureAwait(false);
            if (fallback is not null)
            {
                return EnsureSkuOnPage(fallback, resolvedSku, page.Url);
            }

            throw new HttpRequestException(
                $"Не удалось извлечь цену со страницы {productUrl} (composer/HTML).");
        }
        finally
        {
            page.Response -= OnResponse;
        }
    }

    private static OzonComposerPage EnsureSkuOnPage(OzonComposerPage page, long sku, string pageUrl)
    {
        if (sku <= 0)
        {
            sku = OzonWidgetParser.SkuFromUrl(pageUrl)
                  ?? OzonWidgetParser.SkuFromUrl(page.Seo?.Link?.FirstOrDefault()?.Href)
                  ?? 0;
        }

        if (sku <= 0 || page.WidgetStates is null)
        {
            return page;
        }

        var hasGallery = page.WidgetStates.Keys.Any(k =>
            k.StartsWith("webGallery", StringComparison.Ordinal));
        if (!hasGallery)
        {
            page.WidgetStates["webGallery-1-default-1"] = JsonSerializer.Serialize(new
            {
                sku,
                coverImage = string.Empty
            });
        }

        page.LayoutTrackingInfo = JsonSerializer.SerializeToElement(new
        {
            sku,
            currentPageUrl = pageUrl,
            pageType = "pdp"
        });

        if (page.Seo?.Link is null || page.Seo.Link.Count == 0)
        {
            page.Seo ??= new OzonSeo();
            page.Seo.Link =
            [
                new OzonSeoLink
                {
                    Rel = "canonical",
                    Href = pageUrl.Contains("/product/", StringComparison.OrdinalIgnoreCase)
                        ? pageUrl.Split('?', 2)[0]
                        : $"https://www.ozon.ru/product/{sku}/"
                }
            ];
        }

        return page;
    }

    private static async Task CaptureComposerResponseAsync(
        IResponse response,
        long sku,
        TaskCompletionSource<string> capturedJson)
    {
        try
        {
            if (capturedJson.Task.IsCompleted)
            {
                return;
            }

            var url = response.Url;
            if (!url.Contains("composer-api.bx/page/json", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // PDP или явный sku в query (sku=0 — любая product-страница, напр. после /t/).
            var looksLikePdp =
                url.Contains("/product/", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("%2Fproduct%2F", StringComparison.OrdinalIgnoreCase) ||
                (sku > 0 &&
                 url.Contains(sku.ToString(System.Globalization.CultureInfo.InvariantCulture),
                     StringComparison.Ordinal));

            if (!looksLikePdp)
            {
                return;
            }

            if (response.Status is < 200 or >= 300)
            {
                return;
            }

            var text = await response.TextAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text) ||
                !text.Contains("widgetStates", StringComparison.Ordinal))
            {
                return;
            }

            capturedJson.TrySetResult(text);
        }
        catch
        {
            // ignore race / closed body
        }
    }

    private static async Task<OzonComposerPage?> TryBuildPageFromDomAsync(IPage page, long sku)
    {
        try
        {
            var raw = await page.EvaluateAsync<string>(@"() => {
                const digPrice = (s) => {
                  if (s == null || s === '') return null;
                  const m = String(s).replace(/\u00a0/g, ' ').match(/(\d[\d\s]*)/);
                  return m ? m[1].replace(/\s+/g, '') : null;
                };
                const digNum = (s) => {
                  if (s == null || s === '') return null;
                  const n = Number(String(s).replace(',', '.').replace(/\s+/g, ''));
                  return Number.isFinite(n) ? n : null;
                };
                const digReviews = (s) => {
                  if (s == null || s === '') return null;
                  const m = String(s).replace(/\u00a0/g, ' ').match(/(\d[\d\s]*)/);
                  return m ? Number(m[1].replace(/\s+/g, '')) : null;
                };

                let price = null, name = '', image = '', rating = null, reviews = null;

                const takeRating = (r, c) => {
                  if (rating == null && r != null) rating = r;
                  if (reviews == null && c != null) reviews = c;
                };

                // 1) JSON-LD Product
                for (const el of document.querySelectorAll('script[type=""application/ld+json""]')) {
                  try {
                    const parsed = JSON.parse(el.textContent || '');
                    const items = Array.isArray(parsed) ? parsed : [parsed];
                    for (const j of items) {
                      if (!j) continue;
                      const type = j['@type'];
                      const isProduct = type === 'Product' || (Array.isArray(type) && type.includes('Product'));
                      if (!isProduct && !j.offers && !j.aggregateRating) continue;

                      if (!name && j.name) name = j.name;
                      if (!image) {
                        image = Array.isArray(j.image) ? (j.image[0] || '') : (j.image || '');
                      }
                      const offers = Array.isArray(j.offers) ? j.offers[0] : j.offers;
                      if (!price) price = digPrice(offers && (offers.price ?? offers.lowPrice));

                      const ar = j.aggregateRating;
                      if (ar) {
                        takeRating(
                          digNum(ar.ratingValue ?? ar.rating),
                          digReviews(ar.reviewCount ?? ar.ratingCount ?? ar.reviewsCount)
                        );
                      }
                    }
                  } catch { /* ignore */ }
                }

                const html = document.documentElement ? document.documentElement.innerHTML : '';

                // 2) Embedded composer widgets
                if (!price) {
                  const priceKey = html.indexOf('webPrice');
                  if (priceKey >= 0) {
                    const slice = html.slice(Math.max(0, priceKey - 50), priceKey + 800);
                    const card = slice.match(/""cardPrice""\s*:\s*""([^""]+)""/);
                    const pMatch = slice.match(/""price""\s*:\s*""([^""]+)""/);
                    price = digPrice((pMatch && pMatch[1]) || (card && card[1]));
                  }
                }

                const reviewKey = html.indexOf('webReviewProductScore');
                if (reviewKey >= 0) {
                  const slice = html.slice(reviewKey, reviewKey + 500);
                  const ts = slice.match(/""totalScore""\s*:\s*([0-9]+(?:[.,][0-9]+)?)/);
                  const sc = slice.match(/""score""\s*:\s*([0-9]+(?:[.,][0-9]+)?)/);
                  const rc = slice.match(/""reviewsCount""\s*:\s*(\d+)/);
                  takeRating(
                    digNum((ts && ts[1]) || (sc && sc[1])),
                    rc ? Number(rc[1]) : null
                  );
                }

                const singleKey = html.indexOf('webSingleProductScore');
                if (singleKey >= 0 && (rating == null || reviews == null)) {
                  const slice = html.slice(singleKey, singleKey + 300);
                  const text = slice.match(/""text""\s*:\s*""([^""]+)""/);
                  if (text) {
                    const decoded = text[1].replace(/\\u([0-9a-fA-F]{4})/g, (_, h) =>
                      String.fromCharCode(parseInt(h, 16)));
                    const rm = decoded.match(/(\d+[.,]\d+)/);
                    const cm = decoded.match(/(\d[\d\s]*)\s*(?:отзыв|review)/i);
                    takeRating(
                      rm ? digNum(rm[1]) : null,
                      cm ? digReviews(cm[1]) : null
                    );
                  }
                }

                // 3) Visible DOM widgets
                if (!price) {
                  const widget = document.querySelector('[data-widget=""webPrice""]');
                  if (widget) price = digPrice(widget.innerText || widget.textContent || '');
                }

                if (rating == null || reviews == null) {
                  const scoreWidget =
                    document.querySelector('[data-widget=""webReviewProductScore""]') ||
                    document.querySelector('[data-widget=""webSingleProductScore""]') ||
                    document.querySelector('[data-widget=""webProductHeading""]');
                  if (scoreWidget) {
                    const t = scoreWidget.innerText || scoreWidget.textContent || '';
                    const rm = t.match(/(\d+[.,]\d+)/);
                    const cm = t.match(/(\d[\d\s]*)\s*(?:отзыв|review)/i);
                    takeRating(
                      rm ? digNum(rm[1]) : null,
                      cm ? digReviews(cm[1]) : null
                    );
                  }
                }

                if (!name) name = document.title || '';
                if (!price) return null;

                return JSON.stringify({
                  price,
                  name,
                  image: image || '',
                  rating,
                  reviews
                });
            }").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var priceText = root.TryGetProperty("price", out var pe) ? pe.GetString() : null;
            if (string.IsNullOrWhiteSpace(priceText) ||
                !decimal.TryParse(priceText, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var priceValue) ||
                priceValue <= 0)
            {
                return null;
            }

            var name = root.TryGetProperty("name", out var ne) ? ne.GetString() : null;
            var image = root.TryGetProperty("image", out var ie) ? ie.GetString() : null;
            double? rating = null;
            int? reviews = null;
            if (root.TryGetProperty("rating", out var re) &&
                re.ValueKind is JsonValueKind.Number &&
                re.TryGetDouble(out var ratingValue) &&
                ratingValue > 0)
            {
                rating = ratingValue;
            }

            if (root.TryGetProperty("reviews", out var rve) &&
                rve.ValueKind is JsonValueKind.Number &&
                rve.TryGetInt32(out var reviewsValue) &&
                reviewsValue >= 0)
            {
                reviews = reviewsValue;
            }

            // Синтетический composer-ответ — тот же путь, что и PDP-парсер.
            var priceRub = $"{decimal.Truncate(priceValue)} ₽";
            var widgets = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["webPrice-1-default-1"] = JsonSerializer.Serialize(new
                {
                    isAvailable = true,
                    price = priceRub,
                    cardPrice = priceRub
                }),
                ["webGallery-1-default-1"] = JsonSerializer.Serialize(new
                {
                    sku,
                    coverImage = image ?? string.Empty
                }),
                ["webProductHeading-1-default-1"] = JsonSerializer.Serialize(new
                {
                    title = string.IsNullOrWhiteSpace(name) ? $"SKU {sku}" : name
                })
            };

            if (rating is > 0 || reviews is >= 0)
            {
                widgets["webReviewProductScore-1-default-1"] = JsonSerializer.Serialize(new
                {
                    totalScore = rating,
                    reviewsCount = reviews,
                    itemId = sku,
                    score = rating
                });

                if (rating is > 0 && reviews is >= 0)
                {
                    widgets["webSingleProductScore-1-default-1"] = JsonSerializer.Serialize(new
                    {
                        text = $"{rating.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} • {reviews} отзывов"
                    });
                }
            }

            return new OzonComposerPage
            {
                WidgetStates = widgets,
                Seo = new OzonSeo
                {
                    Title = name,
                    Link =
                    [
                        new OzonSeoLink
                        {
                            Rel = "canonical",
                            Href = $"https://www.ozon.ru/product/{sku}/"
                        }
                    ]
                },
                LayoutTrackingInfo = JsonSerializer.SerializeToElement(new
                {
                    sku,
                    currentPageUrl = $"https://www.ozon.ru/product/{sku}/",
                    pageType = "pdp"
                })
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool HasReviewMeta(OzonComposerPage page)
    {
        if (page.WidgetStates is null)
        {
            return false;
        }

        return page.WidgetStates.Keys.Any(k =>
            k.StartsWith("webReviewProductScore", StringComparison.Ordinal) ||
            k.StartsWith("webSingleProductScore", StringComparison.Ordinal));
    }

    private async Task<string> FetchJsonFromPageAsync(string sitePath, CancellationToken ct)
    {
        if (_page is null)
        {
            throw new InvalidOperationException("Browser page is not ready.");
        }

        var apiUrl = "https://www.ozon.ru/api/composer-api.bx/page/json/v2?url=" +
                     Uri.EscapeDataString(sitePath);

        Exception? last = null;
        // Не ходим навигацией на composer URL: при soft-antibot это стабильный 403.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var resultJson = await _page.EvaluateAsync<string>(@"async (apiUrl) => {
                    try {
                        const r = await fetch(apiUrl, {
                            headers: { accept: 'application/json' },
                            credentials: 'include',
                            referrer: location.href
                        });
                        const text = await r.text();
                        return JSON.stringify({ status: r.status, text, error: null });
                    } catch (e) {
                        return JSON.stringify({
                            status: 0,
                            text: '',
                            error: String(e && e.message ? e.message : e)
                        });
                    }
                }", apiUrl).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(resultJson);
                var status = doc.RootElement.GetProperty("status").GetInt32();
                var text = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
                var fetchError = doc.RootElement.TryGetProperty("error", out var errEl)
                    ? errEl.GetString()
                    : null;

                if (status is >= 200 and < 300 && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (status is 0 or 403 or 307)
                {
                    last = new HttpRequestException(
                        string.IsNullOrWhiteSpace(fetchError)
                            ? $"Ozon browser fetch HTTP {status} (session expired / antibot)."
                            : $"Ozon browser fetch failed: {fetchError}");
                    Console.WriteLine(
                        $"[browser] fetch HTTP {status} {fetchError} (attempt {attempt}/2), backoff…");
                    await Task.Delay(TimeSpan.FromMilliseconds(800 * attempt), ct)
                        .ConfigureAwait(false);
                    continue;
                }

                var preview = text.Length > 300 ? text[..300] : text;
                throw new HttpRequestException($"Ozon browser fetch HTTP {status}. Body: {preview}");
            }
            catch (Exception ex) when (
                attempt < 2 &&
                IsComposerForbidden(ex))
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(800 * attempt), ct)
                    .ConfigureAwait(false);
            }
        }

        throw last ?? new HttpRequestException("Ozon browser fetch failed after retries.");
    }

    private static void TryDumpCartShareComposer(string body, string sitePath)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "ozon-cart-share-last.json");
            File.WriteAllText(path, body);
            Console.WriteLine($"[cart-share] dumped composer {sitePath} -> {path} ({body.Length} chars)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[cart-share] dump failed: {ex.Message}");
        }
    }

    private static OzonComposerPage DeserializePage(string body)
    {
        var page = OzonJson.Deserialize<OzonComposerPage>(body)
                   ?? throw new InvalidOperationException("Не удалось десериализовать ответ composer-api.");

        if (page.WidgetStates is null || page.WidgetStates.Count == 0)
        {
            throw new InvalidOperationException(
                "Ответ composer-api без widgetStates — challenge/сессия недействительны.");
        }

        return page;
    }

    private static bool IsSearchPath(string sitePath) =>
        sitePath.Contains("/search", StringComparison.OrdinalIgnoreCase);

    private static bool IsComposerForbidden(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("HTTP 403", StringComparison.Ordinal) ||
               msg.Contains("HTTP 307", StringComparison.Ordinal) ||
               msg.Contains("HTTP 0", StringComparison.Ordinal) ||
               msg.Contains("Failed to fetch", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("TypeError", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("composer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Браузер реально умер. Composer 403 при живой HTML-главной — не это.
    /// </summary>
    private static bool IsHardSessionError(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("Target closed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("has been closed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("session is dead", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("нет соединения", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("Browser context is not ready", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSessionError(Exception ex) =>
        IsHardSessionError(ex) || IsComposerForbidden(ex);

    private async Task<OzonComposerPage?> TryFetchSearchFromHtmlAsync(string sitePath, CancellationToken ct)
    {
        if (_page is null)
        {
            return null;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var url = "https://www.ozon.ru" + sitePath;
            Console.WriteLine($"[browser] HTML search: {url}");
            await NavigateForAntibotAsync(_page, url, initialTimeoutMs: 30_000, ct).ConfigureAwait(false);
            await _page.WaitForTimeoutAsync(1500).ConfigureAwait(false);

            if (IsBlockedTitle(await _page.TitleAsync().ConfigureAwait(false)))
            {
                return null;
            }

            var raw = await _page.EvaluateAsync<string>(@"() => {
                const items = [];
                const seen = new Set();
                const links = document.querySelectorAll('a[href*=""/product/""]');
                for (const a of links) {
                  const href = a.getAttribute('href') || '';
                  const m = href.match(/\/product\/(?:[^\/?#]*-)?(\d{8,})\/?/);
                  if (!m) continue;
                  const sku = Number(m[1]);
                  if (!Number.isFinite(sku) || sku < 10000000 || seen.has(sku)) continue;
                  seen.add(sku);
                  const card = a.closest('[data-widget], article') || a.parentElement || a;
                  const text = (card.innerText || a.innerText || '')
                    .replace(/\u00a0/g, ' ')
                    .split('\n')
                    .map(s => s.trim())
                    .filter(Boolean);
                  const name = a.getAttribute('title')
                    || text.find(t => t.length > 18 && !t.includes('₽'))
                    || '';
                  const priceLine = text.find(t => t.includes('₽')) || '';
                  const pm = priceLine.match(/(\d[\d\s]{1,})\s*₽/);
                  const img = card.querySelector('img');
                  items.push({
                    sku,
                    name,
                    href,
                    price: pm ? pm[1].replace(/\s+/g, '') : null,
                    image: img ? (img.getAttribute('src') || img.getAttribute('data-src') || '') : ''
                  });
                }
                return JSON.stringify(items);
            }").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw) || raw == "[]")
            {
                Console.WriteLine("[browser] HTML search: 0 tiles");
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            var tiles = new List<OzonSearchTileItem>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var sku = el.TryGetProperty("sku", out var skuEl) ? skuEl.GetInt64() : 0;
                if (sku < 10_000_000)
                {
                    continue;
                }

                var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var href = el.TryGetProperty("href", out var hrefEl) ? hrefEl.GetString() : null;
                var priceText = el.TryGetProperty("price", out var priceEl) ? priceEl.GetString() : null;
                var image = el.TryGetProperty("image", out var imgEl) ? imgEl.GetString() : null;

                var mainState = new List<OzonMainStateBlock>
                {
                    new()
                    {
                        Id = "name",
                        Type = "textDS",
                        TextDs = new OzonTextDs { Text = name }
                    }
                };
                if (!string.IsNullOrWhiteSpace(priceText))
                {
                    mainState.Add(new OzonMainStateBlock
                    {
                        Type = "priceV2",
                        PriceV2 = new OzonPriceV2Block
                        {
                            Price =
                            [
                                new OzonPriceText { Text = priceText + " ₽", TextStyle = "PRICE" }
                            ]
                        }
                    });
                }

                tiles.Add(new OzonSearchTileItem
                {
                    Sku = sku,
                    Id = sku,
                    Action = new OzonTileAction { Link = href },
                    TileImage = string.IsNullOrWhiteSpace(image)
                        ? null
                        : new OzonTileImage { CoverImage = image },
                    MainState = mainState
                });
            }

            if (tiles.Count == 0)
            {
                Console.WriteLine("[browser] HTML search: tiles parsed=0");
                return null;
            }

            Console.WriteLine($"[browser] HTML search: tiles={tiles.Count}");
            var grid = new OzonTileGridWidget { Items = tiles, Page = 1 };
            return new OzonComposerPage
            {
                WidgetStates = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["tileGridDesktop-1-default-1"] = OzonJson.Serialize(grid)
                }
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[browser] HTML search failed: {ex.Message}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsBlockedTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) &&
        (title.Contains("antibot", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("доступ ограничен", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("нет соединения", StringComparison.OrdinalIgnoreCase));

    private static bool IsSoftBlockedUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.Contains("antibot", StringComparison.OrdinalIgnoreCase) ||
         url.Contains("challenge", StringComparison.OrdinalIgnoreCase) ||
         url.Contains("abt_att=", StringComparison.OrdinalIgnoreCase) ||
         url.Contains("__rr=", StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

    private async Task ShutdownAsync()
    {
        _challenged = false;
        try { if (_page is not null) await _page.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
        try { if (_context is not null) await _context.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
        try { if (_browser is not null) await _browser.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
        _playwright?.Dispose();
        _page = null;
        _context = null;
        _browser = null;
        _playwright = null;
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
        _gate.Dispose();
        _productSlots.Dispose();
    }
}
