using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using VkNet.Abstractions.Utils;
using VkNet.Utils;
using WildBerriesAnalyzer.Business.Helpers;

namespace WildBerriesAnalyzer.Bots.Clients
{
    /// <summary>
    /// RestClient VkNet с IPv4/DoH — на RUVDS системный DNS часто не резолвит api.vk.com.
    /// </summary>
    public sealed class VkIpv4RestClient : IRestClient
    {
        private readonly ILogger<VkIpv4RestClient>? _logger;
        private readonly HttpClient _http;

        public VkIpv4RestClient(ILogger<VkIpv4RestClient>? logger = null)
        {
            _logger = logger;
            _http = new HttpClient(Ipv4Http.CreateHandler())
            {
                Timeout = TimeSpan.FromSeconds(100)
            };
        }

        public TimeSpan Timeout
        {
            get => _http.Timeout;
            set => _http.Timeout = value;
        }

        public Task<HttpResponse<string>> GetAsync(
            Uri uri,
            IEnumerable<KeyValuePair<string, string>> parameters,
            Encoding encoding,
            CancellationToken token = default)
        {
            var url = AppendQuery(uri, parameters);
            _logger?.LogDebug("VK GET {Url}", url);
            return SendAsync(() => _http.GetAsync(url, token), encoding, token);
        }

        public Task<HttpResponse<string>> PostAsync(
            Uri uri,
            IEnumerable<KeyValuePair<string, string>> parameters,
            Encoding encoding,
            IEnumerable<KeyValuePair<string, string>> headers,
            CancellationToken token = default)
        {
            _logger?.LogDebug("VK POST {Url}", uri);
            var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new FormUrlEncodedContent(parameters)
            };

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return SendAsync(() => _http.SendAsync(request, token), encoding, token);
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private async Task<HttpResponse<string>> SendAsync(
            Func<Task<HttpResponseMessage>> send,
            Encoding encoding,
            CancellationToken token)
        {
            using var response = await send().ConfigureAwait(false);
            var requestUri = response.RequestMessage?.RequestUri
                             ?? new Uri("https://api.vk.com/");
            var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
            var body = (encoding ?? Encoding.UTF8).GetString(bytes);

            if (response.IsSuccessStatusCode)
            {
                return HttpResponse<string>.Success(response.StatusCode, body, requestUri);
            }

            return HttpResponse<string>.Fail(response.StatusCode, body, requestUri);
        }

        private static Uri AppendQuery(Uri uri, IEnumerable<KeyValuePair<string, string>> parameters)
        {
            var queries = parameters
                .Where(k => !string.IsNullOrWhiteSpace(k.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");

            var builder = new UriBuilder(uri)
            {
                Query = string.Join("&", queries)
            };
            return builder.Uri;
        }
    }
}
