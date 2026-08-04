namespace WildBerriesAnalyzer.Mobile.Services
{
    public enum AppThemePreference
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    public interface IAppThemeService
    {
        AppThemePreference Preference { get; }

        bool IsDark { get; }

        event EventHandler? ThemeChanged;

        void SetPreference(AppThemePreference preference);

        void Apply();
    }
}
