namespace WildBerriesAnalyzer.Business.Models
{
    public sealed class VkAuthPublicConfig
    {
        public bool Enabled { get; init; }

        public string ClientId { get; init; } = string.Empty;

        /// <summary>
        /// redirect_uri для authorize (должен совпадать при обмене кода).
        /// </summary>
        public string RedirectUri { get; init; } = string.Empty;

        /// <summary>
        /// URI, который ждёт WebAuthenticator (часто совпадает с RedirectUri).
        /// </summary>
        public string AppCallbackUri { get; init; } = string.Empty;

        public string AuthorizeUrl { get; init; } = string.Empty;

        public string Scope { get; init; } = string.Empty;
    }

    public sealed class VkLoginRequest
    {
        public string Code { get; set; } = string.Empty;

        public string CodeVerifier { get; set; } = string.Empty;

        public string DeviceId { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        /// <summary>
        /// Тот же redirect_uri, что в authorize.
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;
    }
}
