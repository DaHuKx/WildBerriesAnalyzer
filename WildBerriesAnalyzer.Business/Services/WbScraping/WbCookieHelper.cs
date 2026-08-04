using System.Text;

namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    internal static class WbCookieHelper
    {
        public static string UpsertCookie(string? cookieHeader, string name, string value)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(cookieHeader))
            {
                foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var eq = part.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    map[part[..eq].Trim()] = part[(eq + 1)..].Trim();
                }
            }

            map[name] = value;

            var sb = new StringBuilder();
            foreach (var pair in map)
            {
                if (sb.Length > 0)
                {
                    sb.Append("; ");
                }

                sb.Append(pair.Key);
                sb.Append('=');
                sb.Append(pair.Value);
            }

            return sb.ToString();
        }
    }
}
