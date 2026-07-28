using Esotera.Application.DTOs.Addresses;
using FluentValidation;

namespace Esotera.Application.Validators;

public class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.Cep)
            .NotEmpty().WithMessage("CEP é obrigatório.")
            .Matches(@"^\d{8}$").WithMessage("CEP deve conter 8 dígitos.");

        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Rua é obrigatória.")
            .MaximumLength(200).WithMessage("Rua deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Number)
            .NotEmpty().WithMessage("Número é obrigatório.")
            .MaximumLength(20).WithMessage("Número deve ter no máximo 20 caracteres.");

        RuleFor(x => x.Neighborhood)
            .NotEmpty().WithMessage("Bairro é obrigatório.")
            .MaximumLength(100).WithMessage("Bairro deve ter no máximo 100 caracteres.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("Cidade é obrigatória.")
            .MaximumLength(100).WithMessage("Cidade deve ter no máximo 100 caracteres.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("Estado é obrigatório.")
            .Length(2).WithMessage("Estado deve ter 2 caracteres.");
    }
}
