using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Modules.Auth.Services;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.Auth.ViewModels
{
    public class LoginPageViewModel : BindableBase
    {
        private readonly IAuthService _authService;
        private readonly IAuthClient _authClient;
        private readonly IAuthSessionService _authSessionService;
        private readonly IVkIdLoginService _vkIdLoginService;
        private readonly INavigationService _navigationService;

        private string _login = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        private bool _isRegisterMode;
        private bool _isVkLoginAvailable;

        public LoginPageViewModel(
            IAuthService authService,
            IAuthClient authClient,
            IAuthSessionService authSessionService,
            IVkIdLoginService vkIdLoginService,
            INavigationService navigationService)
        {
            _authService = authService;
            _authClient = authClient;
            _authSessionService = authSessionService;
            _vkIdLoginService = vkIdLoginService;
            _navigationService = navigationService;

            SubmitCommand = new DelegateCommand(async () => await SubmitAsync(), CanSubmit)
                .ObservesProperty(() => Login)
                .ObservesProperty(() => Password)
                .ObservesProperty(() => IsRegisterMode)
                .ObservesProperty(() => IsBusy);

            ToggleModeCommand = new DelegateCommand(ToggleMode, () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            LoginWithVkCommand = new DelegateCommand(async () => await LoginWithVkAsync(), () => !IsBusy && IsVkLoginAvailable)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsVkLoginAvailable);

            _ = LoadVkAvailabilityAsync();
        }

        public string Login
        {
            get => _login;
            set => SetProperty(ref _login, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsRegisterMode
        {
            get => _isRegisterMode;
            set
            {
                if (SetProperty(ref _isRegisterMode, value))
                {
                    RaisePropertyChanged(nameof(TitleText));
                    RaisePropertyChanged(nameof(ToggleModeButtonText));
                    RaisePropertyChanged(nameof(VkButtonText));
                    RaisePropertyChanged(nameof(ShowCredentialsForm));
                    RaisePropertyChanged(nameof(ShowRegisterVkPanel));
                    RaisePropertyChanged(nameof(ShowLoginDivider));
                }
            }
        }

        public bool IsVkLoginAvailable
        {
            get => _isVkLoginAvailable;
            private set
            {
                if (SetProperty(ref _isVkLoginAvailable, value))
                {
                    RaisePropertyChanged(nameof(ShowVkLoginButton));
                    RaisePropertyChanged(nameof(ShowRegisterVkPanel));
                    RaisePropertyChanged(nameof(ShowVkUnavailableHint));
                    RaisePropertyChanged(nameof(ShowLoginDivider));
                }
            }
        }

        /// <summary>
        /// Форма логин/пароль только во входе.
        /// </summary>
        public bool ShowCredentialsForm => !IsRegisterMode;

        /// <summary>
        /// Регистрация — только через VK ID.
        /// </summary>
        public bool ShowRegisterVkPanel => IsRegisterMode;

        public bool ShowVkLoginButton => IsVkLoginAvailable;

        public bool ShowLoginDivider => !IsRegisterMode && IsVkLoginAvailable;

        public bool ShowVkUnavailableHint => IsRegisterMode && !IsVkLoginAvailable;

        public string TitleText => IsRegisterMode ? "Регистрация" : "Вход";

        public string VkButtonText => IsRegisterMode
            ? "Зарегистрироваться через VK ID"
            : "Войти через VK ID";

        public string ToggleModeButtonText => IsRegisterMode
            ? "Уже есть аккаунт? Войти"
            : "Нет аккаунта? Зарегистрироваться";

        public string RegisterHint =>
            "Регистрация проходит через VK ID: откроется окно авторизации VK, " +
            "после подтверждения аккаунт PriceLab создастся автоматически.";

        public DelegateCommand SubmitCommand { get; }

        public DelegateCommand ToggleModeCommand { get; }

        public DelegateCommand LoginWithVkCommand { get; }

        private bool CanSubmit() =>
            !IsBusy &&
            !IsRegisterMode &&
            !string.IsNullOrWhiteSpace(Login) &&
            !string.IsNullOrWhiteSpace(Password);

        private async Task LoadVkAvailabilityAsync()
        {
            try
            {
                var config = await _authClient.GetVkAuthConfigAsync();
                IsVkLoginAvailable = config.Enabled && !string.IsNullOrWhiteSpace(config.ClientId);
            }
            catch
            {
                IsVkLoginAvailable = false;
            }
        }

        private async Task SubmitAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var loginTokens = await _authService.LoginAsync(Login.Trim(), Password);
                _authSessionService.SignIn(loginTokens);
                StatusMessage = "Авторизация успешна.";
                await _navigationService.NavigateAsync($"/{NavigationNames.MainWindow}");
            }
            catch (UnauthorizedAccessException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (ArgumentException ex)
            {
                ErrorMessage = ex.Message;
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

        private async Task LoginWithVkAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = IsRegisterMode
                    ? "Открывается регистрация через VK..."
                    : "Открывается авторизация VK...";

                var tokens = await _vkIdLoginService.LoginAsync();
                _authSessionService.SignIn(tokens);

                StatusMessage = IsRegisterMode
                    ? "Регистрация через VK выполнена."
                    : "Вход через VK выполнен.";
                await _navigationService.NavigateAsync($"/{NavigationNames.MainWindow}");
            }
            catch (UnauthorizedAccessException ex)
            {
                ErrorMessage = ex.Message;
                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                StatusMessage = string.Empty;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ToggleMode()
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;
        }
    }
}
