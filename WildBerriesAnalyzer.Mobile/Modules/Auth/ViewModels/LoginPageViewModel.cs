using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.Mobile.Services;
using WildBerriesAnalyzer.Modules.Auth.Services;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.Auth.ViewModels
{
    public class LoginPageViewModel : BindableBase, INavigatedAware
    {
        private static readonly TimeSpan VkConfigTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan SessionRestoreTimeout = TimeSpan.FromSeconds(6);

        private readonly IAuthClient _authClient;
        private readonly IAuthSessionService _authSessionService;
        private readonly IVkIdLoginService _vkIdLoginService;
        private readonly INavigationService _navigationService;
        private readonly IPendingShareStore _pendingShareStore;

        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        private bool _isVkLoginAvailable;
        private bool _vkConfigChecked;
        private int _bootstrapVersion;
        private bool _navigatedAway;

        public LoginPageViewModel(
            IAuthClient authClient,
            IAuthSessionService authSessionService,
            IVkIdLoginService vkIdLoginService,
            INavigationService navigationService,
            IPendingShareStore pendingShareStore)
        {
            _authClient = authClient;
            _authSessionService = authSessionService;
            _vkIdLoginService = vkIdLoginService;
            _navigationService = navigationService;
            _pendingShareStore = pendingShareStore;

            LoginWithVkCommand = new DelegateCommand(async () => await LoginWithVkAsync(), () => !IsBusy && IsVkLoginAvailable)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => IsVkLoginAvailable);

            RefreshVkCommand = new DelegateCommand(() =>
            {
                _ = LoadVkAvailabilityAsync(force: true);
            }, () => !IsBusy).ObservesProperty(() => IsBusy);
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

        public bool IsVkLoginAvailable
        {
            get => _isVkLoginAvailable;
            private set
            {
                if (SetProperty(ref _isVkLoginAvailable, value))
                {
                    RaisePropertyChanged(nameof(ShowVkButton));
                    RaisePropertyChanged(nameof(ShowVkUnavailableHint));
                }
            }
        }

        public bool ShowVkButton => IsVkLoginAvailable;

        public bool ShowVkUnavailableHint => _vkConfigChecked && !IsVkLoginAvailable;

        public string HintText =>
            "Вход и регистрация — через VK ID. Если аккаунта ещё нет, он создастся автоматически после подтверждения в VK.";

        public string VkUnavailableHint =>
            "VK ID недоступен. На сервере нужны VK_ID_ENABLED=true и ClientId; приложение должно достучаться до API.";

        public DelegateCommand LoginWithVkCommand { get; }

        public DelegateCommand RefreshVkCommand { get; }

        public void OnNavigatedFrom(INavigationParameters parameters) =>
            _navigatedAway = true;

        public void OnNavigatedTo(INavigationParameters parameters)
        {
            _navigatedAway = false;
            if (_pendingShareStore.HasPending)
            {
                StatusMessage = "После входа товар из Wildberries добавится в корзину.";
            }

            _ = BootstrapAsync();
        }

        private async Task BootstrapAsync()
        {
            var version = Interlocked.Increment(ref _bootstrapVersion);
            AppLog.Action("Auth", "Bootstrap");

            // 1) Быстрый auto-login по сохранённой сессии (в фоне, с таймаутом).
            if (_authSessionService.IsAuthenticated)
            {
                StatusMessage = _pendingShareStore.HasPending
                    ? "Вход… добавляем товар в корзину."
                    : "Проверка сессии...";
                var restored = await RestoreSessionInBackgroundAsync();
                if (version != _bootstrapVersion || _navigatedAway)
                {
                    return;
                }

                if (restored)
                {
                    AppLog.Action("Auth", "Bootstrap", "restored → MainWindow");
                    StatusMessage = string.Empty;
                    await _navigationService.NavigateAsync($"/{NavigationNames.MainWindow}");
                    return;
                }

                AppLog.Action("Auth", "Bootstrap", "restore failed");
                StatusMessage = _pendingShareStore.HasPending
                    ? "После входа товар из Wildberries добавится в корзину."
                    : string.Empty;
            }

            // 2) Конфиг VK ID — тоже не блокирует UI.
            await LoadVkAvailabilityAsync(force: true);
        }

        private async Task<bool> RestoreSessionInBackgroundAsync()
        {
            try
            {
                AppLog.Action("Auth", "RestoreSession");
                var restoreTask = Task.Run(() => _authSessionService.TryRestoreSessionAsync());
                var completed = await Task.WhenAny(restoreTask, Task.Delay(SessionRestoreTimeout));
                if (completed != restoreTask)
                {
                    AppLog.Warning("Auth", "RestoreSession", "timeout");
                    return false;
                }

                return await restoreTask;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Auth", "RestoreSession");
                return false;
            }
        }

        private async Task LoadVkAvailabilityAsync(bool force)
        {
            if (_vkConfigChecked && !force && IsVkLoginAvailable)
            {
                return;
            }

            var loadVersion = _bootstrapVersion;

            try
            {
                using var cts = new CancellationTokenSource(VkConfigTimeout);
                var config = await Task.Run(
                    () => _authClient.GetVkAuthConfigAsync(cts.Token),
                    CancellationToken.None);

                if (loadVersion != _bootstrapVersion || _navigatedAway)
                {
                    return;
                }

                IsVkLoginAvailable = config.Enabled && !string.IsNullOrWhiteSpace(config.ClientId);
                AppLog.Action("Auth", "LoadVkConfig", $"available={IsVkLoginAvailable}");
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Auth", "LoadVkConfig");
                if (loadVersion != _bootstrapVersion || _navigatedAway)
                {
                    return;
                }

                IsVkLoginAvailable = false;
            }
            finally
            {
                if (loadVersion == _bootstrapVersion)
                {
                    _vkConfigChecked = true;
                    RaisePropertyChanged(nameof(ShowVkUnavailableHint));
                }
            }
        }

        private async Task LoginWithVkAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = "Открывается авторизация VK...";
                AppLog.Action("Auth", "LoginWithVk");

                var tokens = await _vkIdLoginService.LoginAsync();
                _authSessionService.SignIn(tokens);

                StatusMessage = "Вход выполнен.";
                AppLog.Action("Auth", "LoginWithVk", "success");
                await _navigationService.NavigateAsync($"/{NavigationNames.MainWindow}");
            }
            catch (UnauthorizedAccessException ex)
            {
                AppLog.Error(ex, "Auth", "LoginWithVk", "unauthorized");
                ErrorMessage = ex.Message;
                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Auth", "LoginWithVk");
                ErrorMessage = ex.Message;
                StatusMessage = string.Empty;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
