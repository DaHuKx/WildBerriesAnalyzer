using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.Auth.Services
{
    public interface IAuthSessionService
    {
        bool IsAuthenticated { get; }

        int? UserId { get; }

        string? Login { get; }

        WbUser? CurrentUser { get; }

        /// <summary>
        /// Восстанавливает токены из Preferences и при необходимости обновляет через refresh.
        /// </summary>
        Task<bool> TryRestoreSessionAsync();

        void SignIn(AuthTokensResult tokens);

        void SignOut();
    }
}
