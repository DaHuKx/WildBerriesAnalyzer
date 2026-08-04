using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.ServerClient.Interfaces
{
    public interface IDashboardClient
    {
        Task<HomeDashboardSummary> GetHomeAsync(CancellationToken cancellationToken = default);
    }
}
