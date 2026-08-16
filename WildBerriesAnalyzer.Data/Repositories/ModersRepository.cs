using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public class ModersRepository : IModersRepository
    {
        private readonly WbDataBase _context;

        public ModersRepository(WbDataBase context)
        {
            _context = context;
        }

        public Task<bool> IsModerAsync(int userId) =>
            _context.Moders.AsNoTracking().AnyAsync(m => m.UserId == userId);

        public Task<WbModer?> GetByUserIdAsync(int userId) =>
            _context.Moders.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == userId);
    }
}
