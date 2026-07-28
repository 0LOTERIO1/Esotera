using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ShortDescription)
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasMaxLength(10000);

        builder.Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken();

        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.IsFeatured);
        builder.HasIndex(x => x.IsAvailable);
        builder.HasIndex(x => x.IsArchived);

        builder.HasMany(x => x.Images)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
