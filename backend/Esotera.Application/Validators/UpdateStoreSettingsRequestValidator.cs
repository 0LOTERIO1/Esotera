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
    }
}
