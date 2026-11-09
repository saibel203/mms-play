using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("Invites");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code).HasMaxLength(32).IsUnicode(false);
        // /start finds the invite by its code
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Label).HasMaxLength(InviteDto.LabelMaxLength);

        // short lists read with the invite: JSON columns
        builder.PrimitiveCollection(x => x.ChildIds);
        builder.PrimitiveCollection(x => x.UsedByIds);

        builder.Property(x => x.CreatedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.Property(x => x.ExpiresAtUtc).HasConversion(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);
    }
}
