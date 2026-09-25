using IdentityService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();

        builder.HasIndex(x => x.OrganizationId);

        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);

        // Unique across the table, not within an organisation: signing in is email plus password,
        // with nothing to say which organisation was meant. The index is also what makes a second
        // account for the same address fail at the database rather than halfway through a handler.
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);

        // A BCrypt hash is 60 characters today. The column is wider so that raising the work
        // factor, or changing algorithm, is not also a migration.
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(256);

        builder.Property(x => x.Role).IsRequired().HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
