using System.Globalization;
using Prism.Mvvm;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Mobile.Services;

namespace WildBerriesAnalyzer.Modules.ActualDiscounts.Models
{
    public sealed class DiscontListItem : BindableBase
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        private ImageSource? _displayImage;
        private bool _isImageLoading;
        private bool _hasDisplayImage;
        private int _imageLoadGeneration;

        public int ProductId { get; init; }

        public long IdInMarket { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public string? ImageUrl { get; init; }

        public string? SizeImageUrl { get; init; }

        public string? Link { get; init; }

        public decimal DiscontPercent { get; init; }

        public decimal CurrentPrice { get; init; }

        public DateTime? CurrentPriceCheckTime { get; init; }

        public decimal? ReferencePrice { get; init; }

        public DateTime? ReferencePriceCheckTime { get; init; }

        public DateTime? ReferencePricePeriodFrom { get; init; }

        public ReferencePriceStrategy Strategy { get; init; }

        public double ReviewRating { get; init; }

        public int FeedBacksCount { get; init; }

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

        public string PercentText =>
            $"−{Math.Round(DiscontPercent).ToString("0", CultureInfo.InvariantCulture)}%";

        public string PriceText =>
            $"{CurrentPrice.ToString("N0", Ru)} ₽";

        public string CurrentPriceDateText => FormatDate(CurrentPriceCheckTime);

        public bool HasCurrentPriceDate => CurrentPriceCheckTime.HasValue;

        public string ReferencePriceText =>
            ReferencePrice is null
                ? string.Empty
                : $"{ReferencePrice.Value.ToString("N0", Ru)} ₽";

        public string ReferencePriceDateText => FormatReferencePeriod(ReferencePricePeriodFrom, ReferencePriceCheckTime);

        public bool HasReferencePrice =>
            ReferencePrice is not null && ReferencePrice.Value > 0;

        public bool HasReferencePriceDate =>
            HasReferencePrice
            && (ReferencePricePeriodFrom.HasValue || ReferencePriceCheckTime.HasValue);

        public string StrategyText => Strategy switch
        {
            ReferencePriceStrategy.LastKnownPrice => "Последняя цена",
            ReferencePriceStrategy.AveragePrice => "Средняя",
            ReferencePriceStrategy.Median => "Медиана",
            ReferencePriceStrategy.MinimumHistorical => "Исторический мин.",
            ReferencePriceStrategy.LowestPriceForLast30Days => "Мин. за 30 дней",
            ReferencePriceStrategy.AveragePriceForLast30Days => "Средняя за 30 дней",
            ReferencePriceStrategy.MedianPriceForLast30Days => "Медиана за 30 дней",
            _ => Strategy.ToString()
        };

        public string RatingText => ReviewRating.ToString("N1", CultureInfo.InvariantCulture);

        public string ArticleText => IdInMarket.ToString(CultureInfo.InvariantCulture);

        public string FeedBacksText => FeedBacksCount.ToString(CultureInfo.InvariantCulture);

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

                var streamBytes = bytes;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (generation != _imageLoadGeneration)
                    {
                        return;
                    }

                    DisplayImage = ImageSource.FromStream(() => new MemoryStream(streamBytes));
                    HasDisplayImage = true;
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

        public static DiscontListItem FromDiscont(Discont discont)
        {
            ArgumentNullException.ThrowIfNull(discont);

            var product = discont.Product;
            string? imageUrl = product?.ImageUrl;
            string? sizeImageUrl = null;
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                sizeImageUrl = imageUrl;
            }

            var referenceCheckTime = AsNullable(discont.ReferencePrice?.CheckTime);
            DateTime? periodFrom = discont.ReferencePricePeriodFrom;
            if (periodFrom is null
                && discont.ReferencePrice is { CreatedAt: var created }
                && created != default)
            {
                periodFrom = created;
            }

            return new DiscontListItem
            {
                ProductId = product?.Id ?? 0,
                IdInMarket = product?.IdInMarket ?? 0,
                Name = product?.Name ?? "Товар",
                Brand = product?.Brand ?? string.Empty,
                ImageUrl = imageUrl,
                SizeImageUrl = sizeImageUrl ?? imageUrl,
                Link = product?.Link,
                DiscontPercent = discont.DiscontPercent,
                CurrentPrice = discont.CurrentPrice?.Price ?? 0,
                CurrentPriceCheckTime = AsNullable(discont.CurrentPrice?.CheckTime),
                ReferencePrice = discont.ReferencePrice?.Price,
                ReferencePriceCheckTime = referenceCheckTime,
                ReferencePricePeriodFrom = periodFrom,
                Strategy = discont.ReferencePriceStrategy,
                ReviewRating = product?.ReviewRating ?? 0,
                FeedBacksCount = product?.FeedBacksCount ?? 0
            };
        }

        private static DateTime? AsNullable(DateTime? value)
        {
            if (value is null || value.Value == default)
            {
                return null;
            }

            return value;
        }

        private static string FormatReferencePeriod(DateTime? from, DateTime? to)
        {
            if (from is null && to is null)
            {
                return string.Empty;
            }

            if (from is null)
            {
                return FormatDate(to);
            }

            if (to is null)
            {
                return FormatDate(from);
            }

            var fromLocal = ToLocal(from.Value);
            var toLocal = ToLocal(to.Value);

            if (Math.Abs((toLocal - fromLocal).TotalMinutes) < 1)
            {
                return FormatDate(to);
            }

            return $"{fromLocal:dd.MM.yyyy} – {toLocal:dd.MM.yyyy}";
        }

        private static string FormatDate(DateTime? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            return ToLocal(value.Value).ToString("dd.MM.yyyy HH:mm", Ru);
        }

        private static DateTime ToLocal(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value.ToLocalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
        }
    }
}
