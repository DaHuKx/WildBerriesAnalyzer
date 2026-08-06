using FluentValidation;
using System.Text.RegularExpressions;
using WildBerriesAnalyzer.Business.Helpers;

namespace WildBerriesAnalyzer.Business.Validators
{
    /// <summary>
    /// Ссылка на общую корзину WB (?shareId=…) или «голый» shareId.
    /// </summary>
    public class BasketShareUrlValidator : AbstractValidator<string>
    {
        private static readonly Regex BareShareIdRegex = new(
            @"^[A-Za-z0-9_-]{4,64}$",
            RegexOptions.Compiled);

        public BasketShareUrlValidator()
        {
            RuleFor(x => x)
                .NotNull().WithMessage("Укажите ссылку на общую корзину Wildberries.")
                .NotEmpty().WithMessage("Укажите ссылку на общую корзину Wildberries.");

            RuleFor(x => x)
                .Must(BeValidBasketShare)
                .WithMessage(
                    "Некорректная ссылка. Ожидается вида:\n" +
                    "https://www.wildberries.ru/basket?shareId=…");
        }

        private static bool BeValidBasketShare(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (ProductHelper.TryExtractBasketShareId(trimmed, out _))
            {
                return true;
            }

            // Голый shareId без URL (без / и ?).
            return !trimmed.Contains('/') &&
                   !trimmed.Contains('?') &&
                   BareShareIdRegex.IsMatch(trimmed);
        }

        /// <summary>
        /// Извлекает shareId из уже провалидированной строки.
        /// </summary>
        public static bool TryGetShareId(string? value, out string shareId)
        {
            shareId = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (ProductHelper.TryExtractBasketShareId(trimmed, out shareId))
            {
                return true;
            }

            if (!trimmed.Contains('/') &&
                !trimmed.Contains('?') &&
                BareShareIdRegex.IsMatch(trimmed))
            {
                shareId = trimmed;
                return true;
            }

            return false;
        }
    }
}
