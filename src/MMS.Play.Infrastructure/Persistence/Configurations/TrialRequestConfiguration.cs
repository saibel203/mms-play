using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MMS.Play.Contracts.Models.Trial;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class TrialRequestConfiguration : IEntityTypeConfiguration<TrialRequest>
{
    // datetime2 has no kind, without it the API would serialize the time without "Z" and clients would read it as local
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(value => value, value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

    public void Configure(EntityTypeBuilder<TrialRequest> builder)
    {
        builder.ToTable("TrialRequests");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(TrialRequestInput.NameMaxLength);
        builder.Property(x => x.Contact).HasMaxLength(TrialRequestInput.ContactMaxLength);
        builder.Property(x => x.Comment).HasMaxLength(TrialRequestInput.CommentMaxLength);
        builder.Property(x => x.CreatedAtUtc).HasConversion(UtcConverter);
        builder.Property(x => x.DecidedAtUtc).HasConversion(NullableUtcConverter);

        // subscribers are never deleted; SQL Server would also refuse a second cascade path (subscriber -> lesson -> request)
        builder.HasOne(x => x.Subscriber).WithMany().HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.NoAction);
        // a deleted lesson leaves the request booked, without the link
        builder.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.SetNull);

        // the admin list by status, newest first; the bot's "already asked" check by subscriber
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.SubscriberId, x.Status });
    }
}
