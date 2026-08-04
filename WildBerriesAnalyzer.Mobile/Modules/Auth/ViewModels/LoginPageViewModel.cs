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
        private string _vkProfileUrl = string.Empty;
        private string _verificationCode = string.Empty;
        private string _registrationId = string.Empty;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private string _botChatUrl = string.Empty;
        private bool _isBusy;
        private bool _isRegisterMode;
        private bool _isVkLoginAvailable;
        private bool _showBotFallback;
        private bool _awaitingVkCode;

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
                .ObservesProperty(() => VkProfileUrl)
                .ObservesProperty(() => IsRegisterMode)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => AwaitingVkCode);

            ConfirmCodeCommand = new DelegateCommand(async () => await ConfirmCodeAsync(), CanConfirmCode)
                .ObservesProperty(() => VerificationCode)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => AwaitingVkCode)
                .ObservesProperty(() => RegistrationId);

            ResendCodeCommand = new DelegateCommand(async () => await ResendCodeAsync(), CanResendCode)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => AwaitingVkCode)
                .ObservesProperty(() => RegistrationId);

            ToggleModeCommand = new DelegateCommand(ToggleMode, () => !IsBusy)
                .ObservesProperty(() => IsBusy);

            LoginWithVkCommand = new DelegateCommand(async () => await LoginWithVkAsync(), () => !IsBusy && IsVkLoginAvailable && !AwaitingVkCode)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsVkLoginAvailable)
                .ObservesProperty(() => AwaitingVkCode);

            OpenBotChatCommand = new DelegateCommand(async () => await OpenBotChatAsync(), () => !string.IsNullOrWhiteSpace(BotChatUrl))
                .ObservesProperty(() => BotChatUrl);

            CancelVerificationCommand = new DelegateCommand(CancelVerification, () => !IsBusy && AwaitingVkCode)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => AwaitingVkCode);

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

        public string VkProfileUrl
        {
            get => _vkProfileUrl;
            set => SetProperty(ref _vkProfileUrl, value);
        }

        public string VerificationCode
        {
            get => _verificationCode;
            set => SetProperty(ref _verificationCode, value);
        }

        public string RegistrationId
        {
            get => _registrationId;
            private set => SetProperty(ref _registrationId, value);
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

        public string BotChatUrl
        {
            get => _botChatUrl;
            private set => SetProperty(ref _botChatUrl, value);
        }

        public bool ShowBotFallback
        {
            get => _showBotFallback;
            private set => SetProperty(ref _showBotFallback, value);
        }

        public bool AwaitingVkCode
        {
            get => _awaitingVkCode;
            private set
            {
                if (SetProperty(ref _awaitingVkCode, value))
                {
                    RaisePropertyChanged(nameof(TitleText));
                    RaisePropertyChanged(nameof(ShowCredentialsForm));
                    RaisePropertyChanged(nameof(ShowVkLoginButton));
                }
            }
        }

        public bool ShowCredentialsForm => !AwaitingVkCode;

        public bool ShowVkLoginButton => IsVkLoginAvailable && !AwaitingVkCode;

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
                    RaisePropertyChanged(nameof(SubmitButtonText));
                    RaisePropertyChanged(nameof(ToggleModeButtonText));
                    ResetVerificationState();
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
                }
            }
        }

        public string TitleText => AwaitingVkCode
            ? "Подтверждение VK"
            : IsRegisterMode ? "Регистрация" : "Вход";

        public string SubmitButtonText => IsRegisterMode ? "Получить код в VK" : "Войти";

        public string ToggleModeButtonText => IsRegisterMode
            ? "Уже есть аккаунт? Войти"
            : "Нет аккаунта? Зарегистрироваться";

        public DelegateCommand SubmitCommand { get; }

        public DelegateCommand ConfirmCodeCommand { get; }

        public DelegateCommand ResendCodeCommand { get; }

        public DelegateCommand ToggleModeCommand { get; }

        public DelegateCommand LoginWithVkCommand { get; }

        public DelegateCommand OpenBotChatCommand { get; }

        public DelegateCommand CancelVerificationCommand { get; }

        private bool CanSubmit()
        {
            if (IsBusy || AwaitingVkCode || string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
            {
                return false;
            }

            if (IsRegisterMode && string.IsNullOrWhiteSpace(VkProfileUrl))
            {
                return false;
            }

            return true;
        }

        private bool CanConfirmCode() =>
            !IsBusy &&
            AwaitingVkCode &&
            !string.IsNullOrWhiteSpace(RegistrationId) &&
            !string.IsNullOrWhiteSpace(VerificationCode);

        private bool CanResendCode() =>
            !IsBusy &&
            AwaitingVkCode &&
            !string.IsNullOrWhiteSpace(RegistrationId);

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
                ShowBotFallback = false;
                BotChatUrl = string.Empty;

                var login = Login.Trim();
                var password = Password;

                if (IsRegisterMode)
                {
                    var result = await _authService.RegisterAsync(login, password, VkProfileUrl.Trim());
                    RegistrationId = result.RegistrationId;
                    AwaitingVkCode = true;
                    VerificationCode = string.Empty;
                    StatusMessage = result.Message;
                    BotChatUrl = result.BotChatUrl;
                    ShowBotFallback = !result.VerificationMessageSent;
                    return;
                }

                var loginTokens = await _authService.LoginAsync(login, password);
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

        private async Task ConfirmCodeAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                var tokens = await _authService.ConfirmRegisterAsync(RegistrationId, VerificationCode.Trim());
                _authSessionService.SignIn(tokens);
                StatusMessage = "Регистрация подтверждена.";
                ResetVerificationState();
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

        private async Task ResendCodeAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                var result = await _authService.ResendRegisterCodeAsync(RegistrationId);
                RegistrationId = result.RegistrationId;
                StatusMessage = result.Message;
                BotChatUrl = result.BotChatUrl;
                ShowBotFallback = !result.VerificationMessageSent;
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
                StatusMessage = "Открывается авторизация VK...";
                ShowBotFallback = false;

                var tokens = await _vkIdLoginService.LoginAsync();
                _authSessionService.SignIn(tokens);

                StatusMessage = "Вход через VK выполнен.";
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

        private async Task OpenBotChatAsync()
        {
            if (string.IsNullOrWhiteSpace(BotChatUrl))
            {
                return;
            }

            try
            {
                await Launcher.Default.OpenAsync(BotChatUrl);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void ToggleMode()
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;
        }

        private void CancelVerification()
        {
            ResetVerificationState();
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;
        }

        private void ResetVerificationState()
        {
            AwaitingVkCode = false;
            RegistrationId = string.Empty;
            VerificationCode = string.Empty;
            ShowBotFallback = false;
            BotChatUrl = string.Empty;
        }
    }
}
