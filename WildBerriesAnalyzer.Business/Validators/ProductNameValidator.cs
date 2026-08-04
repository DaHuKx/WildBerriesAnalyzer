using FluentValidation;
using System.Text.RegularExpressions;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class ProductNameValidator : AbstractValidator<string>
    {
        private static readonly Regex UrlPattern = new(
            @"(https?:\/\/)|(www\.)|(\.com|\.ru|\.net|\.org)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EmailPattern = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RepeatingCharsPattern = new(
            @"(.)\1{4,}",
            RegexOptions.Compiled);

        private static readonly Regex CurrencySymbolsPattern = new(
            @"[$€£¥₽₴₸]",
            RegexOptions.Compiled);

        public ProductNameValidator()
        {
            RuleFor(x => x)
                .NotNull()
                    .WithMessage("Название продукта не может быть null.")
                .NotEmpty()
                    .WithMessage("Название продукта не может быть пустым.")
                .Length(2, 100)
                    .WithMessage("Длина названия должна быть от 2 до 100 символов.")
                .Must(name => name == name.Trim())
                    .WithMessage("Название не должно содержать пробелы в начале или конце.")

                .Matches(@"^[\p{L}\p{N}\s\.\-\'&\/]+$")
                    .WithMessage("Название содержит недопустимые символы.")

                .Must(name => name.Any(char.IsLetter))
                    .WithMessage("Название должно содержать хотя бы одну букву.")

                .Must(name => !name.All(char.IsDigit))
                    .WithMessage("Название не может состоять только из цифр.")

                .Must(name => name.Any(c => char.IsLetterOrDigit(c)))
                    .WithMessage("Название не может состоять только из специальных символов.")

                .Must(name => !RepeatingCharsPattern.IsMatch(name))
                    .WithMessage("Название не должно содержать длинных последовательностей одинаковых символов.")

                .Must(name => !UrlPattern.IsMatch(name))
                    .WithMessage("Название не должно содержать ссылок.")

                .Must(name => !EmailPattern.IsMatch(name))
                    .WithMessage("Название не должно содержать email-адрес.")

                .Must(name => !CurrencySymbolsPattern.IsMatch(name))
                    .WithMessage("Название не должно содержать символов валют.");
        }
    }
}
