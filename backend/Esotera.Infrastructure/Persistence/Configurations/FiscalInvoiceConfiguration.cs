using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class FiscalInvoiceConfiguration : IEntityTypeConfiguration<FiscalInvoice>
{
    public void Configure(EntityTypeBuilder<FiscalInvoice> builder)
    {
        builder.ToTable("FiscalInvoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ChNFe)
            .HasMaxLength(44);

        builder.Property(x => x.Number)
            .HasMaxLength(16);

        builder.Property(x => x.Series)
            .HasMaxLength(8);

        builder.Property(x => x.Environment)
            .HasMaxLength(8);

        builder.Property(x => x.IssuerCnpj)
            .HasMaxLength(14);

        builder.Property(x => x.RecipientDocument)
            .HasMaxLength(14);

        builder.Property(x => x.ProtocolNumber)
            .HasMaxLength(32);

        builder.Property(x => x.XmlCipher)
            .IsRequired();

        builder.Property(x => x.XmlSha256)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasMaxLength(32)
            .IsRequired();

        // Unique filtrado: mesma chave não pode repetir.
        builder.HasIndex(x => x.ChNFe)
            .IsUnique()
            .HasFilter("\"ChNFe\" IS NOT NULL");

        // No máximo uma NF-e authorized vigente por pedido.
        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter($"\"Status\" = '{FiscalInvoiceStatus.Authorized}'");

        builder.HasIndex(x => x.XmlSha256);

        builder.HasIndex(x => x.OrderId);

        builder.HasOne(x => x.Order)
            .WithMany(o => o.FiscalInvoices)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
