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

        public event EventHandler? CredentialsChanged;

        public WbScrapingAuthState GetSnapshot()
        {
            var raiseChanged = false;

            lock (_sync)
            {
                raiseChanged = TryReloadFromDiskUnlocked();
            }

            if (raiseChanged)
            {
                RaiseCredentialsChanged();
            }

            lock (_sync)
            {
                return _state.Clone();
            }
        }

        public void Update(Action<WbScrapingAuthState> update)
        {
            ArgumentNullException.ThrowIfNull(update);

            var raiseChanged = false;

            lock (_sync)
            {
                var previousToken = _state.AccessToken;
                var previousCookie = _state.Cookie;

                update(_state);
                PersistUnlocked();

                raiseChanged = !CredentialsEqual(previousToken, _state.AccessToken) ||
                               !CredentialsEqual(previousCookie, _state.Cookie);
            }

            if (raiseChanged)
            {
                RaiseCredentialsChanged();
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
                        FillMissingMeta(fromFile, seeded);
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

        /// <returns>true, если AccessToken/Cookie изменились.</returns>
        private bool TryReloadFromDiskUnlocked()
        {
            try
            {
                if (!File.Exists(PersistFilePath))
                {
                    return false;
                }

                var lastWrite = File.GetLastWriteTimeUtc(PersistFilePath);
                if (lastWrite <= _lastLoadedUtc)
                {
                    return false;
                }

                var json = File.ReadAllText(PersistFilePath);
                var fromFile = JsonConvert.DeserializeObject<WbScrapingAuthState>(json);
                if (fromFile is null)
                {
                    return false;
                }

                FillMissingMeta(fromFile, _state);

                var changed = !CredentialsEqual(_state.AccessToken, fromFile.AccessToken) ||
                              !CredentialsEqual(_state.Cookie, fromFile.Cookie);

                _state = fromFile;
                _lastLoadedUtc = lastWrite;
                return changed;
            }
            catch
            {
                return false;
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

        private void RaiseCredentialsChanged()
        {
            try
            {
                CredentialsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // подписчики не должны ломать обновление auth
            }
        }

        private static void FillMissingMeta(WbScrapingAuthState target, WbScrapingAuthState fallback)
        {
            if (string.IsNullOrWhiteSpace(target.DeviceId))
            {
                target.DeviceId = fallback.DeviceId;
            }

            if (string.IsNullOrWhiteSpace(target.UserAgent))
            {
                target.UserAgent = fallback.UserAgent;
            }

            if (string.IsNullOrWhiteSpace(target.SpaVersion))
            {
                target.SpaVersion = fallback.SpaVersion;
            }

            if (string.IsNullOrWhiteSpace(target.SecChUa))
            {
                target.SecChUa = fallback.SecChUa;
            }
        }

        private static bool CredentialsEqual(string? left, string? right) =>
            string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);

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
