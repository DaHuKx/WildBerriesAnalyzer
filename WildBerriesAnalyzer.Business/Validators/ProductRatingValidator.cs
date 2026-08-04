using FluentValidation;
using System.Globalization;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class ProductRatingValidator : AbstractValidator<string>
    {
        public ProductRatingValidator()
        {
            RuleFor(x => x)
                .NotEmpty().WithMessage("Значение рейтинга не может быть пустым.")

                .Must(IsValidRating).WithMessage(
                    "Рейтинг должен быть числом от 0 до 5. Допускается использование точки или запятой (например: 4.5 или 4,5)."
                );
        }

        private bool IsValidRating(string rating)
        {
            if (string.IsNullOrWhiteSpace(rating))
                return false;

            string normalizedRating = rating.Trim().Replace(',', '.');

            if (float.TryParse(normalizedRating, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedRating))
            {
                return parsedRating >= 0.0 && parsedRating <= 5.0;
            }

            return false;
        }
    }
}
