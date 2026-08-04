using FluentValidation;
using System.Globalization;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class ReviewsCountValidator : AbstractValidator<string>
    {
        public ReviewsCountValidator()
        {
            RuleFor(x => x)
                .NotEmpty().WithMessage("Значение количества отзывов не может быть пустым.")

                .Must(IsValidReviewsCount).WithMessage(
                    "Количество отзывов должно быть целым неотрицательным числом (например: 0, 10, 150)."
                );
        }

        private bool IsValidReviewsCount(string reviewsCount)
        {
            if (string.IsNullOrWhiteSpace(reviewsCount))
                return false;

            string normalizedCount = reviewsCount.Trim();

            if (int.TryParse(normalizedCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCount))
            {
                return parsedCount >= 0;
            }

            return false;
        }
    }
}
