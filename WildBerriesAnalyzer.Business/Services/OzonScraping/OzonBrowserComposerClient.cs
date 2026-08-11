using System.Text.Json;
using Microsoft.Playwright;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Auth;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Models.Composer;
using WildBerriesAnalyzer.Business.Services.OzonScraping.Serialization;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping;

/// <summary>
/// Composer-api через Chromium: один раз проходит Variti challenge, дальше fetch() из страницы.
/// </summary>
public sealed class OzonBrowserComposerClient : IOzonComposerClient
{
    private readonly OzonScrapingAuthOptions _auth;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _challenged;

    public OzonBrowserComposerClient(OzonScrapingAuthOptions auth)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
    }

    public async Task<OzonComposerPage> FetchPageAsync(string sitePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sitePath))
        {
            throw new ArgumentException("sitePath is required", nameof(sitePath));
        }

        if (!sitePath.StartsWith('/'))
        {
            sitePath = "/" + sitePath;
        }

        Exception? last = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await EnsureReadyAsync(ct).ConfigureAwait(false);
                var json = await FetchJsonFromPageAsync(sitePath, ct).ConfigureAwait(false);
                return DeserializePage(json);
            }
            catch (Exception ex) when (attempt == 0 && IsSessionError(ex))
            {
                last = ex;
                Console.WriteLine($"[browser] session error, relaunch: {ex.Message}");
                await ShutdownAsync().ConfigureAwait(false);
            }
        }

        throw last ?? new InvalidOperationException("Ozon browser fetch failed.");
    }

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_page is not null && _challenged)
            {
                return;
            }

            await LaunchAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LaunchAsync(CancellationToken ct)
    {
            Console.WriteLine("[browser] launching Chromium (Playwright)…");
            // Пустой/битый PLAYWRIGHT_DRIVER_SEARCH_PATH ломает поиск драйвера рядом с dll.
            var driverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
            if (driverPath is not null && string.IsNullOrWhiteSpace(driverPath))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            }

            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);

        var launch = new BrowserTypeLaunchOptions
        {
            Headless = _auth.Headless,
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled",
                "--no-sandbox",
                "--disable-dev-shm-usage"
            }
        };

        if (!string.IsNullOrWhiteSpace(_auth.ProxyUrl))
        {
            launch.Proxy = new Proxy { Server = _auth.ProxyUrl };
        }

        _browser = await _playwright.Chromium.LaunchAsync(launch).ConfigureAwait(false);

        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = _auth.UserAgent,
            Locale = "ru-RU",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["accept-language"] = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7"
            }
        };

        _context = await _browser.NewContextAsync(contextOptions).ConfigureAwait(false);

        if (_auth.HasCookie &&
            !_auth.Cookie.Contains("PASTE_OZON_COOKIE", StringComparison.OrdinalIgnoreCase))
        {
            var cookies = CookieHeaderParser.ToPlaywrightCookies(_auth.Cookie, "https://www.ozon.ru");
            if (cookies.Count > 0)
            {
                await _context.AddCookiesAsync(cookies).ConfigureAwait(false);
                Console.WriteLine($"[browser] injected cookies: {cookies.Count}");
            }
        }

        _page = await _context.NewPageAsync().ConfigureAwait(false);
        Console.WriteLine("[browser] opening https://www.ozon.ru/ (anti-bot challenge)…");

        var response = await _page.GotoAsync(
            "https://www.ozon.ru/",
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 90_000
            }).ConfigureAwait(false);

        await _page.WaitForTimeoutAsync(Math.Max(3000, _auth.ChallengeWaitMs)).ConfigureAwait(false);

        var title = await _page.TitleAsync().ConfigureAwait(false);
        var finalUrl = _page.Url;
        Console.WriteLine($"[browser] title=\"{Truncate(title, 60)}\" url={finalUrl} http={(int?)response?.Status}");

        if (IsBlockedTitle(title) || IsBlockedUrl(finalUrl))
        {
            throw new InvalidOperationException(
                $"Антибот не пройден (title=\"{title}\", url={finalUrl}). " +
                "Обновите cookie после полной загрузки ozon.ru, поставьте headless=false или RU proxyUrl.");
        }

        _challenged = true;
        Console.WriteLine("[browser] challenge passed");
    }

    private async Task<string> FetchJsonFromPageAsync(string sitePath, CancellationToken ct)
    {
        if (_page is null)
        {
            throw new InvalidOperationException("Browser page is not ready.");
        }

        var apiUrl = "https://www.ozon.ru/api/composer-api.bx/page/json/v2?url=" +
                     Uri.EscapeDataString(sitePath);

        // fetch из контекста страницы — те же cookies/origin, что после challenge
        var resultJson = await _page.EvaluateAsync<string>(@"async (apiUrl) => {
            const r = await fetch(apiUrl, {
                headers: { accept: 'application/json' },
                credentials: 'include'
            });
            const text = await r.text();
            return JSON.stringify({ status: r.status, text });
        }", apiUrl).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        using var doc = JsonDocument.Parse(resultJson);
        var status = doc.RootElement.GetProperty("status").GetInt32();
        var text = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;

        if (status is 403 or 307)
        {
            throw new HttpRequestException($"Ozon browser fetch HTTP {status} (session expired / antibot).");
        }

        if (status < 200 || status >= 300)
        {
            var preview = text.Length > 300 ? text[..300] : text;
            throw new HttpRequestException($"Ozon browser fetch HTTP {status}. Body: {preview}");
        }

        return text;
    }

    private static OzonComposerPage DeserializePage(string body)
    {
        var page = OzonJson.Deserialize<OzonComposerPage>(body)
                   ?? throw new InvalidOperationException("Не удалось десериализовать ответ composer-api.");

        if (page.WidgetStates is null || page.WidgetStates.Count == 0)
        {
            throw new InvalidOperationException(
                "Ответ composer-api без widgetStates — challenge/сессия недействительны.");
        }

        return page;
    }

    private static bool IsSessionError(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("403", StringComparison.Ordinal) ||
               msg.Contains("307", StringComparison.Ordinal) ||
               msg.Contains("antibot", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("Target closed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("has been closed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockedTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) &&
        (title.Contains("antibot", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("доступ ограничен", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("forbidden", StringComparison.OrdinalIgnoreCase));

    private static bool IsBlockedUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.Contains("antibot", StringComparison.OrdinalIgnoreCase) ||
         url.Contains("challenge", StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

    private async Task ShutdownAsync()
    {
        _challenged = false;
        try { if (_page is not null) await _page.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
        try { if (_context is not null) await _context.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
        try { if (_browser is not null) await _browser.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
        _playwright?.Dispose();
        _page = null;
        _context = null;
        _browser = null;
        _playwright = null;
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
