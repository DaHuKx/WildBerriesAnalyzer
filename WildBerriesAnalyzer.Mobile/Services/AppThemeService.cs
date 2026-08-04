namespace WildBerriesAnalyzer.Mobile.Services
{
    public sealed class AppThemeService : IAppThemeService
    {
        private const string PreferenceKey = "app_theme_preference";

        public AppThemeService()
        {
            Preference = LoadPreference();
            Apply();

            if (Application.Current is not null)
            {
                Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
            }
        }

        public AppThemePreference Preference { get; private set; }

        public bool IsDark =>
            Application.Current?.RequestedTheme == AppTheme.Dark;

        public event EventHandler? ThemeChanged;

        public void SetPreference(AppThemePreference preference)
        {
            Preference = preference;
            Preferences.Default.Set(PreferenceKey, preference.ToString());
            Apply();
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Apply()
        {
            if (Application.Current is null)
            {
                return;
            }

            Application.Current.UserAppTheme = Preference switch
            {
                AppThemePreference.Light => AppTheme.Light,
                AppThemePreference.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }

        private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        private static AppThemePreference LoadPreference()
        {
            var raw = Preferences.Default.Get(PreferenceKey, AppThemePreference.System.ToString());
            return Enum.TryParse(raw, ignoreCase: true, out AppThemePreference preference)
                ? preference
                : AppThemePreference.System;
        }
    }
}
