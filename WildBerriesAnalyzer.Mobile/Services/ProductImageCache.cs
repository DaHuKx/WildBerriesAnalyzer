using System.Collections.Concurrent;
using System.Net.Http;

namespace WildBerriesAnalyzer.Mobile.Services
{
    /// <summary>
    /// Фоновая загрузка и кэш байтов превью товаров.
    /// </summary>
    public interface IProductImageCache
    {
        Task<byte[]?> GetOrLoadAsync(string? url, CancellationToken cancellationToken = default);
    }

    public sealed class ProductImageCache : IProductImageCache, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, byte[]> _memory = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Task<byte[]?>> _inflight = new(StringComparer.OrdinalIgnoreCase);

        public ProductImageCache()
        {
            _httpClient = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public Task<byte[]?> GetOrLoadAsync(string? url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                return Task.FromResult<byte[]?>(null);
            }

            if (_memory.TryGetValue(url, out var cached))
            {
                return Task.FromResult<byte[]?>(cached);
            }

            var task = _inflight.GetOrAdd(url, key => LoadCoreAsync(key));
            return AwaitAndCleanupAsync(url, task);
        }

        private async Task<byte[]?> AwaitAndCleanupAsync(string url, Task<byte[]?> task)
        {
            try
            {
                return await task.ConfigureAwait(false);
            }
            finally
            {
                _inflight.TryRemove(url, out _);
            }
        }

        private async Task<byte[]?> LoadCoreAsync(string url)
        {
            try
            {
                using var response = await _httpClient
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (bytes.Length == 0)
                {
                    return null;
                }

                _memory[url] = bytes;
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose() => _httpClient.Dispose();
    }
}
