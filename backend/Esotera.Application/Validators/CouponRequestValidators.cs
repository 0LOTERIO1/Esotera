using Esotera.Application.DTOs.Coupons;
using FluentValidation;

namespace Esotera.Application.Validators;

public class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Código é obrigatório.")
            .MaximumLength(50).WithMessage("Código deve ter no máximo 50 caracteres.")
            .Must(c => !string.IsNullOrWhiteSpace(c))
            .WithMessage("Código é obrigatório.");

        RuleFor(x => x.DiscountAmount)
            .GreaterThan(0).WithMessage("Desconto deve ser maior que zero.");

        RuleFor(x => x.MinPurchase)
            .GreaterThanOrEqualTo(0).WithMessage("Compra mínima não pode ser negativa.");

        RuleFor(x => x.MaxTotalUses)
            .GreaterThan(0).When(x => x.MaxTotalUses.HasValue)
            .WithMessage("Limite global de utilizações deve ser maior que zero.");

        RuleFor(x => x)
            .Must(x => !x.ValidFromUtc.HasValue || !x.ValidUntilUtc.HasValue || x.ValidFromUtc <= x.ValidUntilUtc)
            .WithMessage("Data inicial deve ser menor ou igual à data final.");
    }
}

public class UpdateCouponRequestValidator : AbstractValidator<UpdateCouponRequest>
{
    public UpdateCouponRequestValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(50).When(x => x.Code != null)
            .Must(c => c == null || !string.IsNullOrWhiteSpace(c))
            .When(x => x.Code != null)
            .WithMessage("Código inválido.");

        RuleFor(x => x.DiscountAmount)
            .GreaterThan(0).When(x => x.DiscountAmount.HasValue);

        RuleFor(x => x.MinPurchase)
            .GreaterThanOrEqualTo(0).When(x => x.MinPurchase.HasValue);

        RuleFor(x => x.MaxTotalUses)
            .GreaterThan(0).When(x => x.MaxTotalUses.HasValue && x.ClearMaxTotalUses != true);

        RuleFor(x => x)
            .Must(x =>
            {
                var from = x.ClearValidFrom == true ? null : x.ValidFromUtc;
                var until = x.ClearValidUntil == true ? null : x.ValidUntilUtc;
                if (from.HasValue && until.HasValue)
                    return from <= until;
                return true;
            })
            .WithMessage("Data inicial deve ser menor ou igual à data final.");
    }
}
