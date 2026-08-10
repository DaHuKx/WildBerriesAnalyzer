namespace WildBerriesAnalyzer.Mobile.Services
{
    public interface IAdultContentPreferenceService
    {
        /// <summary>
        /// Если false — товары 18+ показываются с размытием, переход к деталям блокируется.
        /// </summary>
        bool ShowAdultContent { get; }

        event EventHandler? Changed;

        void SetShowAdultContent(bool showAdultContent);
    }
}
