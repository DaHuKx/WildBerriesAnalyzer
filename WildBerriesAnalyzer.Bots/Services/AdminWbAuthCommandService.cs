using WildBerriesAnalyzer.Bots.Models.Messages;
using WildBerriesAnalyzer.Business.Consts;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.WbScraping;

namespace WildBerriesAnalyzer.Bots.Services
{
    /// <summary>
    /// Админ-команды обновления WB/Ozon auth. Доступны только для фиксированного VkId.
    /// </summary>
    public sealed class AdminWbAuthCommandService
    {
        public const string AdminVkId = AdminAccounts.VkId;

        private readonly IWbScrapingAuthUpdater _wbAuthUpdater;
        private readonly IOzonScrapingAuthUpdater _ozonAuthUpdater;

        public AdminWbAuthCommandService(
            IWbScrapingAuthUpdater wbAuthUpdater,
            IOzonScrapingAuthUpdater ozonAuthUpdater)
        {
            _wbAuthUpdater = wbAuthUpdater ?? throw new ArgumentNullException(nameof(wbAuthUpdater));
            _ozonAuthUpdater = ozonAuthUpdater ?? throw new ArgumentNullException(nameof(ozonAuthUpdater));
        }

        public bool IsAdmin(string? vkId) =>
            string.Equals(vkId?.Trim(), AdminAccounts.VkId, StringComparison.Ordinal);

        /// <summary>
        /// Обрабатывает /token, /cookie wb, /cookie ozon.
        /// Возвращает false, если сообщение не админ-команда.
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
                if (_wbAuthUpdater.ApplyAccessToken(tokenValue))
                {
                    reply = "WB AccessToken обновлён.";
                }
                else
                {
                    reply = $"Не удалось обновить token: {_wbAuthUpdater.LastError}";
                }

                return true;
            }

            if (TryParseCookieCommand(text, out var market, out var cookieValue))
            {
                if (string.IsNullOrWhiteSpace(market))
                {
                    reply =
                        "Укажите маркетплейс:\n" +
                        "/cookie wb <cookie>\n" +
                        "/cookie ozon <cookie>";
                    return true;
                }

                if (string.Equals(market, "wb", StringComparison.OrdinalIgnoreCase))
                {
                    if (_wbAuthUpdater.ApplyCookie(cookieValue))
                    {
                        reply = "WB Cookie обновлена.";
                    }
                    else
                    {
                        reply = $"Не удалось обновить WB cookie: {_wbAuthUpdater.LastError}";
                    }

                    return true;
                }

                if (string.Equals(market, "ozon", StringComparison.OrdinalIgnoreCase))
                {
                    if (_ozonAuthUpdater.ApplyCookie(cookieValue))
                    {
                        reply = "Ozon Cookie обновлена.";
                    }
                    else
                    {
                        reply = $"Не удалось обновить Ozon cookie: {_ozonAuthUpdater.LastError}";
                    }

                    return true;
                }

                reply =
                    $"Неизвестный маркетплейс «{market}».\n" +
                    "/cookie wb <cookie>\n" +
                    "/cookie ozon <cookie>";
                return true;
            }

            return false;
        }

        /// <summary>
        /// /cookie → help; /cookie wb|ozon [value]
        /// </summary>
        private static bool TryParseCookieCommand(string text, out string market, out string value)
        {
            market = string.Empty;
            value = string.Empty;

            const string command = "/cookie";
            if (!text.StartsWith(command, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (text.Length == command.Length)
            {
                return true;
            }

            if (!char.IsWhiteSpace(text[command.Length]))
            {
                return false;
            }

            var rest = text[(command.Length + 1)..].Trim().TrimEnd(',');
            if (string.IsNullOrWhiteSpace(rest))
            {
                return true;
            }

            var parts = rest.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            market = parts[0].Trim();
            value = parts.Length > 1 ? parts[1].Trim().TrimEnd(',') : string.Empty;
            return true;
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
