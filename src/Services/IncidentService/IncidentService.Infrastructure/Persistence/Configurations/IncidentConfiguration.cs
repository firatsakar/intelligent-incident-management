using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentService.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(IncidentConstants.TitleMaxLength);

        builder
            .Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(IncidentConstants.DescriptionMaxLength);

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(64);

        builder.Property(x => x.Priority).IsRequired().HasConversion<string>().HasMaxLength(64);

        builder.Property(x => x.Source).IsRequired().HasConversion<string>().HasMaxLength(64);

        builder.Property(x => x.AssignedTeam).HasMaxLength(IncidentConstants.TeamMaxLength);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.UpdatedAt);
        builder.Ignore(x => x.DomainEvents);
    }
}
