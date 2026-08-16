using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IModersRepository
    {
        Task<bool> IsModerAsync(int userId);

        Task<WbModer?> GetByUserIdAsync(int userId);
    }
}
