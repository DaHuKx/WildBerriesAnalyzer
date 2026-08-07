using System;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    public class WbUser : BaseDbEntity
    {
        public string? VkId { get; set; }

        public string? Login { get; set; }

        public string? Password { get; set; }

        public string? AccessToken { get; set; }

        public string? RefreshToken { get; set; }

        public BotUserPlace BotPlace { get; set; }

        /// <summary>
        /// Последняя известная версия Mobile (semver major.minor.patch), например 1.0.19.
        /// </summary>
        public string? MobileClientVersion { get; set; }

        /// <summary>
        /// Когда клиент последний раз сообщил свою версию (UTC).
        /// </summary>
        public DateTime? MobileClientVersionReportedAt { get; set; }

        /// <summary>
        /// Фильтр по продуктам пользователя
        /// </summary>
        public WbFilter Filter { get; set; }
    }
}
