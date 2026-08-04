using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    /// <summary>
    /// Интерфейс методов работы с WildBerries.
    /// </summary>
    public interface IWildBerriesService
    {
        /// <summary>
        /// Получение продуктов с WildBerries по названию.
        /// </summary>
        /// <param name="name">Название продукта.</param>
        /// <returns>Набор продуктов, полученных с WildBerries.</returns>
        Task<List<WbProduct>> ParseProductsAsync(string name);

        /// <summary>
        /// Получение цен входного набора продуктов.
        /// На входных сущностях обновляет Rating, ReviewRating и FeedBacksCount.
        /// </summary>
        Task<ParseProductsPricesResult> ParseProductsPricesAsync(IEnumerable<WbProduct> products);

        /// <summary>
        /// Получение продуктов с WildBerries по артикулу.
        /// </summary>
        /// <param name="ids">Список артикулов</param>
        /// <returns></returns>
        Task<List<WbProduct>> GetProductsForIdsAsync(IEnumerable<string> ids);
    }
}
