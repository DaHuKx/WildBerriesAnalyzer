namespace WildBerriesAnalyzer.Mobile.Services
{
    /// <summary>
    /// Очередь текста из Android Share / intent до обработки после авторизации.
    /// </summary>
    public interface IPendingShareStore
    {
        bool HasPending { get; }

        /// <summary>
        /// Сырой текст из Intent.ExtraText (ссылка WB/Ozon, артикул или «название + URL»).
        /// </summary>
        /// <param name="notifyListeners">
        /// false — только сохранить (вызывать из Activity lifecycle до OnResume).
        /// </param>
        void EnqueueRaw(string? sharedText, bool notifyListeners = true);

        /// <summary>
        /// Сообщить подписчикам, если в очереди есть необработанный share.
        /// </summary>
        void NotifyPendingListeners();

        bool TryPeek(out string? articleOrUrl, out string? errorMessage);

        bool TryDequeue(out string? articleOrUrl, out string? errorMessage);

        /// <summary>Ожидает обработки ссылка на общую корзину (shareId), а не карточку.</summary>
        bool TryPeekIsBasketShare();

        event EventHandler? PendingChanged;
    }
}
