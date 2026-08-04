using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Consts;
using WildBerriesAnalyzer.Business.Services.WbScraping;

namespace WildBerriesAnalyzer.Bots.Services
{
    /// <summary>
    /// Админ-команды обновления WB auth. Доступны только для фиксированного VkId.
    /// </summary>
    public sealed class AdminWbAuthCommandService
    {
        public const string AdminVkId = AdminAccounts.VkId;

        private readonly IWbScrapingAuthUpdater _authUpdater;

        public AdminWbAuthCommandService(IWbScrapingAuthUpdater authUpdater)
        {
            _authUpdater = authUpdater ?? throw new ArgumentNullException(nameof(authUpdater));
        }

        public bool IsAdmin(string? vkId) =>
            string.Equals(vkId?.Trim(), AdminAccounts.VkId, StringComparison.Ordinal);

        /// <summary>
        /// Обрабатывает /token и /cookie. Возвращает false, если сообщение не админ-команда.
        /// </summary>
        public bool TryHandle(UserMessage message, out string reply)
        {
            reply = string.Empty;

            if (message is null || !IsAdmin(message.UserSocialId))
            {
                return false;
            }

            var text = message.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (TryParseCommand(text, "/token", out var tokenValue))
            {
                if (_authUpdater.ApplyAccessToken(tokenValue))
                {
                    reply = "WB AccessToken обновлён.";
                }
                else
                {
                    reply = $"Не удалось обновить token: {_authUpdater.LastError}";
                }

                return true;
            }

            if (TryParseCommand(text, "/cookie", out var cookieValue))
            {
                if (_authUpdater.ApplyCookie(cookieValue))
                {
                    reply = "WB Cookie обновлена.";
                }
                else
                {
                    reply = $"Не удалось обновить cookie: {_authUpdater.LastError}";
                }

                return true;
            }

            return false;
        }

        private static bool TryParseCommand(string text, string command, out string value)
        {
            value = string.Empty;

            if (!text.StartsWith(command, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (text.Length == command.Length)
            {
                return true;
            }

            var separator = text[command.Length];
            if (!char.IsWhiteSpace(separator))
            {
                return false;
            }

            value = text[(command.Length + 1)..].Trim().TrimEnd(',');
            return true;
        }
    }
}
