using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace ModerMobile.Auth;

public interface IAuthSessionService
{
    bool IsAuthenticated { get; }

    int? UserId { get; }

    string? Login { get; }

    Task<bool> TryRestoreSessionAsync();

    void SignIn(AuthTokensResult tokens);

    void SignOut();
}

public sealed class AuthSessionService : IAuthSessionService
{
    private const string UserIdKey = "moder_auth_user_id";
    private const string LoginKey = "moder_auth_login";
    private const string AccessTokenKey = "moder_auth_access_token";
    private const string RefreshTokenKey = "moder_auth_refresh_token";

    private readonly IWbAuthTokenStore _tokenStore;
    private readonly IAuthClient _authClient;
    private WbUser? _currentUser;

    public AuthSessionService(IWbAuthTokenStore tokenStore, IAuthClient authClient)
    {
        _tokenStore = tokenStore;
        _authClient = authClient;
        _tokenStore.TokensChanged += OnTokensChanged;
        _tokenStore.TokensCleared += OnTokensCleared;
        RestoreTokensToStore();
    }

    public bool IsAuthenticated =>
        UserId is > 0 &&
        (!string.IsNullOrWhiteSpace(_tokenStore.AccessToken) ||
         !string.IsNullOrWhiteSpace(_tokenStore.RefreshToken));

    public int? UserId
    {
        get
        {
            if (_currentUser?.Id > 0)
            {
                return _currentUser.Id;
            }

            var id = Preferences.Default.Get(UserIdKey, 0);
            return id > 0 ? id : null;
        }
    }

    public string? Login => _currentUser?.Login ?? Preferences.Default.Get<string?>(LoginKey, null);

    public async Task<bool> TryRestoreSessionAsync()
    {
        RestoreTokensToStore();
        if (UserId is null)
        {
            return false;
        }

        if (_tokenStore.HasAccessToken)
        {
            return true;
        }

        var refreshToken = _tokenStore.RefreshToken
                           ?? Preferences.Default.Get<string?>(RefreshTokenKey, null);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            SignOut();
            return false;
        }

        try
        {
            var tokens = await _authClient.RefreshAsync(refreshToken);
            SignIn(tokens);
            return true;
        }
        catch
        {
            SignOut();
            return false;
        }
    }

    public void SignIn(AuthTokensResult tokens)
    {
        _currentUser = new WbUser { Id = tokens.UserId, Login = tokens.Login };
        _tokenStore.SetTokens(tokens);
    }

    public void SignOut()
    {
        _currentUser = null;
        _tokenStore.Clear();
    }

    private void OnTokensChanged(object? sender, AuthTokensResult tokens)
    {
        Preferences.Default.Set(UserIdKey, tokens.UserId);
        Preferences.Default.Set(LoginKey, tokens.Login);
        Preferences.Default.Set(AccessTokenKey, tokens.AccessToken);
        Preferences.Default.Set(RefreshTokenKey, tokens.RefreshToken);
        _currentUser = new WbUser { Id = tokens.UserId, Login = tokens.Login };
    }

    private void OnTokensCleared(object? sender, EventArgs e)
    {
        _currentUser = null;
        Preferences.Default.Remove(UserIdKey);
        Preferences.Default.Remove(LoginKey);
        Preferences.Default.Remove(AccessTokenKey);
        Preferences.Default.Remove(RefreshTokenKey);
    }

    private void RestoreTokensToStore()
    {
        var accessToken = Preferences.Default.Get<string?>(AccessTokenKey, null);
        var refreshToken = Preferences.Default.Get<string?>(RefreshTokenKey, null);
        var userId = Preferences.Default.Get(UserIdKey, 0);
        var login = Preferences.Default.Get<string?>(LoginKey, null);

        if (userId <= 0 ||
            string.IsNullOrWhiteSpace(login) ||
            (string.IsNullOrWhiteSpace(accessToken) && string.IsNullOrWhiteSpace(refreshToken)))
        {
            return;
        }

        _currentUser = new WbUser { Id = userId, Login = login };
        _tokenStore.SetTokens(new AuthTokensResult
        {
            UserId = userId,
            Login = login,
            AccessToken = accessToken ?? string.Empty,
            RefreshToken = refreshToken ?? string.Empty
        });
    }
}
