using System.Linq;
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

        // Aceita máscara (ex.: 000.000.000-00); valida apenas dígitos.
        RuleFor(x => x.Cpf)
            .Must(cpf =>
            {
                if (string.IsNullOrWhiteSpace(cpf)) return true;
                var digits = new string(cpf.Where(char.IsDigit).ToArray());
                return digits.Length == 11;
            })
            .WithMessage("CPF deve conter 11 dígitos.");

        RuleFor(x => x.Phone)
            .Must(phone =>
            {
                if (string.IsNullOrWhiteSpace(phone)) return true;
                var digits = new string(phone.Where(char.IsDigit).ToArray());
                return digits.Length is 10 or 11;
            })
            .WithMessage("Telefone deve conter 10 ou 11 dígitos.");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("Aceite os termos de uso para continuar.");

        RuleFor(x => x.AcceptedPrivacy)
            .Equal(true).WithMessage("Aceite a política de privacidade para continuar.");
    }
}
