namespace ModerMobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        EnsureBindingContext();
        if (BindingContext is ViewModels.LoginPageViewModel vm)
        {
            await vm.BootstrapAsync();
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
            BindingContext = services.GetRequiredService<ViewModels.LoginPageViewModel>();
        }
    }
}
