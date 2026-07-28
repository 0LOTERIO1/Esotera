using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SecureUrl)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.PublicId)
            .HasMaxLength(300);

        builder.Property(x => x.AltText)
            .HasMaxLength(300);

        builder.HasIndex(x => new { x.ProductId, x.SortOrder });
        builder.HasIndex(x => x.PublicId);
    }
}
