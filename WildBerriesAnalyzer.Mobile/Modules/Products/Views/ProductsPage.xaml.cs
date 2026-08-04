using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Modules.Products.ViewModels;

namespace WildBerriesAnalyzer.Modules.Products.Views
{
    public partial class ProductsPage : ContentView
    {
        private const double LoadMoreThresholdPx = 240;

        public static readonly BindableProperty TileWidthProperty =
            BindableProperty.Create(nameof(TileWidth), typeof(double), typeof(ProductsPage), 168d);

        public static readonly BindableProperty TileImageHeightProperty =
            BindableProperty.Create(nameof(TileImageHeight), typeof(double), typeof(ProductsPage), 224d);

        public ProductsPage()
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

        private void OnProductsScroll(object? sender, ScrolledEventArgs e)
        {
            if (BindingContext is not ProductsPageViewModel viewModel)
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
