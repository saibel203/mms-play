using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class RoomHoursConfiguration : IEntityTypeConfiguration<RoomHours>
{
    public void Configure(EntityTypeBuilder<RoomHours> builder)
    {
        builder.ToTable("RoomHours");

        // one row per room and day of the week
        builder.HasKey(x => new { x.RoomId, x.DayOfWeek });
    }
}
