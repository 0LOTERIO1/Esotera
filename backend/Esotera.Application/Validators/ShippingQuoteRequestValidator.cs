using Esotera.Application.Common;
using Esotera.Application.DTOs.Shipping;
using FluentValidation;

namespace Esotera.Application.Validators;

public class ShippingQuoteRequestValidator : AbstractValidator<ShippingQuoteRequest>
{
    public const decimal MaxProductsSubtotal = 1_000_000m;

    public ShippingQuoteRequestValidator()
    {
        RuleFor(x => x.DestinationCep)
            .NotEmpty().WithMessage("CEP de destino é obrigatório.")
            .Must(c => BrazilianCep.IsValid(c))
            .WithMessage("CEP de destino inválido.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("UF é obrigatória.")
            .Length(2).WithMessage("UF deve ter 2 caracteres.")
            .Matches(@"^[A-Za-z]{2}$").WithMessage("UF inválida.");

        RuleFor(x => x.ProductsSubtotal)
            .GreaterThanOrEqualTo(0).WithMessage("Subtotal inválido.")
            .LessThanOrEqualTo(MaxProductsSubtotal).WithMessage("Subtotal excede o limite permitido.");
    }
}
