using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MMS.Play.Infrastructure.Persistence.Entities;
using MMS.Play.Infrastructure.Telegram;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class TelegramSubscriberConfiguration : IEntityTypeConfiguration<TelegramSubscriber>
{
    // datetime2 has no kind, without it the API would serialize the time without "Z" and clients would read it as local
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<TelegramSubscriber> builder)
    {
        builder.ToTable("TelegramSubscribers");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Telegram limits: first/last name 64, username 32 characters
        builder.Property(x => x.FirstName).HasMaxLength(64);
        builder.Property(x => x.LastName).HasMaxLength(64);
        builder.Property(x => x.Username).HasMaxLength(32);
        builder.Property(x => x.LanguageCode).HasMaxLength(16).IsUnicode(false);

        builder.Property(x => x.SubscribedAtUtc).HasConversion(UtcConverter);
        builder.Property(x => x.UnsubscribedAtUtc).HasConversion(UtcConverter);
        builder.Property(x => x.LastNotifiedAtUtc).HasConversion(UtcConverter);

        // URL-safe random string, looked up by the anonymous calendar and practice endpoints
        builder.Property(x => x.AccessToken).HasMaxLength(TelegramSubscriberAccessToken.MaxLength).IsUnicode(false);
        builder.HasIndex(x => x.AccessToken).IsUnique().HasFilter("[AccessToken] IS NOT NULL");

        // one row per Telegram user, /start updates it
        builder.HasIndex(x => x.TelegramUserId).IsUnique();
        // reminders read active subscribers in TelegramUserId order (keyset paging)
        builder.HasIndex(x => new { x.IsActive, x.TelegramUserId });

        // parent <-> child, both are subscribers. Subscribers are never deleted, and SQL Server refuses
        // two cascading paths from one table anyway, so the links are not cascaded
        builder.HasMany(x => x.Children)
            .WithMany(x => x.Parents)
            .UsingEntity<Dictionary<string, object>>(
                "TelegramSubscriberChildren",
                link => link.HasOne<TelegramSubscriber>().WithMany().HasForeignKey("ChildId").OnDelete(DeleteBehavior.NoAction),
                link => link.HasOne<TelegramSubscriber>().WithMany().HasForeignKey("ParentId").OnDelete(DeleteBehavior.NoAction),
                link => link.HasKey("ParentId", "ChildId"));
    }
}
