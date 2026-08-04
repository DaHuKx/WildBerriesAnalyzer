using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.ServerClient.Interfaces
{
    /// <summary>
    /// Хранит текущие access/refresh токены для HTTP-запросов к серверу.
    /// </summary>
    public interface IWbAuthTokenStore
    {
        string? AccessToken { get; }

        string? RefreshToken { get; }

        int? UserId { get; }

        string? Login { get; }

        bool HasAccessToken { get; }

        event EventHandler<AuthTokensResult>? TokensChanged;

        event EventHandler? TokensCleared;

        void SetTokens(AuthTokensResult tokens);

        void Clear();
    }
}
