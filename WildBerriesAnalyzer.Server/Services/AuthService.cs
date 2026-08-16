using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Business.Validators;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;
using WildBerriesAnalyzer.Server.Options;
using WildBerriesAnalyzer.Server.Services.Auth;
using WildBerriesAnalyzer.Server.Services.VkBot;
using WildBerriesAnalyzer.Server.Services.VkId;

namespace WildBerriesAnalyzer.Server.Services
{
    public class AuthService : IAuthService
    {
        private static readonly TimeSpan RegistrationTtl = TimeSpan.FromMinutes(15);
        private const int MaxFailedAttempts = 5;

        private readonly IUsersRepository _usersRepository;
        private readonly IModersRepository _modersRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenIssuer _tokenIssuer;
        private readonly IVkIdOAuthClient _vkIdOAuthClient;
        private readonly IVkCommunityMessenger _vkCommunityMessenger;
        private readonly IPendingRegistrationStore _pendingRegistrations;
        private readonly VkIdOptions _vkIdOptions;
        private readonly RegisterCredentialsValidator _registerValidator;
        private readonly LoginCredentialsValidator _loginValidator;
        private readonly RefreshCredentialsValidator _refreshValidator;

        public AuthService(
            IUsersRepository usersRepository,
            IModersRepository modersRepository,
            IPasswordHasher passwordHasher,
            ITokenIssuer tokenIssuer,
            IVkIdOAuthClient vkIdOAuthClient,
            IVkCommunityMessenger vkCommunityMessenger,
            IPendingRegistrationStore pendingRegistrations,
            IOptions<VkIdOptions> vkIdOptions,
            RegisterCredentialsValidator registerValidator,
            LoginCredentialsValidator loginValidator,
            RefreshCredentialsValidator refreshValidator)
        {
            _usersRepository = usersRepository;
            _modersRepository = modersRepository;
            _passwordHasher = passwordHasher;
            _tokenIssuer = tokenIssuer;
            _vkIdOAuthClient = vkIdOAuthClient;
            _vkCommunityMessenger = vkCommunityMessenger;
            _pendingRegistrations = pendingRegistrations;
            _vkIdOptions = vkIdOptions.Value;
            _registerValidator = registerValidator;
            _loginValidator = loginValidator;
            _refreshValidator = refreshValidator;
        }

        public async Task<RegisterResult> RegisterAsync(string login, string password, string vkProfileUrl)
        {
            var credentials = new RegisterCredentials
            {
                Login = login?.Trim() ?? string.Empty,
                Password = password ?? string.Empty,
                VkProfileUrl = vkProfileUrl?.Trim() ?? string.Empty
            };

            var validationResult = await _registerValidator.ValidateAsync(credentials);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException(string.Join(
                    Environment.NewLine,
                    validationResult.Errors.Select(e => e.ErrorMessage)));
            }

            if (_pendingRegistrations.HasActiveLogin(credentials.Login))
            {
                throw new ArgumentException(
                    "Регистрация для этого логина уже начата. Введите код из VK или дождитесь истечения сессии.");
            }

            var vkId = await _vkCommunityMessenger.ResolveUserIdAsync(credentials.VkProfileUrl);

            if (_pendingRegistrations.HasActiveVkId(vkId))
            {
                throw new ArgumentException(
                    "Для этого VK уже есть незавершённая регистрация. Введите код из сообщения или запросите новый.");
            }

            var existingVkUser = await _usersRepository.GetUserByVkIdAsync(vkId);
            if (existingVkUser is not null && !string.IsNullOrWhiteSpace(existingVkUser.Login)
                && !string.IsNullOrWhiteSpace(existingVkUser.Password))
            {
                throw new ArgumentException("Этот VK уже зарегистрирован в PriceLab.");
            }

