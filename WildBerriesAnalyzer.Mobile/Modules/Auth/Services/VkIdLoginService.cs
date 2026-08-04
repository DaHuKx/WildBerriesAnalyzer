using System.Text;
using WildBerriesAnalyzer.Business.Models;
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
            var config = await _authClient.GetVkAuthConfigAsync(cancellationToken);
            if (!config.Enabled || string.IsNullOrWhiteSpace(config.ClientId))
            {
                throw new InvalidOperationException(
                    "Вход через VK не настроен. Укажите VkId:ClientId на Server и Enabled=true.");
            }

            var codeVerifier = Pkce.GenerateCodeVerifier();
            var codeChallenge = Pkce.CreateCodeChallengeS256(codeVerifier);
            var state = Pkce.GenerateState();

            var authorizeUrl = BuildAuthorizeUrl(config, codeChallenge, state);
            var callbackUri = new Uri(config.AppCallbackUri);

            WebAuthenticatorResult authResult;
            try
            {
                authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new WebAuthenticatorOptions
                    {
                        Url = new Uri(authorizeUrl),
                        CallbackUrl = callbackUri,
                        PrefersEphemeralWebBrowserSession = true
                    });
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException("Авторизация VK отменена.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Не удалось открыть авторизацию VK: {ex.Message}", ex);
            }

            if (authResult.Properties.TryGetValue("error", out var error) &&
                !string.IsNullOrWhiteSpace(error))
            {
                authResult.Properties.TryGetValue("error_description", out var desc);
                throw new UnauthorizedAccessException(
                    string.IsNullOrWhiteSpace(desc) ? error : $"{error}: {desc}");
            }

            var code = GetProperty(authResult, "code");
            var returnedState = GetProperty(authResult, "state");
            var deviceId = GetProperty(authResult, "device_id");

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(returnedState) ||
                string.IsNullOrWhiteSpace(deviceId))
            {
                throw new InvalidOperationException("VK ID не вернул code/state/device_id.");
            }

            if (!string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Несовпадение state — авторизация отклонена.");
            }

            return await _authClient.LoginWithVkAsync(new VkLoginRequest
            {
                Code = code,
                CodeVerifier = codeVerifier,
                DeviceId = deviceId,
                State = returnedState,
                RedirectUri = config.RedirectUri
            });
        }

        private static string BuildAuthorizeUrl(VkAuthPublicConfig config, string codeChallenge, string state)
        {
            var sb = new StringBuilder();
            sb.Append(config.AuthorizeUrl.TrimEnd('?', '&'));
            sb.Append("?response_type=code");
            sb.Append("&client_id=").Append(Uri.EscapeDataString(config.ClientId));
            sb.Append("&redirect_uri=").Append(Uri.EscapeDataString(config.RedirectUri));
            sb.Append("&state=").Append(Uri.EscapeDataString(state));
            sb.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
            sb.Append("&code_challenge_method=S256");
            if (!string.IsNullOrWhiteSpace(config.Scope))
            {
                sb.Append("&scope=").Append(Uri.EscapeDataString(config.Scope));
            }

            return sb.ToString();
        }

        private static string GetProperty(WebAuthenticatorResult result, string key)
        {
            if (result.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            foreach (var pair in result.Properties)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(pair.Value))
                {
                    return pair.Value;
                }
            }

            return string.Empty;
        }
    }
}
