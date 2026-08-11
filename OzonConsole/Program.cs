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
        1681720585,
        1185261285
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
                return await RunLiveAsync().ConfigureAwait(false);
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

    private static async Task<int> RunLiveAsync()
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

        Console.WriteLine($"--- IParseService.ParseProductsAsync: \"{SearchQuery}\" ---");
        var byName = await parser.ParseProductsAsync(SearchQuery).ConfigureAwait(false);
        PrintProducts(byName);

        Console.WriteLine();
        Console.WriteLine($"--- IParseService.GetProductsForIdsAsync: {string.Join(", ", ProductIds)} ---");
        var byIds = await parser.GetProductsForIdsAsync(ProductIds.Select(id => id.ToString()))
            .ConfigureAwait(false);
        PrintProducts(byIds);

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

        return 0;
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
