using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Services;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public class BagProductItem : BindableBase
    {
        private ImageSource? _displayImage;
        private bool _isImageLoading;
        private bool _hasDisplayImage;
        private bool _isAdultContentRestricted;
        private int _imageLoadGeneration;
        private byte[]? _clearImageBytes;

        public int ProductId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Article { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public bool IsAdult { get; init; }

        public string? ImageUrl { get; init; }

        public string? SizeImageUrl { get; init; }

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

        public string DisplayTitle => string.IsNullOrWhiteSpace(Brand)
            ? Name
            : $"{Name} ({Brand})";

        public DelegateCommand? RemoveCommand { get; set; }

        public void ApplyShowAdultContent(bool showAdultContent)
        {
            var restricted = AdultContentAccess.IsRestricted(IsAdult, showAdultContent);
            if (IsAdultContentRestricted == restricted)
            {
                return;
            }

            IsAdultContentRestricted = restricted;
            if (_clearImageBytes is { Length: > 0 })
            {
                ApplyDisplayBytes(_clearImageBytes);
            }
        }

        public async Task LoadImageAsync(IProductImageCache imageCache, CancellationToken cancellationToken = default)
        {
            var url = SizeImageUrl ?? ImageUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (HasDisplayImage)
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

        private void ApplyDisplayBytes(byte[] clearBytes)
        {
            var displayBytes = IsAdultContentRestricted
                ? AdultImageEffects.CreateBlurredPreview(clearBytes) ?? clearBytes
                : clearBytes;

            var streamBytes = displayBytes;
            DisplayImage = ImageSource.FromStream(() => new MemoryStream(streamBytes));
            HasDisplayImage = true;
        }
    }
}
