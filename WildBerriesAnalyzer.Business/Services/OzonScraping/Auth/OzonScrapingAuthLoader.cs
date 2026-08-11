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
            UseBrowser = preferred.UseBrowser,
            Headless = preferred.Headless,
            ChallengeWaitMs = preferred.ChallengeWaitMs > 0 ? preferred.ChallengeWaitMs : fallback.ChallengeWaitMs,
            SearchLimit = preferred.SearchLimit > 0 ? preferred.SearchLimit : fallback.SearchLimit
        };
    }

    private static string FirstNonEmpty(string? preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
