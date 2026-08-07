using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient
{
    public sealed class WbAuthTokenStore : IWbAuthTokenStore
    {
        private readonly object _sync = new();
        private AuthTokensResult? _tokens;

        public event EventHandler<AuthTokensResult>? TokensChanged;

        public event EventHandler? TokensCleared;

        public string? AccessToken
        {
            get
            {
                lock (_sync)
                {
                    return _tokens?.AccessToken;
                }
            }
        }

        public string? RefreshToken
        {
            get
            {
                lock (_sync)
                {
                    return _tokens?.RefreshToken;
                }
            }
        }

        public int? UserId
        {
            get
            {
                lock (_sync)
                {
                    return _tokens?.UserId;
                }
            }
        }

        public string? Login
        {
            get
            {
                lock (_sync)
                {
                    return _tokens?.Login;
                }
            }
        }

        public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);

        public void SetTokens(AuthTokensResult tokens)
        {
            ArgumentNullException.ThrowIfNull(tokens);

            lock (_sync)
            {
                _tokens = tokens;
            }

            TokensChanged?.Invoke(this, tokens);
        }

        public void Clear()
        {
            var hadTokens = false;
            lock (_sync)
            {
                hadTokens = _tokens is not null;
                _tokens = null;
            }

            if (hadTokens)
            {
                TokensCleared?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
