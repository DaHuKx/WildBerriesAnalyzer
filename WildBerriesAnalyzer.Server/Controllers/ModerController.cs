using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Data.Services;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/moder")]
    public class ModerController : ControllerBase
    {
        private readonly CategoryModerationService _moderation;
        private readonly IModersRepository _modersRepository;

        public ModerController(CategoryModerationService moderation, IModersRepository modersRepository)
        {
            _moderation = moderation;
            _modersRepository = modersRepository;
        }

        [HttpGet("queue/count")]
        [ProducesResponseType(typeof(ModerQueueCountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ModerQueueCountDto>> GetQueueCount()
        {
            var denied = await EnsureModerAsync();
            if (denied is not null)
            {
                return denied;
            }

            var count = await _moderation.CountUncategorizedAsync();
            return Ok(new ModerQueueCountDto { Count = count });
        }

        [HttpGet("queue/next")]
        [ProducesResponseType(typeof(ModerProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ModerProductDto>> GetNext()
        {
            var denied = await EnsureModerAsync();
            if (denied is not null)
            {
                return denied;
            }

            var product = await _moderation.GetNextUncategorizedAsync();
            if (product is null)
            {
                return NoContent();
            }

            return Ok(ToDto(product));
        }

        [HttpGet("categories")]
        [ProducesResponseType(typeof(List<ModerCategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<ModerCategoryDto>>> GetCategories()
        {
            var denied = await EnsureModerAsync();
            if (denied is not null)
            {
                return denied;
            }

            var categories = await _moderation.GetCategoriesAsync();
            return Ok(categories
                .Select(c => new ModerCategoryDto { Id = c.Id, Name = c.Name })
                .ToList());
        }

        [HttpGet("queue/uncategorized")]
        [ProducesResponseType(typeof(List<ModerProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<ModerProductDto>>> GetUncategorized()
        {
            var denied = await EnsureModerAsync();
            if (denied is not null)
            {
                return denied;
            }

            var products = await _moderation.GetUncategorizedProductsAsync();
            return Ok(products.Select(ToDto).ToList());
        }

        [HttpPost("assign")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Assign([FromBody] ModerAssignRequest request)
        {
            var denied = await EnsureModerAsync();
            if (denied is not null)
            {
                return denied;
            }

            if (request is null || request.ProductId <= 0)
            {
                return BadRequest("Укажите ProductId.");
            }

            try
            {
                await _moderation.AssignAsync(
                    request.ProductId,
                    request.CategoryIds ?? new List<int>(),
                    request.NewCategoryNames);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("assign/bulk")]
        [ProducesResponseType(typeof(ModerBulkAssignResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ModerBulkAssignResultDto>> AssignBulk([FromBody] ModerBulkAssignRequest request)
        {
            var denied = await EnsureModerAsync();
            if (denied is not null)
            {
                return denied;
            }

            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var count = await _moderation.AssignManyAsync(
                    request.ProductIds ?? new List<int>(),
                    request.CategoryIds ?? new List<int>(),
                    request.NewCategoryNames);
                return Ok(new ModerBulkAssignResultDto { AssignedCount = count });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<ActionResult?> EnsureModerAsync()
        {
            var userId = GetUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            if (!await _modersRepository.IsModerAsync(userId.Value))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Нет доступа к модерации.");
            }

            return null;
        }

        private int? GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) && id > 0 ? id : null;
        }

        private static ModerProductDto ToDto(Domain.Models.DataBase.WbProduct product) =>
            new()
            {
                Id = product.Id,
                Name = product.Name ?? string.Empty,
                Brand = product.Brand,
                IdInMarket = product.IdInMarket,
                MarketType = product.MarketType == MarketType.Ozon ? "Ozon" : "WB",
                ImageUrl = product.ImageUrl,
                Link = product.Link
            };
    }
}
