using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Server.Options;
using WildBerriesAnalyzer.Server.Services;

namespace WildBerriesAnalyzer.Server.Controllers
{
    [ApiController]
    [Route("api/mobile")]
    public class MobileVersionController : ControllerBase
    {
        private readonly MobileVersionOptions _options;
        private readonly IUsersRepository _usersRepository;

        public MobileVersionController(
            IOptions<MobileVersionOptions> options,
            IUsersRepository usersRepository)
        {
            _options = options.Value;
            _usersRepository = usersRepository;
        }

        /// <summary>
        /// Актуальная версия Mobile из конфигурации (.env) и, при авторизации, версия клиента пользователя.
        /// </summary>
        [HttpGet("version")]
        [ProducesResponseType(typeof(MobileVersionInfo), StatusCodes.Status200OK)]
        public async Task<ActionResult<MobileVersionInfo>> GetVersion()
        {
            string? clientVersion = null;
            DateTime? clientReportedAt = null;

            if (User.Identity?.IsAuthenticated == true &&
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
                userId > 0)
            {
                var user = await _usersRepository.GetAsync(userId);
                if (user is not null)
                {
                    clientVersion = user.MobileClientVersion;
                    clientReportedAt = user.MobileClientVersionReportedAt;
                }
            }

            var latest = ClientSemVersion.Normalize(_options.LatestVersion) ?? _options.LatestVersion?.Trim() ?? "1.0.0";
            return Ok(new MobileVersionInfo
            {
                LatestVersion = latest,
                ClientVersion = clientVersion,
                ClientVersionReportedAtUtc = clientReportedAt,
                IsOutdated = ClientSemVersion.IsOlderThan(clientVersion, latest)
            });
        }
    }

    public sealed class MobileVersionInfo
    {
        /// <summary>Актуальная версия (major.minor.patch).</summary>
        public string LatestVersion { get; init; } = "1.0.0";

        /// <summary>Версия клиента пользователя, если известна.</summary>
        public string? ClientVersion { get; init; }

        public DateTime? ClientVersionReportedAtUtc { get; init; }

        public bool IsOutdated { get; init; }
    }
}
