using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class SchoolSettingsConfiguration : IEntityTypeConfiguration<SchoolSettings>
{
    public void Configure(EntityTypeBuilder<SchoolSettings> builder)
    {
        builder.ToTable("SchoolSettings");

        builder.HasKey(x => x.Id);
        // a single row with a fixed id
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.DefaultLessonPrice).HasPrecision(10, 2);
        // a primitive collection, stored as a JSON array: ["09:00:00","13:00:00"]
        builder.PrimitiveCollection(x => x.ReminderTimes);
        builder.Property(x => x.PaymentLink).HasMaxLength(NotificationSettingsDto.PaymentLinkMaxLength);
        builder.Property(x => x.PaymentDetails).HasMaxLength(NotificationSettingsDto.PaymentDetailsMaxLength);
        builder.Property(x => x.UpdatedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
