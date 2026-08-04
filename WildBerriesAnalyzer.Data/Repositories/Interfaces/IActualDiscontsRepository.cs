using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IActualDiscontsRepository
    {
        /// <summary>
        /// Полностью заменить снимок актуальных скидок.
        /// </summary>
        Task ReplaceAllAsync(IEnumerable<WbActualDiscont> disconts, CancellationToken cancellationToken = default);

        /// <summary>
        /// Все актуальные скидки с продуктами.
        /// </summary>
        Task<List<WbActualDiscont>> GetAllWithProductsAsync(CancellationToken cancellationToken = default);

        Task<long> GetCountAsync(CancellationToken cancellationToken = default);
    }
}
