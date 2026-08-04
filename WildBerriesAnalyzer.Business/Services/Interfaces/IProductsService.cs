using WildBerriesAnalyzer.Business.Models;
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

        List<WbProduct> FilterProductsByName(IEnumerable<WbProduct> products, string productName);

        /// <summary>
        /// Поиск на WildBerries по названию (без сохранения в БД).
        /// </summary>
        Task<List<WbProduct>> SearchOnWildBerriesAsync(string name);

        /// <summary>
        /// Добавить в каталог по артикулам / ссылкам WB.
        /// </summary>
        Task<AddCatalogProductsResult> AddByArticlesAsync(IEnumerable<string> articleInputs);

        /// <summary>
        /// Найти на WB по названию и добавить новые товары в каталог.
        /// </summary>
        Task<AddCatalogProductsResult> AddByNameAsync(string name);
    }
}
