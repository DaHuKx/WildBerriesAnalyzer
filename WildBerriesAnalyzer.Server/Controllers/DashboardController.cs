using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Server.Options;

namespace WildBerriesAnalyzer.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IActualDiscontsService _actualDiscontsService;
        private readonly IOptionsMonitor<PriceUpdateOptions> _priceUpdateOptions;

        public DashboardController(
            IActualDiscontsService actualDiscontsService,
            IOptionsMonitor<PriceUpdateOptions> priceUpdateOptions)
        {
            _actualDiscontsService = actualDiscontsService;
            _priceUpdateOptions = priceUpdateOptions;
        }

        /// <summary>
        /// Сводка для главного экрана мобильного клиента.
        /// </summary>
        [HttpGet("home")]
        [ProducesResponseType(typeof(HomeDashboardSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<HomeDashboardSummary>> GetHome(CancellationToken cancellationToken = default)
        {
            var userId = GetUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var options = _priceUpdateOptions.CurrentValue;
            var summary = await _actualDiscontsService.GetHomeDashboardAsync(
                userId.Value,
                options.Enabled,
                options.Interval,
                cancellationToken);

            return Ok(summary);
        }

        private int? GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
