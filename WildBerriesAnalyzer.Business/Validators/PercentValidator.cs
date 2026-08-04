using FluentValidation;
using System.Globalization;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class PercentValidator : AbstractValidator<string>
    {
        public PercentValidator()
        {
            RuleFor(x => x)
                .NotEmpty()
                .WithMessage("Значение не может быть пустым.")

                .Must(BeValidPercentage)
                .WithMessage("Процент должен быть числом в диапазоне от 1 до 100.");
        }

        private bool BeValidPercentage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var percentage))
                return false;

            return percentage >= 1m && percentage <= 100m;
        }
    }
}
