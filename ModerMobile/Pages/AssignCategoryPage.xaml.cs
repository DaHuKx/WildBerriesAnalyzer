namespace ModerMobile.Pages;

public partial class AssignCategoryPage : ContentPage
{
    public AssignCategoryPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        EnsureBindingContext();
        if (BindingContext is ViewModels.AssignCategoryPageViewModel vm)
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
            BindingContext = services.GetRequiredService<ViewModels.AssignCategoryPageViewModel>();
        }
    }
}
