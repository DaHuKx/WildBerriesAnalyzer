using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Modules.ProductDetail.Controls
{
    public sealed class PriceHistoryChartDrawable : IDrawable
    {
        private IReadOnlyList<ProductPricePoint> _points = [];

        public void SetPoints(IReadOnlyList<ProductPricePoint>? points)
        {
            _points = points ?? [];
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();

            var bg = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#13201E")
                : Colors.White;
            canvas.FillColor = bg;
            canvas.FillRectangle(dirtyRect);

            if (_points.Count == 0)
            {
                canvas.FontColor = Color.FromArgb("#8A9A97");
                canvas.FontSize = 13;
                canvas.DrawString(
                    "Недостаточно данных для графика",
                    dirtyRect,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
                canvas.RestoreState();
                return;
            }

            const float padL = 44;
            const float padR = 12;
            const float padT = 16;
            const float padB = 28;

            var plot = new RectF(
                dirtyRect.X + padL,
                dirtyRect.Y + padT,
                Math.Max(1, dirtyRect.Width - padL - padR),
                Math.Max(1, dirtyRect.Height - padT - padB));

            var min = (float)_points.Min(p => p.Price);
            var max = (float)_points.Max(p => p.Price);
            if (Math.Abs(max - min) < 0.01f)
            {
                min -= 1;
                max += 1;
            }

            var grid = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2A3C39")
                : Color.FromArgb("#D0DBD8");
            var muted = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#6F827E")
                : Color.FromArgb("#8A9A97");
            var line = Color.FromArgb("#0F766E");
            if (Application.Current?.RequestedTheme == AppTheme.Dark)
            {
                line = Color.FromArgb("#2DD4BF");
            }

            canvas.StrokeColor = grid;
            canvas.StrokeSize = 1;
            canvas.FontColor = muted;
            canvas.FontSize = 10;

            for (var i = 0; i <= 4; i++)
            {
                var t = i / 4f;
                var y = plot.Bottom - t * plot.Height;
                canvas.DrawLine(plot.Left, y, plot.Right, y);
                var value = min + (max - min) * t;
                canvas.DrawString(
                    FormatPrice(value),
                    new RectF(dirtyRect.X, y - 8, padL - 4, 16),
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center);
            }

            var n = _points.Count;
            var pathPoints = new List<PointF>(n);
            for (var i = 0; i < n; i++)
            {
                var x = n == 1
                    ? plot.Left + plot.Width / 2f
                    : plot.Left + i / (float)(n - 1) * plot.Width;
                var priceT = ((float)_points[i].Price - min) / (max - min);
                var y = plot.Bottom - priceT * plot.Height;
                pathPoints.Add(new PointF(x, y));
            }

            canvas.StrokeColor = line;
            canvas.StrokeSize = 2.5f;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.StrokeLineCap = LineCap.Round;

            for (var i = 1; i < pathPoints.Count; i++)
            {
                canvas.DrawLine(pathPoints[i - 1], pathPoints[i]);
            }

            canvas.FillColor = line;
            foreach (var p in pathPoints)
            {
                canvas.FillCircle(p.X, p.Y, 3.2f);
            }

            // Подписи дат по краям
            canvas.FontColor = muted;
            canvas.FontSize = 10;
            var first = _points[0].CheckTime.ToLocalTime();
            var last = _points[^1].CheckTime.ToLocalTime();
            canvas.DrawString(
                first.ToString("dd.MM"),
                new RectF(plot.Left, plot.Bottom + 4, 48, 16),
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
            canvas.DrawString(
                last.ToString("dd.MM"),
                new RectF(plot.Right - 48, plot.Bottom + 4, 48, 16),
                HorizontalAlignment.Right,
                VerticalAlignment.Top);

            canvas.RestoreState();
        }

        private static string FormatPrice(float value)
        {
            if (value >= 1000)
            {
                return $"{value / 1000f:0.#}k";
            }

            return $"{value:0}";
        }
    }
}
