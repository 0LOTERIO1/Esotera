using Esotera.Application.Common;
using Esotera.Application.DTOs.Settings;
using FluentValidation;

namespace Esotera.Application.Validators;

public class UpdateStoreSettingsRequestValidator : AbstractValidator<UpdateStoreSettingsRequest>
{
    public UpdateStoreSettingsRequestValidator()
    {
        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("Nome da loja é obrigatório.")
            .MaximumLength(200);

        RuleFor(x => x.FreeShippingMin)
            .GreaterThanOrEqualTo(0).WithMessage("Limite de frete grátis inválido.");

        RuleFor(x => x.FreeShippingStates)
            .NotNull().WithMessage("Estados são obrigatórios.")
            .Must(states => FreeShippingStatesParser.TryValidate(states, out _, out _))
            .WithMessage("Lista de estados inválida.");

        RuleFor(x => x.J3Price)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.J3CutoffHour)
            .InclusiveBetween(0, 23).WithMessage("Horário-limite da J3 deve estar entre 0 e 23.");

        RuleFor(x => x.ShippingSubsidyAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.ShippingOriginCep)
            .NotEmpty().WithMessage("CEP de origem é obrigatório.")
            .Must(c => BrazilianCep.IsValid(c))
            .WithMessage("CEP de origem inválido.");

        RuleFor(x => x.PackageLengthCm)
            .InclusiveBetween(1, 100).WithMessage("Comprimento do pacote inválido.");

        RuleFor(x => x.PackageWidthCm)
            .InclusiveBetween(1, 100).WithMessage("Largura do pacote inválido.");

        RuleFor(x => x.PackageHeightCm)
            .InclusiveBetween(1, 100).WithMessage("Altura do pacote inválida.");

        RuleFor(x => x.PackageWeightGrams)
            .InclusiveBetween(1, 30_000).WithMessage("Peso do pacote inválido.");
    }
}
