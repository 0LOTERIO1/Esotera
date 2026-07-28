using Esotera.Application.DTOs.Orders;
using Esotera.Domain.Enums;
using FluentValidation;

namespace Esotera.Application.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Pedido deve conter ao menos um item.")
            .Must(items => items.Length <= 20).WithMessage("Pedido pode conter no máximo 20 itens distintos.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("ID do produto é obrigatório.");
            
            item.RuleFor(i => i.Quantity)
                .InclusiveBetween(1, 99).WithMessage("Quantidade deve estar entre 1 e 99.");
        });

        RuleFor(x => x.ShippingMethodId)
            .NotEmpty().WithMessage("Método de envio é obrigatório.")
            .Must(ShippingMethod.IsValid).WithMessage("Método de envio inválido.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Método de pagamento é obrigatório.")
            .Must(PaymentMethod.IsValid).WithMessage("Método de pagamento inválido.");

        When(x => x.PaymentMethod == PaymentMethod.Card, () =>
        {
            RuleFor(x => x.Installments)
                .NotNull().WithMessage("Informe a quantidade de parcelas.")
                .InclusiveBetween(1, 2).WithMessage("Parcelas devem ser 1 ou 2.");
        });

        When(x => x.PaymentMethod != PaymentMethod.Card, () =>
        {
            RuleFor(x => x.Installments)
                .Must(i => i == null || i == 1)
                .WithMessage("Parcelas só se aplicam ao pagamento com cartão.");
        });

        RuleFor(x => x)
            .Must(x => x.AddressId.HasValue || x.Address != null)
            .WithMessage("Endereço é obrigatório.");

        When(x => x.Address != null, () =>
        {
            RuleFor(x => x.Address!.Cep)
                .NotEmpty().WithMessage("CEP é obrigatório.")
                .Matches(@"^\d{8}$").WithMessage("CEP deve conter 8 dígitos.");

            RuleFor(x => x.Address!.Street)
                .NotEmpty().WithMessage("Rua é obrigatória.");

            RuleFor(x => x.Address!.Number)
                .NotEmpty().WithMessage("Número é obrigatório.");

            RuleFor(x => x.Address!.Neighborhood)
                .NotEmpty().WithMessage("Bairro é obrigatório.");

            RuleFor(x => x.Address!.City)
                .NotEmpty().WithMessage("Cidade é obrigatória.");

            RuleFor(x => x.Address!.State)
                .NotEmpty().WithMessage("Estado é obrigatório.")
                .Length(2).WithMessage("Estado deve ter 2 caracteres.");
        });
    }
}
