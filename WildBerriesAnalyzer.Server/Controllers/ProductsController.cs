using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.Server.Models;

namespace WildBerriesAnalyzer.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductsService _productsService;

        public ProductsController(IProductsService productsService)
        {
            _productsService = productsService;
        }

        /// <summary>
        /// Поиск на выбранных маркетплейсах по названию (без сохранения).
        /// </summary>
        [HttpGet("wb-search")]
        [ProducesResponseType(typeof(List<WbProduct>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<WbProduct>>> SearchOnWildBerries(
            [FromQuery] string name,
            [FromQuery] List<MarketType>? markets = null)
        {
            try
            {
                var products = await _productsService.SearchOnWildBerriesAsync(name, markets);
                return Ok(products);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Добавить товары в каталог по артикулам / ссылкам.
        /// </summary>
        [HttpPost("by-articles")]
        [ProducesResponseType(typeof(AddCatalogProductsResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AddCatalogProductsResult>> AddByArticles(
            [FromBody] AddProductsByArticlesRequest request)
        {
            try
            {
                var result = await _productsService.AddByArticlesAsync(
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
        }

        /// <summary>
        /// Найти на WildBerries по названию и добавить новые товары в каталог.
        /// </summary>
        [HttpPost("by-name")]
        [ProducesResponseType(typeof(AddCatalogProductsResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AddCatalogProductsResult>> AddByName(
            [FromBody] AddProductsByNameRequest request)
        {
            try
            {
                var result = await _productsService.AddByNameAsync(
                    request?.Name ?? string.Empty,
                    request?.MarketTypes);
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
        }

        /// <summary>
        /// Поиск товаров по названию в базе.
        /// </summary>
        [HttpGet("name")]
        [ProducesResponseType(typeof(List<WbProduct>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<WbProduct>>> GetByName([FromQuery] string name)
        {
            try
            {
                var products = await _productsService.GetByNameAsync(name);
                return Ok(products);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Случайная выборка товаров.
        /// </summary>
        [HttpGet("random")]
        [ProducesResponseType(typeof(List<WbProduct>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<WbProduct>>> GetRandom([FromQuery] int count = 10)
        {
            try
            {
                var products = await _productsService.GetRandomAsync(count);
                return Ok(products);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Общее количество товаров в базе.
        /// </summary>
        [HttpGet("count")]
        [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
        public async Task<ActionResult<long>> GetCount()
        {
            var count = await _productsService.GetCountAsync();
            return Ok(count);
        }

        /// <summary>
        /// Товар по Id (с историей цен).
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(WbProduct), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WbProduct>> GetById(int id)
        {
            try
            {
                var product = await _productsService.GetByIdAsync(id);
                if (product is null)
                {
                    return NotFound($"Товар с Id={id} не найден.");
                }

                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Последняя цена товара.
        /// </summary>
        [HttpGet("{id:int}/last-price")]
        [ProducesResponseType(typeof(WbPrice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WbPrice>> GetLastPrice(int id)
        {
            try
            {
                var price = await _productsService.GetLastPriceAsync(id);
                return Ok(price);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// История цен товара за период (график Mobile).
        /// period: Month | HalfYear | Year | AllTime
        /// </summary>
        [HttpGet("{id:int}/prices")]
        [ProducesResponseType(typeof(ProductPriceHistory), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductPriceHistory>> GetPriceHistory(
            int id,
            [FromQuery] PriceHistoryPeriod period = PriceHistoryPeriod.Month)
        {
            try
            {
                var history = await _productsService.GetPriceHistoryAsync(id, period);
                return Ok(history);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
