using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Cep)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(x => x.Street)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Number)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Complement)
            .HasMaxLength(100);

        builder.Property(x => x.Neighborhood)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.State)
            .HasMaxLength(2)
            .IsRequired();

        builder.HasIndex(x => x.UserId);
    }
}
