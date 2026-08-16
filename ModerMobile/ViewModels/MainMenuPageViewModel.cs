using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ModerMobile.Auth;
using WildBerriesAnalyzer.ServerClient.Clients;

namespace ModerMobile.ViewModels;

public sealed class MainMenuPageViewModel : INotifyPropertyChanged
{
    private readonly IModerClient _moderClient;
    private readonly IAuthSessionService _session;
    private string _queueHint = "Товары без категории";
    private bool _isBusy;

    public MainMenuPageViewModel(IModerClient moderClient, IAuthSessionService session)
    {
        _moderClient = moderClient;
        _session = session;
        StartCommand = new Command(async () => await StartAsync(), () => !IsBusy);
        BulkCommand = new Command(async () => await OpenBulkAsync(), () => !IsBusy);
        LogoutCommand = new Command(async () => await LogoutAsync());
    }

    public ICommand StartCommand { get; }
    public ICommand BulkCommand { get; }
    public ICommand LogoutCommand { get; }

    public string QueueHint
    {
        get => _queueHint;
        private set => Set(ref _queueHint, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                (StartCommand as Command)?.ChangeCanExecute();
                (BulkCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshHintAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//LoginPage");
            return;
        }

        try
        {
            var count = await _moderClient.GetQueueCountAsync();
            QueueHint = count > 0
                ? $"В очереди: {count} товар(ов) без категории"
                : "Очередь пуста — все товары с категорией";
        }
        catch (Exception ex)
        {
            QueueHint = ex.GetBaseException().Message;
        }
    }

    private async Task StartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await Shell.Current.GoToAsync(nameof(Pages.AssignCategoryPage));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenBulkAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await Shell.Current.GoToAsync(nameof(Pages.BulkAssignPage));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LogoutAsync()
    {
        _session.SignOut();
        await Shell.Current.GoToAsync("//LoginPage");
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
