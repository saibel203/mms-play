using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class LogEntryConfiguration : IEntityTypeConfiguration<LogEntry>
{
    public void Configure(EntityTypeBuilder<LogEntry> builder)
    {
        builder.ToTable("Logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Level).HasMaxLength(128);
        builder.Property(x => x.SourceContext).HasMaxLength(256);

        // the sink writes UTC (convertToUtc), datetime2 has no kind: without it the API would serialize the time without "Z"
        builder.Property(x => x.TimeStamp).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        builder.HasIndex(x => x.TimeStamp);
        // the log page filters by minimum level and orders by time
        builder.HasIndex(x => new { x.Level, x.TimeStamp });
    }
}
