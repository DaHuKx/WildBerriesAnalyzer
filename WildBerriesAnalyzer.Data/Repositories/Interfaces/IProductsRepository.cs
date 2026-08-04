using System.Collections.Generic;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IProductsRepository : IBaseRepository<WbProduct>
    {
        Task<WbPrice> GetProductLastPriceAsync(int id);
        Task<IEnumerable<WbProduct>> GetProductsByNameAsync(string name);
        Task<long> GetProductsCountAsync();
        Task<IEnumerable<WbProduct>> GetProductsWithPricesAsync();
        Task<IEnumerable<WbProduct>> GetRandomProductsAsync(int count);
        Task<List<WbProduct>> GetUserBagProductsAsync(int userId);
        Task<List<WbProduct>> GetOrAddProducts(List<WbProduct> products);
    }
}
