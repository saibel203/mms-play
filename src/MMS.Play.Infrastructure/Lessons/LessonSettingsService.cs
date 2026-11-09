using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

public class LessonSettingsService(
    ApplicationDbContext context,
    SchoolSettingsDefaults defaults,
    IOptions<LessonSettings> lessonOptions) : ILessonSettingsService
{
    public async Task<LessonSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var saved = await SchoolSettingsDefaults.FindAsync(context, track: false, cancellationToken);

        return saved is not null
            ? ToDto(saved)
            : new LessonSettingsDto(lessonOptions.Value.DefaultLessonPrice, lessonOptions.Value.TimeZoneId, IsDefault: true, UpdatedAt: null);
    }

    public async Task<LessonSettingsDto> UpdateAsync(LessonSettingsUpdate update, CancellationToken cancellationToken = default)
    {
        if (update.DefaultLessonPrice is < 0 or > LessonDto.MaxPrice)
            throw new ValidationFailedException(
                nameof(LessonSettingsUpdate.DefaultLessonPrice), $"The price must be between 0 and {LessonDto.MaxPrice:0}.");

        // the notification columns keep what they have (or get their defaults on the first save)
        var settings = await defaults.GetOrAddAsync(context, cancellationToken);
        settings.DefaultLessonPrice = update.DefaultLessonPrice;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private LessonSettingsDto ToDto(SchoolSettings settings) =>
        new(settings.DefaultLessonPrice, lessonOptions.Value.TimeZoneId, IsDefault: false, settings.UpdatedAtUtc);
}
