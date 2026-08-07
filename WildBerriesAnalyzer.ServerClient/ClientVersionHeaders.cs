namespace WildBerriesAnalyzer.ServerClient
{
    public static class ClientVersionHeaders
    {
        /// <summary>SemVer major.minor.patch, например 1.0.19.</summary>
        public const string ClientVersion = "X-Client-Version";

        public const string ClientPlatform = "X-Client-Platform";
    }
}
