namespace WildBerriesAnalyzer.Mobile.Helpers
{
    /// <summary>
    /// Размеры плитки каталога (2 колонки, фото 3:4).
    /// </summary>
    public static class CatalogTileSizing
    {
        public static (double TileWidth, double ImageHeight) FromPageWidth(
            double pageWidth,
            double horizontalPadding = 24,
            double gap = 8)
        {
            if (pageWidth <= 40)
            {
                return (168, 224);
            }

            var tileWidth = Math.Max(140, Math.Floor((pageWidth - horizontalPadding - gap) / 2.0));
            var imageHeight = Math.Floor(tileWidth * 4.0 / 3.0);
            return (tileWidth, imageHeight);
        }
    }
}
