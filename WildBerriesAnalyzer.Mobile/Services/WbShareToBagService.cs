using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Modules.Auth.Services;

namespace WildBerriesAnalyzer.Mobile.Services
{
    public sealed class WbShareToBagService : IWbShareToBagService
    {
        private readonly IPendingShareStore _pendingShareStore;
        private readonly IFiltersService _filtersService;
        private readonly IAuthSessionService _authSessionService;

        public WbShareToBagService(
            IPendingShareStore pendingShareStore,
            IFiltersService filtersService,
            IAuthSessionService authSessionService)
        {
            _pendingShareStore = pendingShareStore;
            _filtersService = filtersService;
            _authSessionService = authSessionService;

            _pendingShareStore.PendingChanged += (_, _) => PendingShareAvailable?.Invoke(this, EventArgs.Empty);
        }

        public bool HasPending => _pendingShareStore.HasPending;

        public event EventHandler? PendingShareAvailable;

        public async Task<WbShareProcessResult?> TryProcessPendingAsync()
        {
            if (!_pendingShareStore.TryPeek(out var articleOrUrl, out var parseError))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(parseError))
            {
                _pendingShareStore.TryDequeue(out _, out _);
                return new WbShareProcessResult
                {
                    Message = parseError,
                    IsError = true
                };
            }

            var user = _authSessionService.CurrentUser;
            if (user is null || user.Id <= 0 || string.IsNullOrWhiteSpace(articleOrUrl))
            {
                return null;
            }

            var isBasketShare = _pendingShareStore.TryPeekIsBasketShare();

            if (!_pendingShareStore.TryDequeue(out articleOrUrl, out _) || string.IsNullOrWhiteSpace(articleOrUrl))
            {
                return null;
            }

            try
            {
                if (isBasketShare || ProductHelper.TryExtractBasketShareId(articleOrUrl, out _))
                {
                    var bagResult = await _filtersService.AddProductsToBagFromBasketShareAsync(
                        user.Id,
                        articleOrUrl);

                    return new WbShareProcessResult
                    {
                        Message = bagResult.AddedProducts.Count > 0
                            ? $"Из общей корзины добавлено: {bagResult.AddedProducts.Count}."
                            : "Товары из общей корзины уже есть в вашей корзине.",
                        IsError = false
                    };
                }

                var result = await _filtersService.AddProductsToBagAsync(user.Id, [articleOrUrl]);
                var article = ProductHelper.ExtractCleanArticle(articleOrUrl);
                var name = result.AddedProducts.FirstOrDefault()?.Name
                           ?? result.BagProducts.FirstOrDefault(p =>
                               string.Equals(
                                   p.IdInMarket.ToString(),
                                   article,
                                   StringComparison.Ordinal))?.Name;

                if (result.AddedProducts.Count > 0)
                {
                    return new WbShareProcessResult
                    {
                        Message = string.IsNullOrWhiteSpace(name)
                            ? "Товар добавлен в корзину."
                            : $"«{name}» добавлен в корзину.",
                        IsError = false
                    };
                }

                return new WbShareProcessResult
                {
                    Message = string.IsNullOrWhiteSpace(name)
                        ? "Товар уже есть в корзине."
                        : $"«{name}» уже есть в корзине.",
                    IsError = false
                };
            }
            catch (ArgumentException ex)
            {
                return new WbShareProcessResult
                {
                    Message = ex.Message,
                    IsError = true
                };
            }
            catch (Exception ex)
            {
                return new WbShareProcessResult
                {
                    Message = $"Не удалось добавить: {ex.Message}",
                    IsError = true
                };
            }
        }
    }
}
