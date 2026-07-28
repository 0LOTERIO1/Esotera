using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class CouponUsageConfiguration : IEntityTypeConfiguration<CouponUsage>
{
    public void Configure(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.CouponId, x.UserId })
            .IsUnique();

        builder.HasIndex(x => x.OrderId);
    }
}
