using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface IActualDiscontsService
    {
        /// <summary>
        /// Пересчитать скидки по всем стратегиям и заменить снимок в БД.
        /// </summary>
        Task<int> RecalculateAndReplaceAsync(int? priceUpdateJobId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Актуальные скидки по фильтру пользователя.
        /// </summary>
        Task<List<Discont>> GetForFilterAsync(WbFilter filter, int? limit = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Актуальные скидки по userId (фильтр подгружается с деталями).
        /// </summary>
        Task<List<Discont>> GetForUserAsync(int userId, int? limit = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Все актуальные скидки без пользовательского фильтра.
        /// </summary>
        Task<List<Discont>> GetAllAsync(int? limit = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Сводка для главного экрана: товары, скидки, время обновления.
        /// </summary>
        Task<HomeDashboardSummary> GetHomeDashboardAsync(
            int userId,
            bool updatesEnabled,
            TimeSpan updateInterval,
            CancellationToken cancellationToken = default);
    }
}
