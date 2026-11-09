using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class PaymentReminderDeliveryConfiguration : IEntityTypeConfiguration<PaymentReminderDelivery>
{
    public void Configure(EntityTypeBuilder<PaymentReminderDelivery> builder)
    {
        builder.ToTable("PaymentReminderDeliveries");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SentAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        // no foreign keys: subscribers are never deleted, and the rows are only a log of what was sent.
        // "was this person reminded about this student lately" and "the last reminder about a student"
        builder.HasIndex(x => new { x.SubscriberId, x.StudentId, x.SentAtUtc });
        builder.HasIndex(x => new { x.StudentId, x.SentAtUtc });
    }
}
