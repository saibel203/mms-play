using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class HomeworkConfiguration : IEntityTypeConfiguration<Homework>
{
    // datetime2 has no kind, without it the API would serialize the time without "Z" and clients would read it as local
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<Homework> builder)
    {
        builder.ToTable("Homeworks");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(HomeworkDto.TitleMaxLength);
        builder.Property(x => x.Instructions).HasMaxLength(HomeworkDto.InstructionsMaxLength);
        builder.Property(x => x.CreatedAtUtc).HasConversion(UtcConverter);
        builder.Property(x => x.CompletedAtUtc).HasConversion(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

        // subscribers are never deleted; homework goes with its student anyway
        builder.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        // deleting a score from the library keeps the homework, described by its title and instructions
        builder.HasOne(x => x.Score).WithMany().HasForeignKey(x => x.ScoreId).OnDelete(DeleteBehavior.SetNull);

        // a student's homework; the reminders read the active ones of everybody
        builder.HasIndex(x => new { x.StudentId, x.Status });
        builder.HasIndex(x => x.Status);
    }
}
