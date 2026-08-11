using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    /// <summary>
    /// Общий контракт парсинга товаров маркетплейса (WB / Ozon).
    /// </summary>
    public interface IParseService
    {
        /// <summary>
        /// Поиск товаров по названию / текстовому запросу.
        /// </summary>
        Task<List<WbProduct>> ParseProductsAsync(string name);

        /// <summary>
        /// Получение карточек товаров по артикулам (nmId / SKU).
        /// </summary>
        Task<List<WbProduct>> GetProductsForIdsAsync(IEnumerable<string> ids);

        /// <summary>
        /// Актуальные цены (и обновление meta) для набора товаров.
        /// </summary>
        Task<ParseProductsPricesResult> ParseProductsPricesAsync(IEnumerable<WbProduct> products);
    }
}
