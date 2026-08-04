using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data.Repositories
{
    public class UsersRepository : BaseRepository<WbUser>, IUsersRepository
    {
        public UsersRepository(WbDataBase context) : base(context)
        {

        }

        public async Task<WbUser?> GetUserByVkIdAsync(string vkId)
        {
            return await Context.Users.FirstOrDefaultAsync(u => u.VkId == vkId);
        }

        public async Task<WbUser?> GetUserByLoginAsync(string login)
        {
            return await Context.Users.FirstOrDefaultAsync(u => u.Login == login);
        }

        public async Task<WbUser?> GetUserByRefreshTokenAsync(string refreshToken)
        {
            return await Context.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        }

        public async Task<WbUser?> GetUserByAccessTokenAsync(string accessToken)
        {
            return await Context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);
        }
    }
}
