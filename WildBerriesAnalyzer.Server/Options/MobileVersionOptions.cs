namespace WildBerriesAnalyzer.Server.Options
{
    /// <summary>
    /// Актуальная (последняя) версия Mobile из .env / конфигурации.
    /// </summary>
    public class MobileVersionOptions
    {
        public const string SectionName = "Mobile";

        /// <summary>SemVer major.minor.patch, например 1.0.19.</summary>
        public string LatestVersion { get; set; } = "1.0.0";
    }
}
