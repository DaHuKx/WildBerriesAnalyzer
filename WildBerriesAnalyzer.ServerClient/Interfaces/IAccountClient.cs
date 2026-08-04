using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.ServerClient.Interfaces
{
    public interface IAccountClient
    {
        Task<AccountProfile> GetMeAsync(CancellationToken cancellationToken = default);

        Task<VkLinkCodeResult> CreateVkLinkCodeAsync(CancellationToken cancellationToken = default);
    }
}
