using System.Net.Http.Json;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;
using WildBerriesAnalyzer.ServerClient.Models;

namespace WildBerriesAnalyzer.ServerClient
{
    /// <summary>
    /// Обновляет access/refresh токены через /api/auth/refresh.
    /// </summary>
    public interface IAuthTokenRefresher
    {
        Task<bool> TryRefreshAsync(string? accessTokenUsedInRequest, CancellationToken cancellationToken = default);
    }

    public sealed class AuthTokenRefresher : IAuthTokenRefresher
    {
        private readonly HttpClient _httpClient;
        private readonly IWbAuthTokenStore _tokenStore;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public AuthTokenRefresher(HttpClient httpClient, IWbAuthTokenStore tokenStore)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        }

        public async Task<bool> TryRefreshAsync(
            string? accessTokenUsedInRequest,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                // Другой запрос уже обновил токены.
                var currentAccess = _tokenStore.AccessToken;
                if (!string.IsNullOrWhiteSpace(currentAccess) &&
                    !string.Equals(currentAccess, accessTokenUsedInRequest, StringComparison.Ordinal))
                {
                    return true;
                }

                var refreshToken = _tokenStore.RefreshToken;
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return false;
                }

                var request = new RefreshTokenRequest
                {
                    RefreshToken = refreshToken
                };

                using var response = await _httpClient.PostAsJsonAsync(
                    "api/auth/refresh",
                    request,
                    WbServerJson.Options,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _tokenStore.Clear();
                    return false;
                }

                var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResult>(
                    WbServerJson.Options,
                    cancellationToken);

                if (tokens is null ||
                    string.IsNullOrWhiteSpace(tokens.AccessToken) ||
                    string.IsNullOrWhiteSpace(tokens.RefreshToken))
                {
                    _tokenStore.Clear();
                    return false;
                }

                _tokenStore.SetTokens(tokens);
                return true;
            }
            catch
            {
                _tokenStore.Clear();
                return false;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
