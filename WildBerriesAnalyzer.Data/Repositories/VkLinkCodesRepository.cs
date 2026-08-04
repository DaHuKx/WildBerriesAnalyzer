using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public sealed class VkLinkCodesRepository : IVkLinkCodesRepository
    {
        private readonly WbDataBase _context;

        public VkLinkCodesRepository(WbDataBase context)
        {
            _context = context;
        }

        public async Task InvalidateUnusedForUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            var unused = await _context.Set<VkLinkCode>()
                .Where(c => c.UserId == userId && c.UsedAt == null)
                .ToListAsync(cancellationToken);

            if (unused.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var code in unused)
            {
                code.UsedAt = now;
                code.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<VkLinkCode> AddAsync(VkLinkCode entity, CancellationToken cancellationToken = default)
        {
            await _context.Set<VkLinkCode>().AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return entity;
        }

        public Task<VkLinkCode?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
            var now = DateTime.UtcNow;

            return _context.Set<VkLinkCode>()
                .Include(c => c.User)
                .FirstOrDefaultAsync(
                    c => c.Code == normalized && c.UsedAt == null && c.ExpiresAt > now,
                    cancellationToken);
        }

        public async Task MarkUsedAsync(VkLinkCode entity, CancellationToken cancellationToken = default)
        {
            entity.UsedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
