using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Services.OzonScraping;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Parsing;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace OzonConsole;

internal static class Program
{
    // ═══════════════════════════════════════════════════════════════════════════
    // ПАРАМЕТРЫ ТЕСТА — заполните перед live-запуском
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// true  = live-запросы к Ozon (нужны Cookie в ozon-scraping-auth.json).
    /// false = офлайн-разбор Fixtures/*.json (без сети, для проверки парсеров).
    /// Переопределение: dotnet run -- --live | --offline
    /// </summary>
    private const bool UseLiveHttp = true;

    /// <summary>Поисковый запрос (тест ParseProductsAsync).</summary>
    private const string SearchQuery = "Шорты мужские";

    /// <summary>Лимит товаров из поиска (пишется в auth.SearchLimit на время запуска).</summary>
    private const int SearchLimit = 100;

    /// <summary>SKU Ozon для теста GetProductsForIdsAsync.</summary>
    private static readonly long[] ProductIds =
    {
        3760630727
    };

    private static readonly string[] ProductUrls =
    {
        "https://ozon.ru/t/RhWvoBC"
    };

    private const string AuthFileName = "ozon-scraping-auth.json";

    // ═══════════════════════════════════════════════════════════════════════════

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        PrintRequiredParameters();

        try
        {
            var live = args.Contains("--live", StringComparer.OrdinalIgnoreCase)
                || (!args.Contains("--offline", StringComparer.OrdinalIgnoreCase) && UseLiveHttp);

            if (live)
            {
                return await RunLiveAsync(args).ConfigureAwait(false);
            }

            return RunOfflineFixtures();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + ex.Message);
            Console.ResetColor();
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static void PrintRequiredParameters()
    {
        Console.WriteLine("=== OzonConsole — IParseService (Business.OzonService) ===");
        Console.WriteLine();
        Console.WriteLine("1) ozon-scraping-auth.json: cookie, userAgent, useBrowser, proxyUrl, searchLimit…");
        Console.WriteLine("2) Program.cs: UseLiveHttp, SearchQuery, SearchLimit, ProductIds");
        Console.WriteLine();
    }

    private static async Task<int> RunLiveAsync(string[] args)
    {
        var auth = LoadAuth();
        auth.SearchLimit = SearchLimit;

        var cookiePlaceholder = !auth.HasCookie ||
                                auth.Cookie.Contains("PASTE_OZON_COOKIE", StringComparison.OrdinalIgnoreCase);

        if (cookiePlaceholder && !auth.UseBrowser)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Cookie не заполнен, а useBrowser=false.");
            Console.ResetColor();
            return 2;
        }

        await using IOzonService ozon = new OzonService(auth);
        IParseService parser = ozon;

            var idsOnly = args.Contains("--ids-only", StringComparer.OrdinalIgnoreCase);
            var cartShare = GetArgValue(args, "--cart-share");
            var cartShareDump = args.Contains("--cart-share-dump", StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(cartShare))
            {
                return await RunCartShareProbeAsync(ozon, cartShare, cartShareDump).ConfigureAwait(false);
            }

            if (!idsOnly)
        {
            Console.WriteLine($"--- IParseService.ParseProductsAsync: \"{SearchQuery}\" ---");
            var byName = await parser.ParseProductsAsync(SearchQuery).ConfigureAwait(false);
            PrintProducts(byName);
            Console.WriteLine();
        }

        Console.WriteLine($"--- IParseService.GetProductsForIdsAsync: {string.Join(", ", ProductIds)} ---");
        var byIds = await parser.GetProductsForIdsAsync(ProductIds.Select(id => id.ToString()))
            .ConfigureAwait(false);
        PrintProducts(byIds);

