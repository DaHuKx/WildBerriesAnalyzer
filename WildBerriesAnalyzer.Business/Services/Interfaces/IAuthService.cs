using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// Устаревший поток регистрации (логин/пароль + код в сообществе VK).
        /// Основной путь для Mobile — <see cref="LoginWithVkAsync"/>.
        /// </summary>
        Task<RegisterResult> RegisterAsync(string login, string password, string vkProfileUrl);

        /// <summary>
        /// Подтверждает устаревшую регистрацию кодом из VK и выдаёт токены.
        /// </summary>
        Task<AuthTokensResult> ConfirmRegisterAsync(string registrationId, string code);

        /// <summary>
        /// Повторно отправляет код подтверждения в VK (устаревший поток).
        /// </summary>
        Task<RegisterResult> ResendRegisterCodeAsync(string registrationId);

        Task<AuthTokensResult> LoginAsync(string login, string password);

        Task<AuthTokensResult> RefreshAsync(string refreshToken);

        /// <summary>
        /// Вход и регистрация через VK ID (authorization code + PKCE).
        /// Если пользователя с таким VkId нет — создаёт аккаунт.
        /// </summary>
        Task<AuthTokensResult> LoginWithVkAsync(VkLoginRequest request);
    }
}
