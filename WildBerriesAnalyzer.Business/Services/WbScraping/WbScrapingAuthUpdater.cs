using Newtonsoft.Json.Linq;

namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    public sealed class WbScrapingAuthUpdater : IWbScrapingAuthUpdater
    {
        private readonly IWbScrapingAuthStore _store;

        public WbScrapingAuthUpdater(IWbScrapingAuthStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string? LastError { get; private set; }

        public bool ApplyOauthBffTokenJson(string json)
        {
            LastError = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                LastError = "Пустой JSON.";
                return false;
            }

            try
            {
                var root = JObject.Parse(json);
                var accessToken =
                    root.Value<string>("accessToken")
                    ?? root.Value<string>("access_token");
                var validationKey =
                    root.Value<string>("validationKey")
                    ?? root.Value<string>("validation_key");

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    LastError = "В JSON нет accessToken.";
                    return false;
                }

                return ApplyManualTokens(accessToken, validationKey);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool ApplyManualTokens(string accessToken, string? validationKey = null, string? cookie = null)
        {
            LastError = null;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                LastError = "AccessToken пустой.";
                return false;
            }

            _store.Update(state =>
            {
                state.AccessToken = accessToken.Trim();

                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    state.Cookie = cookie.Trim();
                }

                if (!string.IsNullOrWhiteSpace(validationKey))
                {
                    state.Cookie = WbCookieHelper.UpsertCookie(state.Cookie, "wbx-validation-key", validationKey.Trim());
                }
            });

            LastError = $"Токены сохранены в {_store.PersistFilePath}";
            return true;
        }

        public bool ApplyAccessToken(string accessToken)
        {
            LastError = null;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                LastError = "AccessToken пустой.";
                return false;
            }

            _store.Update(state => state.AccessToken = accessToken.Trim());
            LastError = $"AccessToken сохранён в {_store.PersistFilePath}";
            return true;
        }

        public bool ApplyCookie(string cookie)
        {
            LastError = null;

            if (string.IsNullOrWhiteSpace(cookie))
            {
                LastError = "Cookie пустая.";
                return false;
            }

            _store.Update(state => state.Cookie = cookie.Trim());
            LastError = $"Cookie сохранена в {_store.PersistFilePath}";
            return true;
        }
    }
}
