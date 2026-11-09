using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class LessonReminderDeliveryConfiguration : IEntityTypeConfiguration<LessonReminderDelivery>
{
    public void Configure(EntityTypeBuilder<LessonReminderDelivery> builder)
    {
        builder.ToTable("LessonReminderDeliveries");

        // at most one reminder per lesson, recipient and slot, also when two job runs overlap.
        // No foreign key to the subscriber: subscribers are never deleted, the lesson cascade is enough
        builder.HasKey(x => new { x.LessonId, x.SubscriberId, x.SlotUtc });

        builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.SlotUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.Property(x => x.SentAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
