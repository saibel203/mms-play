using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class HomeworkReminderDeliveryConfiguration : IEntityTypeConfiguration<HomeworkReminderDelivery>
{
    public void Configure(EntityTypeBuilder<HomeworkReminderDelivery> builder)
    {
        builder.ToTable("HomeworkReminderDeliveries");

        // one reminder per person and day; no foreign key, the rows are only a log of what was sent
        builder.HasKey(x => new { x.SubscriberId, x.Date });

        builder.Property(x => x.SentAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
