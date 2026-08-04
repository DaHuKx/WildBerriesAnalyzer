using System.Threading;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IVkLinkCodesRepository
    {
        Task InvalidateUnusedForUserAsync(int userId, CancellationToken cancellationToken = default);

        Task<VkLinkCode> AddAsync(VkLinkCode entity, CancellationToken cancellationToken = default);

        Task<VkLinkCode?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task MarkUsedAsync(VkLinkCode entity, CancellationToken cancellationToken = default);
    }
}
