using IdentityService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();

        // A SHA-256 in hex: 64 characters, always.
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);

        // Every refresh is a lookup by this value and nothing else, so it carries the only index
        // that is on the request path. Unique because two rows for one token would mean the reuse
        // check had two answers to choose between.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.ExpiresAt).IsRequired();

        builder.Property(x => x.RevokedAt);

        builder.Property(x => x.ReplacedByHash).HasMaxLength(64);

        // Reuse detection ends every token a user still holds, and the cleanup sweep reads by
        // expiry. Both walk this index instead of the table.
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