            var code = GenerateVerificationCode();
            var pending = new PendingRegistration
            {
                RegistrationId = Guid.NewGuid().ToString("N"),
                Login = credentials.Login,
                PasswordHash = _passwordHasher.HashPassword(credentials.Password),
                VkId = vkId,
                Code = code,
                ExpiresAtUtc = DateTime.UtcNow.Add(RegistrationTtl),
                CreatedAtUtc = DateTime.UtcNow
            };

            _pendingRegistrations.Save(pending);

            var sent = await SendVerificationCodeAsync(pending);
            return BuildRegisterResult(pending, sent);
        }

        public async Task<AuthTokensResult> ConfirmRegisterAsync(string registrationId, string code)
        {
            var pending = _pendingRegistrations.Get(registrationId);
            if (pending is null)
            {
                throw new ArgumentException(
                    "Сессия регистрации не найдена или истекла. Начните регистрацию заново.");
            }

            var normalizedCode = (code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                throw new ArgumentException("Введите код из сообщения VK.");
            }

            if (!string.Equals(pending.Code, normalizedCode, StringComparison.Ordinal))
            {
                pending.FailedAttempts++;
                if (pending.FailedAttempts >= MaxFailedAttempts)
                {
                    _pendingRegistrations.TryRemove(pending.RegistrationId, out _);
                    throw new UnauthorizedAccessException(
                        "Слишком много неверных попыток. Начните регистрацию заново.");
                }

                _pendingRegistrations.Update(pending);
                throw new UnauthorizedAccessException("Неверный код подтверждения.");
            }

            // Повторная проверка на гонки.
            var existingLogin = await _usersRepository.GetUserByLoginAsync(pending.Login);
            if (existingLogin is not null)
            {
                _pendingRegistrations.TryRemove(pending.RegistrationId, out _);
                throw new ArgumentException("Пользователь с таким логином уже существует.");
            }

            var existingVkUser = await _usersRepository.GetUserByVkIdAsync(pending.VkId);
            if (existingVkUser is not null && !string.IsNullOrWhiteSpace(existingVkUser.Login)
                && !string.IsNullOrWhiteSpace(existingVkUser.Password))
            {
                _pendingRegistrations.TryRemove(pending.RegistrationId, out _);
                throw new ArgumentException("Этот VK уже зарегистрирован в PriceLab.");
            }

            WbUser user;
            if (existingVkUser is not null)
            {
                existingVkUser.Login = pending.Login;
                existingVkUser.Password = pending.PasswordHash;
                existingVkUser.UpdatedAt = DateTime.UtcNow;
                await _usersRepository.UpdateAsync(existingVkUser);
                user = existingVkUser;
            }
            else
            {
                user = await _usersRepository.AddAsync(new WbUser
                {
                    Login = pending.Login,
                    Password = pending.PasswordHash,
                    VkId = pending.VkId,
                    BotPlace = BotUserPlace.Start
                });
            }

            _pendingRegistrations.TryRemove(pending.RegistrationId, out _);
            return await IssueTokensAsync(user);
        }

        public async Task<RegisterResult> ResendRegisterCodeAsync(string registrationId)
        {
            var pending = _pendingRegistrations.Get(registrationId);
            if (pending is null)
            {
                throw new ArgumentException(
                    "Сессия регистрации не найдена или истекла. Начните регистрацию заново.");
            }

            pending.Code = GenerateVerificationCode();
            pending.ExpiresAtUtc = DateTime.UtcNow.Add(RegistrationTtl);
            pending.FailedAttempts = 0;
            _pendingRegistrations.Update(pending);

            var sent = await SendVerificationCodeAsync(pending);
            return BuildRegisterResult(pending, sent);
        }

        private async Task<bool> SendVerificationCodeAsync(PendingRegistration pending)
        {
            var text =
                $"PriceLab: код подтверждения регистрации — {pending.Code}\n" +
                $"Логин: {pending.Login}\n" +
                "Введите код в приложении. Код действует 15 минут.\n" +
                "Если это были не вы — проигнорируйте сообщение.";

            return await _vkCommunityMessenger.TrySendMessageAsync(pending.VkId, text);
        }

