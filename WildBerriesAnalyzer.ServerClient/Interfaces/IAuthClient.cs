using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;

namespace WildBerriesAnalyzer.ServerClient.Interfaces
{
    /// <summary>
    /// HTTP-клиент к WildBerriesAnalyzer.Server (авторизация).
    /// </summary>
    public interface IAuthClient : IAuthService
    {
        Task<VkAuthPublicConfig> GetVkAuthConfigAsync(CancellationToken cancellationToken = default);
    }
}
