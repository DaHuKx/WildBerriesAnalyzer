using System.Net.Http.Json;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;
using WildBerriesAnalyzer.ServerClient.Models;

namespace WildBerriesAnalyzer.ServerClient.Clients
{
    public class AuthClient : IAuthClient
    {
        private readonly HttpClient _httpClient;
        private readonly IWbAuthTokenStore _tokenStore;

        public AuthClient(HttpClient httpClient, IWbAuthTokenStore tokenStore)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        }

        public async Task<RegisterResult> RegisterAsync(string login, string password, string vkProfileUrl)
        {
            var request = new RegisterRequest
            {
                Login = login,
                Password = password,
                VkProfileUrl = vkProfileUrl
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/register",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<RegisterResult>(WbServerJson.Options))!;
        }

        public async Task<AuthTokensResult> ConfirmRegisterAsync(string registrationId, string code)
        {
            var request = new ConfirmRegisterRequest
            {
                RegistrationId = registrationId,
                Code = code
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/register/confirm",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);

            var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensResult>(WbServerJson.Options))!;
            _tokenStore.SetTokens(tokens);
            return tokens;
        }

        public async Task<RegisterResult> ResendRegisterCodeAsync(string registrationId)
        {
            var request = new ResendRegisterCodeRequest
            {
                RegistrationId = registrationId
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/register/resend",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<RegisterResult>(WbServerJson.Options))!;
        }

        public async Task<AuthTokensResult> LoginAsync(string login, string password)
        {
            var request = new LoginRequest
            {
                Login = login,
                Password = password
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/login",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);

            var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensResult>(WbServerJson.Options))!;
            _tokenStore.SetTokens(tokens);
            return tokens;
        }

        public async Task<AuthTokensResult> RefreshAsync(string refreshToken)
        {
            var request = new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/refresh",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);

            var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensResult>(WbServerJson.Options))!;
            _tokenStore.SetTokens(tokens);
            return tokens;
        }

        public async Task<VkAuthPublicConfig> GetVkAuthConfigAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/auth/vk/config", cancellationToken);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);
            return (await response.Content.ReadFromJsonAsync<VkAuthPublicConfig>(WbServerJson.Options, cancellationToken))!;
        }

        public async Task<AuthTokensResult> LoginWithVkAsync(VkLoginRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/vk",
                request,
                WbServerJson.Options);
            await WbServerJson.EnsureSuccessOrThrowAsync(response);

            var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensResult>(WbServerJson.Options))!;
            _tokenStore.SetTokens(tokens);
            return tokens;
        }
    }
}
