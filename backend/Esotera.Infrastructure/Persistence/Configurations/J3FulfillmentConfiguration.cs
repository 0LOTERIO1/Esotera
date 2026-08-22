using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class J3FulfillmentConfiguration : IEntityTypeConfiguration<J3Fulfillment>
{
    public void Configure(EntityTypeBuilder<J3Fulfillment> builder)
    {
        builder.ToTable("J3Fulfillments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.J3OrderId)
            .HasMaxLength(64);

        builder.Property(x => x.J3OrderCode)
            .HasMaxLength(64);

        builder.Property(x => x.J3TrackingNumber)
            .HasMaxLength(64);

        builder.Property(x => x.J3DeliveryPointId)
            .HasMaxLength(64);

        builder.Property(x => x.J3StampUrl)
            .HasMaxLength(500);

        builder.Property(x => x.LastErrorCode)
            .HasMaxLength(64);

        // Status logístico RAW — tamanho alinhado a códigos J3 (não é o Status de integração).
        builder.Property(x => x.J3RemoteStatus)
            .HasMaxLength(64);

        builder.Property(x => x.J3LastStatusSyncErrorCode)
            .HasMaxLength(64);

        // 1:1 Order — UNIQUE
        builder.HasIndex(x => x.OrderId)
            .IsUnique();

        // PostgreSQL: unique filtrado quando J3OrderId preenchido (padrão IdempotencyKey em Order)
        builder.HasIndex(x => x.J3OrderId)
            .IsUnique()
            .HasFilter("\"J3OrderId\" IS NOT NULL");

        builder.HasIndex(x => x.Status);

        // Restrict: não apagar fulfillment (IDs externos) automaticamente com delete de Order.
        builder.HasOne(x => x.Order)
            .WithOne(x => x.J3Fulfillment)
            .HasForeignKey<J3Fulfillment>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
