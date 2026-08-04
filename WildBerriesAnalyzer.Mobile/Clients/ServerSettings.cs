namespace WildBerriesAnalyzer.Mobile.Clients
{
    /// <summary>
    /// Адрес WildBerriesAnalyzer.Server.
    /// </summary>
    public static class ServerSettings
    {
        /// <summary>
        /// true — VDS; false — локальный API (adb reverse / LAN / localhost).
        /// </summary>
        private const bool UseRemoteServer = true;

        private const string RemoteBaseAddress = "http://62.233.35.144:5146/";

        /// <summary>
        /// IPv4 ПК в Wi‑Fi (только если не используете adb reverse).
        /// </summary>
        private const string DevHostLanIp = "192.168.1.106";

        /// <summary>
        /// true = http://127.0.0.1:5146/ + обязательно
        /// <c>adb reverse tcp:5146 tcp:5146</c> (обходит Wi‑Fi/firewall).
        /// false = http://{DevHostLanIp}:5146/ по Wi‑Fi в одной сети.
        /// </summary>
        private const bool UseAdbReverse = true;

        /// <summary>
        /// Базовый URL API.
        /// </summary>
        public static string BaseAddress
        {
            get
            {
                if (UseRemoteServer)
                {
                    return RemoteBaseAddress;
                }

#if ANDROID
                if (UseAdbReverse)
                {
                    return "http://127.0.0.1:5146/";
                }

                return $"http://{DevHostLanIp}:5146/";
#elif WINDOWS
                return "http://localhost:5146/";
#else
                return "http://localhost:5146/";
#endif
            }
        }
    }
}
