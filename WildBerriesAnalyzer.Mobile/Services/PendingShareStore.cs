using WildBerriesAnalyzer.Business.Helpers;

namespace WildBerriesAnalyzer.Mobile.Services
{
    public sealed class PendingShareStore : IPendingShareStore
    {
        /// <summary>
        /// Доступ до готовности DI (MainActivity.OnCreate / OnNewIntent).
        /// </summary>
        public static PendingShareStore Instance { get; } = new();

        private readonly object _gate = new();
        private string? _articleOrUrl;
        private string? _errorMessage;
        private bool _isBasketShare;

        public bool HasPending
        {
            get
            {
                lock (_gate)
                {
                    return _articleOrUrl is not null || _errorMessage is not null;
                }
            }
        }

        public event EventHandler? PendingChanged;

        public void EnqueueRaw(string? sharedText, bool notifyListeners = true)
        {
            if (string.IsNullOrWhiteSpace(sharedText))
            {
                return;
            }

            lock (_gate)
            {
                if (ProductHelper.TryExtractBasketShareId(sharedText, out _))
                {
                    _articleOrUrl = sharedText.Trim();
                    _isBasketShare = true;
                    _errorMessage = null;
                }
                else if (ProductHelper.TryExtractArticleInput(sharedText, out var articleOrUrl))
                {
                    _articleOrUrl = articleOrUrl;
                    _isBasketShare = false;
                    _errorMessage = null;
                }
                else
                {
                    _articleOrUrl = null;
                    _isBasketShare = false;
                    _errorMessage =
                        "Не удалось распознать товар или корзину Wildberries. Поделитесь ссылкой на карточку или basket?shareId=…";
                }
            }

            if (notifyListeners)
            {
                NotifyPendingListeners();
            }
        }

        public void NotifyPendingListeners()
        {
            if (!HasPending)
            {
                return;
            }

            PendingChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool TryPeek(out string? articleOrUrl, out string? errorMessage)
        {
            lock (_gate)
            {
                articleOrUrl = _articleOrUrl;
                errorMessage = _errorMessage;
                return articleOrUrl is not null || errorMessage is not null;
            }
        }

        public bool TryDequeue(out string? articleOrUrl, out string? errorMessage)
        {
            lock (_gate)
            {
                articleOrUrl = _articleOrUrl;
                errorMessage = _errorMessage;
                if (articleOrUrl is null && errorMessage is null)
                {
                    return false;
                }

                _articleOrUrl = null;
                _errorMessage = null;
                _isBasketShare = false;
                return true;
            }
        }

        public bool TryPeekIsBasketShare()
        {
            lock (_gate)
            {
                return _isBasketShare && _articleOrUrl is not null;
            }
        }
    }
}
