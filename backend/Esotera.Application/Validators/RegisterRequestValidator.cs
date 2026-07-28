using Esotera.Application.DTOs.Auth;
using FluentValidation;

namespace Esotera.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(200).WithMessage("Nome deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email é obrigatório.")
            .EmailAddress().WithMessage("Email inválido.")
            .MaximumLength(256).WithMessage("Email deve ter no máximo 256 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(6).WithMessage("Senha deve ter no mínimo 6 caracteres.");

        RuleFor(x => x.Cpf)
            .Matches(@"^\d{11}$").When(x => !string.IsNullOrEmpty(x.Cpf))
            .WithMessage("CPF deve conter 11 dígitos.");

        RuleFor(x => x.Phone)
            .Matches(@"^\d{10,11}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Telefone deve conter 10 ou 11 dígitos.");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("Aceite os termos de uso para continuar.");

        RuleFor(x => x.AcceptedPrivacy)
            .Equal(true).WithMessage("Aceite a política de privacidade para continuar.");
    }
}
