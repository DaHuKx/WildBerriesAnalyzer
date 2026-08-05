namespace WildBerriesAnalyzer.Mobile.Services
{
    public interface IWbShareToBagService
    {
        bool HasPending { get; }

        event EventHandler? PendingShareAvailable;

        /// <summary>
        /// Добавляет ожидающий share в корзину текущего пользователя.
        /// null — нечего обрабатывать.
        /// </summary>
        Task<WbShareProcessResult?> TryProcessPendingAsync();
    }

    public sealed class WbShareProcessResult
    {
        public required string Message { get; init; }

        public required bool IsError { get; init; }
    }
}
