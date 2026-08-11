using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.Mobile.Services;

namespace WildBerriesAnalyzer.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            AppLog.App.Information("App starting");

            // Apply saved preference before first page renders (DI may not be ready yet).
            var preference = Preferences.Default.Get("app_theme_preference", AppThemePreference.System.ToString());
            if (Enum.TryParse(preference, ignoreCase: true, out AppThemePreference parsed))
            {
                UserAppTheme = parsed switch
                {
                    AppThemePreference.Light => AppTheme.Light,
                    AppThemePreference.Dark => AppTheme.Dark,
                    _ => AppTheme.Unspecified
                };
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = base.CreateWindow(activationState);
            window.Destroying += (_, _) => MobileSerilog.Close();
            return window;
        }
    }
}
