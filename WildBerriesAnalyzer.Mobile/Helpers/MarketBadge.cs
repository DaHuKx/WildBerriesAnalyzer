using WildBerriesAnalyzer.Domain.Enums;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace WildBerriesAnalyzer.Mobile.Helpers
{
    /// <summary>
    /// Бейдж маркетплейса на превью товара.
    /// </summary>
    public static class MarketBadge
    {
        public static string LabelFor(MarketType marketType) =>
            marketType == MarketType.Ozon ? "Ozon" : "WB";

        public static MauiColor ColorFor(MarketType marketType) =>
            marketType == MarketType.Ozon
                ? MauiColor.FromArgb("#005BFF")
                : MauiColor.FromArgb("#CB11AB");
    }
}
