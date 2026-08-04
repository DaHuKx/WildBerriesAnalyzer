using System.Threading;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    /// <summary>
    /// Outbox задач обновления цен (Server пишет, Bots читает).
    /// </summary>
    public interface IPriceUpdateJobsRepository
    {
        /// <summary>
        /// Server: создать Pending-задачу после успешного обновления цен.
        /// </summary>
        Task<PriceUpdateJob> EnqueueCompletedAsync(
            int productsCount,
            int pricesSavedCount,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bots: атомарно взять следующую Pending (или просроченную Processing) задачу.
        /// </summary>
        Task<PriceUpdateJob?> ClaimNextAsync(
            string workerId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bots: пометить задачу обработанной.
        /// </summary>
        Task MarkProcessedAsync(int jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bots: пометить задачу ошибочной (вернётся в очередь по политике retry).
        /// </summary>
        Task MarkFailedAsync(int jobId, string error, CancellationToken cancellationToken = default);
    }
}
