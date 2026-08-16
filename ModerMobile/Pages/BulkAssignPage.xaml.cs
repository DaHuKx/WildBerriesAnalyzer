namespace ModerMobile.Pages;

public partial class BulkAssignPage : ContentPage
{
    public BulkAssignPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        EnsureBindingContext();
        if (BindingContext is ViewModels.BulkAssignPageViewModel vm)
        {
            await vm.LoadAsync();
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
            BindingContext = services.GetRequiredService<ViewModels.BulkAssignPageViewModel>();
        }
    }
}
