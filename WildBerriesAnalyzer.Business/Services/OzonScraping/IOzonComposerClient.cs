using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping;

public interface IOzonComposerClient : IAsyncDisposable, IDisposable
{
    Task<OzonComposerPage> FetchPageAsync(string sitePath, CancellationToken ct = default);

    /// <summary>
    /// Страница товара: навигация на /product/{sku}/ и разбор (HTML / network).
    /// </summary>
    Task<OzonComposerPage> FetchProductPageAsync(long sku, CancellationToken ct = default);

    /// <summary>
    /// Страница товара по любой ссылке Ozon (/product/…, /t/… и т.п.) — тот же парсинг, что и по SKU.
    /// </summary>
    Task<OzonComposerPage> FetchProductByUrlAsync(string productUrl, CancellationToken ct = default);

    /// <summary>
    /// Запускает браузер и проходит antibot-challenge заранее (no-op в HttpClient-режиме).
    /// </summary>
    Task WarmUpAsync(CancellationToken ct = default);

    /// <summary>
    /// Страница общей корзины /cart?share=…
    /// </summary>
    Task<OzonComposerPage> FetchCartSharePageAsync(string shareToken, CancellationToken ct = default);
}
