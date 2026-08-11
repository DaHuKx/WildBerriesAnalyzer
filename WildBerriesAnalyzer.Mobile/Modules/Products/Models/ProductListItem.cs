using System.Globalization;
using Prism.Mvvm;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Services;

namespace WildBerriesAnalyzer.Modules.Products.Models
{
    public class ProductListItem : BindableBase
    {
        private ImageSource? _displayImage;
        private bool _isImageLoading;
        private bool _hasDisplayImage;
        private bool _isInBag;
        private bool _isAdultContentRestricted;
        private int _imageLoadGeneration;
        private byte[]? _clearImageBytes;

        public int Id { get; init; }

        public long IdInMarket { get; init; }

        public MarketType MarketType { get; init; } = MarketType.Wildberries;

        public string MarketBadgeLabel => MarketBadge.LabelFor(MarketType);

        public Color MarketBadgeColor => MarketBadge.ColorFor(MarketType);

        public string Name { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public double ReviewRating { get; init; }

        public int FeedBacksCount { get; init; }

        public bool IsAdult { get; init; }

        public string? ImageUrl { get; init; }

        public string? SizeImageUrl { get; init; }

        public string? Link { get; init; }

        public decimal LastPrice { get; init; }

        public decimal MedianPrice { get; init; }

        public ImageSource? DisplayImage
        {
            get => _displayImage;
            private set => SetProperty(ref _displayImage, value);
        }

        public bool IsImageLoading
        {
            get => _isImageLoading;
            private set => SetProperty(ref _isImageLoading, value);
        }

        public bool HasDisplayImage
        {
            get => _hasDisplayImage;
            private set => SetProperty(ref _hasDisplayImage, value);
        }

        public bool IsAdultContentRestricted
        {
            get => _isAdultContentRestricted;
            private set
            {
                if (SetProperty(ref _isAdultContentRestricted, value))
                {
                    RaisePropertyChanged(nameof(AdultImageOpacity));
                }
            }
        }

        public double AdultImageOpacity => IsAdultContentRestricted ? 0.35 : 1d;

        /// <summary>
        /// Товар уже в корзине фильтров пользователя.
        /// </summary>
        public bool IsInBag
        {
            get => _isInBag;
            set
            {
                if (SetProperty(ref _isInBag, value))
                {
                    RaisePropertyChanged(nameof(BagActionIcon));
                    RaisePropertyChanged(nameof(BagActionBackground));
                }
            }
        }

        /// <summary>
        /// 🛒 — добавить в корзину; ✕ — убрать из корзины.
        /// </summary>
        public string BagActionIcon => IsInBag ? "✕" : "🛒";

        public Color BagActionBackground =>
            IsInBag
                ? Color.FromArgb("#99000000")
                : Color.FromArgb("#CC0F766E");

        public string RatingText => ReviewRating.ToString("N2", CultureInfo.InvariantCulture);

        public string PriceText => $"{LastPrice.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))} ₽";

        public string ArticleText => IdInMarket.ToString(CultureInfo.InvariantCulture);

        public string FeedBacksText => FeedBacksCount.ToString(CultureInfo.InvariantCulture);

        public void ApplyShowAdultContent(bool showAdultContent)
        {
            var restricted = AdultContentAccess.IsRestricted(IsAdult, showAdultContent);
            if (IsAdultContentRestricted == restricted)
            {
                return;
            }

            IsAdultContentRestricted = restricted;
            RefreshDisplayImage();
        }

        public async Task LoadImageAsync(IProductImageCache imageCache, CancellationToken cancellationToken = default)
        {
            var url = SizeImageUrl ?? ImageUrl;
            if (string.IsNullOrWhiteSpace(url) || HasDisplayImage)
            {
                return;
            }

            var generation = Interlocked.Increment(ref _imageLoadGeneration);
            await MainThread.InvokeOnMainThreadAsync(() => IsImageLoading = true);

            try
            {
                var bytes = await imageCache.GetOrLoadAsync(url, cancellationToken).ConfigureAwait(false);
                if (bytes is null || bytes.Length == 0 || generation != _imageLoadGeneration)
                {
                    return;
                }

                _clearImageBytes = bytes;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (generation != _imageLoadGeneration)
                    {
                        return;
                    }

                    ApplyDisplayBytes(bytes);
                });
            }
            finally
            {
                if (generation == _imageLoadGeneration)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsImageLoading = false);
                }
            }
        }

        public static ProductListItem FromProduct(WbProduct product)
        {
            ArgumentNullException.ThrowIfNull(product);

            var history = product.PricesHistory?
                .OrderBy(p => p.CheckTime)
                .ToList() ?? [];

            var lastPrice = history.Count > 0
                ? history[^1].Price
                : 0m;

            var medianPrice = GetMedian(history.Select(p => p.Price));

            string? sizeImageUrl = null;
            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
            {
                // Full-size CDN image for large card previews.
                sizeImageUrl = product.ImageUrl;
            }

            return new ProductListItem
            {
                Id = product.Id,
                IdInMarket = product.IdInMarket,
                MarketType = product.MarketType,
                Name = product.Name ?? string.Empty,
                Brand = product.Brand ?? string.Empty,
                ReviewRating = product.ReviewRating,
                FeedBacksCount = product.FeedBacksCount,
                IsAdult = product.IsAdult,
                ImageUrl = product.ImageUrl,
                SizeImageUrl = sizeImageUrl ?? product.ImageUrl,
                Link = product.Link,
                LastPrice = lastPrice,
                MedianPrice = medianPrice
            };
        }

        private void RefreshDisplayImage()
        {
            if (_clearImageBytes is null || _clearImageBytes.Length == 0)
            {
                return;
            }

            ApplyDisplayBytes(_clearImageBytes);
        }

        private void ApplyDisplayBytes(byte[] clearBytes)
        {
            var displayBytes = IsAdultContentRestricted
                ? AdultImageEffects.CreateBlurredPreview(clearBytes) ?? clearBytes
                : clearBytes;

            var streamBytes = displayBytes;
            DisplayImage = ImageSource.FromStream(() => new MemoryStream(streamBytes));
            HasDisplayImage = true;
        }

        private static decimal GetMedian(IEnumerable<decimal> values)
        {
            var ordered = values.OrderBy(v => v).ToList();
            if (ordered.Count == 0)
            {
                return 0m;
            }

            var mid = ordered.Count / 2;
            if (ordered.Count % 2 == 0)
            {
                return (ordered[mid - 1] + ordered[mid]) / 2m;
            }

            return ordered[mid];
        }
    }
}
