using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Server.Options;

namespace WildBerriesAnalyzer.Server.Services
{
    public class TokenIssuer : ITokenIssuer
    {
        public const string TokenTypeClaim = "token_type";
        public const string AccessTokenType = "access";
        public const string RefreshTokenType = "refresh";

        private readonly JwtOptions _options;
        private readonly TokenValidationParameters _validationParameters;
        private readonly SigningCredentials _signingCredentials;
        private readonly JwtSecurityTokenHandler _tokenHandler = new();

        public TokenIssuer(IOptions<JwtOptions> options)
        {
            _options = options.Value;

            if (string.IsNullOrWhiteSpace(_options.Secret) || _options.Secret.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:Secret должен быть задан и содержать не менее 32 символов.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
            _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            _validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        }

        public string CreateAccessToken(int userId, string login)
        {
            return CreateToken(
                userId,
                login,
                AccessTokenType,
                TimeSpan.FromMinutes(_options.AccessTokenExpirationMinutes));
        }

        public string CreateRefreshToken(int userId, string login)
        {
            return CreateToken(
                userId,
                login,
                RefreshTokenType,
                TimeSpan.FromDays(_options.RefreshTokenExpirationDays));
        }

        public ValidatedTokenInfo? ValidateAccessToken(string accessToken)
        {
            return ValidateToken(accessToken, AccessTokenType);
        }

        public ValidatedTokenInfo? ValidateRefreshToken(string refreshToken)
        {
            return ValidateToken(refreshToken, RefreshTokenType);
        }

        private ValidatedTokenInfo? ValidateToken(string token, string expectedTokenType)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var principal = _tokenHandler.ValidateToken(token, _validationParameters, out var securityToken);
                if (securityToken is not JwtSecurityToken)
                {
                    return null;
                }

                var tokenType = principal.FindFirst(TokenTypeClaim)?.Value;
                if (!string.Equals(tokenType, expectedTokenType, StringComparison.Ordinal))
                {
                    return null;
                }

                var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
                {
                    return null;
                }

                var login = principal.FindFirst(ClaimTypes.Name)?.Value
                    ?? principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                    ?? string.Empty;

                return new ValidatedTokenInfo
                {
                    UserId = userId,
                    Login = login
                };
            }
            catch (SecurityTokenException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private string CreateToken(int userId, string login, string tokenType, TimeSpan lifetime)
        {
            var now = DateTime.UtcNow;
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, login),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, login),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new(TokenTypeClaim, tokenType)
            };

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: now.Add(lifetime),
                signingCredentials: _signingCredentials);

            return _tokenHandler.WriteToken(token);
        }
    }
}
