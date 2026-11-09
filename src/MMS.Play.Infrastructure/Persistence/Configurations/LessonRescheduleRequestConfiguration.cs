using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class LessonRescheduleRequestConfiguration : IEntityTypeConfiguration<LessonRescheduleRequest>
{
    public void Configure(EntityTypeBuilder<LessonRescheduleRequest> builder)
    {
        builder.ToTable("LessonRescheduleRequests");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        // the requests go with their lesson
        builder.HasOne(x => x.Lesson).WithMany(x => x.RescheduleRequests).HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ProposedRoom).WithMany().HasForeignKey(x => x.ProposedRoomId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.DecidedBy).HasMaxLength(128);

        // a short list read and written with the request, JSON keeps it in the same row
        builder.OwnsMany(x => x.AdminMessages, messages => messages.ToJson());

        builder.Property(x => x.CreatedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.Property(x => x.DecidedAtUtc).HasConversion(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

        // the pending request of a lesson; the declined ones to cross out
        builder.HasIndex(x => new { x.LessonId, x.Status });
    }
}
