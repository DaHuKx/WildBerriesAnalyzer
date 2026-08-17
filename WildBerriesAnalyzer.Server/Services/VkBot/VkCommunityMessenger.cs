using System.Text.Json;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Server.Options;

namespace WildBerriesAnalyzer.Server.Services.VkBot
{
    public sealed class VkCommunityMessenger : IVkCommunityMessenger
    {
        private readonly HttpClient _httpClient;
        private readonly VkBotOptions _options;
        private readonly string _accessToken;
        private readonly ILogger<VkCommunityMessenger> _logger;
        private readonly Random _random = new();

        public VkCommunityMessenger(
            HttpClient httpClient,
            IOptions<VkBotOptions> options,
            ILogger<VkCommunityMessenger> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            // .env часто даёт пробел/CRLF в конце — VK тогда отвечает error 5.
            _accessToken = (_options.AccessToken ?? string.Empty).Trim();
            _logger = logger;
        }

        public string BotChatUrl =>
            string.IsNullOrWhiteSpace(_options.WriteUrl)
                ? $"https://vk.me/club{_options.GroupId}"
                : _options.WriteUrl.Trim();

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_accessToken);

        public async Task<string> ResolveUserIdAsync(
            string profileUrlOrScreenName,
            CancellationToken cancellationToken = default)
        {
            if (!VkProfileLinkParser.TryParse(profileUrlOrScreenName, out var screenNameOrId, out var isNumericId))
            {
                throw new ArgumentException(
                    "Укажите корректную ссылку на профиль VK (например, https://vk.com/id123 или https://vk.com/username).");
            }

            if (isNumericId)
            {
                return screenNameOrId;
            }

            EnsureConfigured();

            var url =
                $"https://api.vk.com/method/utils.resolveScreenName" +
                $"?screen_name={Uri.EscapeDataString(screenNameOrId)}" +
                $"&access_token={Uri.EscapeDataString(_accessToken)}" +
                $"&v={Uri.EscapeDataString(_options.ApiVersion)}";

            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("error_msg", out var msg)
                    ? msg.GetString()
                    : "Не удалось распознать профиль VK.";
                throw new ArgumentException(message);
            }

            if (!doc.RootElement.TryGetProperty("response", out var resolved) ||
                resolved.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Профиль VK не найден. Проверьте ссылку.");
            }

            var type = resolved.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            if (!string.Equals(type, "user", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Ссылка должна вести на профиль пользователя VK, а не на группу.");
            }

            if (!resolved.TryGetProperty("object_id", out var objectIdEl))
            {
                throw new ArgumentException("Не удалось определить VkId по ссылке.");
            }

            return objectIdEl.ValueKind == JsonValueKind.Number
                ? objectIdEl.GetInt64().ToString()
                : objectIdEl.GetString()
                  ?? throw new ArgumentException("Не удалось определить VkId по ссылке.");
        }

        public async Task<bool> TrySendMessageAsync(
            string vkUserId,
            string text,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(vkUserId) || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                // POST: надёжнее GET (длинный текст / спецсимволы не ломают access_token в query).
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["user_id"] = vkUserId.Trim(),
                    ["random_id"] = _random.Next().ToString(),
                    ["message"] = text,
                    ["access_token"] = _accessToken,
                    ["v"] = _options.ApiVersion
                });

                using var response = await _httpClient
                    .PostAsync("https://api.vk.com/method/messages.send", content, cancellationToken)
                    .ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    var code = error.TryGetProperty("error_code", out var codeEl) ? codeEl.GetInt32() : 0;
                    var message = error.TryGetProperty("error_msg", out var msgEl) ? msgEl.GetString() : "unknown";
                    _logger.LogWarning("VK messages.send failed for {VkUserId}: {Code} {Message}", vkUserId, code, message);
                    return false;
                }

                return doc.RootElement.TryGetProperty("response", out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VK messages.send exception for {VkUserId}", vkUserId);
                return false;
            }
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "VK-бот не настроен на сервере (VkBot:AccessToken). Обратитесь к администратору.");
            }
        }
    }
}
