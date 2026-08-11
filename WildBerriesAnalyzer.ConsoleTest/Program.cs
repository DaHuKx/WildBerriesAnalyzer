using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WildBerriesAnalyzer.Business.Services;
using WildBerriesAnalyzer.Business.Services.WbScraping;
using WildBerriesAnalyzer.Data;
using WildBerriesAnalyzer.Data.Repositories;
using WildBerriesAnalyzer.Domain.Models.DataBase;

Console.OutputEncoding = Encoding.UTF8;

// Пачками по 200 nmId запрашиваем карточки WB, начиная с 400000.
// В удалённую БД пишем только товары с FeedBacksCount > 3000.
//
// Подключение (приоритет):
//   1) WB_CONNECTION_STRING
//   2) .env: POSTGRES_USER / POSTGRES_PASSWORD / POSTGRES_DB + WB_DB_HOST (по умолчанию 62.233.35.144)
//
// Локальный 127.0.0.1 запрещён, пока не задано WB_ALLOW_LOCAL_DB=1.
//
// Опционально:
//   WB_SCAN_START_ID, WB_SCAN_END_ID, WB_SCAN_MIN_REVIEWS, WB_SCAN_BATCH_SIZE, WB_SCAN_DELAY_MS

const long DefaultStartId = 400_000;
const int DefaultBatchSize = 100;
const int DefaultMinReviews = 3000;
const int DefaultDelayMs = 400;
const string DefaultRemoteHost = "62.233.35.144";

LoadDotEnvFiles();
var connectionString = ResolveRemoteConnectionString();
Environment.SetEnvironmentVariable("WB_CONNECTION_STRING", connectionString);

var startId = ReadLong("WB_SCAN_START_ID", DefaultStartId);
var endId = ReadLong("WB_SCAN_END_ID", long.MaxValue);
var batchSize = (int)ReadLong("WB_SCAN_BATCH_SIZE", DefaultBatchSize);
var minReviews = (int)ReadLong("WB_SCAN_MIN_REVIEWS", DefaultMinReviews);
var delayMs = (int)ReadLong("WB_SCAN_DELAY_MS", DefaultDelayMs);

if (batchSize <= 0)
{
    batchSize = DefaultBatchSize;
}

var progressPath = Path.Combine(AppContext.BaseDirectory, "wb-id-scan-progress.txt");
if (File.Exists(progressPath)
    && long.TryParse(File.ReadAllText(progressPath).Trim(), out var resumeFrom)
    && resumeFrom > startId)
{
    Console.WriteLine($"Продолжение с сохранённой позиции: {resumeFrom}");
    startId = resumeFrom;
}

var dbInfo = ParseDbInfo(connectionString);
Console.WriteLine("=== Сбор товаров WB по ID → удалённая БД ===");
Console.WriteLine($"DB host     : {dbInfo.Host}");
Console.WriteLine($"DB name     : {dbInfo.Database}");
Console.WriteLine($"DB user     : {dbInfo.Username}");
Console.WriteLine($"DB full     : {MaskConnectionString(connectionString)}");
Console.WriteLine($"Start ID    : {startId}");
Console.WriteLine($"End ID      : {(endId == long.MaxValue ? "∞" : endId.ToString())}");
Console.WriteLine($"Batch       : {batchSize}");
Console.WriteLine($"Min reviews : > {minReviews}");
Console.WriteLine($"Delay       : {delayMs} ms");
Console.WriteLine($"Progress    : {progressPath}");
Console.WriteLine("Ctrl+C — остановить (позиция сохранится).");
Console.WriteLine();

if (IsLocalHost(dbInfo.Host)
    && !string.Equals(Environment.GetEnvironmentVariable("WB_ALLOW_LOCAL_DB"), "1", StringComparison.Ordinal))
{
    Console.WriteLine("ОШИБКА: сейчас цель — локальная БД. Для удалённого сервера задайте:");
    Console.WriteLine("  WB_CONNECTION_STRING=Host=62.233.35.144;Port=5432;Database=WildBerriesAnalyzerDb;Username=...;Password=...");
    Console.WriteLine("или положите .env с POSTGRES_USER / POSTGRES_PASSWORD / POSTGRES_DB");
    Console.WriteLine("(для локального прогона: WB_ALLOW_LOCAL_DB=1).");
    Environment.Exit(2);
}

