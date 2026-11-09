using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class ParentBroadcastConfiguration : IEntityTypeConfiguration<ParentBroadcast>
{
    public void Configure(EntityTypeBuilder<ParentBroadcast> builder)
    {
        builder.ToTable("ParentBroadcasts");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Text).HasMaxLength(ParentBroadcastLimits.TextMaxLength);
        builder.Property(x => x.CreatedBy).HasMaxLength(128);
        // a few short strings read with the broadcast: a JSON column
        builder.PrimitiveCollection(x => x.Options);
        builder.Ignore(x => x.IsQuestion);

        builder.Property(x => x.CreatedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        builder.HasMany(x => x.Deliveries).WithOne(x => x.Broadcast).HasForeignKey(x => x.BroadcastId).OnDelete(DeleteBehavior.Cascade);

        // the history, latest first
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
