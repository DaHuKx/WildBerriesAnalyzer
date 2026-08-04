namespace WildBerriesAnalyzer.ServerClient
{
    public class WbServerClientOptions
    {
        public const string SectionName = "WbServer";

        /// <summary>
        /// Базовый адрес API, например http://localhost:5146/
        /// </summary>
        public string BaseAddress { get; set; } = "http://localhost:5146/";
    }
}
