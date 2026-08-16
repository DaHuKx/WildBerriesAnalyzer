using WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;

/// <summary>
/// Обновляет cookie в памяти (<see cref="OzonScrapingAuthOptions"/>) и в JSON-файле.
/// </summary>
public sealed class OzonScrapingAuthUpdater : IOzonScrapingAuthUpdater
{
    private readonly OzonScrapingAuthOptions _live;
    private readonly object _sync = new();

    public OzonScrapingAuthUpdater(OzonScrapingAuthOptions liveOptions, string persistFilePath)
    {
        _live = liveOptions ?? throw new ArgumentNullException(nameof(liveOptions));
        PersistFilePath = OzonScrapingAuthLoader.ResolvePersistPath(persistFilePath);
    }

    public string PersistFilePath { get; }

    public string? LastError { get; private set; }

    public bool ApplyCookie(string cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            LastError = "Cookie пустая.";
            return false;
        }

        try
        {
            lock (_sync)
            {
                _live.Cookie = cookie.Trim();
                PersistUnlocked();
            }

            LastError = $"Cookie сохранена в {PersistFilePath}";
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Перечитывает cookie с диска в live-options (для Server, когда файл обновил бот).
    /// </summary>
    public bool TryReloadCookieFromDisk()
    {
        try
        {
            lock (_sync)
            {
                if (!File.Exists(PersistFilePath))
                {
                    return false;
                }

                var json = File.ReadAllText(PersistFilePath);
                var fromFile = OzonJson.Deserialize<OzonScrapingAuthOptions>(json);
                if (fromFile is null || string.IsNullOrWhiteSpace(fromFile.Cookie))
                {
                    return false;
                }

                var previous = _live.Cookie;
                _live.Cookie = fromFile.Cookie.Trim();
                return !string.Equals(previous, _live.Cookie, StringComparison.Ordinal);
            }
        }
        catch
        {
            return false;
        }
    }

    private void PersistUnlocked()
    {
        OzonScrapingAuthOptions toSave;
        if (File.Exists(PersistFilePath))
        {
            try
            {
                var existing = OzonJson.Deserialize<OzonScrapingAuthOptions>(File.ReadAllText(PersistFilePath))
                               ?? OzonScrapingAuthOptions.CreateDefault();
                toSave = OzonScrapingAuthLoader.Merge(_live, existing);
                toSave.Cookie = _live.Cookie;
            }
            catch
            {
                toSave = CloneForPersist(_live);
            }
        }
        else
        {
            toSave = CloneForPersist(_live);
        }

        var directory = Path.GetDirectoryName(PersistFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(PersistFilePath, OzonJson.SerializePretty(toSave));
    }

    private static OzonScrapingAuthOptions CloneForPersist(OzonScrapingAuthOptions source) =>
        new()
        {
            Cookie = source.Cookie,
            UserAgent = source.UserAgent,
            BaseUrl = source.BaseUrl,
            ComposerPath = source.ComposerPath,
            ProxyUrl = source.ProxyUrl,
            RequestDelayMs = source.RequestDelayMs,
            ProductConcurrency = source.ProductConcurrency,
            UseBrowser = source.UseBrowser,
            Headless = source.Headless,
            ChromeChannel = source.ChromeChannel,
            UserDataDir = source.UserDataDir,
            ChallengeWaitMs = source.ChallengeWaitMs,
            SearchLimit = source.SearchLimit
        };
}
