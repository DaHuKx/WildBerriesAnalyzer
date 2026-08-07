using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient.Handlers
{
    /// <summary>
    /// Добавляет Bearer access-токен и при 401 один раз обновляет токены и повторяет запрос.
    /// </summary>
    public sealed class BearerTokenHandler : DelegatingHandler
    {
        private readonly IWbAuthTokenStore _tokenStore;
        private readonly IAuthTokenRefresher _tokenRefresher;

        public BearerTokenHandler(IWbAuthTokenStore tokenStore, IAuthTokenRefresher tokenRefresher)
        {
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _tokenRefresher = tokenRefresher ?? throw new ArgumentNullException(nameof(tokenRefresher));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await BufferContentAsync(request, cancellationToken).ConfigureAwait(false);

            var accessToken = _tokenStore.AccessToken;
            ApplyBearer(request, accessToken);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            if (IsAnonymousAuthPath(request.RequestUri))
            {
                return response;
            }

            response.Dispose();

            var refreshed = await _tokenRefresher.TryRefreshAsync(accessToken, cancellationToken)
                .ConfigureAwait(false);
            if (!refreshed)
            {
                // Сессия мертва: очищаем токены → клиент (AuthSessionGuard) уходит на логин.
                _tokenStore.Clear();
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    RequestMessage = request,
                    Content = JsonContent.Create(
                        new { message = "Недействительный или просроченный access-токен." },
                        options: WbServerJson.Options)
                };
            }

            using var retryRequest = await CloneHttpRequestAsync(request, cancellationToken)
                .ConfigureAwait(false);
            ApplyBearer(retryRequest, _tokenStore.AccessToken);
            return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
        }

        private static async Task BufferContentAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is null || request.Content is ByteArrayContent)
            {
                return;
            }

            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var buffered = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Content = buffered;
        }

        private static void ApplyBearer(HttpRequestMessage request, string? accessToken)
        {
            request.Headers.Authorization = null;
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        private static bool IsAnonymousAuthPath(Uri? uri)
        {
            if (uri is null)
            {
                return false;
            }

            var path = uri.AbsolutePath;
            return path.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                   || path.Contains("/api/auth/register", StringComparison.OrdinalIgnoreCase)
                   || path.Contains("/api/auth/refresh", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is not null)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
                clone.Content = new ByteArrayContent(bytes);

                foreach (var header in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            foreach (var option in request.Options)
            {
                clone.Options.TryAdd(option.Key, option.Value);
            }

            return clone;
        }
    }
}