        private RegisterResult BuildRegisterResult(PendingRegistration pending, bool sent)
        {
            return new RegisterResult
            {
                RegistrationId = pending.RegistrationId,
                Login = pending.Login,
                VkId = pending.VkId,
                RequiresVkVerification = true,
                VerificationMessageSent = sent,
                BotChatUrl = _vkCommunityMessenger.BotChatUrl,
                Message = sent
                    ? "Код подтверждения отправлен в VK. Введите его в приложении."
                    : "Не удалось отправить код в VK. Напишите боту в сообществе, затем нажмите «Отправить код снова»."
            };
        }

        private static string GenerateVerificationCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        public async Task<AuthTokensResult> LoginAsync(string login, string password)
        {
            var credentials = new LoginCredentials
            {
                Login = login?.Trim() ?? string.Empty,
                Password = password ?? string.Empty
            };

            var validationResult = await _loginValidator.ValidateAsync(credentials);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException(string.Join(
                    Environment.NewLine,
                    validationResult.Errors.Select(e => e.ErrorMessage)));
            }

            var user = await _usersRepository.GetUserByLoginAsync(credentials.Login);
            if (user is null ||
                string.IsNullOrWhiteSpace(user.Password) ||
                !_passwordHasher.VerifyPassword(credentials.Password, user.Password))
            {
                throw new UnauthorizedAccessException("Неверный логин или пароль.");
            }

