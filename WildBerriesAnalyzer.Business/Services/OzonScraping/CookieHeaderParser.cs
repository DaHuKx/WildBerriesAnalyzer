using Microsoft.Playwright;

namespace WildBerriesAnalyzer.Business.Services.OzonScraping;

internal static class CookieHeaderParser
{
    public static IReadOnlyList<Cookie> ToPlaywrightCookies(string cookieHeader, string originUrl)
    {
        var origin = new Uri(originUrl);
        var list = new List<Cookie>();

        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var name = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            list.Add(new Cookie
            {
                Name = name,
                Value = value,
                Domain = ".ozon.ru",
                Path = "/",
                Secure = name.StartsWith("__Secure-", StringComparison.Ordinal) ||
                         name.StartsWith("__Host-", StringComparison.Ordinal) ||
                         origin.Scheme == Uri.UriSchemeHttps,
                HttpOnly = false,
                SameSite = SameSiteAttribute.Lax
            });
        }

        return list;
    }
}
