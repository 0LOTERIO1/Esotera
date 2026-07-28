using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StoreName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FreeShippingStatesCsv)
            .HasMaxLength(100);

        builder.Property(x => x.FreeShippingMin).HasPrecision(18, 2);
        builder.Property(x => x.J3Price).HasPrecision(18, 2);
#pragma warning disable CS0618
        builder.Property(x => x.CouponDiscount).HasPrecision(18, 2);
        builder.Property(x => x.CouponMinPurchase).HasPrecision(18, 2);
#pragma warning restore CS0618
        builder.Property(x => x.ShippingSubsidyAmount).HasPrecision(18, 2);
    }
}
