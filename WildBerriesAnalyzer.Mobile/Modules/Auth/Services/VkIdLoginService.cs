using System.Text;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.Auth.Services
{
    public sealed class VkIdLoginService : IVkIdLoginService
    {
        private readonly IAuthClient _authClient;

        public VkIdLoginService(IAuthClient authClient)
        {
            _authClient = authClient;
        }

        public async Task<AuthTokensResult> LoginAsync(CancellationToken cancellationToken = default)
        {
            AppLog.Action("Auth", "VkLogin", "start");

            var config = await _authClient.GetVkAuthConfigAsync(cancellationToken);
            if (!config.Enabled || string.IsNullOrWhiteSpace(config.ClientId))
            {
                AppLog.Warning("Auth", "VkLogin", "disabled or missing ClientId");
                throw new InvalidOperationException(
                    "VK ID отключён на сервере. В .env укажите VK_ID_ENABLED=true и VK_ID_CLIENT_ID, затем перезапустите server.");
            }

            if (string.IsNullOrWhiteSpace(config.RedirectUri))
            {
                AppLog.Warning("Auth", "VkLogin", "missing RedirectUri");
                throw new InvalidOperationException("Сервер не вернул RedirectUri для VK ID.");
            }

            // Android VK ID: vk{clientId}://vk.ru/blank.html (отдельного trusted redirect в кабинете нет).
            var redirectUri = ResolveMobileRedirectUri(config);
            var callbackRaw = string.IsNullOrWhiteSpace(config.AppCallbackUri)
                ? redirectUri
                : config.AppCallbackUri.Trim();
            // Callback WebAuthenticator должен совпадать со схемой redirect (или быть её префиксом).
            if (!callbackRaw.StartsWith("vk", StringComparison.OrdinalIgnoreCase) &&
                redirectUri.StartsWith("vk", StringComparison.OrdinalIgnoreCase))
            {
                callbackRaw = redirectUri;
            }

            if (!Uri.TryCreate(callbackRaw, UriKind.Absolute, out var callbackUri))
            {
                throw new InvalidOperationException($"Некорректный callback URI: {callbackRaw}");
            }

            var codeVerifier = Pkce.GenerateCodeVerifier();
            var codeChallenge = Pkce.CreateCodeChallengeS256(codeVerifier);
            var state = Pkce.GenerateState();

            var authorizeUrl = BuildAuthorizeUrl(config, redirectUri, codeChallenge, state);

            WebAuthenticatorResult authResult;
            try
            {
                authResult = await MainThread.InvokeOnMainThreadAsync(() =>
                    WebAuthenticator.Default.AuthenticateAsync(
                        new WebAuthenticatorOptions
                        {
                            Url = new Uri(authorizeUrl),
                            CallbackUrl = callbackUri,
                            // Ephemeral на части Android даёт ложный TaskCanceledException.
                            PrefersEphemeralWebBrowserSession = false
                        }));
            }
            catch (TaskCanceledException ex)
            {
                AppLog.Warning("Auth", "VkLogin", "cancel/return failed");
                AppLog.Error(ex, "Auth", "VkLogin", "TaskCanceled");
                throw new InvalidOperationException(
                    "Не удалось вернуться из VK в приложение. " +
                    "В кабинете VK добавьте redirect URI сервера и убедитесь, что после входа открывается PriceLab " +
                    "(или нажмите ссылку «Открыть PriceLab» на странице возврата).");
            }
            catch (OperationCanceledException ex)
            {
                AppLog.Warning("Auth", "VkLogin", "cancelled");
                AppLog.Error(ex, "Auth", "VkLogin", "cancelled");
                throw new InvalidOperationException("Авторизация VK прервана.");
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Auth", "VkLogin", "open authenticator");
                throw new InvalidOperationException($"Не удалось открыть авторизацию VK: {ex.Message}", ex);
            }

            var properties = MergeCallbackProperties(authResult);

            if (properties.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
            {
                properties.TryGetValue("error_description", out var desc);
                AppLog.Warning("Auth", "VkLogin", $"oauth error={error}");
                throw new UnauthorizedAccessException(
                    string.IsNullOrWhiteSpace(desc) ? error : $"{error}: {desc}");
            }

            var code = GetProperty(properties, "code");
            var returnedState = GetProperty(properties, "state");
            var deviceId = GetProperty(properties, "device_id");

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(returnedState) ||
                string.IsNullOrWhiteSpace(deviceId))
            {
                AppLog.Warning("Auth", "VkLogin", "missing code/state/device_id");
                throw new InvalidOperationException(
                    "VK ID не вернул code/state/device_id. Проверьте Redirect URI в кабинете VK и callback сервера.");
            }

            if (!string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                AppLog.Warning("Auth", "VkLogin", "state mismatch");
                throw new UnauthorizedAccessException("Несовпадение state — авторизация отклонена.");
            }

            var tokens = await _authClient.LoginWithVkAsync(new VkLoginRequest
            {
                Code = code,
                CodeVerifier = codeVerifier,
                DeviceId = deviceId,
                State = returnedState,
                RedirectUri = redirectUri
            });

            AppLog.Action("Auth", "VkLogin", $"success userId={tokens.UserId}");
            return tokens;
        }

        private static string ResolveMobileRedirectUri(VkAuthPublicConfig config)
        {
#if ANDROID
            if (!string.IsNullOrWhiteSpace(config.ClientId))
            {
                return $"vk{config.ClientId.Trim()}://vk.ru/blank.html";
            }
#endif
            if (!string.IsNullOrWhiteSpace(config.RedirectUri))
            {
                return config.RedirectUri.Trim();
            }

            return string.IsNullOrWhiteSpace(config.AppCallbackUri)
                ? "wbanalyzer://vk-auth"
                : config.AppCallbackUri.Trim();
        }

        private static string BuildAuthorizeUrl(
            VkAuthPublicConfig config,
            string redirectUri,
            string codeChallenge,
            string state)
        {
            var sb = new StringBuilder();
            sb.Append(config.AuthorizeUrl.TrimEnd('?', '&'));
            sb.Append("?response_type=code");
            sb.Append("&client_id=").Append(Uri.EscapeDataString(config.ClientId));
            sb.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
            sb.Append("&state=").Append(Uri.EscapeDataString(state));
            sb.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
            sb.Append("&code_challenge_method=S256");
            sb.Append("&lang=ru");
            if (!string.IsNullOrWhiteSpace(config.Scope))
            {
                sb.Append("&scope=").Append(Uri.EscapeDataString(config.Scope));
            }

            return sb.ToString();
        }

        private static Dictionary<string, string> MergeCallbackProperties(WebAuthenticatorResult result)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in result.Properties)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
                {
                    map[pair.Key] = pair.Value;
                }
            }

            if (result.CallbackUri is not null)
            {
                foreach (var pair in ParseQuery(result.CallbackUri))
                {
                    if (!map.ContainsKey(pair.Key))
                    {
                        map[pair.Key] = pair.Value;
                    }
                }
            }

            return map;
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseQuery(Uri uri)
        {
            var query = uri.Query;
            if (string.IsNullOrEmpty(query) && !string.IsNullOrEmpty(uri.Fragment))
            {
                query = uri.Fragment.TrimStart('#');
                if (!query.Contains('=', StringComparison.Ordinal))
                {
                    yield break;
                }

                query = "?" + query;
            }

            if (string.IsNullOrEmpty(query))
            {
                yield break;
            }

            var trimmed = query.TrimStart('?');
            foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(part[..eq]);
                var value = Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
                yield return new KeyValuePair<string, string>(key, value);
            }
        }

        private static string GetProperty(IReadOnlyDictionary<string, string> properties, string key) =>
            properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : string.Empty;
    }
}
