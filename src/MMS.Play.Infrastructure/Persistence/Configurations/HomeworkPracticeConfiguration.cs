using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class HomeworkPracticeConfiguration : IEntityTypeConfiguration<HomeworkPractice>
{
    public void Configure(EntityTypeBuilder<HomeworkPractice> builder)
    {
        builder.ToTable("HomeworkPractices");

        // one mark per homework and day, pressing the button twice adds nothing
        builder.HasKey(x => new { x.HomeworkId, x.Date });

        builder.Property(x => x.PracticedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        builder.HasOne(x => x.Homework).WithMany(x => x.Practices).HasForeignKey(x => x.HomeworkId).OnDelete(DeleteBehavior.Cascade);
    }
}
