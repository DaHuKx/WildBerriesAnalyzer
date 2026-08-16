using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ModerMobile.Auth;

namespace ModerMobile.ViewModels;

public sealed class LoginPageViewModel : INotifyPropertyChanged
{
    private readonly IVkIdLoginService _vkLogin;
    private readonly IAuthSessionService _session;
    private string? _errorMessage;
    private bool _isBusy;

    public LoginPageViewModel(IVkIdLoginService vkLogin, IAuthSessionService session)
    {
        _vkLogin = vkLogin;
        _session = session;
        LoginCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
    }

    public ICommand LoginCommand { get; }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                (LoginCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task BootstrapAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (await _session.TryRestoreSessionAsync())
            {
                await Shell.Current.GoToAsync("//MainMenuPage");
            }
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

    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var tokens = await _vkLogin.LoginAsync();
            _session.SignIn(tokens);
            await Shell.Current.GoToAsync("//MainMenuPage");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.GetBaseException().Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
