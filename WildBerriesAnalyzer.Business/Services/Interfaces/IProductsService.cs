using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface IProductsService
    {
        Task<WbProduct?> GetByIdAsync(int id);

        Task<List<WbProduct>> GetByNameAsync(string name);

        Task<List<WbProduct>> GetRandomAsync(int count);

        Task<long> GetCountAsync();

        Task<WbPrice> GetLastPriceAsync(int productId);

        /// <summary>
        /// История цен товара за период (для графика Mobile).
        /// </summary>
        Task<ProductPriceHistory> GetPriceHistoryAsync(int productId, PriceHistoryPeriod period);

        List<WbProduct> FilterProductsByName(IEnumerable<WbProduct> products, string productName);

        /// <summary>
        /// Поиск на выбранных маркетплейсах по названию (без сохранения в БД).
        /// null / пусто = все магазины.
        /// </summary>
        Task<List<WbProduct>> SearchOnWildBerriesAsync(
            string name,
            IReadOnlyCollection<MarketType>? marketTypes = null);

        /// <summary>
        /// Добавить в каталог по артикулам / ссылкам выбранного маркетплейса.
        /// </summary>
        Task<AddCatalogProductsResult> AddByArticlesAsync(
            IEnumerable<string> articleInputs,
            MarketType marketType = MarketType.Wildberries);

        /// <summary>
        /// Найти по названию на выбранных маркетплейсах и добавить новые товары в каталог.
        /// null / пусто = все магазины.
        /// </summary>
        Task<AddCatalogProductsResult> AddByNameAsync(
            string name,
            IReadOnlyCollection<MarketType>? marketTypes = null);
    }
}
