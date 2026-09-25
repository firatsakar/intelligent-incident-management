using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.Property(x => x.OrganizationId).IsRequired();

        // "00-{32 hex}-{16 hex}-{2 hex}": the only form Activity.Id takes in W3C format.
        builder.Property(x => x.TraceParent).HasMaxLength(55);
        builder.HasIndex(x => x.ProcessedOn);
    }
}
