namespace ModerMobile;

/// <summary>
/// Адрес WildBerriesAnalyzer.Server (как в Mobile).
/// </summary>
public static class ServerSettings
{
    private const bool UseRemoteServer = true;
    private const string RemoteBaseAddress = "http://62.233.35.144:5146/";
    private const string DevHostLanIp = "192.168.1.106";
    private const bool UseAdbReverse = true;

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
