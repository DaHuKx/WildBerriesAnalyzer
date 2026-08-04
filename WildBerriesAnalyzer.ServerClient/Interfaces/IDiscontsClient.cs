using WildBerriesAnalyzer.Domain.Models;

namespace WildBerriesAnalyzer.ServerClient.Interfaces
{
    public interface IDiscontsClient
    {
        Task<List<Discont>> GetForCurrentUserAsync(int? limit = 50, CancellationToken cancellationToken = default);

        Task<List<Discont>> GetAllAsync(int? limit = 100, CancellationToken cancellationToken = default);

        Task<List<Discont>> GetForUserAsync(int userId, int? limit = 50, CancellationToken cancellationToken = default);
    }
}
