using System.Net.Http.Json;
using Serilog;
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
        private static readonly ILogger Log = Serilog.Log.ForContext("Area", "Auth");

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
                    Log.Debug("Refresh skipped — access token already rotated");
                    return true;
                }

                var refreshToken = _tokenStore.RefreshToken;
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    Log.Warning("Refresh aborted — no refresh token");
                    _tokenStore.Clear();
                    return false;
                }

                Log.Information("Refreshing access token");

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
                    Log.Warning("Refresh failed with status {StatusCode}", (int)response.StatusCode);
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
                    Log.Warning("Refresh response missing tokens");
                    _tokenStore.Clear();
                    return false;
                }

                _tokenStore.SetTokens(tokens);
                Log.Information("Access token refreshed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Refresh threw exception");
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
