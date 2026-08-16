using FluentValidation;
using WildBerriesAnalyzer.Business.Helpers;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class ProductIdValidator : AbstractValidator<string>
    {
        public ProductIdValidator()
        {
            RuleFor(x => x)
                .NotNull().WithMessage("Артикул не может быть null.")
                .NotEmpty().WithMessage("Артикул не может быть пустым.");

            RuleFor(x => x)
                .Must(value => ProductHelper.IsValidArticleOrUrl(value, MarketType.Wildberries))
                .WithMessage(value => ProductHelper.GetArticleValidationError(value, MarketType.Wildberries));
        }

        public FluentValidation.Results.ValidationResult Validate(string value, MarketType marketType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new FluentValidation.Results.ValidationResult(
                [
                    new FluentValidation.Results.ValidationFailure(nameof(value), "Артикул не может быть пустым.")
                ]);
            }

            if (ProductHelper.IsValidArticleOrUrl(value, marketType))
            {
                return new FluentValidation.Results.ValidationResult();
            }

            return new FluentValidation.Results.ValidationResult(
            [
                new FluentValidation.Results.ValidationFailure(
                    nameof(value),
                    ProductHelper.GetArticleValidationError(value, marketType))
            ]);
        }
    }
}
