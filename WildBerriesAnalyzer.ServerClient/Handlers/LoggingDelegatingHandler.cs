using System.Diagnostics;
using Serilog;

namespace WildBerriesAnalyzer.ServerClient.Handlers
{
    /// <summary>
    /// Логирует все HTTP-запросы к API (метод, путь, статус, длительность).
    /// </summary>
    public sealed class LoggingDelegatingHandler : DelegatingHandler
    {
        private static readonly ILogger Log = Serilog.Log.ForContext("Area", "Http");

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var method = request.Method.Method;
            var path = FormatPath(request.RequestUri);
            var sw = Stopwatch.StartNew();

            Log.Debug("→ {Method} {Path}", method, path);

            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    Log.Information(
                        "← {Method} {Path} {StatusCode} in {ElapsedMs}ms",
                        method,
                        path,
                        status,
                        sw.ElapsedMilliseconds);
                }
                else
                {
                    Log.Warning(
                        "← {Method} {Path} {StatusCode} in {ElapsedMs}ms",
                        method,
                        path,
                        status,
                        sw.ElapsedMilliseconds);
                }

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Log.Error(
                    ex,
                    "✖ {Method} {Path} failed after {ElapsedMs}ms",
                    method,
                    path,
                    sw.ElapsedMilliseconds);
                throw;
            }
        }

        private static string FormatPath(Uri? uri)
        {
            if (uri is null)
            {
                return "<null>";
            }

            // Path + query без host (base address уже известен), без секретов в fragment.
            var path = uri.IsAbsoluteUri ? uri.PathAndQuery : uri.ToString();
            return RedactSensitiveQuery(path);
        }

        private static string RedactSensitiveQuery(string pathAndQuery)
        {
            if (string.IsNullOrEmpty(pathAndQuery) || !pathAndQuery.Contains('?', StringComparison.Ordinal))
            {
                return pathAndQuery;
            }

            // Не пишем потенциальные токены из query.
            var sensitive = new[] { "token", "access_token", "refresh_token", "password", "code" };
            var parts = pathAndQuery.Split('?', 2);
            if (parts.Length != 2)
            {
                return pathAndQuery;
            }

            var query = parts[1]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair =>
                {
                    var kv = pair.Split('=', 2);
                    var key = kv[0];
                    if (sensitive.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    {
                        return $"{key}=***";
                    }

                    return pair;
                });

            return $"{parts[0]}?{string.Join('&', query)}";
        }
    }
}
