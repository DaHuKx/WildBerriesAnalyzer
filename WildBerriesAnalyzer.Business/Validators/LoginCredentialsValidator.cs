using FluentValidation;
using WildBerriesAnalyzer.Business.Models;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class LoginCredentialsValidator : AbstractValidator<LoginCredentials>
    {
        public LoginCredentialsValidator()
        {
            RuleFor(x => x.Login)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Логин не может быть пустым.");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Пароль не может быть пустым.");
        }
    }
}
