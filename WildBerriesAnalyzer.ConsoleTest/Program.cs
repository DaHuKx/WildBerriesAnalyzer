//using System.Text;
//using Microsoft.EntityFrameworkCore;
//using WildBerriesAnalyzer.Business.Helpers;
//using WildBerriesAnalyzer.Data;

//Console.OutputEncoding = Encoding.UTF8;

//Console.WriteLine("=== Update product ImageUrl ===");

//const int batchSize = 500;
//var updated = 0;
//var unchanged = 0;
//var errors = 0;

//await using var db = new WbDataBase();

//var total = await db.Products.CountAsync();
//Console.WriteLine($"Products in DB: {total}");

//var processed = 0;
//while (processed < total)
//{
//    var batch = await db.Products
//        .OrderBy(p => p.Id)
//        .Skip(processed)
//        .Take(batchSize)
//        .ToListAsync();

//    if (batch.Count == 0)
//    {
//        break;
//    }

//    foreach (var product in batch)
//    {
//        try
//        {
//            var newUrl = WbProductImageUrlBuilder.BuildBigImageUrl(product.IdInMarket);
//            if (string.Equals(product.ImageUrl, newUrl, StringComparison.Ordinal))
//            {
//                unchanged++;
//                continue;
//            }

//            product.ImageUrl = newUrl;
//            updated++;
//        }
//        catch (Exception ex)
//        {
//            errors++;
//            Console.WriteLine($"  ERROR id={product.Id} nm={product.IdInMarket}: {ex.Message}");
//        }
//    }

//    await db.SaveChangesAsync();
//    processed += batch.Count;
//    Console.WriteLine($"  progress: {processed}/{total} (updated={updated}, unchanged={unchanged}, errors={errors})");
//}

//Console.WriteLine();
//Console.WriteLine($"DONE. updated={updated}, unchanged={unchanged}, errors={errors}");

//Console.ReadKey();

//Environment.ExitCode = errors == 0 ? 0 : 1;


using System.Text;
using WildBerriesAnalyzer.Business.Services;
using WildBerriesAnalyzer.Business.Services.WbScraping;
using WildBerriesAnalyzer.Domain.Models.DataBase;

Console.OutputEncoding = Encoding.UTF8;

const string SearchQuery = "чехол iphone";
const string SampleArticle = "923778047";

var options = WildBerriesService.CreateDefaultOptions();
options.PersistFilePath = Path.Combine(AppContext.BaseDirectory, "wb-scraping-auth.json");

IWbScrapingAuthStore store = new FileWbScrapingAuthStore(options);
TryImportOauthToken(store);

var auth = store.GetSnapshot();
Console.WriteLine("=== WildBerriesService smoke test ===");
Console.WriteLine($"Auth file : {store.PersistFilePath}");
Console.WriteLine($"Token     : {(string.IsNullOrWhiteSpace(auth.AccessToken) ? "<empty>" : Truncate(auth.AccessToken, 40))}");
Console.WriteLine($"Cookie len: {auth.Cookie?.Length ?? 0}");
Console.WriteLine();

var service = new WildBerriesService(store);
var failed = 0;

failed += await RunAsync(
    "1) ParseProductsAsync",
    async () =>
    {
        var products = await service.ParseProductsAsync(SearchQuery);
        Console.WriteLine($"  query: \"{SearchQuery}\"");
        Console.WriteLine($"  count: {products.Count}");
        foreach (var p in products.Take(5))
        {
            PrintProduct("  ", p);
        }

        if (products.Count == 0)
        {
            Console.WriteLine("  WARN: пустой список (ошибка сети/токена глотается внутри метода, либо нет результатов).");
        }

        return products.Count > 0;
    });

failed += await RunAsync(
    "2) GetProductsForIdsAsync",
    async () =>
    {
        var products = await service.GetProductsForIdsAsync(new[] { SampleArticle });
        Console.WriteLine($"  ids  : {SampleArticle}");
        Console.WriteLine($"  count: {products.Count}");
        foreach (var p in products)
        {
            PrintProduct("  ", p);
        }

        return products.Count > 0;
    });

failed += await RunAsync(
    "3) ParseProductsPricesAsync",
    async () =>
    {
        var seed = new List<WbProduct>
        {
            new() { Id = 1, IdInMarket = long.Parse(SampleArticle), Name = "sample" }
        };

        var parsed = await service.ParseProductsPricesAsync(seed);
        Console.WriteLine($"  products: {seed.Count}");
        Console.WriteLine($"  prices  : {parsed.Prices.Count}");
        Console.WriteLine($"  meta    : {parsed.ProductsWithRefreshedMeta.Count} (rating/reviews refreshed)");
        foreach (var product in parsed.ProductsWithRefreshedMeta.Take(5))
        {
            Console.WriteLine(
                $"  - idInMarket={product.IdInMarket}, rating={product.Rating}, reviewRating={product.ReviewRating}, feedbacks={product.FeedBacksCount}");
        }

        foreach (var price in parsed.Prices.Take(5))
        {
            Console.WriteLine($"  - productId={price.ProductId}, price={price.Price}, at={price.CheckTime:u}");
        }

        if (parsed.Prices.Count == 0)
        {
            Console.WriteLine("  WARN: пустой список (ошибка глотается внутри метода, либо нет цены в наличии).");
        }

        return parsed.Prices.Count > 0;
    });

Console.WriteLine();
Console.WriteLine(failed == 0 ? "RESULT: ALL OK" : $"RESULT: FAIL ({failed} step(s))");
Environment.ExitCode = failed == 0 ? 0 : 1;

static async Task<int> RunAsync(string title, Func<Task<bool>> action)
{
    Console.WriteLine($"--- {title} ---");
    try
    {
        var ok = await action();
        Console.WriteLine(ok ? "  STATUS: OK" : "  STATUS: FAIL");
        Console.WriteLine();
        return ok ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  STATUS: EXCEPTION");
        Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine();
        return 1;
    }
}

static void TryImportOauthToken(IWbScrapingAuthStore store)
{
    var path = FindImportFile();
    if (path is null)
    {
        return;
    }

    Console.WriteLine($"Импорт oauth-токена: {path}");
    var updater = new WbScrapingAuthUpdater(store);
    var json = File.ReadAllText(path);
    var ok = updater.ApplyOauthBffTokenJson(json);
    Console.WriteLine(ok ? $"Импорт OK → {updater.LastError}" : $"Импорт FAIL → {updater.LastError}");
    Console.WriteLine();
}

static string? FindImportFile()
{
    var names = new[] { "oauth-bff-token.json", "wb-oauth-bff-token.json" };
    foreach (var name in names)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, name),
            Path.Combine(Directory.GetCurrentDirectory(), name)
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null)
        {
            return found;
        }
    }

    return null;
}

static void PrintProduct(string indent, WbProduct p)
{
    var price = p.PriceFromInit?.Price;
    var priceText = price is null ? "-" : price.Value.ToString("0.##");
    Console.WriteLine($"{indent}- {p.IdInMarket} | {p.Brand} | {p.Name} | {priceText} ₽");
}

static string Truncate(string value, int max)
{
    return value.Length <= max ? value : $"{value[..(max / 2)]}...{value[^(max / 2)..]}";
}

