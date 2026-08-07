//using System.Text;
//using WildBerriesAnalyzer.Business.Services;
//using WildBerriesAnalyzer.Business.Services.WbScraping;
//using WildBerriesAnalyzer.Domain.Models.DataBase;

//Console.OutputEncoding = Encoding.UTF8;

//const string SearchQuery = "чехол iphone";
//const string SampleArticle = "923778047";

//var options = WildBerriesService.CreateDefaultOptions();
//options.PersistFilePath = Path.Combine(AppContext.BaseDirectory, "wb-scraping-auth.json");

//IWbScrapingAuthStore store = new FileWbScrapingAuthStore(options);
//TryImportOauthToken(store);

//var auth = store.GetSnapshot();
//Console.WriteLine("=== WildBerriesService smoke test ===");
//Console.WriteLine($"Auth file : {store.PersistFilePath}");
//Console.WriteLine($"Token     : {(string.IsNullOrWhiteSpace(auth.AccessToken) ? "<empty>" : Truncate(auth.AccessToken, 40))}");
//Console.WriteLine($"Cookie len: {auth.Cookie?.Length ?? 0}");
//Console.WriteLine();

//var service = new WildBerriesService(store);
//var failed = 0;

//failed += await RunAsync(
//    "1) ParseProductsAsync",
//    async () =>
//    {
//        var products = await service.ParseProductsAsync(SearchQuery);
//        Console.WriteLine($"  query: \"{SearchQuery}\"");
//        Console.WriteLine($"  count: {products.Count}");
//        foreach (var p in products.Take(5))
//        {
//            PrintProduct("  ", p);
//        }

//        if (products.Count == 0)
//        {
//            Console.WriteLine("  WARN: пустой список (ошибка сети/токена глотается внутри метода, либо нет результатов).");
//        }

//        return products.Count > 0;
//    });

//failed += await RunAsync(
//    "2) GetProductsForIdsAsync",
//    async () =>
//    {
//        var products = await service.GetProductsForIdsAsync(new[] { SampleArticle });
//        Console.WriteLine($"  ids  : {SampleArticle}");
//        Console.WriteLine($"  count: {products.Count}");
//        foreach (var p in products)
//        {
//            PrintProduct("  ", p);
//        }

//        return products.Count > 0;
//    });

//failed += await RunAsync(
//    "3) ParseProductsPricesAsync",
//    async () =>
//    {
//        var seed = new List<WbProduct>
//        {
//            new() { Id = 1, IdInMarket = long.Parse(SampleArticle), Name = "sample" }
//        };

//        var parsed = await service.ParseProductsPricesAsync(seed);
//        Console.WriteLine($"  products: {seed.Count}");
//        Console.WriteLine($"  prices  : {parsed.Prices.Count}");
//        Console.WriteLine($"  meta    : {parsed.ProductsWithRefreshedMeta.Count} (rating/reviews refreshed)");
//        foreach (var product in parsed.ProductsWithRefreshedMeta.Take(5))
//        {
//            Console.WriteLine(
//                $"  - idInMarket={product.IdInMarket}, rating={product.Rating}, reviewRating={product.ReviewRating}, feedbacks={product.FeedBacksCount}");
//        }

//        foreach (var price in parsed.Prices.Take(5))
//        {
//            Console.WriteLine($"  - productId={price.ProductId}, price={price.Price}, at={price.CheckTime:u}");
//        }

//        if (parsed.Prices.Count == 0)
//        {
//            Console.WriteLine("  WARN: пустой список (ошибка глотается внутри метода, либо нет цены в наличии).");
//        }

//        return parsed.Prices.Count > 0;
//    });

//Console.WriteLine();
//Console.WriteLine(failed == 0 ? "RESULT: ALL OK" : $"RESULT: FAIL ({failed} step(s))");
//Environment.ExitCode = failed == 0 ? 0 : 1;

//static async Task<int> RunAsync(string title, Func<Task<bool>> action)
//{
//    Console.WriteLine($"--- {title} ---");
//    try
//    {
//        var ok = await action();
//        Console.WriteLine(ok ? "  STATUS: OK" : "  STATUS: FAIL");
//        Console.WriteLine();
//        return ok ? 0 : 1;
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"  STATUS: EXCEPTION");
//        Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
//        Console.WriteLine();
//        return 1;
//    }
//}

//static void TryImportOauthToken(IWbScrapingAuthStore store)
//{
//    var path = FindImportFile();
//    if (path is null)
//    {
//        return;
//    }

//    Console.WriteLine($"Импорт oauth-токена: {path}");
//    var updater = new WbScrapingAuthUpdater(store);
//    var json = File.ReadAllText(path);
//    var ok = updater.ApplyOauthBffTokenJson(json);
//    Console.WriteLine(ok ? $"Импорт OK → {updater.LastError}" : $"Импорт FAIL → {updater.LastError}");
//    Console.WriteLine();
//}

//static string? FindImportFile()
//{
//    var names = new[] { "oauth-bff-token.json", "wb-oauth-bff-token.json" };
//    foreach (var name in names)
//    {
//        var candidates = new[]
//        {
//            Path.Combine(AppContext.BaseDirectory, name),
//            Path.Combine(Directory.GetCurrentDirectory(), name)
//        };

//        var found = candidates.FirstOrDefault(File.Exists);
//        if (found is not null)
//        {
//            return found;
//        }
//    }

//    return null;
//}

//static void PrintProduct(string indent, WbProduct p)
//{
//    var price = p.PriceFromInit?.Price;
//    var priceText = price is null ? "-" : price.Value.ToString("0.##");
//    Console.WriteLine($"{indent}- {p.IdInMarket} | {p.Brand} | {p.Name} | {priceText} ₽");
//}

//static string Truncate(string value, int max)
//{
//    return value.Length <= max ? value : $"{value[..(max / 2)]}...{value[^(max / 2)..]}";
//}

using WildBerriesAnalyzer.Data;
using WildBerriesAnalyzer.Data.Repositories;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.GPT;

WbDataBase db = new WbDataBase();
ProductsRepository repository = new ProductsRepository(db);
FiltersRepository filtersRepository = new FiltersRepository(db);
GPTChat chat = new GPTChat();
chat.Initialize("http://localhost:11434", "gemma3:12b");

var products = await repository.GetAllAsync();

var categories = (await filtersRepository.GetAllCategoriesAsync()).Select(c => c.Name).ToList();

var productCategory = new List<WbFilterCategory>();

foreach (var product in products)
{
    var cat = await chat.SendAsync(PromtBases.GetCategoryPromt(product.Name, categories));

    if (categories.Contains(cat))
    {
        categories.Add(cat);
    }

    Console.WriteLine($"{product.Name} - {cat}");
}

