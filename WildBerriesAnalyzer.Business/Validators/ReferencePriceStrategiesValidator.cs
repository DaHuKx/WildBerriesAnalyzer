using FluentValidation;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class ReferencePriceStrategiesValidator : AbstractValidator<string>
    {
        public ReferencePriceStrategiesValidator()
        {
            RuleFor(x => x)
                .NotEmpty().WithMessage("Необходимо выбрать хотя бы одну стратегию определения цены.")

                .Must(IsValidStrategies).WithMessage(
                    "Стратегии должны быть указаны в формате: числа от 1 до 7, разделенные пробелом (например: \"1 3 5\")."
                );
        }

        private bool IsValidStrategies(string strategies)
        {
            if (string.IsNullOrWhiteSpace(strategies))
                return false;

            var strategyNumbers = strategies
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (!strategyNumbers.Any())
                return false;

            foreach (var strategyNumber in strategyNumbers)
            {
                if (!int.TryParse(strategyNumber, out int parsedNumber))
                    return false;

                if (!Enum.IsDefined(typeof(ReferencePriceStrategy), parsedNumber))
                    return false;
            }

            return true;
        }
    }
}
