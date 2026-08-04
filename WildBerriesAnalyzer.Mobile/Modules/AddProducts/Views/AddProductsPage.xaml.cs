using WildBerriesAnalyzer.Mobile.Helpers;

namespace WildBerriesAnalyzer.Modules.AddProducts.Views
{
    public partial class AddProductsPage : ContentView
    {
        public static readonly BindableProperty TileWidthProperty =
            BindableProperty.Create(nameof(TileWidth), typeof(double), typeof(AddProductsPage), 168d);

        public static readonly BindableProperty TileImageHeightProperty =
            BindableProperty.Create(nameof(TileImageHeight), typeof(double), typeof(AddProductsPage), 224d);

        public AddProductsPage()
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
    }
}
