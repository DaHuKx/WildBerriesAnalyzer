using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public sealed class PriceUpdateJobsRepository : IPriceUpdateJobsRepository
    {
        /// <summary>
        /// Processing дольше этого интервала снова становится доступной для Claim.
        /// </summary>
        private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(30);

        private const int MaxAttempts = 5;

        private readonly WbDataBase _context;

        public PriceUpdateJobsRepository(WbDataBase context)
        {
            _context = context;
        }

        public async Task<PriceUpdateJob> EnqueueCompletedAsync(
            int productsCount,
            int pricesSavedCount,
            CancellationToken cancellationToken = default)
        {
            var job = new PriceUpdateJob
            {
                Status = PriceUpdateJobStatus.Pending,
                CompletedAt = DateTime.UtcNow,
                ProductsCount = productsCount,
                PricesSavedCount = pricesSavedCount
            };

            await _context.PriceUpdateJobs.AddAsync(job, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return job;
        }

        public async Task<PriceUpdateJob?> ClaimNextAsync(
            string workerId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(workerId))
            {
                throw new ArgumentException("workerId обязателен.", nameof(workerId));
            }

            var now = DateTime.UtcNow;
            var lockExpiredBefore = now - LockTimeout;

            using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
            {
                var job = await _context.PriceUpdateJobs
                    .Where(j =>
                        j.Status == PriceUpdateJobStatus.Pending
                        || (j.Status == PriceUpdateJobStatus.Processing
                            && j.LockedAt != null
                            && j.LockedAt < lockExpiredBefore)
                        || (j.Status == PriceUpdateJobStatus.Failed
                            && j.AttemptCount < MaxAttempts))
                    .OrderBy(j => j.CompletedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (job is null)
                {
                    transaction.Commit();
                    return null;
                }

                job.Status = PriceUpdateJobStatus.Processing;
                job.LockedAt = now;
                job.LockedBy = workerId;
                job.AttemptCount += 1;
                job.UpdatedAt = now;
                job.LastError = null;

                await _context.SaveChangesAsync(cancellationToken);
                transaction.Commit();
                return job;
            }
        }

        public async Task MarkProcessedAsync(int jobId, CancellationToken cancellationToken = default)
        {
            var job = await _context.PriceUpdateJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                return;
            }

            job.Status = PriceUpdateJobStatus.Processed;
            job.ProcessedAt = DateTime.UtcNow;
            job.UpdatedAt = job.ProcessedAt;
            job.LastError = null;

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkFailedAsync(int jobId, string error, CancellationToken cancellationToken = default)
        {
            var job = await _context.PriceUpdateJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                return;
            }

            job.Status = PriceUpdateJobStatus.Failed;
            job.UpdatedAt = DateTime.UtcNow;
            job.LastError = Truncate(error, 2000);

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
            {
                return value;
            }

            return value.Substring(0, max);
        }
    }
}
