using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Play.Contracts.Models.Scores;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence.Configurations;

public class ScoreConfiguration : IEntityTypeConfiguration<Score>
{
    public void Configure(EntityTypeBuilder<Score> builder)
    {
        builder.ToTable("Scores");

        builder.HasKey(x => x.Id);
        // ids are generated in code as version 7 GUIDs
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(ScoreUpload.TitleMaxLength);
        builder.Property(x => x.Artist).HasMaxLength(ScoreUpload.ArtistMaxLength);
        builder.Property(x => x.FileName).HasMaxLength(ScoreUpload.FileNameMaxLength);
        builder.Property(x => x.Extension).HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.StoragePath).HasMaxLength(512);
        // datetime2 has no kind, without it the API would serialize the time without "Z" and clients would read it as local
        builder.Property(x => x.UploadedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        // duplicate detection on upload
        builder.HasIndex(x => x.Sha256).IsUnique();
        // default list order
        builder.HasIndex(x => x.UploadedAtUtc);
    }
}
