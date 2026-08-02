using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class MelhorEnvioConnectionConfiguration : IEntityTypeConfiguration<MelhorEnvioConnection>
{
    public void Configure(EntityTypeBuilder<MelhorEnvioConnection> builder)
    {
        builder.ToTable("MelhorEnvioConnections");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccessTokenCipher)
            .IsRequired();

        builder.Property(x => x.RefreshTokenCipher)
            .IsRequired();

        builder.Property(x => x.Scopes)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Environment)
            .HasMaxLength(32)
            .IsRequired();
    }
}
