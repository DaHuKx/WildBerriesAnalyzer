namespace WildBerriesAnalyzer.Server.Options
{
    public sealed class PriceUpdateOptions
    {
        public const string SectionName = "PriceUpdate";

        /// <summary>
        /// Включить фоновое обновление цен.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Интервал между полными проходами по каталогу.
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Размер батча артикулов для запроса к WB.
        /// </summary>
        public int BatchSize { get; set; } = 100;
    }
}
