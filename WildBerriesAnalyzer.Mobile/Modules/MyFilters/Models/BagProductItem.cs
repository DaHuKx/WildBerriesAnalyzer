using Prism.Commands;
using Prism.Mvvm;
using WildBerriesAnalyzer.Mobile.Services;

namespace WildBerriesAnalyzer.Modules.MyFilters.Models
{
    public class BagProductItem : BindableBase
    {
        private ImageSource? _displayImage;
        private bool _isImageLoading;
        private bool _hasDisplayImage;
        private int _imageLoadGeneration;

        public int ProductId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Article { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

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

        public string DisplayTitle => string.IsNullOrWhiteSpace(Brand)
            ? Name
            : $"{Name} ({Brand})";

        public DelegateCommand? RemoveCommand { get; set; }

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
    }
}
