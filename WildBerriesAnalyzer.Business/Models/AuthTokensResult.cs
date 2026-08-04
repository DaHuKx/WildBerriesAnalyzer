namespace WildBerriesAnalyzer.Business.Models
{
    public class AuthTokensResult
    {
        public int UserId { get; init; }

        public string Login { get; init; } = string.Empty;

        public string AccessToken { get; init; } = string.Empty;

        public string RefreshToken { get; init; } = string.Empty;
    }
}
