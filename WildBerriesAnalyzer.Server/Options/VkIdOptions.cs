namespace WildBerriesAnalyzer.Server.Options
{
    public sealed class VkIdOptions
    {
        public const string SectionName = "VkId";

        /// <summary>
        /// Включить вход через VK ID.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// ID приложения VK ID (публичный).
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Сервисный ключ для конфиденциального приложения (опционально).
        /// </summary>
        public string ServiceToken { get; set; } = string.Empty;

        /// <summary>
        /// redirect_uri, зарегистрированный в кабинете VK ID.
        /// Обычно HTTPS callback на Server или схема приложения.
        /// </summary>
        public string RedirectUri { get; set; } = "wbanalyzer://vk-auth";

        /// <summary>
        /// Схема, которую ловит WebAuthenticator в Mobile.
        /// Если RedirectUri — HTTPS callback Server, сюда уходит 302 после VK.
        /// </summary>
        public string AppCallbackUri { get; set; } = "wbanalyzer://vk-auth";

        public string AuthorizeUrl { get; set; } = "https://id.vk.ru/authorize";

        public string TokenUrl { get; set; } = "https://id.vk.ru/oauth2/auth";

        public string UserInfoUrl { get; set; } = "https://id.vk.ru/oauth2/user_info";

        public string Scope { get; set; } = "vkid.personal_info";
    }
}
