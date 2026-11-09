using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class ParentBroadcastDeliveryConfiguration : IEntityTypeConfiguration<ParentBroadcastDelivery>
{
    public void Configure(EntityTypeBuilder<ParentBroadcastDelivery> builder)
    {
        builder.ToTable("ParentBroadcastDeliveries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);

        // the lesson is a copy, not a reference: deleting a lesson keeps what the parent was told
        builder.Property(x => x.ChildName).HasMaxLength(256);
        builder.Property(x => x.Answer).HasMaxLength(ParentBroadcastLimits.AnswerMaxLength);
        builder.Property(x => x.Error).HasMaxLength(512);

        builder.Property(x => x.SentAtUtc).HasConversion(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);
        builder.Property(x => x.AnsweredAtUtc).HasConversion(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

        // a reply to the question message finds its delivery; the latest open question of a chat
        builder.HasIndex(x => new { x.ChatId, x.MessageId });
    }
}
