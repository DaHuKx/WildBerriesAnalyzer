using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.Modules.Auth.Services
{
    public class AuthSessionService : IAuthSessionService
    {
        private const string UserIdKey = "auth_user_id";
        private const string LoginKey = "auth_login";
        private const string AccessTokenKey = "auth_access_token";
        private const string RefreshTokenKey = "auth_refresh_token";

        private readonly IWbAuthTokenStore _tokenStore;
        private readonly IAuthClient _authClient;
        private WbUser? _currentUser;

        public AuthSessionService(IWbAuthTokenStore tokenStore, IAuthClient authClient)
        {
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));

            _tokenStore.TokensChanged += OnTokensChanged;
            _tokenStore.TokensCleared += OnTokensCleared;

            RestoreTokensToStore();
        }

        public bool IsAuthenticated =>
            CurrentUser is not null &&
            CurrentUser.Id > 0 &&
            (!string.IsNullOrWhiteSpace(_tokenStore.AccessToken) ||
             !string.IsNullOrWhiteSpace(_tokenStore.RefreshToken));

        public int? UserId => CurrentUser?.Id > 0 ? CurrentUser.Id : null;

        public string? Login => CurrentUser?.Login ?? _tokenStore.Login;

        public WbUser? CurrentUser
        {
            get
            {
                if (_currentUser is not null)
                {
                    return _currentUser;
                }

                var userId = Preferences.Default.Get(UserIdKey, 0);
                var login = Preferences.Default.Get<string?>(LoginKey, null);
                if (userId <= 0 || string.IsNullOrWhiteSpace(login))
                {
                    return null;
                }

                _currentUser = new WbUser
                {
                    Id = userId,
                    Login = login
                };

                return _currentUser;
            }
        }

        public async Task<bool> TryRestoreSessionAsync()
        {
            RestoreTokensToStore();

            if (CurrentUser is null)
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
            ArgumentNullException.ThrowIfNull(tokens);

            _currentUser = new WbUser
            {
                Id = tokens.UserId,
                Login = tokens.Login
            };

            // Persist через событие TokensChanged.
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

            _currentUser = new WbUser
            {
                Id = tokens.UserId,
                Login = tokens.Login
            };
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

            _currentUser = new WbUser
            {
                Id = userId,
                Login = login
            };

            // Без повторной записи в Preferences: пишем напрямую в store через временный mute?
            // TokensChanged всё равно обновит Preferences теми же значениями — это ок.
            _tokenStore.SetTokens(new AuthTokensResult
            {
                UserId = userId,
                Login = login,
                AccessToken = accessToken ?? string.Empty,
                RefreshToken = refreshToken ?? string.Empty
            });
        }
    }
}
