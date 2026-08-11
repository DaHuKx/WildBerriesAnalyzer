using Prism.Navigation;
using WildBerriesAnalyzer.Mobile.Core;
using WildBerriesAnalyzer.Mobile.Logging;
using WildBerriesAnalyzer.Modules.Auth.Views;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.Auth.Services
{
    public sealed class AuthSessionGuard : IAuthSessionGuard
    {
        private readonly IWbAuthTokenStore _tokenStore;
        private INavigationService? _navigationService;
        private int _redirecting;

        public AuthSessionGuard(IWbAuthTokenStore tokenStore)
        {
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _tokenStore.TokensCleared += OnTokensCleared;
        }

        public void Attach(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        }

        private void OnTokensCleared(object? sender, EventArgs e)
        {
            _ = RedirectToLoginAsync();
        }

        private async Task RedirectToLoginAsync()
        {
            if (Interlocked.Exchange(ref _redirecting, 1) == 1)
            {
                return;
            }

            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (IsLoginVisible())
                    {
                        return;
                    }

                    var navigation = _navigationService;
                    if (navigation is null)
                    {
                        return;
                    }

                    AppLog.Action("Auth", "RedirectToLogin");
                    await navigation.NavigateAsync($"/{NavigationNames.LoginPage}");
                });
            }
            catch (Exception ex)
            {
                AppLog.Auth.Error(ex, "RedirectToLogin failed");
            }
            finally
            {
                Interlocked.Exchange(ref _redirecting, 0);
            }
        }

        private static bool IsLoginVisible()
        {
            var root = Application.Current?.Windows.FirstOrDefault()?.Page;
            return FindLoginPage(root) is not null;
        }

        private static LoginPage? FindLoginPage(Page? page)
        {
            return page switch
            {
                LoginPage login => login,
                NavigationPage nav => FindLoginPage(nav.CurrentPage),
                FlyoutPage flyout => FindLoginPage(flyout.Detail),
                TabbedPage tabbed => FindLoginPage(tabbed.CurrentPage),
                _ => null
            };
        }
    }
}
