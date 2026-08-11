using WildBerriesAnalyzer.Mobile.Logging;

namespace WildBerriesAnalyzer.Mobile.Services
{
    public sealed class AdultContentPreferenceService : IAdultContentPreferenceService
    {
        private const string PreferenceKey = "show_adult_content";

        public AdultContentPreferenceService()
        {
            ShowAdultContent = Preferences.Default.Get(PreferenceKey, false);
        }

        public bool ShowAdultContent { get; private set; }

        public event EventHandler? Changed;

        public void SetShowAdultContent(bool showAdultContent)
        {
            if (ShowAdultContent == showAdultContent)
            {
                return;
            }

            ShowAdultContent = showAdultContent;
            Preferences.Default.Set(PreferenceKey, showAdultContent);
            AppLog.Action("Service", "AdultPreference", $"show={showAdultContent}");
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
