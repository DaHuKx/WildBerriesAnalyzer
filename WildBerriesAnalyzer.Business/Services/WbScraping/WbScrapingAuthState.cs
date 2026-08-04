using WildBerriesAnalyzer.Business.Options;

namespace WildBerriesAnalyzer.Business.Services.WbScraping
{
    public sealed class WbScrapingAuthState
    {
        public string AccessToken { get; set; } = string.Empty;

        public string Cookie { get; set; } = string.Empty;

        public string DeviceId { get; set; } = string.Empty;

        public string UserAgent { get; set; } = string.Empty;

        public string SpaVersion { get; set; } = string.Empty;

        public string SecChUa { get; set; } = string.Empty;

        public static WbScrapingAuthState FromOptions(WbScrapingAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return new WbScrapingAuthState
            {
                AccessToken = options.AccessToken ?? string.Empty,
                Cookie = options.Cookie ?? string.Empty,
                DeviceId = options.DeviceId ?? string.Empty,
                UserAgent = options.UserAgent ?? string.Empty,
                SpaVersion = options.SpaVersion ?? string.Empty,
                SecChUa = options.SecChUa ?? string.Empty
            };
        }

        public WbScrapingAuthState Clone()
        {
            return new WbScrapingAuthState
            {
                AccessToken = AccessToken,
                Cookie = Cookie,
                DeviceId = DeviceId,
                UserAgent = UserAgent,
                SpaVersion = SpaVersion,
                SecChUa = SecChUa
            };
        }
    }
}
