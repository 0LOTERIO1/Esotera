using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.OrderNumber)
            .IsUnique();

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.Status);

        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.Discount).HasPrecision(18, 2);
        builder.Property(x => x.ShippingPrice).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);

        builder.Property(x => x.CouponCode).HasMaxLength(50);
        builder.Property(x => x.CouponNominalDiscount).HasPrecision(18, 2);
        builder.Property(x => x.CouponMinPurchaseSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.CouponDiscountApplied).HasPrecision(18, 2);
        builder.Property(x => x.FreeShippingMinSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.FreeShippingStatesSnapshot).HasMaxLength(100);
        builder.Property(x => x.J3PriceSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.ShippingSubsidyAmountSnapshot).HasPrecision(18, 2);

        builder.Property(x => x.ShippingMethodId).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ShippingMethodName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ShippingProvider).HasMaxLength(100).IsRequired();

        builder.Property(x => x.ShipCep).HasMaxLength(8).IsRequired();
        builder.Property(x => x.ShipStreet).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ShipNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ShipComplement).HasMaxLength(100);
        builder.Property(x => x.ShipNeighborhood).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ShipCity).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ShipState).HasMaxLength(2).IsRequired();

        builder.Property(x => x.PaymentMethod).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PaymentStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MercadoPagoOrderId).HasMaxLength(64);
        builder.Property(x => x.MercadoPagoPaymentId).HasMaxLength(64);
        builder.Property(x => x.MercadoPagoPaymentStatus).HasMaxLength(50);
        builder.Property(x => x.PaymentIdempotencyKey).HasMaxLength(64);
        builder.HasIndex(x => x.MercadoPagoOrderId);
        builder.HasIndex(x => x.MercadoPagoPaymentId);

        builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CustomerPhone).HasMaxLength(20);
        builder.Property(x => x.CustomerCpf).HasMaxLength(11);

        builder.Property(x => x.IdempotencyKey).HasMaxLength(64);
        builder.Property(x => x.IdempotencyFingerprint).HasMaxLength(128);

        builder.Property(x => x.RowVersion).IsConcurrencyToken();

        // Pedidos legados podem ter chave nula; novos pedidos exigem chave no fluxo da API
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistory)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
