using FluentValidation;
using System.Text.RegularExpressions;
using WildBerriesAnalyzer.Business.Helpers;

namespace WildBerriesAnalyzer.Business.Validators
{
    /// <summary>
    /// Ссылка на общую корзину Ozon (?share=…) или «голый» share-токен.
    /// </summary>
    public class OzonCartShareUrlValidator : AbstractValidator<string>
    {
        private static readonly Regex BareShareTokenRegex = new(
            @"^[A-Za-z0-9_-]{4,64}$",
            RegexOptions.Compiled);

        public OzonCartShareUrlValidator()
        {
            RuleFor(x => x)
                .NotNull().WithMessage("Укажите ссылку на общую корзину Ozon.")
                .NotEmpty().WithMessage("Укажите ссылку на общую корзину Ozon.");

            RuleFor(x => x)
                .Must(BeValidOzonCartShare)
                .WithMessage(
                    "Некорректная ссылка. Ожидается вида:\n" +
                    "https://www.ozon.ru/cart?share=…");
        }

        private static bool BeValidOzonCartShare(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (ProductHelper.TryExtractOzonCartShareId(trimmed, out _))
            {
                return true;
            }

            return !trimmed.Contains('/') &&
                   !trimmed.Contains('?') &&
                   BareShareTokenRegex.IsMatch(trimmed);
        }

        /// <summary>
        /// Извлекает share-токен из уже провалидированной строки.
        /// </summary>
        public static bool TryGetShareToken(string? value, out string shareToken)
        {
            shareToken = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (ProductHelper.TryExtractOzonCartShareId(trimmed, out shareToken))
            {
                return true;
            }

            if (!trimmed.Contains('/') &&
                !trimmed.Contains('?') &&
                BareShareTokenRegex.IsMatch(trimmed))
            {
                shareToken = trimmed;
                return true;
            }

            return false;
        }
    }
}
