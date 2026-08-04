using FluentValidation;
using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class RefreshCredentialsValidator : AbstractValidator<RefreshCredentials>
    {
        public RefreshCredentialsValidator()
        {
            RuleFor(x => x.RefreshToken)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Refresh-токен не может быть пустым.");
        }
    }
}
