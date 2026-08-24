using Esotera.Application.DTOs.Payments;
using FluentValidation;

namespace Esotera.Application.Validators;

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bank_transfer",
        "credit_card",
        "debit_card",
        "ticket"
    };

    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.PaymentMethodId)
            .NotEmpty().WithMessage("Método de pagamento é obrigatório.")
            .MaximumLength(50);

        RuleFor(x => x)
            .Custom((req, ctx) =>
            {
                var type = ResolveType(req);
                if (type is null)
                {
                    ctx.AddFailure(
                        "paymentMethodType",
                        "Tipo de pagamento inválido. Use bank_transfer, credit_card, debit_card ou ticket.");
                    return;
                }

                var methodId = (req.PaymentMethodId ?? "").Trim().ToLowerInvariant();

                switch (type)
                {
                    case "bank_transfer":
                        if (methodId is not "pix")
                        {
                            ctx.AddFailure(
                                "paymentMethodId",
                                "Para bank_transfer somente Pix (pix) está disponível.");
                        }

                        if (!string.IsNullOrWhiteSpace(req.Token))
                        {
                            ctx.AddFailure("token", "Pix não utiliza token de cartão.");
                        }

                        break;

                    case "credit_card":
                    case "debit_card":
                        if (IsReservedNonCardMethodId(methodId))
                        {
                            ctx.AddFailure(
                                nameof(CreatePaymentRequest.PaymentMethodId),
                                "ID de método incompatível com cartão (pix/boleto reservados).");
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(req.Token))
                        {
                            ctx.AddFailure("token", "Token do cartão é obrigatório.");
                        }
                        else if (req.Token.Trim().Length > 512)
                        {
                            ctx.AddFailure("token", "Token inválido.");
                        }

                        if (string.IsNullOrWhiteSpace(methodId))
                        {
                            ctx.AddFailure("paymentMethodId", "Informe o método do cartão.");
                        }

                        if (type == "credit_card"
                            && req.Installments is null or < 1 or > 1)
                        {
                            ctx.AddFailure(
                                "installments",
                                "Nesta fase o crédito aceita somente 1 parcela.");
                        }

                        if (type == "debit_card" && req.Installments is not null and not 1)
                        {
                            ctx.AddFailure(
                                "installments",
                                "Débito não utiliza parcelamento.");
                        }

                        break;

                    case "ticket":
                        if (methodId is not ("bolbradesco" or "boleto"))
                        {
                            ctx.AddFailure(
                                "paymentMethodId",
                                "Para boleto use bolbradesco (Brick) ou boleto.");
                        }

                        if (!string.IsNullOrWhiteSpace(req.Token))
                        {
                            ctx.AddFailure("token", "Boleto não utiliza token de cartão.");
                        }

                        break;
                }
            });

        RuleFor(x => x.Token)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.Token));

        RuleFor(x => x.PayerEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.PayerEmail))
            .WithMessage("E-mail do pagador inválido.");

        RuleFor(x => x.PayerIdentificationType)
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.PayerIdentificationType));

        RuleFor(x => x.PayerIdentificationNumber)
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.PayerIdentificationNumber));
    }

    /// <summary>
    /// Resolve o tipo canônico. Null = inválido.
    /// Compat: paymentMethodId=pix sem type → bank_transfer.
    /// </summary>
    public static string? ResolveType(CreatePaymentRequest request)
    {
        var raw = (request.PaymentMethodType ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(raw))
        {
            var methodId = (request.PaymentMethodId ?? "").Trim().ToLowerInvariant();
            return methodId == "pix" ? "bank_transfer" : null;
        }

        return AllowedTypes.Contains(raw) ? raw : null;
    }

    /// <summary>IDs reservados a Pix/boleto — inválidos em credit_card/debit_card.</summary>
    public static bool IsReservedNonCardMethodId(string? methodId)
    {
        var id = (methodId ?? "").Trim().ToLowerInvariant();
        return id is "pix" or "bolbradesco" or "boleto";
    }
}
