using IncidentService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentService.Infrastructure.Persistence.Configurations;

public sealed class IncidentActivityConfiguration : IEntityTypeConfiguration<IncidentActivity>
{
    public void Configure(EntityTypeBuilder<IncidentActivity> builder)
    {
        builder.ToTable("incident_activities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId).IsRequired();

        // The relationship is also what makes EF insert a new incident before its "opened" row
        // when both are in one save.
        builder
            .HasOne<Incident>()
            .WithMany()
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Every read is one incident's rows in time order.
        builder.HasIndex(x => new { x.IncidentId, x.CreatedAt });

        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(x => x.ActorKind).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(x => x.ActorName).HasMaxLength(IncidentActivity.ActorNameMaxLength);

        builder.Property(x => x.From).HasMaxLength(IncidentActivity.ValueMaxLength);

        builder.Property(x => x.To).HasMaxLength(IncidentActivity.ValueMaxLength);

        builder.Property(x => x.Verdict).HasConversion<string>().HasMaxLength(32);

        builder.Property(x => x.Text).HasMaxLength(IncidentActivity.TextMaxLength);

        builder.Property(x => x.CreatedAt).IsRequired();

        // Never updated; the column would only ever hold null.
        builder.Ignore(x => x.UpdatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
