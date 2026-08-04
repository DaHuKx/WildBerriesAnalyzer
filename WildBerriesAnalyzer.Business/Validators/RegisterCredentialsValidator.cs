using FluentValidation;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.Data.Repositories.Interfaces;

namespace WildBerriesAnalyzer.Business.Validators
{
    public class RegisterCredentialsValidator : AbstractValidator<RegisterCredentials>
    {
        public RegisterCredentialsValidator(IUsersRepository usersRepository)
        {
            RuleFor(x => x.Login)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Логин не может быть пустым.")
                .MinimumLength(3)
                .WithMessage("Логин должен содержать минимум 3 символа.")
                .MustAsync(async (login, cancellationToken) =>
                {
                    var existing = await usersRepository.GetUserByLoginAsync(login);
                    return existing is null;
                })
                .WithMessage("Пользователь с таким логином уже существует.");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Пароль не может быть пустым.")
                .MinimumLength(6)
                .WithMessage("Пароль должен содержать минимум 6 символов.");

            RuleFor(x => x.VkProfileUrl)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Укажите ссылку на ваш профиль VK.");
        }
    }
}
