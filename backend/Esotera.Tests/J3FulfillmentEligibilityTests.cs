using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using FluentAssertions;

namespace Esotera.Tests;

/// <summary>Gate fiscal local J3 — zero HTTP / zero XmlCipher.</summary>
public class J3FulfillmentEligibilityTests
{
    private static readonly string ValidChNFe = new string('9', 44);

    [Fact]
    public void HappyPath_IsEligible()
    {
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            ValidFiscal(),
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeTrue();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.Eligible);
        result.Fiscal!.ChNFe.Should().Be(ValidChNFe);
        result.Fiscal.Number.Should().Be("2");
        result.Fiscal.Series.Should().Be("9");
    }

    [Fact]
    public void MissingFiscal_NotEligible()
    {
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            fiscal: null,
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.MissingFiscalInvoice);
    }

    [Fact]
    public void FiscalUnknown_NotEligible()
    {
        var fiscal = ValidFiscal();
        fiscal = fiscal with { Status = FiscalInvoiceStatus.Unknown };
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            fiscal,
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized);
    }

    [Fact]
    public void AuthorizedWithoutChNFe_NotEligible()
    {
        var fiscal = ValidFiscal() with { ChNFe = null };
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            fiscal,
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.MissingNfeKey);
    }

    [Fact]
    public void ChNFeTooShort_NotEligible()
    {
        var fiscal = ValidFiscal() with { ChNFe = new string('1', 43) };
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            fiscal,
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.InvalidNfeKey);
    }

    [Fact]
    public void ChNFeWithLetter_NotEligible()
    {
        var fiscal = ValidFiscal() with { ChNFe = new string('1', 43) + "A" };
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            fiscal,
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.InvalidNfeKey);
    }

    [Fact]
    public void WrongShippingMethod_NotEligible()
    {
        var order = ValidOrder();
        order.ShippingMethodId = ShippingMethod.MelhorExpresso;
        var result = J3FulfillmentEligibility.Evaluate(
            order,
            ValidFiscal(),
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.WrongShippingMethod);
    }

    [Fact]
    public void AwaitingPayment_NotEligible()
    {
        var order = ValidOrder();
        order.Status = OrderStatus.AwaitingPayment;
        var result = J3FulfillmentEligibility.Evaluate(
            order,
            ValidFiscal(),
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.PaymentNotApproved);
    }

    [Theory]
    [InlineData(nameof(Order.ShipStreet))]
    [InlineData(nameof(Order.ShipNumber))]
    [InlineData(nameof(Order.ShipNeighborhood))]
    [InlineData(nameof(Order.ShipCity))]
    [InlineData(nameof(Order.ShipState))]
    [InlineData(nameof(Order.ShipCep))]
    public void IncompleteAddress_NotEligible(string field)
    {
        var order = ValidOrder();
        typeof(Order).GetProperty(field)!.SetValue(order, field == nameof(Order.ShipCep) ? "123" : " ");
        var result = J3FulfillmentEligibility.Evaluate(
            order,
            ValidFiscal(),
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.IncompleteShippingAddress);
    }

    [Fact]
    public void MissingResidential_NotEligible()
    {
        var order = ValidOrder();
        order.ShippingIsResidentialAddress = null;
        var result = J3FulfillmentEligibility.Evaluate(
            order,
            ValidFiscal(),
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.MissingResidentialFlag);
    }

    [Fact]
    public void PhoneOptional_StillEligible()
    {
        var order = ValidOrder();
        order.CustomerPhone = null;
        var result = J3FulfillmentEligibility.Evaluate(
            order,
            ValidFiscal(),
            PendingFulfillment(),
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeTrue();
    }

    [Fact]
    public void NoFulfillmentRow_CanBeEligible()
    {
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            ValidFiscal(),
            fulfillment: null,
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeTrue();
    }

    [Fact]
    public void Created_NotEligible()
    {
        var f = PendingFulfillment();
        f.Status = J3FulfillmentStatus.Created;
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            ValidFiscal(),
            f,
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.FulfillmentAlreadyCreated);
    }

    [Fact]
    public void Processing_NotEligible()
    {
        var f = PendingFulfillment();
        f.Status = J3FulfillmentStatus.Processing;
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            ValidFiscal(),
            f,
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.FulfillmentAlreadyExists);
    }

    [Fact]
    public void UnknownOutcome_NotEligible()
    {
        var f = PendingFulfillment();
        f.Status = J3FulfillmentStatus.UnknownOutcome;
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            ValidFiscal(),
            f,
            fulfillmentEnabled: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview);
    }

    [Fact]
    public void FeatureDisabled_NotEligible()
    {
        var result = J3FulfillmentEligibility.Evaluate(
            ValidOrder(),
            ValidFiscal(),
            PendingFulfillment(),
            fulfillmentEnabled: false);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(J3FulfillmentEligibilityCodes.FeatureDisabled);
    }

    [Fact]
    public void SnapshotFiscal_DoesNotTouchXmlCipher()
    {
        var invoice = new FiscalInvoice
        {
            Status = FiscalInvoiceStatus.Authorized,
            ChNFe = ValidChNFe,
            Number = "1",
            Series = "1",
            XmlCipher = "SHOULD_NOT_APPEAR_IN_SNAPSHOT",
            XmlSha256 = new string('a', 64),
            Source = FiscalInvoiceSource.ManualUpload
        };

        var snap = J3FulfillmentEligibility.SnapshotFiscal(invoice)!;
        snap.ChNFe.Should().Be(ValidChNFe);
        typeof(J3FiscalEligibilitySnapshot).GetProperty("XmlCipher").Should().BeNull();
    }

    private static Order ValidOrder() => new()
    {
        Id = Guid.NewGuid(),
        Status = OrderStatus.PaymentApproved,
        ShippingMethodId = ShippingMethod.J3,
        ShipCep = "01310100",
        ShipStreet = "Av Paulista",
        ShipNumber = "1000",
        ShipNeighborhood = "Bela Vista",
        ShipCity = "São Paulo",
        ShipState = "SP",
        ShippingIsResidentialAddress = true,
        CustomerName = "Cliente"
    };

    private static J3FiscalEligibilitySnapshot ValidFiscal() => new()
    {
        Status = FiscalInvoiceStatus.Authorized,
        ChNFe = ValidChNFe,
        Number = "2",
        Series = "9",
        AuthorizedAtUtc = DateTime.UtcNow
    };

    private static J3Fulfillment PendingFulfillment() => new()
    {
        Id = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        Status = J3FulfillmentStatus.Pending
    };
}
