using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class AdminDigestDeliveryConfiguration : IEntityTypeConfiguration<AdminDigestDelivery>
{
    public void Configure(EntityTypeBuilder<AdminDigestDelivery> builder)
    {
        builder.ToTable("AdminDigestDeliveries");

        // one row per school day: inserting the day's row is what claims the digest
        builder.HasKey(x => x.Date);

        builder.Property(x => x.SentAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
