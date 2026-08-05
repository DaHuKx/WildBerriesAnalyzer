using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Modules.MyFilters.ViewModels;

namespace WildBerriesAnalyzer.Modules.MyFilters.Views
{
    public partial class MyFiltersPage : ContentView
    {
        public static readonly BindableProperty TileWidthProperty =
            BindableProperty.Create(nameof(TileWidth), typeof(double), typeof(MyFiltersPage), 168d);

        public static readonly BindableProperty TileImageHeightProperty =
            BindableProperty.Create(nameof(TileImageHeight), typeof(double), typeof(MyFiltersPage), 224d);

        public MyFiltersPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
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
            // Grid Padding 12*2 + Border корзины 14*2 — иначе плитки не влазят в 2 колонки.
            var (tileWidth, imageHeight) = CatalogTileSizing.FromPageWidth(
                width,
                horizontalPadding: 52,
                gap: 8);
            if (Math.Abs(TileWidth - tileWidth) > 0.5)
            {
                TileWidth = tileWidth;
                TileImageHeight = imageHeight;
            }
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            await Task.Yield();
            await Task.Delay(32);

            if (BindingContext is not MyFiltersPageViewModel viewModel)
            {
                return;
            }

            await viewModel.LoadIfNeededAsync();
        }

        private void OnNumericEntryFocused(object? sender, FocusEventArgs e)
        {
            if (sender is Entry entry && entry.Parent is Border frame)
            {
                frame.Stroke = new SolidColorBrush(ThemeColors.Primary);
                frame.StrokeThickness = 2;
            }
        }

        private void OnNumericEntryUnfocused(object? sender, FocusEventArgs e)
        {
            if (sender is Entry entry && entry.Parent is Border frame)
            {
                frame.Stroke = new SolidColorBrush(ThemeColors.Outline);
                frame.StrokeThickness = 1;
            }
        }
    }
}
