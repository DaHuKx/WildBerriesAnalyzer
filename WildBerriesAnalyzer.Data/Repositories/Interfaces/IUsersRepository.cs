using System.Threading.Tasks;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories.Interfaces
{
    public interface IUsersRepository : IBaseRepository<WbUser>
    {
        Task<WbUser?> GetUserByVkIdAsync(string vkId);

        Task<WbUser?> GetUserByLoginAsync(string login);

        Task<WbUser?> GetUserByRefreshTokenAsync(string refreshToken);

        Task<WbUser?> GetUserByAccessTokenAsync(string accessToken);
    }
}
