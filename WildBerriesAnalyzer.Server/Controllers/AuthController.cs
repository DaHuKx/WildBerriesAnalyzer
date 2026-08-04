using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Server.Models;
using WildBerriesAnalyzer.Server.Options;

namespace WildBerriesAnalyzer.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly VkIdOptions _vkIdOptions;

        public AuthController(IAuthService authService, IOptions<VkIdOptions> vkIdOptions)
        {
            _authService = authService;
            _vkIdOptions = vkIdOptions.Value;
        }

        /// <summary>
        /// Публичные параметры VK ID для Mobile (без секретов).
        /// </summary>
        [HttpGet("vk/config")]
        [ProducesResponseType(typeof(VkAuthPublicConfig), StatusCodes.Status200OK)]
        public ActionResult<VkAuthPublicConfig> GetVkConfig()
        {
            var redirectUri = ResolveRedirectUri();
            var appCallback = string.IsNullOrWhiteSpace(_vkIdOptions.AppCallbackUri)
                ? redirectUri
                : _vkIdOptions.AppCallbackUri.Trim();

            return Ok(new VkAuthPublicConfig
            {
                Enabled = _vkIdOptions.Enabled && !string.IsNullOrWhiteSpace(_vkIdOptions.ClientId),
                ClientId = _vkIdOptions.ClientId,
                RedirectUri = redirectUri,
                AppCallbackUri = appCallback,
                AuthorizeUrl = _vkIdOptions.AuthorizeUrl,
                Scope = _vkIdOptions.Scope
            });
        }

        /// <summary>
        /// Для Android-приложения VK ID канонический redirect:
        /// vk{clientId}://vk.ru/blank.html — отдельного «доверенного URL» в кабинете нет.
        /// </summary>
        private string ResolveRedirectUri()
        {
            var configured = _vkIdOptions.RedirectUri?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            if (!string.IsNullOrWhiteSpace(_vkIdOptions.ClientId))
            {
                return $"vk{_vkIdOptions.ClientId.Trim()}://vk.ru/blank.html";
            }

            return "wbanalyzer://vk-auth";
        }

        /// <summary>
        /// Callback для HTTP(S) redirect_uri: отдаёт HTML, который открывает схему приложения.
        /// 302 на custom scheme в Chrome Custom Tabs часто не возвращает управление в Mobile
        /// (WebAuthenticator получает TaskCanceledException — «авторизация отменена»).
        /// </summary>
        [HttpGet("vk/callback")]
        [Produces("text/html")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult VkCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery(Name = "device_id")] string? deviceId,
            [FromQuery] string? error,
            [FromQuery(Name = "error_description")] string? errorDescription)
        {
            var appCallback = string.IsNullOrWhiteSpace(_vkIdOptions.AppCallbackUri)
                ? "wbanalyzer://vk-auth"
                : _vkIdOptions.AppCallbackUri.TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(error))
            {
                var errQs = $"error={Uri.EscapeDataString(error)}" +
                            $"&error_description={Uri.EscapeDataString(errorDescription ?? string.Empty)}";
                return VkAppBridgeHtml($"{appCallback}?{errQs}");
            }

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(state) ||
                string.IsNullOrWhiteSpace(deviceId))
            {
                return BadRequest("VK ID callback: отсутствуют code/state/device_id.");
            }

            var qs =
                $"code={Uri.EscapeDataString(code)}" +
                $"&state={Uri.EscapeDataString(state)}" +
                $"&device_id={Uri.EscapeDataString(deviceId)}";

            return VkAppBridgeHtml($"{appCallback}?{qs}");
        }

        private static ContentResult VkAppBridgeHtml(string deepLink)
        {
            var encoded = System.Net.WebUtility.HtmlEncode(deepLink);
            var jsLiteral = System.Text.Json.JsonSerializer.Serialize(deepLink);
            var html =
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                $"<meta http-equiv=\"refresh\" content=\"0;url={encoded}\">" +
                "<title>PriceLab</title>" +
                "<script>try{window.location.replace(" + jsLiteral + ");}catch(e){}" +
                "setTimeout(function(){window.location.href=" + jsLiteral + ";},50);</script>" +
                "</head><body style=\"font-family:sans-serif;text-align:center;padding:2rem\">" +
                "<p>Возврат в PriceLab…</p>" +
                $"<p><a href=\"{encoded}\">Нажмите здесь, если приложение не открылось</a></p>" +
                "</body></html>";

            return new ContentResult
            {
                Content = html,
                ContentType = "text/html; charset=utf-8",
                StatusCode = StatusCodes.Status200OK
            };
        }

        /// <summary>
        /// Обмен authorization code VK ID на JWT приложения.
        /// </summary>
        [HttpPost("vk")]
        [ProducesResponseType(typeof(AuthTokensResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthTokensResult>> LoginWithVk([FromBody] VkLoginRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var result = await _authService.LoginWithVkAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    $"VK ID недоступен с сервера (DNS/сеть): {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Вход по логину и паролю.
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthTokensResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthTokensResult>> Login([FromBody] LoginRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var result = await _authService.LoginAsync(request.Login, request.Password);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        /// <summary>
        /// Начало регистрации: отправка кода подтверждения в VK.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RegisterResult>> Register([FromBody] RegisterRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var result = await _authService.RegisterAsync(
                    request.Login,
                    request.Password,
                    request.VkProfileUrl);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Подтверждение регистрации кодом из VK.
        /// </summary>
        [HttpPost("register/confirm")]
        [ProducesResponseType(typeof(AuthTokensResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthTokensResult>> ConfirmRegister([FromBody] ConfirmRegisterRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var result = await _authService.ConfirmRegisterAsync(request.RegistrationId, request.Code);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        /// <summary>
        /// Повторная отправка кода подтверждения в VK.
        /// </summary>
        [HttpPost("register/resend")]
        [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RegisterResult>> ResendRegisterCode([FromBody] ResendRegisterCodeRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var result = await _authService.ResendRegisterCodeAsync(request.RegistrationId);
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
        /// Обновление access/refresh токенов.
        /// </summary>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthTokensResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthTokensResult>> Refresh([FromBody] RefreshTokenRequest request)
        {
            if (request is null)
            {
                return BadRequest("Тело запроса не может быть пустым.");
            }

            try
            {
                var result = await _authService.RefreshAsync(request.RefreshToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
