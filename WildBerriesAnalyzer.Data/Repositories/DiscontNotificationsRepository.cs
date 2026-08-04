using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public sealed class DiscontNotificationsRepository : IDiscontNotificationsRepository
    {
        private readonly WbDataBase _context;

        public DiscontNotificationsRepository(WbDataBase context)
        {
            _context = context;
        }

        public async Task<Dictionary<(int ProductId, ReferencePriceStrategy Strategy), DiscontNotification>> GetLastByUserAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.Set<DiscontNotification>()
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(
                n => (n.ProductId, n.ReferencePriceStrategy),
                n => n);
        }

        public async Task UpsertSentAsync(
            IEnumerable<DiscontNotification> notifications,
            CancellationToken cancellationToken = default)
        {
            var list = notifications?.ToList() ?? [];
            if (list.Count == 0)
            {
                return;
            }

            var userId = list[0].UserId;
            var productIds = list.Select(n => n.ProductId).Distinct().ToList();

            var existing = await _context.Set<DiscontNotification>()
                .Where(n => n.UserId == userId && productIds.Contains(n.ProductId))
                .ToListAsync(cancellationToken);

            var byKey = existing.ToDictionary(n => (n.ProductId, n.ReferencePriceStrategy));
            var now = DateTime.UtcNow;

            foreach (var incoming in list)
            {
                var key = (incoming.ProductId, incoming.ReferencePriceStrategy);
                if (byKey.TryGetValue(key, out var row))
                {
                    row.DiscontPercent = incoming.DiscontPercent;
                    row.CurrentPrice = incoming.CurrentPrice;
                    row.SentAt = incoming.SentAt;
                    row.PriceUpdateJobId = incoming.PriceUpdateJobId;
                    row.UpdatedAt = now;
                }
                else
                {
                    await _context.Set<DiscontNotification>().AddAsync(incoming, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
