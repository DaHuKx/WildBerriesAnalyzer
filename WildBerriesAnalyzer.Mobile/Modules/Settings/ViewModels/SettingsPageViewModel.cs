using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Auth.Services;

namespace WildBerriesAnalyzer.Modules.Settings.ViewModels
{
    public class SettingsPageViewModel : BindableBase
    {
        private readonly IAuthSessionService _authSessionService;
        private readonly INavigationService _navigationService;
        private readonly IAppThemeService _appThemeService;
        private readonly IAdultContentPreferenceService _adultContentPreference;

        private string _statusMessage = string.Empty;

        public SettingsPageViewModel(
            IAuthSessionService authSessionService,
            INavigationService navigationService,
            IAppThemeService appThemeService,
            IAdultContentPreferenceService adultContentPreference)
        {
            _authSessionService = authSessionService;
            _navigationService = navigationService;
            _appThemeService = appThemeService;
            _adultContentPreference = adultContentPreference;

            SignOutCommand = new DelegateCommand(async () => await SignOutAsync());
            SetThemeCommand = new DelegateCommand<string>(SetTheme);

            _appThemeService.ThemeChanged += (_, _) => RaiseThemeProperties();
            _adultContentPreference.Changed += (_, _) => RaisePropertyChanged(nameof(ShowAdultContent));
        }

        public string Title => "Настройки";

        public string AppVersionText => $"Версия {AppClientVersion.Version}";

        public DelegateCommand SignOutCommand { get; }

        public DelegateCommand<string> SetThemeCommand { get; }

        public bool ShowAdultContent
        {
            get => _adultContentPreference.ShowAdultContent;
            set
            {
                if (_adultContentPreference.ShowAdultContent == value)
                {
                    return;
                }

                AppLog.Action("Settings", "AdultToggle", $"show={value}");
                _adultContentPreference.SetShowAdultContent(value);
                RaisePropertyChanged();
                StatusMessage = value
                    ? "Товары 18+ отображаются без ограничений."
                    : "Товары 18+ скрыты: изображения размыты, карточка недоступна.";
            }
        }

        public bool IsSystemTheme => _appThemeService.Preference == AppThemePreference.System;

        public bool IsLightTheme => _appThemeService.Preference == AppThemePreference.Light;

        public bool IsDarkTheme => _appThemeService.Preference == AppThemePreference.Dark;

        public string ThemeStatusText => _appThemeService.Preference switch
        {
            AppThemePreference.Light => "Светлая",
            AppThemePreference.Dark => "Тёмная",
            _ => "Как в системе"
        };

        public Color LightThemeButtonBackground =>
            IsLightTheme ? ThemeColors.Primary : ThemeColors.SurfaceMuted;

        public Color DarkThemeButtonBackground =>
            IsDarkTheme ? ThemeColors.Primary : ThemeColors.SurfaceMuted;

        public Color SystemThemeButtonBackground =>
            IsSystemTheme ? ThemeColors.Primary : ThemeColors.SurfaceMuted;

        public Color LightThemeButtonTextColor =>
            IsLightTheme ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;

        public Color DarkThemeButtonTextColor =>
            IsDarkTheme ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;

        public Color SystemThemeButtonTextColor =>
            IsSystemTheme ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetProperty(ref _statusMessage, value))
                {
                    RaisePropertyChanged(nameof(HasStatus));
                }
            }
        }

        public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

        private async Task SignOutAsync()
        {
            AppLog.Action("Settings", "SignOut");
            _authSessionService.SignOut();
            await _navigationService.NavigateAsync($"/{NavigationNames.LoginPage}");
        }

        private void SetTheme(string? preference)
        {
            if (!Enum.TryParse(preference, ignoreCase: true, out AppThemePreference parsed))
            {
                return;
            }

            AppLog.Action("Settings", "SetTheme", parsed.ToString());
            _appThemeService.SetPreference(parsed);
            RaiseThemeProperties();
            StatusMessage = $"Тема: {ThemeStatusText}.";
        }

        private void RaiseThemeProperties()
        {
            RaisePropertyChanged(nameof(IsSystemTheme));
            RaisePropertyChanged(nameof(IsLightTheme));
            RaisePropertyChanged(nameof(IsDarkTheme));
            RaisePropertyChanged(nameof(ThemeStatusText));
            RaisePropertyChanged(nameof(LightThemeButtonBackground));
            RaisePropertyChanged(nameof(DarkThemeButtonBackground));
            RaisePropertyChanged(nameof(SystemThemeButtonBackground));
            RaisePropertyChanged(nameof(LightThemeButtonTextColor));
            RaisePropertyChanged(nameof(DarkThemeButtonTextColor));
            RaisePropertyChanged(nameof(SystemThemeButtonTextColor));
        }
    }
}
