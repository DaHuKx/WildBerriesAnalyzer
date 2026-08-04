using Newtonsoft.Json;
using WildBerriesAnalyzer.Business.Options;

namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    /// <summary>
    /// Хранит AccessToken/Cookie в памяти и в JSON-файле (ручное обновление).
    /// </summary>
    public sealed class FileWbScrapingAuthStore : IWbScrapingAuthStore
    {
        private readonly object _sync = new();
        private WbScrapingAuthState _state;
        private DateTime _lastLoadedUtc = DateTime.MinValue;

        public FileWbScrapingAuthStore(WbScrapingAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            PersistFilePath = ResolvePath(options.PersistFilePath);
            _state = LoadOrSeed(options);
        }

        public string PersistFilePath { get; }

        public WbScrapingAuthState GetSnapshot()
        {
            lock (_sync)
            {
                TryReloadFromDiskUnlocked();
                return _state.Clone();
            }
        }

        public void Update(Action<WbScrapingAuthState> update)
        {
            ArgumentNullException.ThrowIfNull(update);

            lock (_sync)
            {
                update(_state);
                PersistUnlocked();
            }
        }

        private WbScrapingAuthState LoadOrSeed(WbScrapingAuthOptions options)
        {
            var seeded = WbScrapingAuthState.FromOptions(options);

            try
            {
                if (File.Exists(PersistFilePath))
                {
                    var json = File.ReadAllText(PersistFilePath);
                    var fromFile = JsonConvert.DeserializeObject<WbScrapingAuthState>(json);
                    if (fromFile is not null && !string.IsNullOrWhiteSpace(fromFile.AccessToken))
                    {
                        if (string.IsNullOrWhiteSpace(fromFile.DeviceId))
                        {
                            fromFile.DeviceId = seeded.DeviceId;
                        }

                        if (string.IsNullOrWhiteSpace(fromFile.UserAgent))
                        {
                            fromFile.UserAgent = seeded.UserAgent;
                        }

                        if (string.IsNullOrWhiteSpace(fromFile.SpaVersion))
                        {
                            fromFile.SpaVersion = seeded.SpaVersion;
                        }

                        if (string.IsNullOrWhiteSpace(fromFile.SecChUa))
                        {
                            fromFile.SecChUa = seeded.SecChUa;
                        }

                        TouchLoadedUtcUnlocked();
                        return fromFile;
                    }
                }
            }
            catch
            {
                // fallback to seeded
            }

            _state = seeded;
            PersistUnlocked();
            return seeded;
        }

        private void TryReloadFromDiskUnlocked()
        {
            try
            {
                if (!File.Exists(PersistFilePath))
                {
                    return;
                }

                var lastWrite = File.GetLastWriteTimeUtc(PersistFilePath);
                if (lastWrite <= _lastLoadedUtc)
                {
                    return;
                }

                var json = File.ReadAllText(PersistFilePath);
                var fromFile = JsonConvert.DeserializeObject<WbScrapingAuthState>(json);
                if (fromFile is null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(fromFile.DeviceId))
                {
                    fromFile.DeviceId = _state.DeviceId;
                }

                if (string.IsNullOrWhiteSpace(fromFile.UserAgent))
                {
                    fromFile.UserAgent = _state.UserAgent;
                }

                if (string.IsNullOrWhiteSpace(fromFile.SpaVersion))
                {
                    fromFile.SpaVersion = _state.SpaVersion;
                }

                if (string.IsNullOrWhiteSpace(fromFile.SecChUa))
                {
                    fromFile.SecChUa = _state.SecChUa;
                }

                _state = fromFile;
                _lastLoadedUtc = lastWrite;
            }
            catch
            {
                // keep in-memory state
            }
        }

        private void PersistUnlocked()
        {
            try
            {
                var directory = Path.GetDirectoryName(PersistFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    PersistFilePath,
                    JsonConvert.SerializeObject(_state, Formatting.Indented));
                TouchLoadedUtcUnlocked();
            }
            catch
            {
                // best-effort
            }
        }

        private void TouchLoadedUtcUnlocked()
        {
            try
            {
                _lastLoadedUtc = File.Exists(PersistFilePath)
                    ? File.GetLastWriteTimeUtc(PersistFilePath)
                    : DateTime.UtcNow;
            }
            catch
            {
                _lastLoadedUtc = DateTime.UtcNow;
            }
        }

        private static string ResolvePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "wb-scraping-auth.json";
            }

            return Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path);
        }
    }
}
