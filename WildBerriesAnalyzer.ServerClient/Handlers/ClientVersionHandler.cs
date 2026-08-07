using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WildBerriesAnalyzer.ServerClient.Handlers
{
    /// <summary>
    /// Добавляет заголовки версии клиента (semver) ко всем HTTP-запросам.
    /// </summary>
    public sealed class ClientVersionHandler : DelegatingHandler
    {
        private readonly Func<string> _version;
        private readonly string _platform;

        public ClientVersionHandler(Func<string> version, string platform = "mobile")
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _platform = platform ?? "mobile";
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var version = _version();
            if (!string.IsNullOrWhiteSpace(version) &&
                !request.Headers.Contains(ClientVersionHeaders.ClientVersion))
            {
                request.Headers.TryAddWithoutValidation(ClientVersionHeaders.ClientVersion, version);
            }

            if (!string.IsNullOrWhiteSpace(_platform) &&
                !request.Headers.Contains(ClientVersionHeaders.ClientPlatform))
            {
                request.Headers.TryAddWithoutValidation(ClientVersionHeaders.ClientPlatform, _platform);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
