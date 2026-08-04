using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public sealed class ActualDiscontsRepository : IActualDiscontsRepository
    {
        private readonly WbDataBase _context;

        public ActualDiscontsRepository(WbDataBase context)
        {
            _context = context;
        }

        public async Task ReplaceAllAsync(
            IEnumerable<WbActualDiscont> disconts,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.ActualDisconts.ToListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                _context.ActualDisconts.RemoveRange(existing);
            }

            var list = disconts as IList<WbActualDiscont> ?? disconts.ToList();
            if (list.Count > 0)
            {
                await _context.ActualDisconts.AddRangeAsync(list);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<List<WbActualDiscont>> GetAllWithProductsAsync(CancellationToken cancellationToken = default)
        {
            return _context.ActualDisconts
                .AsNoTracking()
                .Include(d => d.Product)
                .OrderByDescending(d => d.DiscontPercent)
                .ToListAsync(cancellationToken);
        }

        public Task<long> GetCountAsync(CancellationToken cancellationToken = default)
        {
            return _context.ActualDisconts.LongCountAsync(cancellationToken);
        }
    }
}
