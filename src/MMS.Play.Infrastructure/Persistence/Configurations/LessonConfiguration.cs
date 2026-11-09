using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    // datetime2 has no kind, without it the API would serialize the time without "Z" and clients would read it as local
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Note).HasMaxLength(LessonDto.NoteMaxLength);
        builder.Property(x => x.Price).HasPrecision(10, 2);
        builder.Property(x => x.PaidAtUtc).HasConversion(UtcConverter);
        builder.Property(x => x.CreatedAtUtc).HasConversion(UtcConverter);

        // rooms and teachers with lessons are not deleted, their history would be lost
        builder.Property(x => x.CancelledByName).HasMaxLength(128);
        builder.Property(x => x.CancellationReason).HasMaxLength(LessonCancellationDto.ReasonMaxLength);
        builder.Property(x => x.CancelledAtUtc).HasConversion(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        // the lesson and its payment stay when the subscriber row goes
        builder.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.SetNull);
        // SQL Server refuses SET NULL on a self reference (a cycle), the service clears it before a delete
        builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.RescheduledToId).OnDelete(DeleteBehavior.NoAction);

        // the week view, statistics and reminders read by date; clash checks by date and room / teacher / student
        builder.HasIndex(x => new { x.Date, x.StartTime });
        builder.HasIndex(x => new { x.RoomId, x.Date });
        builder.HasIndex(x => new { x.TeacherId, x.Date });
        builder.HasIndex(x => new { x.StudentId, x.Date });
        // "this and the following lessons" of a series
        builder.HasIndex(x => new { x.SeriesId, x.Date });
    }
}
