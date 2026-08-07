using Prism.Navigation;

namespace WildBerriesAnalyzer.Modules.Auth.Services
{
    /// <summary>
    /// При истечении/сбросе сессии переводит на экран авторизации.
    /// </summary>
    public interface IAuthSessionGuard
    {
        void Attach(INavigationService navigationService);
    }
}
