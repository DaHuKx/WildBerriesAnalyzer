using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IPricesRepository : IBaseRepository<WbPrice>
    {
        Task<long> GetPricesCountAsync();

        Task<IEnumerable<WbPrice>> GetProductPricesAsync(WbProduct product);

        /// <summary>
        /// История цен товара, отсортированная по CheckTime ASC.
        /// </summary>
        /// <param name="fromUtc">Нижняя граница (включительно); null — без ограничения.</param>
        /// <param name="take">Максимум точек (с конца периода, затем снова ASC); null — без лимита.</param>
        Task<List<WbPrice>> GetProductPricesAsync(int productId, DateTime? fromUtc, int? take);

        Task AddPricesFromProductsAsync(IEnumerable<WbProduct> products);
    }
}
