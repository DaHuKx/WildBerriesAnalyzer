using FluentValidation;
using System.Text.RegularExpressions;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class ProductIdValidator : AbstractValidator<string>
    {
        private const int MinLength = 5;
        private const int MaxLength = 10;

        public ProductIdValidator()
        {
            RuleFor(x => x)
                .NotNull().WithMessage("Артикул не может быть null.")
                .NotEmpty().WithMessage("Артикул не может быть пустым.");

            RuleFor(x => x)
                .Must(BeValidArticleOrUrl)
                .WithMessage(context => GetErrorMessage(context));
        }

        private bool BeValidArticleOrUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            if (Regex.IsMatch(value, @"^\d+$"))
            {
                return value.Length >= MinLength &&
                       value.Length <= MaxLength &&
                       value[0] != '0';
            }

            var urlMatch = Regex.Match(
                value,
                @"^https?://(?:www\.)?wildberries\.ru/catalog/(\d+)(?:/[^?\s]*)?(?:\?.*)?$",
                RegexOptions.IgnoreCase
            );

            if (urlMatch.Success)
            {
                var article = urlMatch.Groups[1].Value;
                return article.Length >= MinLength &&
                       article.Length <= MaxLength &&
                       article[0] != '0';
            }

            return false;
        }

        private string GetErrorMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Артикул не может быть пустым.";

            if (Regex.IsMatch(value, @"^\d+$"))
            {
                if (value.Length < MinLength || value.Length > MaxLength)
                    return $"Артикул должен содержать от {MinLength} до {MaxLength} цифр.";
                if (value[0] == '0')
                    return "Артикул не может начинаться с нуля.";

                return "Артикул Wildberries должен содержать только цифры.";
            }

            if (Regex.IsMatch(value, @"^https?://", RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(value, @"wildberries\.ru/catalog/(\d+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                    return "Ссылка должна быть корректным URL каталога Wildberries (например, https://www.wildberries.ru/catalog/567263073).";

                var article = match.Groups[1].Value;
                if (article.Length < MinLength || article.Length > MaxLength)
                    return $"Артикул в ссылке должен содержать от {MinLength} до {MaxLength} цифр (найдено: {article}).";
                if (article[0] == '0')
                    return "Артикул в ссылке не может начинаться с нуля.";
            }

            return "Некорректный формат. Ожидается артикул (5-10 цифр без лидирующего нуля) или ссылка на товар Wildberries.";
        }
    }
}
