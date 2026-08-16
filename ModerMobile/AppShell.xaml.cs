namespace ModerMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(Pages.AssignCategoryPage), typeof(Pages.AssignCategoryPage));
        Routing.RegisterRoute(nameof(Pages.BulkAssignPage), typeof(Pages.BulkAssignPage));
    }
}
