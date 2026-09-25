using IdentityService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Role).IsRequired().HasConversion<string>().HasMaxLength(50);

        // A SHA-256 in hex. Unique across the whole table, like the ingest key's: the link is
        // looked up before anyone knows whose organisation it belongs to, so it has to name exactly
        // one row among all of them.
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.InvitedByUserId).IsRequired();
        builder.Property(x => x.AcceptedAt);
        builder.Property(x => x.AcceptedUserId);
        builder.Property(x => x.RevokedAt);

        // The members screen lists an organisation's pending invitations, and a new invitation
        // looks for pending ones to the same address in the same organisation.
        builder.HasIndex(x => new { x.OrganizationId, x.Email });

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
