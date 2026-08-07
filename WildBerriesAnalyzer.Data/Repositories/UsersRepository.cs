using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public async Task TryUpdateMobileClientVersionAsync(int userId, string version)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(version))
            {
                return;
            }

            var normalized = version.Trim();
            if (normalized.Length > 32)
            {
                normalized = normalized.Substring(0, 32);
            }

            var user = await Context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return;
            }

            if (string.Equals(user.MobileClientVersion, normalized, StringComparison.Ordinal))
            {
                return;
            }

            user.MobileClientVersion = normalized;
            user.MobileClientVersionReportedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();
        }
    }
}
