namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    /// <summary>
    /// Ручное обновление marketplace-токенов (без auto-refresh).
    /// </summary>
    public interface IWbScrapingAuthUpdater
    {
        string? LastError { get; }

        /// <summary>
        /// Применяет ответ POST www.wildberries.ru/oauth-bff/api/v1/token.
        /// </summary>
        bool ApplyOauthBffTokenJson(string json);

        /// <summary>
        /// Задаёт AccessToken и опционально validationKey / полную cookie-строку.
        /// </summary>
        bool ApplyManualTokens(string accessToken, string? validationKey = null, string? cookie = null);

        /// <summary>
        /// Обновляет только AccessToken.
        /// </summary>
        bool ApplyAccessToken(string accessToken);

        /// <summary>
        /// Обновляет только Cookie.
        /// </summary>
        bool ApplyCookie(string cookie);
    }
}
