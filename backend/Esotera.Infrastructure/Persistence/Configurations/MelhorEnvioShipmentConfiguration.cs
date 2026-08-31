using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class MelhorEnvioShipmentConfiguration : IEntityTypeConfiguration<MelhorEnvioShipment>
{
    public void Configure(EntityTypeBuilder<MelhorEnvioShipment> builder)
    {
        builder.ToTable("MelhorEnvioShipments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Environment)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ServiceName).HasMaxLength(100);
        builder.Property(x => x.CarrierName).HasMaxLength(100);
        builder.Property(x => x.SelectedDisplayName).HasMaxLength(100);

        builder.Property(x => x.QuotedPrice).HasPrecision(18, 2);
        builder.Property(x => x.ChargedFreightPrice).HasPrecision(18, 2);

        builder.Property(x => x.MelhorEnvioShipmentId).HasMaxLength(64);
        builder.Property(x => x.MelhorEnvioProtocol).HasMaxLength(64);
        builder.Property(x => x.TrackingCode).HasMaxLength(64);
        builder.Property(x => x.TrackingUrl).HasMaxLength(500);
        builder.Property(x => x.LabelUrl).HasMaxLength(500);

        builder.Property(x => x.LastSyncErrorCode).HasMaxLength(64);
        builder.Property(x => x.LastSyncErrorMessage).HasMaxLength(500);

        // 1:1 Order — UNIQUE
        builder.HasIndex(x => x.OrderId)
            .IsUnique();

        // PostgreSQL: unique filtrado quando o ID remoto existe — impede dois envios locais
        // apontando para o mesmo envio no Melhor Envio.
        builder.HasIndex(x => x.MelhorEnvioShipmentId)
            .IsUnique()
            .HasFilter("\"MelhorEnvioShipmentId\" IS NOT NULL");

        builder.HasIndex(x => x.Status);

        // Restrict: não apagar envio (IDs externos) automaticamente com delete de Order.
        builder.HasOne(x => x.Order)
            .WithOne(x => x.MelhorEnvioShipment)
            .HasForeignKey<MelhorEnvioShipment>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
