using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Modules.ActualDiscounts.ViewModels;

namespace WildBerriesAnalyzer.Modules.ActualDiscounts.Views
{
    public partial class ActualDiscountsPage : ContentView
    {
        private const double LoadMoreThresholdPx = 240;

        public static readonly BindableProperty TileWidthProperty =
            BindableProperty.Create(nameof(TileWidth), typeof(double), typeof(ActualDiscountsPage), 168d);

        public static readonly BindableProperty TileImageHeightProperty =
            BindableProperty.Create(nameof(TileImageHeight), typeof(double), typeof(ActualDiscountsPage), 224d);

        public ActualDiscountsPage()
        {
            InitializeComponent();
        }

        public double TileWidth
        {
            get => (double)GetValue(TileWidthProperty);
            set => SetValue(TileWidthProperty, value);
        }

        public double TileImageHeight
        {
            get => (double)GetValue(TileImageHeightProperty);
            set => SetValue(TileImageHeightProperty, value);
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            var (tileWidth, imageHeight) = CatalogTileSizing.FromPageWidth(width);
            if (Math.Abs(TileWidth - tileWidth) > 0.5)
            {
                TileWidth = tileWidth;
                TileImageHeight = imageHeight;
            }
        }

        private void OnDiscountsScroll(object? sender, ScrolledEventArgs e)
        {
            if (BindingContext is not ActualDiscountsPageViewModel viewModel)
            {
                return;
            }

            if (sender is not ScrollView scrollView)
            {
                return;
            }

            var contentHeight = scrollView.ContentSize.Height;
            var viewportHeight = scrollView.Height;
            if (contentHeight <= viewportHeight)
            {
                return;
            }

            var distanceToBottom = contentHeight - (e.ScrollY + viewportHeight);
            if (distanceToBottom > LoadMoreThresholdPx)
            {
                return;
            }

            if (viewModel.LoadMoreCommand.CanExecute())
            {
                viewModel.LoadMoreCommand.Execute();
            }
        }
    }
}
