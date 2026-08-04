using FluentValidation;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class FilterValidator : AbstractValidator<WbFilter>
    {
        public FilterValidator()
        {
            RuleFor(f => f).NotNull();
            RuleFor(f => f.DiscontMinPercent).GreaterThanOrEqualTo(0)
                                          .LessThanOrEqualTo(99);
            RuleFor(f => f.MinRating).GreaterThanOrEqualTo(0)
                                     .LessThanOrEqualTo(5);
            RuleFor(f => f.MinReviewsCount).GreaterThanOrEqualTo(0);
        }
    }
}
