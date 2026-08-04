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
        /// Фильтр по продуктам пользователя
        /// </summary>
        public WbFilter Filter { get; set; }
    }
}
