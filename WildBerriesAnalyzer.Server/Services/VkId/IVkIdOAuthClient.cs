namespace WildBerriesAnalyzer.Server.Services.VkId
{
    public interface IVkIdOAuthClient
    {
        Task<VkIdTokenResponse> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            string deviceId,
            string state,
            string redirectUri,
            CancellationToken cancellationToken = default);

        Task<VkIdUserInfo> GetUserInfoAsync(
            string accessToken,
            CancellationToken cancellationToken = default);
    }

    public sealed class VkIdTokenResponse
    {
        public string AccessToken { get; init; } = string.Empty;

        public string RefreshToken { get; init; } = string.Empty;

        public string IdToken { get; init; } = string.Empty;

        public string UserId { get; init; } = string.Empty;

        public string State { get; init; } = string.Empty;
    }

    public sealed class VkIdUserInfo
    {
        public string UserId { get; init; } = string.Empty;

        public string? FirstName { get; init; }

        public string? LastName { get; init; }
    }
}
