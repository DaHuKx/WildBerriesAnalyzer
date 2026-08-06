using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Serialization;

namespace WildBerriesAnalyzer.Business.Helpers
{
    /// <summary>
    /// HttpClient только по IPv4. На RUVDS системный DNS часто отдаёт AAAA-only /
    /// «Cannot assign requested address» — тогда резолвим A через DoH на 8.8.8.8.
    /// </summary>
    public static class Ipv4Http
    {
        private static readonly IPAddress GoogleDns = IPAddress.Parse("8.8.8.8");

        public static SocketsHttpHandler CreateHandler(
            DecompressionMethods decompression = DecompressionMethods.All,
            bool useCookies = false)
        {
            return new SocketsHttpHandler
            {
                AutomaticDecompression = decompression,
                UseCookies = useCookies,
                ConnectCallback = ConnectIpv4Async
            };
        }

        private static async ValueTask<Stream> ConnectIpv4Async(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            var host = context.DnsEndPoint.Host;
            var port = context.DnsEndPoint.Port;

            // Уже IP — не резолвим.
            if (IPAddress.TryParse(host, out var literal))
            {
                if (literal.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new HttpRequestException($"IPv6 literal not supported: {host}");
                }

                return await ConnectSocketAsync(literal, port, cancellationToken).ConfigureAwait(false);
            }

            var ipv4 = await ResolveIpv4Async(host, cancellationToken).ConfigureAwait(false);
            if (ipv4.Length == 0)
            {
                throw new HttpRequestException($"No IPv4 addresses for {host}.");
            }

            Exception? lastError = null;
            foreach (var address in ipv4)
            {
                try
                {
                    return await ConnectSocketAsync(address, port, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new HttpRequestException(
                $"Unable to connect to {host}:{port} via IPv4",
                lastError);
        }

        private static async Task<IPAddress[]> ResolveIpv4Async(string host, CancellationToken cancellationToken)
        {
            try
            {
                var fromOs = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
                var ipv4 = fromOs
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .Distinct()
                    .ToArray();
                if (ipv4.Length > 0)
                {
                    return ipv4;
                }
            }
            catch
            {
                // fallback ниже
            }

            // Системный DNS сломан / AAAA-only — берём A-запись через DoH по IP (без рекурсии DNS).
            return await ResolveIpv4ViaDohAsync(host, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<IPAddress[]> ResolveIpv4ViaDohAsync(string host, CancellationToken cancellationToken)
        {
            try
            {
                using var handler = new SocketsHttpHandler
                {
                    ConnectCallback = static async (ctx, ct) =>
                    {
                        // DoH всегда на 8.8.8.8 — не зависим от системного DNS.
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                        {
                            NoDelay = true
                        };
                        try
                        {
                            await socket.ConnectAsync(GoogleDns, 443, ct).ConfigureAwait(false);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
                };

                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://dns.google/resolve?name={Uri.EscapeDataString(host)}&type=A");
                request.Headers.TryAddWithoutValidation("accept", "application/dns-json");
                // SNI: Host должен быть dns.google при коннекте к 8.8.8.8
                request.Headers.Host = "dns.google";

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content
                    .ReadFromJsonAsync<GoogleDnsResponse>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (payload?.Answer is null || payload.Answer.Count == 0)
                {
                    return [];
                }

                return payload.Answer
                    .Where(a => a.Type == 1 && !string.IsNullOrWhiteSpace(a.Data))
                    .Select(a => IPAddress.TryParse(a.Data, out var ip) ? ip : null)
                    .Where(ip => ip is { AddressFamily: AddressFamily.InterNetwork })
                    .Cast<IPAddress>()
                    .Distinct()
                    .ToArray();
            }
            catch (Exception ex)
            {
                throw new HttpRequestException(
                    $"DoH A-lookup failed for {host}: {ex.Message}",
                    ex);
            }
        }

        private static async Task<Stream> ConnectSocketAsync(
            IPAddress address,
            int port,
            CancellationToken cancellationToken)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private sealed class GoogleDnsResponse
        {
            [JsonPropertyName("Answer")]
            public List<GoogleDnsAnswer>? Answer { get; set; }
        }

        private sealed class GoogleDnsAnswer
        {
            [JsonPropertyName("type")]
            public int Type { get; set; }

            [JsonPropertyName("data")]
            public string? Data { get; set; }
        }
    }
}
