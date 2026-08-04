namespace WildBerriesAnalyzer.Business.Options
{
    /// <summary>
    /// Токены для запросов к www.wildberries.ru. Обновляются вручную.
    /// </summary>
    public class WbScrapingAuthOptions
    {
        public const string SectionName = "WbScrapingAuth";

        /// <summary>
        /// Bearer marketplace_web (из ответа oauth-bff/api/v1/token → accessToken).
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Cookie для www.wildberries.ru (в т.ч. wbx-validation-key, x_wbaas_token, device_id).
        /// </summary>
        public string Cookie { get; set; } = string.Empty;

        /// <summary>
        /// Заголовок deviceid.
        /// </summary>
        public string DeviceId { get; set; } = "site_23f3ade8eecd45d0975f5b011a90edca";

        public string UserAgent { get; set; } =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36 OPR/133.0.0.0 (Edition Yx GX)";

        public string SpaVersion { get; set; } = "14.19.1";

        public string SecChUa { get; set; } =
            "\"Opera GX\";v=\"133\", \"Chromium\";v=\"149\", \"Not)A;Brand\";v=\"24\"";

        /// <summary>
        /// Файл с текущими токенами (ручное обновление пишет сюда же).
        /// </summary>
        public string PersistFilePath { get; set; } = "wb-scraping-auth.json";
    }
}
