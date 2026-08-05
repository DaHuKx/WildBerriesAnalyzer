using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.Server.Models;

namespace WildBerriesAnalyzer.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FiltersController : ControllerBase
    {
        private readonly IFiltersService _filtersService;
        private readonly ILogger<FiltersController> _logger;

        public FiltersController(IFiltersService filtersService, ILogger<FiltersController> logger)
        {
            _filtersService = filtersService;
            _logger = logger;
        }

        /// <summary>
        /// Полные данные фильтра пользователя: параметры, корзина и категории.
        /// </summary>
        [HttpGet("{userId:int}")]
        [ProducesResponseType(typeof(UserFilterData), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserFilterData>> GetUserFilterData(int userId)
        {
            var data = await _filtersService.GetUserFilterDataAsync(userId);
            return Ok(data);
        }

        /// <summary>
        /// Получить или создать фильтр пользователя.
        /// </summary>
        [HttpGet("{userId:int}/filter")]
        [ProducesResponseType(typeof(WbFilter), StatusCodes.Status200OK)]
        public async Task<ActionResult<WbFilter>> GetOrCreate(int userId)
        {
            var filter = await _filtersService.GetOrCreateByUserIdAsync(userId);
            return Ok(filter);
        }

        /// <summary>
        /// Обновить параметры фильтра.
        /// </summary>
        [HttpPut("{userId:int}/filter")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateFilter(int userId, [FromBody] UpdateFilterRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var existing = await _filtersService.GetOrCreateByUserIdAsync(userId);
                existing.DiscontMinPercent = request.DiscontMinPercent;
                existing.MinReviewsCount = request.MinReviewsCount;
                existing.MinRating = request.MinRating;
                existing.ProductsFilterType = request.ProductsFilterType;
                // Пустой список храним как null (= все стратегии).
                existing.ReferencePriceStrartegies = request.ReferencePriceStrartegies is { Count: > 0 }
                    ? request.ReferencePriceStrartegies
                    : null;

                await _filtersService.UpdateFilterAsync(existing);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Товары в корзине фильтра.
        /// </summary>
        [HttpGet("{userId:int}/bag")]
        [ProducesResponseType(typeof(List<WbProduct>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<WbProduct>>> GetBagProducts(int userId)
        {
            var products = await _filtersService.GetBagProductsAsync(userId);
            return Ok(products);
        }

        /// <summary>
        /// Добавить товары в корзину по артикулам / ссылкам.
        /// </summary>
        [HttpPost("{userId:int}/bag")]
        [ProducesResponseType(typeof(AddBagProductsResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AddBagProductsResult>> AddProductsToBag(
            int userId,
            [FromBody] AddBagProductsRequest request)
        {
            try
            {
                var result = await _filtersService.AddProductsToBagAsync(
                    userId,
                    request?.Articles ?? Enumerable.Empty<string>());
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    $"Wildberries недоступен с сервера: {ex.Message}");
            }
        }

        /// <summary>
        /// Добавить товары из общей корзины WB (ссылка ?shareId=…).
        /// </summary>
        [HttpPost("{userId:int}/bag/from-share")]
        [ProducesResponseType(typeof(AddBagProductsResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AddBagProductsResult>> AddProductsToBagFromShare(
            int userId,
            [FromBody] AddBagFromBasketShareRequest request)
        {
            try
            {
                var result = await _filtersService.AddProductsToBagFromBasketShareAsync(
                    userId,
                    request?.ShareUrl ?? string.Empty);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "from-share 400 (bad input) userId={UserId} shareUrl={ShareUrl}",
                    userId, request?.ShareUrl);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "from-share 400 userId={UserId} shareUrl={ShareUrl}: {Message}",
                    userId, request?.ShareUrl, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "from-share 503 (WB auth) userId={UserId}", userId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, ex.Message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "from-share 503 (WB network) userId={UserId}: {Message}",
                    userId, ex.Message);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    $"Wildberries недоступен с сервера: {ex.Message}");
            }
        }

        /// <summary>
        /// Удалить товары из корзины.
        /// </summary>
        [HttpDelete("{userId:int}/bag")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveProductsFromBag(
            int userId,
            [FromBody] RemoveBagProductsRequest request)
        {
            if (request?.ProductIds is null || request.ProductIds.Count == 0)
            {
                return BadRequest("Укажите идентификаторы товаров для удаления.");
            }

            await _filtersService.RemoveProductsFromBagAsync(userId, request.ProductIds);
            return NoContent();
        }

        /// <summary>
        /// Категории фильтра (чёрный / белый список).
        /// </summary>
        [HttpGet("{userId:int}/categories")]
        [ProducesResponseType(typeof(List<WbFilterCategory>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<WbFilterCategory>>> GetFilterCategories(int userId)
        {
            var categories = await _filtersService.GetFilterCategoriesAsync(userId);
            return Ok(categories);
        }

        /// <summary>
        /// Добавить категорию в фильтр.
        /// </summary>
        [HttpPost("{userId:int}/categories")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddFilterCategory(
            int userId,
            [FromBody] AddFilterCategoryRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                await _filtersService.AddFilterCategoryAsync(userId, request.CategoryId, request.Type);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Удалить категорию из фильтра.
        /// </summary>
        [HttpDelete("{userId:int}/categories/{filterCategoryId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemoveFilterCategory(int userId, int filterCategoryId)
        {
            await _filtersService.RemoveFilterCategoryAsync(userId, filterCategoryId);
            return NoContent();
        }
    }
}
