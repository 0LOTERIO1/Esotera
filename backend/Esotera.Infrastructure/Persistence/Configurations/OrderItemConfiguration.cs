using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.Property(x => x.Variation).HasMaxLength(100);
        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
    }
}
