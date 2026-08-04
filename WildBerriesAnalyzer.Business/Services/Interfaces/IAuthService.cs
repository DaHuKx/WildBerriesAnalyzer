using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// Начинает регистрацию: отправляет код в VK. Аккаунт создаётся после ConfirmRegisterAsync.
        /// </summary>
        Task<RegisterResult> RegisterAsync(string login, string password, string vkProfileUrl);

        /// <summary>
        /// Подтверждает регистрацию кодом из VK и выдаёт токены.
        /// </summary>
        Task<AuthTokensResult> ConfirmRegisterAsync(string registrationId, string code);

        /// <summary>
        /// Повторно отправляет код подтверждения в VK.
        /// </summary>
        Task<RegisterResult> ResendRegisterCodeAsync(string registrationId);

        Task<AuthTokensResult> LoginAsync(string login, string password);

        Task<AuthTokensResult> RefreshAsync(string refreshToken);

        /// <summary>
        /// Вход / регистрация через VK ID (authorization code + PKCE).
        /// </summary>
        Task<AuthTokensResult> LoginWithVkAsync(VkLoginRequest request);
    }
}