var productsInDbBefore = await EnsureDbReachableAsync();

var options = WildBerriesService.CreateDefaultOptions();
options.PersistFilePath = Path.Combine(AppContext.BaseDirectory, "wb-scraping-auth.json");
IWbScrapingAuthStore store = new FileWbScrapingAuthStore(options);
var wildBerries = new WildBerriesService(store);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine();
    Console.WriteLine("Остановка...");
};

long current = startId;
long batchesOk = 0;
long totalFetched = 0;
long totalQualified = 0;
long totalAdded = 0;

while (!cts.IsCancellationRequested && current <= endId)
{
    var count = (int)Math.Min(batchSize, endId - current + 1);
    if (count <= 0)
    {
        break;
    }

    var batchStart = current;
    var batchEnd = current + count - 1;
    var ids = new string[count];
    for (var i = 0; i < count; i++)
    {
        ids[i] = (batchStart + i).ToString();
    }

    List<WbProduct> products;
    try
    {
        // Внутри сервис режет запрос на под-батчи (~50) — для API это нормально.
        products = await wildBerries.GetProductsForIdsAsync(ids);
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ids {batchStart}-{batchEnd}: ошибка WB — {ex.GetType().Name}: {ex.Message}. Повтор через 5 с...");
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        continue;
    }

    var qualified = products
        .Where(p => p.FeedBacksCount > minReviews)
        .GroupBy(p => p.IdInMarket)
        .Select(g => g.First())
        .ToList();

    var added = 0;
    if (qualified.Count > 0)
    {
        try
        {
            // Category из WB — detached entity без Id; иначе EF пытается вставить лишние категории.
            foreach (var product in qualified)
            {
                product.Category = null;
                product.CategoryId = null;
            }

            using var db = new WbDataBase();
            var marketIds = qualified.Select(p => p.IdInMarket).ToList();
            var already = await db.Products
                .AsNoTracking()
                .Where(p => marketIds.Contains(p.IdInMarket))
                .Select(p => p.IdInMarket)
                .ToListAsync(cts.Token);

            var alreadySet = already.ToHashSet();
            var toAdd = qualified.Where(p => !alreadySet.Contains(p.IdInMarket)).ToList();

            if (toAdd.Count > 0)
            {
                var repo = new ProductsRepository(db);
                await repo.GetOrAddProducts(toAdd);

                // Проверка, что строки реально в этой БД.
                var savedIds = toAdd.Select(p => p.IdInMarket).ToList();
                var confirmed = await db.Products
                    .AsNoTracking()
                    .CountAsync(p => savedIds.Contains(p.IdInMarket), cts.Token);
                added = confirmed;
                if (confirmed != toAdd.Count)
                {
                    Console.WriteLine(
                        $"  WARN: ожидали {toAdd.Count} новых, в БД подтверждено {confirmed} " +
                        $"({dbInfo.Host}/{dbInfo.Database})");
                }
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] ids {batchStart}-{batchEnd}: ошибка БД — {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException is not null)
            {
                Console.WriteLine($"  inner: {ex.InnerException.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            continue;
        }
    }

    batchesOk++;
    totalFetched += products.Count;
    totalQualified += qualified.Count;
    totalAdded += added;

    var nextId = batchEnd + 1;
    await File.WriteAllTextAsync(progressPath, nextId.ToString(), cts.Token);

    Console.WriteLine(
        $"[{DateTime.Now:HH:mm:ss}] #{batchesOk} ids {batchStart}-{batchEnd}: " +
        $"wb={products.Count}, reviews>{minReviews}={qualified.Count}, new={added} | " +
        $"итог new={totalAdded}, qualified={totalQualified} → {dbInfo.Host}/{dbInfo.Database}");

    current = nextId;

    if (delayMs > 0 && !cts.IsCancellationRequested && current <= endId)
    {
        try
        {
            await Task.Delay(delayMs, cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}

var productsInDbAfter = await CountProductsAsync();

Console.WriteLine();
Console.WriteLine("=== Готово ===");
Console.WriteLine($"БД            : {dbInfo.Host}/{dbInfo.Database}");
Console.WriteLine($"Товаров до    : {productsInDbBefore}");
Console.WriteLine($"Товаров после : {productsInDbAfter} (Δ {productsInDbAfter - productsInDbBefore})");
Console.WriteLine($"Батчей OK     : {batchesOk}");
Console.WriteLine($"Получено с WB : {totalFetched}");
Console.WriteLine($"С отзывами>{minReviews}: {totalQualified}");
Console.WriteLine($"Добавлено в БД: {totalAdded}");
Console.WriteLine($"Следующий ID  : {current}");

static long ReadLong(string envName, long defaultValue)
{
    var raw = Environment.GetEnvironmentVariable(envName);
    return long.TryParse(raw, out var value) ? value : defaultValue;
}

static string MaskConnectionString(string connectionString)
{
    return Regex.Replace(
        connectionString,
        @"(Password|Pwd)=([^;]*)",
        "$1=***",
        RegexOptions.IgnoreCase);
}

static async Task<long> EnsureDbReachableAsync()
{
    try
    {
        using var db = new WbDataBase();
        var ok = await db.Database.CanConnectAsync();
        if (!ok)
        {
            throw new InvalidOperationException("CanConnectAsync вернул false.");
        }

        var count = await db.Products.AsNoTracking().LongCountAsync();
        Console.WriteLine($"DB ping     : OK, products={count}");
        Console.WriteLine();
        return count;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB ping     : FAIL — {ex.Message}");
        Console.WriteLine("Проверьте WB_CONNECTION_STRING / .env (POSTGRES_*).");
        Environment.Exit(1);
        return 0;
    }
}

static async Task<long> CountProductsAsync()
{
    using var db = new WbDataBase();
    return await db.Products.AsNoTracking().LongCountAsync();
}

static void LoadDotEnvFiles()
{
    foreach (var path in DiscoverDotEnvPaths())
    {
        if (!File.Exists(path))
        {
            continue;
        }

        Console.WriteLine($"Загрузка .env: {path}");
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"').Trim('\'');
            if (key.Length == 0)
            {
                continue;
            }

            // Не перетираем уже заданные переменные окружения процесса.
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

static IEnumerable<string> DiscoverDotEnvPaths()
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var start in new[]
             {
                 Directory.GetCurrentDirectory(),
                 AppContext.BaseDirectory
             })
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (seen.Add(candidate))
            {
                yield return candidate;
            }

            dir = dir.Parent;
        }
    }
}

static string ResolveRemoteConnectionString()
{
    var fromEnv = FirstNonEmpty(
        Environment.GetEnvironmentVariable("WB_CONNECTION_STRING"),
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
        Environment.GetEnvironmentVariable("ConnectionStrings__MyDb"));

    if (!string.IsNullOrWhiteSpace(fromEnv))
    {
        return fromEnv!;
    }

    var user = Environment.GetEnvironmentVariable("POSTGRES_USER");
    var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
    var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "WildBerriesAnalyzerDb";
    var host = Environment.GetEnvironmentVariable("WB_DB_HOST") ?? DefaultRemoteHost;
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";

    if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
    {
        Console.WriteLine("ОШИБКА: нет строки подключения к удалённой БД.");
        Console.WriteLine("Задайте одно из:");
        Console.WriteLine("  $env:WB_CONNECTION_STRING = \"Host=62.233.35.144;Port=5432;Database=WildBerriesAnalyzerDb;Username=WbAdmin;Password=...\"");
        Console.WriteLine("или создайте .env в корне репозитория:");
        Console.WriteLine("  POSTGRES_USER=WbAdmin");
        Console.WriteLine("  POSTGRES_PASSWORD=...");
        Console.WriteLine("  POSTGRES_DB=WildBerriesAnalyzerDb");
        Console.WriteLine("  WB_DB_HOST=62.233.35.144");
        Environment.Exit(2);
    }

    return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
}

static string? FirstNonEmpty(params string?[] values)
{
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    return null;
}

static bool IsLocalHost(string? host)
{
    if (string.IsNullOrWhiteSpace(host))
    {
        return true;
    }

    return host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
           || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
           || host.Equals("postgresdb", StringComparison.OrdinalIgnoreCase);
}

static (string Host, string Database, string Username) ParseDbInfo(string connectionString)
{
    string Get(string key)
    {
        var match = Regex.Match(
            connectionString,
            $@"(?:^|;)\s*{Regex.Escape(key)}\s*=\s*([^;]*)",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "?";
    }

    return (Get("Host"), Get("Database"), Get("Username"));
}
