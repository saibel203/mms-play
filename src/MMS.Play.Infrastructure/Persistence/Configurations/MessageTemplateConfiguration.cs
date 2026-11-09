using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("MessageTemplates");

        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(64).IsUnicode(false);

        builder.Property(x => x.Text).HasMaxLength(MessageTemplateDto.TextMaxLength);
        // datetime2 has no kind, without it the API would serialize the time without "Z" and clients would read it as local
        builder.Property(x => x.UpdatedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
