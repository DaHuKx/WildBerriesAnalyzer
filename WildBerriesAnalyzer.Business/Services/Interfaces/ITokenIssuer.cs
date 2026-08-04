using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface ITokenIssuer
    {
        string CreateAccessToken(int userId, string login);

        string CreateRefreshToken(int userId, string login);

        ValidatedTokenInfo? ValidateAccessToken(string accessToken);

        ValidatedTokenInfo? ValidateRefreshToken(string refreshToken);
    }
}
