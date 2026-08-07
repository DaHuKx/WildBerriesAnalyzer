using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Server.Services;

namespace WildBerriesAnalyzer.Server.Middleware
{
    /// <summary>
    /// Проверяет Bearer access-токен и устанавливает пользователя в HttpContext.
    /// </summary>
    public class AuthMiddleware
    {
        private static readonly PathString[] AnonymousPaths =
        [
            new("/api/auth/login"),
            new("/api/auth/register"),
            new("/api/auth/refresh"),
            new("/api/auth/vk"),
            new("/swagger"),
            new("/favicon.ico")
        ];

        private readonly RequestDelegate _next;

        public AuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ITokenIssuer tokenIssuer,
            IUsersRepository usersRepository,
            IClientVersionTracker clientVersionTracker)
        {
            if (IsAnonymousPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var accessToken = ExtractBearerToken(context.Request);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                await _next(context);
                return;
            }

            var tokenInfo = tokenIssuer.ValidateAccessToken(accessToken);
            if (tokenInfo is null)
            {
                await WriteUnauthorizedAsync(context, "Недействительный или просроченный access-токен.");
                return;
            }

            var user = await usersRepository.GetUserByAccessTokenAsync(accessToken);
            if (user is null || user.Id != tokenInfo.UserId)
            {
                await WriteUnauthorizedAsync(context, "Недействительный или просроченный access-токен.");
                return;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Login ?? tokenInfo.Login),
                new(TokenIssuer.TokenTypeClaim, TokenIssuer.AccessTokenType)
            };

            var identity = new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
            context.User = new ClaimsPrincipal(identity);

            try
            {
                await clientVersionTracker.TrackFromRequestAsync(user.Id, context.Request, context.RequestAborted);
            }
            catch
            {
                // Не блокируем API из‑за учёта версии.
            }

            await _next(context);
        }

        private static bool IsAnonymousPath(PathString path)
        {
            foreach (var anonymousPath in AnonymousPaths)
            {
                if (path.StartsWithSegments(anonymousPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? ExtractBearerToken(HttpRequest request)
        {
            if (!request.Headers.TryGetValue("Authorization", out var headerValues))
            {
                return null;
            }

            var header = headerValues.ToString();
            if (string.IsNullOrWhiteSpace(header))
            {
                return null;
            }

            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = header[prefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(new { message });
            await context.Response.WriteAsync(payload);
        }
    }
}
