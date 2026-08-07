namespace WildBerriesAnalyzer.Mobile.Core
{
    /// <summary>
    /// Версия приложения в формате major.minor.patch (ApplicationDisplayVersion).
    /// </summary>
    public static class AppClientVersion
    {
        /// <summary>Например 1.0.19.</summary>
        public static string Version => AppInfo.Current.VersionString;

        public const string Platform = "mobile";
    }
}