            return await IssueTokensAsync(user);
        }

        public async Task<AuthTokensResult> RefreshAsync(string refreshToken)
        {
            var credentials = new RefreshCredentials
            {
                RefreshToken = refreshToken?.Trim() ?? string.Empty
            };

            var validationResult = await _refreshValidator.ValidateAsync(credentials);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException(string.Join(
                    Environment.NewLine,
                    validationResult.Errors.Select(e => e.ErrorMessage)));
            }

            var tokenInfo = _tokenIssuer.ValidateRefreshToken(credentials.RefreshToken);
            if (tokenInfo is null)
            {
                throw new UnauthorizedAccessException("Недействительный или просроченный refresh-токен.");
            }

            var user = await _usersRepository.GetUserByRefreshTokenAsync(credentials.RefreshToken);
            if (user is null || user.Id != tokenInfo.UserId)
            {
                throw new UnauthorizedAccessException("Недействительный или просроченный refresh-токен.");
            }

            return await IssueTokensAsync(user);
        }

        public async Task<AuthTokensResult> LoginWithVkAsync(VkLoginRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!_vkIdOptions.Enabled)
            {
                throw new InvalidOperationException("Вход через VK ID отключён на сервере.");
            }

            if (string.IsNullOrWhiteSpace(_vkIdOptions.ClientId))
            {
                throw new InvalidOperationException("VkId:ClientId не настроен.");
            }

            var code = request.Code?.Trim() ?? string.Empty;
            var codeVerifier = request.CodeVerifier?.Trim() ?? string.Empty;
            var deviceId = request.DeviceId?.Trim() ?? string.Empty;
            var state = request.State?.Trim() ?? string.Empty;
            var redirectUri = string.IsNullOrWhiteSpace(request.RedirectUri)
                ? ResolveDefaultRedirectUri()
                : request.RedirectUri.Trim();

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(codeVerifier) ||
                string.IsNullOrWhiteSpace(deviceId) ||
                string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("Не хватает параметров VK ID (code, code_verifier, device_id, state).");
            }

            if (!IsAllowedRedirectUri(redirectUri))
            {
                throw new ArgumentException("redirect_uri не совпадает с настройками сервера.");
            }

            var token = await _vkIdOAuthClient.ExchangeCodeAsync(
                code,
                codeVerifier,
                deviceId,
                state,
                redirectUri);

            if (!string.IsNullOrWhiteSpace(token.State) &&
                !string.Equals(token.State, state, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Несовпадение state — ответ VK ID отклонён.");
            }

            string vkId = token.UserId;
            if (string.IsNullOrWhiteSpace(vkId))
            {
                var info = await _vkIdOAuthClient.GetUserInfoAsync(token.AccessToken);
                vkId = info.UserId;
            }

            if (string.IsNullOrWhiteSpace(vkId))
            {
                throw new UnauthorizedAccessException("Не удалось получить идентификатор пользователя VK.");
            }

            var user = await _usersRepository.GetUserByVkIdAsync(vkId);
            var isModerClient = string.Equals(request.Client?.Trim(), "moder", StringComparison.OrdinalIgnoreCase);

            if (isModerClient)
            {
                if (user is null)
                {
                    throw new UnauthorizedAccessException(
                        "Нет доступа к модерации. Аккаунт не найден — добавьте UserId в таблицу Moders.");
                }

                if (!await _modersRepository.IsModerAsync(user.Id))
                {
                    throw new UnauthorizedAccessException(
                        "Нет доступа к модерации. Ваш аккаунт не в списке Moders.");
                }

                if (string.IsNullOrWhiteSpace(user.Login))
                {
                    user.Login = BuildVkLogin(vkId);
                    user.UpdatedAt = DateTime.UtcNow;
                    await _usersRepository.UpdateAsync(user);
                }

                return await IssueTokensAsync(user);
            }

            if (user is null)
            {
                user = new WbUser
                {
                    VkId = vkId,
                    Login = BuildVkLogin(vkId),
                    BotPlace = BotUserPlace.Start
                };
                user = await _usersRepository.AddAsync(user);
            }
            else if (string.IsNullOrWhiteSpace(user.Login))
            {
                user.Login = BuildVkLogin(vkId);
                user.UpdatedAt = DateTime.UtcNow;
                await _usersRepository.UpdateAsync(user);
            }

            return await IssueTokensAsync(user);
        }

        private async Task<AuthTokensResult> IssueTokensAsync(WbUser user)
        {
            var login = user.Login ?? string.Empty;
            var accessToken = _tokenIssuer.CreateAccessToken(user.Id, login);
            var refreshToken = _tokenIssuer.CreateRefreshToken(user.Id, login);

            user.AccessToken = accessToken;
            user.RefreshToken = refreshToken;
            await _usersRepository.UpdateAsync(user);

            return new AuthTokensResult
            {
                UserId = user.Id,
                Login = login,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        private static string BuildVkLogin(string vkId) => $"vk_{vkId}";

        private string ResolveDefaultRedirectUri()
        {
            if (!string.IsNullOrWhiteSpace(_vkIdOptions.RedirectUri))
            {
                return _vkIdOptions.RedirectUri.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_vkIdOptions.ClientId))
            {
                return $"vk{_vkIdOptions.ClientId.Trim()}://vk.ru/blank.html";
            }

            return _vkIdOptions.AppCallbackUri?.Trim() ?? "wbanalyzer://vk-auth";
        }

        private bool IsAllowedRedirectUri(string redirectUri)
        {
            var allowed = new List<string>();

            if (!string.IsNullOrWhiteSpace(_vkIdOptions.RedirectUri))
            {
                allowed.Add(_vkIdOptions.RedirectUri.Trim());
            }

            if (!string.IsNullOrWhiteSpace(_vkIdOptions.AppCallbackUri))
            {
                allowed.Add(_vkIdOptions.AppCallbackUri.Trim());
            }

            if (!string.IsNullOrWhiteSpace(_vkIdOptions.ClientId))
            {
                allowed.Add($"vk{_vkIdOptions.ClientId.Trim()}://vk.ru/blank.html");
            }

            return allowed.Any(u => string.Equals(u, redirectUri, StringComparison.Ordinal));
        }
    }
}
