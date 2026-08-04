namespace WildBerriesAnalyzer.Domain.Enums
{
    /// <summary>
    /// Стратегия выбора цены, от которой будет определяться скидка
    /// </summary>
    public enum ReferencePriceStrategy
    {
        LastKnownPrice = 1,
        AveragePrice,
        Median,
        MinimumHistorical,
        LowestPriceForLast30Days,
        AveragePriceForLast30Days,
        MedianPriceForLast30Days
    }
}
