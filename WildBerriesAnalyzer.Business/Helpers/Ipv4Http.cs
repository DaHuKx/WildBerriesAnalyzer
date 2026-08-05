using System.Net;
using System.Net.Sockets;

namespace WildBerriesAnalyzer.Business.Helpers
{
    /// <summary>
    /// HttpClient, который предпочитает IPv4 — на части VDS AAAA ломается
    /// («Network is unreachable» / «cannot assign requested address»).
    /// </summary>
    public static class Ipv4Http
    {
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

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"DNS failed for {host}: {ex.Message}", ex);
            }

            // Только IPv4: при disable_ipv6 в Docker connect на AAAA даёт
            // SocketException 99 «Cannot assign requested address».
            var ipv4 = addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Distinct()
                .ToArray();

            if (ipv4.Length == 0)
            {
                throw new HttpRequestException(
                    $"No IPv4 addresses for {host} (AAAA-only / broken DNS). " +
                    "Проверьте DNS контейнера и scripts/fix-vds-dns.sh.");
            }

            Exception? lastError = null;
            foreach (var address in ipv4)
            {
                Socket? socket = null;
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    await socket.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    socket?.Dispose();
                }
            }

            throw new HttpRequestException(
                $"Unable to connect to {host}:{port} via IPv4",
                lastError);
        }
    }
}
