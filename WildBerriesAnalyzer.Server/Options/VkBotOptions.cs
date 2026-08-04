namespace WildBerriesAnalyzer.Server.Options
{
    public sealed class VkBotOptions
    {
        public const string SectionName = "VkBot";

        /// <summary>
        /// ID сообщества VK (без минуса).
        /// </summary>
        public long GroupId { get; set; } = 219811363;

        /// <summary>
        /// Ключ доступа сообщества (messages + offline).
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Ссылка на чат с ботом / сообществом (для пользователя в приложении).
        /// </summary>
        public string WriteUrl { get; set; } = "https://vk.me/club219811363";

        public string ApiVersion { get; set; } = "5.199";
    }
}
