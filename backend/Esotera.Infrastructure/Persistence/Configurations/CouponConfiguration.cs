using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.MinPurchase).HasPrecision(18, 2);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsArchived);

        builder.HasMany(x => x.Usages)
            .WithOne(x => x.Coupon)
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
