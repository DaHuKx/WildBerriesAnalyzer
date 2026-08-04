using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface IAccountService
    {
        Task<AccountProfile> GetProfileAsync(int userId);

        Task<VkLinkCodeResult> CreateVkLinkCodeAsync(int userId);

        /// <summary>
        /// Подтверждение привязки из VK-бота по одноразовому коду.
        /// </summary>
        Task<VkLinkConfirmResult> ConfirmVkLinkAsync(string vkId, string rawMessage);
    }
}
