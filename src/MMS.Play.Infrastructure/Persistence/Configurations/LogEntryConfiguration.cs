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

        builder.HasIndex(x => x.TimeStamp);
    }
}
