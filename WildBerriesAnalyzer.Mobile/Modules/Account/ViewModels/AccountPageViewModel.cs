using System.Globalization;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Helpers;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Auth.Services;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.Account.ViewModels
{
    public class AccountPageViewModel : BindableBase
    {
        private readonly IAccountClient _accountClient;
        private readonly IAuthSessionService _authSessionService;
        private readonly INavigationService _navigationService;
        private readonly IAppThemeService _appThemeService;

        private bool _isBusy;
        private bool _isLoaded;
        private bool _isVkLinked;
        private string _login = string.Empty;
        private string _vkStatusText = string.Empty;
        private string _linkCode = string.Empty;
        private string _linkInstruction = string.Empty;
        private string _linkCodeExpiresText = string.Empty;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public AccountPageViewModel(
            IAccountClient accountClient,
            IAuthSessionService authSessionService,
            INavigationService navigationService,
            IAppThemeService appThemeService)
        {
            _accountClient = accountClient;
            _authSessionService = authSessionService;
            _navigationService = navigationService;
            _appThemeService = appThemeService;

            RefreshCommand = new DelegateCommand(async () => await LoadAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);
            CreateLinkCodeCommand = new DelegateCommand(async () => await CreateLinkCodeAsync(), () => !IsBusy && !IsVkLinked)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsVkLinked);
            CopyLinkCodeCommand = new DelegateCommand(async () => await CopyLinkCodeAsync(), () => !IsBusy && HasLinkCode)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => HasLinkCode);
            SignOutCommand = new DelegateCommand(async () => await SignOutAsync(), () => !IsBusy)
                .ObservesProperty(() => IsBusy);
            SetThemeCommand = new DelegateCommand<string>(SetTheme);

            _appThemeService.ThemeChanged += (_, _) => RaiseThemeProperties();

            _ = LoadAsync();
        }

        public string Title => "Аккаунт";

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand CreateLinkCodeCommand { get; }

        public DelegateCommand CopyLinkCodeCommand { get; }

        public DelegateCommand SignOutCommand { get; }

        public DelegateCommand<string> SetThemeCommand { get; }

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

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public bool IsLoaded
        {
            get => _isLoaded;
            private set
            {
                if (SetProperty(ref _isLoaded, value))
                {
                    RaiseLinkVisibility();
                }
            }
        }

        public string Login
        {
            get => _login;
            private set => SetProperty(ref _login, value);
        }

        public bool IsVkLinked
        {
            get => _isVkLinked;
            private set
            {
                if (SetProperty(ref _isVkLinked, value))
                {
                    RaiseLinkVisibility();
                }
            }
        }

        public string VkStatusText
        {
            get => _vkStatusText;
            private set => SetProperty(ref _vkStatusText, value);
        }

        public string LinkCode
        {
            get => _linkCode;
            private set
            {
                if (SetProperty(ref _linkCode, value))
                {
                    RaisePropertyChanged(nameof(HasLinkCode));
                }
            }
        }

        public string LinkInstruction
        {
            get => _linkInstruction;
            private set => SetProperty(ref _linkInstruction, value);
        }

        public string LinkCodeExpiresText
        {
            get => _linkCodeExpiresText;
            private set => SetProperty(ref _linkCodeExpiresText, value);
        }

        public bool HasLinkCode => !string.IsNullOrWhiteSpace(LinkCode);

        public bool ShowLinkSection => IsLoaded && !IsVkLinked;

        public bool ShowUnlinkSection => IsLoaded && IsVkLinked;

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    RaisePropertyChanged(nameof(HasError));
                }
            }
        }

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

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

        private async Task LoadAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var profile = await _accountClient.GetMeAsync();
                Login = profile.Login ?? _authSessionService.Login ?? "—";
                IsVkLinked = profile.IsVkLinked;
                VkStatusText = profile.IsVkLinked
                    ? $"Привязан: {MaskVkId(profile.VkId)}"
                    : "Не привязан";

                if (profile.IsVkLinked)
                {
                    ClearLinkCodeUi();
                }

                IsLoaded = true;
                // IsVkLinked мог не измениться (остался false) — явно обновить видимость блока привязки.
                RaiseLinkVisibility();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                IsLoaded = false;
                RaiseLinkVisibility();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateLinkCodeAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var result = await _accountClient.CreateVkLinkCodeAsync();
                LinkCode = result.Code;
                LinkInstruction = result.Instruction;
                var localExpires = result.ExpiresAt.Kind == DateTimeKind.Utc
                    ? result.ExpiresAt.ToLocalTime()
                    : DateTime.SpecifyKind(result.ExpiresAt, DateTimeKind.Utc).ToLocalTime();
                LinkCodeExpiresText =
                    $"Действует до {localExpires.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"))}";
                StatusMessage = "Код создан. Отправьте его боту в VK.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CopyLinkCodeAsync()
        {
            if (!HasLinkCode)
            {
                return;
            }

            try
            {
                await Clipboard.Default.SetTextAsync($"ПРИВЯЗАТЬ {LinkCode}");
                StatusMessage = "Команда скопирована в буфер обмена.";
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private async Task SignOutAsync()
        {
            _authSessionService.SignOut();
            await _navigationService.NavigateAsync($"/{NavigationNames.LoginPage}");
        }

        private void ClearLinkCodeUi()
        {
            LinkCode = string.Empty;
            LinkInstruction = string.Empty;
            LinkCodeExpiresText = string.Empty;
        }

        private void SetTheme(string? preference)
        {
            if (!Enum.TryParse(preference, ignoreCase: true, out AppThemePreference parsed))
            {
                return;
            }

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

        private void RaiseLinkVisibility()
        {
            RaisePropertyChanged(nameof(ShowLinkSection));
            RaisePropertyChanged(nameof(ShowUnlinkSection));
        }

        private static string MaskVkId(string? vkId)
        {
            if (string.IsNullOrWhiteSpace(vkId))
            {
                return "—";
            }

            if (vkId.Length <= 4)
            {
                return new string('*', vkId.Length);
            }

            return $"{new string('*', Math.Min(4, vkId.Length - 4))}{vkId[^4..]}";
        }

        private static Page? GetCurrentPage()
        {
            return Application.Current?.Windows.FirstOrDefault()?.Page;
        }
    }
}
