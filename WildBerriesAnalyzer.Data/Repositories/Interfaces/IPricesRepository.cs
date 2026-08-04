using System.Collections.Generic;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IPricesRepository : IBaseRepository<WbPrice>
    {
        Task<long> GetPricesCountAsync();

        Task<IEnumerable<WbPrice>> GetProductPricesAsync(WbProduct product);

        Task AddPricesFromProductsAsync(IEnumerable<WbProduct> products);
    }
}
