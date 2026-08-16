namespace ModerMobile.Pages;

public partial class MainMenuPage : ContentPage
{
    public MainMenuPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        EnsureBindingContext();
        if (BindingContext is ViewModels.MainMenuPageViewModel vm)
        {
            await vm.RefreshHintAsync();
        }
    }

    private void EnsureBindingContext()
    {
        if (BindingContext != null)
        {
            return;
        }

        var services = Handler?.MauiContext?.Services
                       ?? Application.Current?.Handler?.MauiContext?.Services;
        if (services != null)
        {
            BindingContext = services.GetRequiredService<ViewModels.MainMenuPageViewModel>();
        }
    }
}
