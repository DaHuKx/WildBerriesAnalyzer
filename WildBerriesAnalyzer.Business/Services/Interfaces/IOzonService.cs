namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    /// <summary>
    /// Парсер витрины Ozon.
    /// </summary>
    public interface IOzonService : IParseService, IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Запускает Chromium и проходит antibot-challenge заранее,
        /// чтобы первый поиск по названию не ждал холодный старт.
        /// </summary>
        Task WarmUpAsync(CancellationToken ct = default);

        /// <summary>
        /// Артикулы (SKU) из общей корзины Ozon (/cart?share=…).
        /// </summary>
        Task<List<string>> GetArticlesFromCartShareAsync(string shareToken, CancellationToken ct = default);
    }
}
