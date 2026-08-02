using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esotera.Infrastructure.Persistence.Configurations;

public class MelhorEnvioOAuthStateConfiguration : IEntityTypeConfiguration<MelhorEnvioOAuthState>
{
    public void Configure(EntityTypeBuilder<MelhorEnvioOAuthState> builder)
    {
        builder.ToTable("MelhorEnvioOAuthStates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StateHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(x => x.StateHash)
            .IsUnique();

        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
