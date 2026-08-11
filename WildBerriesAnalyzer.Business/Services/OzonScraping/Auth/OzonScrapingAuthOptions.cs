using System.Text.Json.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;

/// <summary>
/// Параметры HTTP/browser-сессии для composer-api Ozon.
/// Заполните ozon-scraping-auth.json перед live-запуском.
/// </summary>
public sealed class OzonScrapingAuthOptions
{
    public const string SectionName = "OzonScraping";
    public const string DefaultFileName = "ozon-scraping-auth.json";

    /// <summary>
    /// Полная строка Cookie из браузера (ozon.ru).
    /// Критично: __Secure-access-token, __Secure-refresh-token, abt_data.
    /// При useBrowser=true cookie подставляются в Chromium (рекомендуется).
    /// </summary>
    [JsonPropertyName("cookie")]
    public string Cookie { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent браузера, из которого скопированы cookie.
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";

    /// <summary>
    /// Базовый host сайта. Обычно https://www.ozon.ru
    /// </summary>
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "https://www.ozon.ru";

    /// <summary>
    /// Путь composer endpoint относительно BaseUrl (для HttpClient-режима).
    /// </summary>
    [JsonPropertyName("composerPath")]
    public string ComposerPath { get; set; } = "/api/composer-api.bx/page/json/v2";

    /// <summary>
    /// Опциональный HTTP/HTTPS proxy (например http://user:pass@host:port).
    /// Для Ozon часто нужен RU residential proxy.
    /// </summary>
    [JsonPropertyName("proxyUrl")]
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// Пауза между запросами карточек по id (мс).
    /// </summary>
    [JsonPropertyName("requestDelayMs")]
    public int RequestDelayMs { get; set; } = 400;

    /// <summary>
    /// true (по умолчанию) — Chromium через Playwright (обходит Variti 307/__rr).
    /// false — сырой HttpClient (часто даёт бесконечный 307 без браузерного challenge).
    /// </summary>
    [JsonPropertyName("useBrowser")]
    public bool UseBrowser { get; set; } = true;

    /// <summary>
    /// Headless Chromium. false — видно окно (удобно отладить антибот).
    /// </summary>
    [JsonPropertyName("headless")]
    public bool Headless { get; set; } = true;

    /// <summary>
    /// Сколько ждать JS-challenge на главной (мс).
    /// </summary>
    [JsonPropertyName("challengeWaitMs")]
    public int ChallengeWaitMs { get; set; } = 12000;

    /// <summary>
    /// Лимит товаров для <see cref="Interfaces.IParseService.ParseProductsAsync"/>.
    /// </summary>
    [JsonPropertyName("searchLimit")]
    public int SearchLimit { get; set; } = 36;

    public bool HasCookie => !string.IsNullOrWhiteSpace(Cookie);

    public static OzonScrapingAuthOptions CreateDefault() => new();
}
