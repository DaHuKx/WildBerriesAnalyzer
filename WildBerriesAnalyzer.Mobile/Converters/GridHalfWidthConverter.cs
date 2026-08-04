using System.Globalization;

namespace WildBerriesAnalyzer.Mobile.Converters
{
    /// <summary>
    /// Ширина карточки в 2-колоночной сетке (или высота фото 3:4).
    /// Parameter: "height" → высота изображения; иначе ширина карточки.
    /// </summary>
    public sealed class GridHalfWidthConverter : IValueConverter
    {
        public double HorizontalPadding { get; set; } = 24;

        public double Gap { get; set; } = 8;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var parentWidth = value is double d ? d : 0;
            if (parentWidth <= 40)
            {
                parentWidth = 360;
            }

            var cardWidth = Math.Max(140, Math.Floor((parentWidth - HorizontalPadding - Gap) / 2));
            var mode = parameter?.ToString()?.Trim().ToLowerInvariant();
            if (mode is "height" or "imageheight")
            {
                return Math.Floor(cardWidth * 4.0 / 3.0);
            }

            return cardWidth;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
