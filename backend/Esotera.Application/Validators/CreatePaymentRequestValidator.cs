using Esotera.Application.DTOs.Payments;
using FluentValidation;

namespace Esotera.Application.Validators;

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.PaymentMethodId)
            .NotEmpty().WithMessage("Método de pagamento é obrigatório.")
            .MaximumLength(50)
            .Must(m => string.Equals(m?.Trim(), "pix", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Nesta fase somente Pix está disponível. Cartão e boleto em breve.");

        RuleFor(x => x.Token)
            .MaximumLength(512);

        RuleFor(x => x.Installments)
            .InclusiveBetween(1, 2)
            .When(x => x.Installments.HasValue)
            .WithMessage("Parcelas permitidas: 1 ou 2.");

        RuleFor(x => x.PayerEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.PayerEmail))
            .WithMessage("E-mail do pagador inválido.");
    }
}
