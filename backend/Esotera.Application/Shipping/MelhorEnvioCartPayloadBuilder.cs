using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;

namespace Esotera.Application.Shipping;

/// <summary>
/// Monta o payload de inserção no carrinho a partir do pedido, da NF-e e da configuração.
/// FUNÇÃO PURA: nenhuma chamada HTTP, nenhum acesso a banco, nenhuma escrita.
/// Falha fechada — sem dado obrigatório, devolve código de erro em vez de payload parcial.
/// </summary>
public static class MelhorEnvioCartPayloadBuilder
{
    /// <summary>Pessoa física não tem inscrição estadual.</summary>
    private const string ExemptStateRegister = "ISENTO";

    public sealed record Result(
        MelhorEnvioCartRequest? Request,
        string? ErrorCode,
        string? ErrorMessage)
    {
        public bool Ok => Request is not null;

        public static Result Success(MelhorEnvioCartRequest request) => new(request, null, null);
        public static Result Fail(string code, string message) => new(null, code, message);
    }

    public static Result Build(
        Order order,
        string? invoiceKey,
        StoreSettings settings,
        MelhorEnvioSenderOptions sender)
    {
        if (order.ShippingServiceId is not { } serviceId || serviceId <= 0)
        {
            return Result.Fail(
                MelhorEnvioShipmentErrorCodes.ServiceIdMissing,
                "Pedido sem id do serviço Melhor Envio na cotação. Não é possível inserir no carrinho.");
        }

        var invoiceDigits = DigitsOnly(invoiceKey);
        if (invoiceDigits.Length != 44)
        {
            return Result.Fail(
                MelhorEnvioShipmentErrorCodes.InvoiceKeyMissing,
                "Envio comercial exige a chave da NF-e autorizada (44 dígitos).");
        }

        if (!sender.IsCompleteForCommercialShipping)
        {
            var missing = string.Join(", ", sender.MissingCommercialFields());
            return Result.Fail(
                MelhorEnvioShipmentErrorCodes.SenderIncomplete,
                $"Remetente incompleto. Configure no servidor: {missing}.");
        }

        var originCep = DigitsOnly(settings.ShippingOriginCep);
        if (originCep.Length != 8)
        {
            return Result.Fail(
                MelhorEnvioShipmentErrorCodes.OriginCepMissing,
                "CEP de origem inválido nas configurações da loja.");
        }

        var destinationCep = DigitsOnly(order.ShipCep);
        if (destinationCep.Length != 8
            || string.IsNullOrWhiteSpace(order.ShipStreet)
            || string.IsNullOrWhiteSpace(order.ShipNumber)
            || string.IsNullOrWhiteSpace(order.ShipNeighborhood)
            || string.IsNullOrWhiteSpace(order.ShipCity)
            || string.IsNullOrWhiteSpace(order.ShipState)
            || string.IsNullOrWhiteSpace(order.CustomerName))
        {
            return Result.Fail(
                MelhorEnvioShipmentErrorCodes.RecipientIncomplete,
                "Endereço ou nome do destinatário incompleto no pedido.");
        }

        var items = order.Items?
            .Where(i => i.Quantity > 0)
            .ToList() ?? [];
        if (items.Count == 0)
        {
            return Result.Fail(
                MelhorEnvioShipmentErrorCodes.ItemsMissing,
                "Pedido sem itens — o Melhor Envio exige a lista de produtos.");
        }

        var weightKg = settings.PackageWeightGrams / 1000m;
        if (settings.PackageHeightCm <= 0
            || settings.PackageWidthCm <= 0
            || settings.PackageLengthCm <= 0
            || weightKg <= 0)
        {
            return Result.Fail(
                MelhorEnvioShipmentErrorCodes.PackageInvalid,
                "Dimensões ou peso do pacote inválidos nas configurações da loja.");
        }

        var products = items
            .Select(i => new MelhorEnvioCartProduct(i.ProductName, i.Quantity, i.UnitPrice))
            .ToList();

        // Valor segurado = valor das mercadorias, sem frete. Nunca o total do pedido.
        var insuranceValue = items.Sum(i => i.UnitPrice * i.Quantity);

        var from = new MelhorEnvioCartParty(
            Name: sender.Name!.Trim(),
            Email: sender.Email!.Trim(),
            Phone: DigitsOnly(sender.Phone),
            Document: null,
            CompanyDocument: DigitsOnly(sender.CompanyDocument),
            StateRegister: sender.StateRegister!.Trim(),
            EconomicActivityCode: DigitsOnly(sender.EconomicActivityCode),
            Address: sender.Address!.Trim(),
            Complement: string.IsNullOrWhiteSpace(sender.Complement) ? null : sender.Complement.Trim(),
            Number: sender.Number!.Trim(),
            District: sender.District!.Trim(),
            City: sender.City!.Trim(),
            PostalCode: originCep,
            StateAbbr: sender.StateAbbr!.Trim().ToUpperInvariant());

        var to = new MelhorEnvioCartParty(
            Name: order.CustomerName.Trim(),
            Email: string.IsNullOrWhiteSpace(order.CustomerEmail) ? null : order.CustomerEmail.Trim(),
            Phone: DigitsOnly(order.CustomerPhone),
            Document: DigitsOnly(order.CustomerCpf),
            CompanyDocument: null,
            StateRegister: ExemptStateRegister,
            EconomicActivityCode: null,
            Address: order.ShipStreet.Trim(),
            Complement: string.IsNullOrWhiteSpace(order.ShipComplement) ? null : order.ShipComplement.Trim(),
            Number: order.ShipNumber.Trim(),
            District: order.ShipNeighborhood.Trim(),
            City: order.ShipCity.Trim(),
            PostalCode: destinationCep,
            StateAbbr: order.ShipState.Trim().ToUpperInvariant(),
            CountryId: "BR");

        var options = new MelhorEnvioCartOptions(
            InsuranceValue: insuranceValue,
            Platform: string.IsNullOrWhiteSpace(sender.Platform) ? null : sender.Platform.Trim(),
            Reminder: order.OrderNumber,
            InvoiceKey: invoiceDigits,
            OrderTag: order.OrderNumber,
            NonCommercial: false);

        return Result.Success(new MelhorEnvioCartRequest(
            Service: serviceId,
            From: from,
            To: to,
            Products: products,
            Volumes: [new MelhorEnvioCartVolume(
                settings.PackageHeightCm,
                settings.PackageWidthCm,
                settings.PackageLengthCm,
                weightKg)],
            Options: options));
    }

    private static string DigitsOnly(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());
}
