using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Modules.Auth.Services
{
    public interface IVkIdLoginService
    {
        Task<AuthTokensResult> LoginAsync(CancellationToken cancellationToken = default);
    }
}
