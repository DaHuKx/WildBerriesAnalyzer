using System.Text;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace ModerMobile.Auth;

public interface IVkIdLoginService
{
    Task<AuthTokensResult> LoginAsync(CancellationToken cancellationToken = default);
}

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
                "VK ID отключён на сервере. Включите VK_ID_ENABLED и ClientId.");
        }

        if (string.IsNullOrWhiteSpace(config.RedirectUri))
        {
            throw new InvalidOperationException("Сервер не вернул RedirectUri для VK ID.");
        }

        var redirectUri = ResolveMobileRedirectUri(config);
        var callbackRaw = string.IsNullOrWhiteSpace(config.AppCallbackUri)
            ? redirectUri
            : config.AppCallbackUri.Trim();
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
                        PrefersEphemeralWebBrowserSession = false
                    }));
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException(
                "Не удалось вернуться из VK в ModerMobile. Проверьте redirect URI.");
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException("Авторизация VK прервана.");
        }

        var properties = MergeCallbackProperties(authResult);

        if (properties.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
        {
            properties.TryGetValue("error_description", out var desc);
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
            throw new InvalidOperationException(
                "VK ID не вернул code/state/device_id.");
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
            RedirectUri = redirectUri,
            Client = "moder"
        });
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
            ? "wbmoder://vk-auth"
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
