using WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;

/// <summary>
/// Загрузка ozon-scraping-auth.json из типичных путей рядом с приложением.
/// </summary>
public static class OzonScrapingAuthLoader
{
    public static OzonScrapingAuthOptions LoadOrDefault(params string[] extraPaths)
    {
        var candidates = new List<string>();
        if (extraPaths is { Length: > 0 })
        {
            candidates.AddRange(extraPaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), OzonScrapingAuthOptions.DefaultFileName));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, OzonScrapingAuthOptions.DefaultFileName));

        foreach (var path in candidates)
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                continue;
            }

            var json = File.ReadAllText(full);
            var loaded = OzonJson.Deserialize<OzonScrapingAuthOptions>(json);
            if (loaded is not null)
            {
                return loaded;
            }
        }

        return OzonScrapingAuthOptions.CreateDefault();
    }

    public static OzonScrapingAuthOptions Merge(OzonScrapingAuthOptions preferred, OzonScrapingAuthOptions fallback)
    {
        ArgumentNullException.ThrowIfNull(preferred);
        ArgumentNullException.ThrowIfNull(fallback);

        return new OzonScrapingAuthOptions
        {
            Cookie = FirstNonEmpty(preferred.Cookie, fallback.Cookie),
            UserAgent = FirstNonEmpty(preferred.UserAgent, fallback.UserAgent),
            BaseUrl = FirstNonEmpty(preferred.BaseUrl, fallback.BaseUrl),
            ComposerPath = FirstNonEmpty(preferred.ComposerPath, fallback.ComposerPath),
            ProxyUrl = string.IsNullOrWhiteSpace(preferred.ProxyUrl) ? fallback.ProxyUrl : preferred.ProxyUrl,
            RequestDelayMs = preferred.RequestDelayMs > 0 ? preferred.RequestDelayMs : fallback.RequestDelayMs,
            ProductConcurrency = preferred.ProductConcurrency > 0
                ? preferred.ProductConcurrency
                : fallback.ProductConcurrency,
            UseBrowser = preferred.UseBrowser,
            Headless = preferred.Headless,
            ChromeChannel = FirstNonEmptyOrNull(preferred.ChromeChannel, fallback.ChromeChannel),
            UserDataDir = FirstNonEmptyOrNull(preferred.UserDataDir, fallback.UserDataDir),
            ChallengeWaitMs = preferred.ChallengeWaitMs > 0 ? preferred.ChallengeWaitMs : fallback.ChallengeWaitMs,
            SearchLimit = preferred.SearchLimit > 0 ? preferred.SearchLimit : fallback.SearchLimit,
            PersistFilePath = FirstNonEmpty(preferred.PersistFilePath, fallback.PersistFilePath)
        };
    }

    /// <summary>
    /// Разрешает путь к ozon-scraping-auth.json (абсолютный или относительно contentRoot).
    /// </summary>
    public static string ResolvePersistPath(string? configuredPath, string? contentRoot = null)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? OzonScrapingAuthOptions.DefaultFileName
            : configuredPath.Trim();

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            return Path.GetFullPath(Path.Combine(contentRoot, path));
        }

        return Path.GetFullPath(path);
    }

    private static string FirstNonEmpty(string? preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

    private static string? FirstNonEmptyOrNull(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
