using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Server.Options;

namespace WildBerriesAnalyzer.Server.Services.VkId
{
    public sealed class VkIdOAuthClient : IVkIdOAuthClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly VkIdOptions _options;

        public VkIdOAuthClient(HttpClient httpClient, IOptions<VkIdOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<VkIdTokenResponse> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            string deviceId,
            string state,
            string redirectUri,
            CancellationToken cancellationToken = default)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["code_verifier"] = codeVerifier,
                ["client_id"] = _options.ClientId,
                ["device_id"] = deviceId,
                ["redirect_uri"] = redirectUri,
                ["state"] = state
            };

            if (!string.IsNullOrWhiteSpace(_options.ServiceToken))
            {
                form["service_token"] = _options.ServiceToken;
            }

            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient.PostAsync(_options.TokenUrl, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(ExtractVkError(body) ?? $"VK ID token error: {(int)response.StatusCode}");
            }

            var parsed = JsonSerializer.Deserialize<TokenDto>(body, JsonOptions)
                         ?? throw new InvalidOperationException("Пустой ответ VK ID при обмене кода.");

            if (string.IsNullOrWhiteSpace(parsed.AccessToken))
            {
                throw new InvalidOperationException(ExtractVkError(body) ?? "VK ID не вернул access_token.");
            }

            return new VkIdTokenResponse
            {
                AccessToken = parsed.AccessToken,
                RefreshToken = parsed.RefreshToken ?? string.Empty,
                IdToken = parsed.IdToken ?? string.Empty,
                UserId = ReadUserId(parsed.UserId),
                State = parsed.State ?? string.Empty
            };
        }

        private static string ReadUserId(JsonElement userId)
        {
            return userId.ValueKind switch
            {
                JsonValueKind.Number => userId.GetInt64().ToString(),
                JsonValueKind.String => userId.GetString() ?? string.Empty,
                _ => string.Empty
            };
        }

        public async Task<VkIdUserInfo> GetUserInfoAsync(
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            var form = new Dictionary<string, string>
            {
                ["access_token"] = accessToken,
                ["client_id"] = _options.ClientId
            };

            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient.PostAsync(_options.UserInfoUrl, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(ExtractVkError(body) ?? $"VK ID user_info error: {(int)response.StatusCode}");
            }

            var parsed = JsonSerializer.Deserialize<UserInfoDto>(body, JsonOptions);
            var user = parsed?.User;
            if (user is null || string.IsNullOrWhiteSpace(user.UserId))
            {
                throw new InvalidOperationException("VK ID не вернул данные пользователя.");
            }

            return new VkIdUserInfo
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }

        private static string? ExtractVkError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error_description", out var desc))
                {
                    return desc.GetString();
                }

                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    return error.ValueKind == JsonValueKind.String
                        ? error.GetString()
                        : error.ToString();
                }
            }
            catch (JsonException)
            {
                // fall through
            }

            return body.Length > 300 ? body[..300] : body;
        }

        private sealed class TokenDto
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("id_token")]
            public string? IdToken { get; set; }

            [JsonPropertyName("user_id")]
            public JsonElement UserId { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; }
        }

        private sealed class UserInfoDto
        {
            [JsonPropertyName("user")]
            public UserDto? User { get; set; }
        }

        private sealed class UserDto
        {
            [JsonPropertyName("user_id")]
            public string? UserId { get; set; }

            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }
        }
    }
}
