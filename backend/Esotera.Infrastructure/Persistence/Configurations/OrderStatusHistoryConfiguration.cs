using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FromStatus).HasMaxLength(50);
        builder.Property(x => x.ToStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => x.OrderId);
    }
}
