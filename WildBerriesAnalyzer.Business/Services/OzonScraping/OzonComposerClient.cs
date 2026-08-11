using System.Net;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping;

/// <summary>
/// Сырой HttpClient к composer-api.
/// Ozon/Variti часто отвечает 307 с __rr=N — без браузерного challenge это бесконечный цикл.
/// Для live используйте <see cref="OzonBrowserComposerClient"/>.
/// </summary>
public sealed class OzonComposerClient : IOzonComposerClient
{
    private const int MaxRedirectHops = 8;

    private readonly OzonScrapingAuthOptions _auth;
    private readonly HttpClient _http;
    private readonly HttpClientHandler _handler;

    public OzonComposerClient(OzonScrapingAuthOptions auth)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _handler = CreateHandler(auth);
        _http = new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(60) };
    }

    private static HttpClientHandler CreateHandler(OzonScrapingAuthOptions auth)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            // Location от Ozon содержит «сырой» url=... с '?' внутри — авто-follow ломается
            // и Cookie из заголовка не переносится. Follow вручную.
            AllowAutoRedirect = false,
            UseCookies = false
        };

        if (!string.IsNullOrWhiteSpace(auth.ProxyUrl))
        {
            handler.Proxy = new WebProxy(auth.ProxyUrl);
            handler.UseProxy = true;
        }

        return handler;
    }

    public async Task<OzonComposerPage> FetchPageAsync(string sitePath, CancellationToken ct = default)
    {
        sitePath = NormalizeSitePath(sitePath);
        var url = BuildComposerUrl(sitePath, rr: null);
        HttpResponseMessage? response = null;
        string body = string.Empty;
        var hops = new List<string>();

        try
        {
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                hops.Add(url);
                response?.Dispose();

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyHeaders(request, sitePath);

                response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var code = (int)response.StatusCode;

                if (code is >= 200 and < 300)
                {
                    return DeserializePage(body);
                }

                if (code is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location
                                   ?? throw new HttpRequestException(
                                       $"Ozon HTTP {code} без Location. Hops: {string.Join(" => ", hops)}");

                    var abs = location.IsAbsoluteUri
                        ? location
                        : new Uri(new Uri(url), location);

                    var rr = ExtractRr(abs) ?? (hop + 1);
                    url = BuildComposerUrl(sitePath, rr, abs);
                    continue;
                }

                var preview = body.Length > 400 ? body[..400] : body;
                throw new HttpRequestException(
                    $"Ozon composer HTTP {code} {response.StatusCode}. " +
                    $"Hops: {string.Join(" => ", hops)}. Body: {preview}");
            }

            throw new HttpRequestException(
                "Ozon антибот: бесконечный 307 с параметром __rr (Variti). " +
                "Сырой HttpClient не проходит JS-challenge. " +
                "Поставьте \"useBrowser\": true в ozon-scraping-auth.json (Playwright/Chromium) " +
                "и/или RU proxy. Hops: " + string.Join(" => ", hops));
        }
        finally
        {
            response?.Dispose();
        }
    }

    private string BuildComposerUrl(string sitePath, int? rr, Uri? hostFrom = null)
    {
        string baseUrl;
        string composer;

        if (hostFrom is not null &&
            hostFrom.AbsolutePath.Contains("composer-api", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = hostFrom.GetLeftPart(UriPartial.Authority);
            composer = hostFrom.AbsolutePath;
        }
        else
        {
            baseUrl = _auth.BaseUrl.TrimEnd('/');
            composer = _auth.ComposerPath.StartsWith('/')
                ? _auth.ComposerPath
                : "/" + _auth.ComposerPath;
        }

        var url = $"{baseUrl}{composer}?url={Uri.EscapeDataString(sitePath)}";
        if (rr is not null)
        {
            url += $"&__rr={rr.Value}";
        }

        return url;
    }

    private static int? ExtractRr(Uri location)
    {
        var q = location.Query.TrimStart('?');
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 &&
                kv[0] == "__rr" &&
                int.TryParse(Uri.UnescapeDataString(kv[1]), out var n))
            {
                return n;
            }
        }

        return null;
    }

    private static string NormalizeSitePath(string sitePath)
    {
        if (string.IsNullOrWhiteSpace(sitePath))
        {
            throw new ArgumentException("sitePath is required", nameof(sitePath));
        }

        return sitePath.StartsWith('/') ? sitePath : "/" + sitePath;
    }

    private static OzonComposerPage DeserializePage(string body)
    {
        var page = OzonJson.Deserialize<OzonComposerPage>(body)
                   ?? throw new InvalidOperationException("Не удалось десериализовать ответ composer-api.");

        if (page.WidgetStates is null || page.WidgetStates.Count == 0)
        {
            throw new InvalidOperationException(
                "Ответ composer-api без widgetStates — вероятно антибот/пустая сессия. Обновите cookie или включите useBrowser.");
        }

        return page;
    }

    private void ApplyHeaders(HttpRequestMessage request, string sitePath)
    {
        request.Headers.TryAddWithoutValidation("accept", "application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("accept-language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
        request.Headers.TryAddWithoutValidation("user-agent", _auth.UserAgent);
        request.Headers.TryAddWithoutValidation("origin", "https://www.ozon.ru");
        request.Headers.TryAddWithoutValidation("referer", "https://www.ozon.ru" + sitePath.Split('?')[0]);
        request.Headers.TryAddWithoutValidation("sec-ch-ua",
            "\"Google Chrome\";v=\"122\", \"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");

        if (_auth.HasCookie)
        {
            request.Headers.TryAddWithoutValidation("cookie", _auth.Cookie);
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
