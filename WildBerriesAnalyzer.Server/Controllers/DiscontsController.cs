using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Models;

namespace WildBerriesAnalyzer.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DiscontsController : ControllerBase
    {
        private readonly IActualDiscontsService _actualDiscontsService;

        public DiscontsController(IActualDiscontsService actualDiscontsService)
        {
            _actualDiscontsService = actualDiscontsService;
        }

        /// <summary>
        /// Актуальные скидки текущего пользователя по его фильтрам.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<Discont>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<Discont>>> GetForCurrentUser(
            [FromQuery] int? limit = 50,
            CancellationToken cancellationToken = default)
        {
            var userId = GetUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var disconts = await _actualDiscontsService.GetForUserAsync(
                userId.Value,
                limit,
                cancellationToken);

            return Ok(disconts);
        }

        /// <summary>
        /// Все актуальные скидки (без пользовательского фильтра).
        /// </summary>
        [HttpGet("all")]
        [ProducesResponseType(typeof(List<Discont>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<Discont>>> GetAll(
            [FromQuery] int? limit = 100,
            CancellationToken cancellationToken = default)
        {
            if (GetUserId() is null)
            {
                return Unauthorized();
            }

            var disconts = await _actualDiscontsService.GetAllAsync(limit, cancellationToken);
            return Ok(disconts);
        }

        /// <summary>
        /// Актуальные скидки по userId (для клиентов, которые передают id явно).
        /// </summary>
        [HttpGet("{userId:int}")]
        [ProducesResponseType(typeof(List<Discont>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<Discont>>> GetForUser(
            int userId,
            [FromQuery] int? limit = 50,
            CancellationToken cancellationToken = default)
        {
            var currentUserId = GetUserId();
            if (currentUserId is null)
            {
                return Unauthorized();
            }

            if (currentUserId.Value != userId)
            {
                return Forbid();
            }

            var disconts = await _actualDiscontsService.GetForUserAsync(userId, limit, cancellationToken);
            return Ok(disconts);
        }

        private int? GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
