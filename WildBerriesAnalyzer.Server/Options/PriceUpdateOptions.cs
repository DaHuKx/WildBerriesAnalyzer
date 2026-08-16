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
        /// Размер батча артикулов для запроса к Wildberries.
        /// </summary>
        public int WbBatchSize { get; set; } = 100;

        /// <summary>
        /// Размер батча артикулов для запроса к Ozon (параллельная загрузка карточек внутри батча).
        /// </summary>
        public int OzonBatchSize { get; set; } = 100;

        /// <summary>
        /// Пауза между батчами запросов к маркетплейсам (снижает риск блокировок).
        /// </summary>
        public TimeSpan BatchDelay { get; set; } = TimeSpan.FromSeconds(3);
    }
}
