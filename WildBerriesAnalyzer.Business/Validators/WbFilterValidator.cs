using FluentValidation;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Validators
{
    /// <summary>
    /// Валидация сущности фильтра (API / сервисный слой).
    /// </summary>
    public class WbFilterValidator : AbstractValidator<WbFilter>
    {
        public WbFilterValidator()
        {
            RuleFor(x => x.DiscontMinPercent)
                .InclusiveBetween(1, 100)
                .WithMessage("Процент должен быть числом в диапазоне от 1 до 100.");

            RuleFor(x => x.MinReviewsCount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Количество отзывов должно быть целым неотрицательным числом (например: 0, 10, 150).");

            RuleFor(x => x.MinRating)
                .InclusiveBetween(0f, 5f)
                .WithMessage("Рейтинг должен быть числом от 0 до 5. Допускается использование точки или запятой (например: 4.5 или 4,5).");

            RuleFor(x => x.ReferencePriceStrartegies)
                .NotNull()
                .WithMessage("Необходимо выбрать хотя бы одну стратегию определения цены.")
                .Must(strategies => strategies is { Count: > 0 })
                .WithMessage("Необходимо выбрать хотя бы одну стратегию определения цены.")
                .Must(strategies => strategies!.All(s => Enum.IsDefined(typeof(ReferencePriceStrategy), s)))
                .WithMessage("Указана неизвестная стратегия определения цены.");

            RuleFor(x => x.ProductsFilterType)
                .IsInEnum()
                .WithMessage("Указан некорректный тип фильтрации товаров.");
        }
    }
}
