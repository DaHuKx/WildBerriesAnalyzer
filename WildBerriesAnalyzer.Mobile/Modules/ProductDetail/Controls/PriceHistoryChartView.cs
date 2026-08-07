using System.Collections;
using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Modules.ProductDetail.Controls
{
    public sealed class PriceHistoryChartView : GraphicsView
    {
        public static readonly BindableProperty PointsProperty = BindableProperty.Create(
            nameof(Points),
            typeof(IEnumerable),
            typeof(PriceHistoryChartView),
            defaultValue: null,
            propertyChanged: OnPointsChanged);

        private readonly PriceHistoryChartDrawable _drawable = new();

        public PriceHistoryChartView()
        {
            Drawable = _drawable;
            HeightRequest = 220;
            BackgroundColor = Colors.Transparent;
        }

        public IEnumerable? Points
        {
            get => (IEnumerable?)GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        private static void OnPointsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is not PriceHistoryChartView view)
            {
                return;
            }

            var list = newValue switch
            {
                IEnumerable<ProductPricePoint> typed => typed.ToList(),
                IEnumerable enumerable => enumerable.OfType<ProductPricePoint>().ToList(),
                _ => []
            };

            view._drawable.SetPoints(list);
            view.Invalidate();
        }
    }
}