        if (ProductUrls.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"--- GetProductsForIdsAsync (URLs): {string.Join(", ", ProductUrls)} ---");
            var byUrls = await parser.GetProductsForIdsAsync(ProductUrls).ConfigureAwait(false);
            PrintProducts(byUrls);
            byIds = byIds.Concat(byUrls).ToList();
        }

        Console.WriteLine();
        Console.WriteLine("--- IParseService.ParseProductsPricesAsync ---");
        var prices = await parser.ParseProductsPricesAsync(byIds).ConfigureAwait(false);
        Console.WriteLine(
            $"Success={prices.Success} prices={prices.Prices.Count} refreshed={prices.ProductsWithRefreshedMeta.Count} " +
            $"err={prices.ErrorMessage}");
        foreach (var price in prices.Prices)
        {
            Console.WriteLine($"  productId={price.ProductId} price={price.Price} at={price.CheckTime:u}");
        }

        return byIds.Count > 0 && prices.Success ? 0 : 3;
    }

    private static async Task<int> RunCartShareProbeAsync(
        IOzonService ozon,
        string shareToken,
        bool dumpWidgets = false)
    {
        Console.WriteLine($"--- GetArticlesFromCartShareAsync: share={shareToken} ---");
        await ozon.WarmUpAsync().ConfigureAwait(false);

        List<string> articles;
        try
        {
            articles = await ozon.GetArticlesFromCartShareAsync(shareToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + ex.Message);
            Console.ResetColor();
            return 3;
        }

        Console.WriteLine($"SKU count: {articles.Count}");
        foreach (var sku in articles)
        {
            Console.WriteLine($"  {sku}");
        }

        if (dumpWidgets)
        {
            await DumpCartShareComposerAsync(shareToken).ConfigureAwait(false);
        }

        if (articles.Count == 0)
        {
            return 3;
        }

        var products = await ozon.GetProductsForIdsAsync(articles).ConfigureAwait(false);
        PrintProducts(products);
        return products.Count > 0 ? 0 : 3;
    }

    private static async Task DumpCartShareComposerAsync(string shareToken)
    {
        var auth = LoadAuth();
        await using var client = new OzonBrowserComposerClient(auth);
        await client.WarmUpAsync().ConfigureAwait(false);

        var path = $"/cart?share={Uri.EscapeDataString(shareToken.Trim())}";
        var page = await client.FetchPageAsync(path).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"--- Composer dump: {path} ---");
        Console.WriteLine($"widgets total: {page.WidgetStates?.Count ?? 0}");

        foreach (var (key, count) in OzonWidgetParser.DebugAllCartShareWidgets(page))
        {
            Console.WriteLine($"  all {key}: {count}");
        }

        foreach (var (key, count) in OzonWidgetParser.DebugCartShareWidgets(page))
        {
            Console.WriteLine($"  cand {key}: {count}");
        }

        var skus = OzonWidgetParser.ParseCartShareSkus(page);
        Console.WriteLine($"parsed SKUs: {skus.Count}");

        var dumpPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "cart-share-live-dump.json");
        Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);
        await File.WriteAllTextAsync(dumpPath, OzonJson.Serialize(page)).ConfigureAwait(false);
        Console.WriteLine($"saved: {dumpPath}");
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int RunOfflineFixtures()
    {
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var searchPath = Path.Combine(fixturesDir, "search-sample.json");
        var pdpPath = Path.Combine(fixturesDir, "pdp-sample.json");

        if (!File.Exists(searchPath) || !File.Exists(pdpPath))
        {
            throw new FileNotFoundException(
                $"Не найдены фикстуры в {fixturesDir}. Ожидаются search-sample.json и pdp-sample.json.");
        }

        Console.WriteLine("--- Offline Search (Fixtures/search-sample.json) ---");
        var searchPage = OzonJson.Deserialize<OzonComposerPage>(File.ReadAllText(searchPath))
                         ?? throw new InvalidOperationException("search fixture deserialize failed");
        var byName = OzonProductMapper.FromSearchPage(searchPage, SearchLimit);
        PrintProducts(byName);

        Console.WriteLine();
        Console.WriteLine("--- Offline Product by Id (Fixtures/pdp-sample.json) ---");
        var pdpPage = OzonJson.Deserialize<OzonComposerPage>(File.ReadAllText(pdpPath))
                      ?? throw new InvalidOperationException("pdp fixture deserialize failed");
        var byId = OzonProductMapper.FromProductPage(pdpPage);
        PrintProducts(new[] { byId });

        var cartSharePath = Path.Combine(fixturesDir, "cart-share-sample.json");
        if (File.Exists(cartSharePath))
        {
            Console.WriteLine();
            Console.WriteLine("--- Offline Cart share (Fixtures/cart-share-sample.json) ---");
            var cartPage = OzonJson.Deserialize<OzonComposerPage>(File.ReadAllText(cartSharePath))
                           ?? throw new InvalidOperationException("cart-share fixture deserialize failed");
            var skus = OzonWidgetParser.ParseCartShareSkus(cartPage);
            Console.WriteLine($"SKU count: {skus.Count} (expected 5: cartSplit + split, without tileGrid/menu)");
            foreach (var sku in skus)
            {
                Console.WriteLine($"  {sku}");
            }

            foreach (var (key, count) in OzonWidgetParser.DebugCartShareWidgets(cartPage))
            {
                Console.WriteLine($"  widget {key}: {count}");
            }

            if (skus.Count != 5)
            {
                return 4;
            }
        }

        return 0;
    }

    private static OzonScrapingAuthOptions LoadAuth()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), AuthFileName),
            Path.Combine(AppContext.BaseDirectory, AuthFileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", AuthFileName)
        };

        foreach (var path in candidates)
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                continue;
            }

            Console.WriteLine($"Auth file: {full}");
            var json = File.ReadAllText(full);
            return OzonJson.Deserialize<OzonScrapingAuthOptions>(json)
                   ?? throw new InvalidOperationException("Не удалось прочитать ozon-scraping-auth.json");
        }

        throw new FileNotFoundException($"Не найден {AuthFileName}.");
    }

    private static void PrintProducts(IEnumerable<WbProduct> products)
    {
        var list = products.ToList();
        Console.WriteLine($"Найдено: {list.Count}");

        foreach (var p in list)
        {
            Console.WriteLine(
                $"  [{p.MarketType}] SKU={p.IdInMarket} | {p.Brand} | {p.Name} | " +
                $"price={p.PriceFromInit?.Price} | rating={p.ReviewRating} | " +
                $"fb={p.FeedBacksCount} | adult={p.IsAdult}");
            Console.WriteLine($"    link={p.Link}");
            Console.WriteLine($"    img={p.ImageUrl}");
        }

        Console.WriteLine();
        Console.WriteLine("--- JSON ---");
        var dto = list.Select(p => new
        {
            p.MarketType,
            p.IdInMarket,
            p.Name,
            p.Brand,
            p.CategoryId,
            Category = p.Category?.Name,
            p.Rating,
            p.ReviewRating,
            p.FeedBacksCount,
            p.IsAdult,
            p.ImageUrl,
            p.Link,
            PriceFromInit = p.PriceFromInit is null
                ? null
                : new { p.PriceFromInit.Price, p.PriceFromInit.CheckTime }
        });
        Console.WriteLine(OzonJson.SerializePretty(dto));
    }
}
