using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Business.Services.Interfaces;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services
{
    public sealed class AccountService : IAccountService
    {
        private static readonly TimeSpan LinkCodeTtl = TimeSpan.FromMinutes(10);
        private static readonly Regex LinkCommandRegex = new(
            @"^\s*привязать\s+([A-Z0-9]{6,8})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private readonly IUsersRepository _usersRepository;
        private readonly IVkLinkCodesRepository _vkLinkCodesRepository;
        private readonly IFiltersRepository _filtersRepository;

        public AccountService(
            IUsersRepository usersRepository,
            IVkLinkCodesRepository vkLinkCodesRepository,
            IFiltersRepository filtersRepository)
        {
            _usersRepository = usersRepository;
            _vkLinkCodesRepository = vkLinkCodesRepository;
            _filtersRepository = filtersRepository;
        }

        public static bool TryParseLinkCommand(string? text, out string code)
        {
            code = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var match = LinkCommandRegex.Match(text);
            if (!match.Success)
            {
                return false;
            }

            code = match.Groups[1].Value.ToUpperInvariant();
            return true;
        }

        public async Task<AccountProfile> GetProfileAsync(int userId)
        {
            var user = await GetRequiredUserAsync(userId);
            return ToProfile(user);
        }

        public async Task<VkLinkCodeResult> CreateVkLinkCodeAsync(int userId)
        {
            var user = await GetRequiredUserAsync(userId);
            if (!string.IsNullOrWhiteSpace(user.VkId))
            {
                throw new InvalidOperationException("VK уже привязан к этому аккаунту.");
            }

            await _vkLinkCodesRepository.InvalidateUnusedForUserAsync(userId);

            var code = GenerateCode(8);
            var expiresAt = DateTime.UtcNow.Add(LinkCodeTtl);
            await _vkLinkCodesRepository.AddAsync(new VkLinkCode
            {
                Code = code,
                UserId = userId,
                ExpiresAt = expiresAt
            });

            return new VkLinkCodeResult
            {
                Code = code,
                ExpiresAt = expiresAt,
                Instruction =
                    $"Напишите сообществу VK бота сообщение:\nПРИВЯЗАТЬ {code}\n\nКод действует 10 минут."
            };
        }

        public async Task<VkLinkConfirmResult> ConfirmVkLinkAsync(string vkId, string rawMessage)
        {
            if (string.IsNullOrWhiteSpace(vkId))
            {
                return Fail("Не удалось определить VK id.");
            }

            if (!TryParseLinkCommand(rawMessage, out var code))
            {
                return Fail("Неверный формат. Отправьте: ПРИВЯЗАТЬ <код>");
            }

            var linkCode = await _vkLinkCodesRepository.GetActiveByCodeAsync(code);
            if (linkCode is null)
            {
                return Fail("Код не найден или истёк. Сгенерируйте новый в приложении.");
            }

            var target = linkCode.User ?? await _usersRepository.GetAsync(linkCode.UserId);
            if (target is null)
            {
                return Fail("Пользователь для привязки не найден.");
            }

            if (!string.IsNullOrWhiteSpace(target.VkId))
            {
                if (string.Equals(target.VkId, vkId, StringComparison.Ordinal))
                {
                    await _vkLinkCodesRepository.MarkUsedAsync(linkCode);
                    return Ok("Этот VK уже привязан к вашему аккаунту.");
                }

                return Fail("К аккаунту уже привязан другой VK.");
            }

            var vkUser = await _usersRepository.GetUserByVkIdAsync(vkId);
            if (vkUser is not null)
            {
                if (vkUser.Id == target.Id)
                {
                    await _vkLinkCodesRepository.MarkUsedAsync(linkCode);
                    return Ok("VK уже привязан.");
                }

                if (!string.IsNullOrWhiteSpace(vkUser.Login))
                {
                    return Fail(
                        "Этот VK уже привязан к другому аккаунту. Обратитесь в поддержку.");
                }

                await MergeOrphanIntoTargetAsync(vkUser, target);
            }

            target.VkId = vkId;
            target.UpdatedAt = DateTime.UtcNow;
            await _usersRepository.UpdateAsync(target);
            await _vkLinkCodesRepository.MarkUsedAsync(linkCode);

            return Ok("VK успешно привязан к аккаунту. Уведомления о скидках будут приходить сюда.");
        }

        private async Task MergeOrphanIntoTargetAsync(WbUser orphan, WbUser target)
        {
            // Снимаем VkId с orphan до назначения target — уникальный индекс.
            orphan.VkId = null;
            orphan.UpdatedAt = DateTime.UtcNow;
            await _usersRepository.UpdateAsync(orphan);

            var reassigned = await _filtersRepository.TryReassignFilterAsync(orphan.Id, target.Id);
            if (!reassigned)
            {
                await _filtersRepository.MergeBagProductsAsync(orphan.Id, target.Id);
                await _filtersRepository.DeleteFilterCascadeAsync(orphan.Id);
            }

            await _usersRepository.RemoveAsync(orphan.Id);
        }

        private async Task<WbUser> GetRequiredUserAsync(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("Некорректный идентификатор пользователя.", nameof(userId));
            }

            var user = await _usersRepository.GetAsync(userId);
            if (user is null)
            {
                throw new KeyNotFoundException($"Пользователь Id={userId} не найден.");
            }

            return user;
        }

        private static AccountProfile ToProfile(WbUser user)
        {
            return new AccountProfile
            {
                UserId = user.Id,
                Login = user.Login,
                VkId = user.VkId,
                IsVkLinked = !string.IsNullOrWhiteSpace(user.VkId)
            };
        }

        private static string GenerateCode(int length)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                sb.Append(CodeAlphabet[bytes[i] % CodeAlphabet.Length]);
            }

            return sb.ToString();
        }

        private static VkLinkConfirmResult Ok(string message) =>
            new() { Success = true, Message = message };

        private static VkLinkConfirmResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
