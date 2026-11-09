using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Homework;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Homeworks;

public class HomeworkService(ApplicationDbContext context, IHomeworkNotifier notifier, SchoolClock clock) : IHomeworkService
{
    public async Task<IReadOnlyList<HomeworkDto>> ListAsync(HomeworkListQuery query, CancellationToken cancellationToken = default)
    {
        var homework = context.Homeworks.AsQueryable();
        if (query.StudentId is { } studentId)
            homework = homework.Where(x => x.StudentId == studentId);
        if (query.Status is { } status)
            homework = homework.Where(x => x.Status == status);

        var rows = await HomeworkRow.ListAsync(
            homework.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAtUtc),
            cancellationToken);

        var today = clock.Today;
        return [.. rows.Select(row => row.ToDto(today))];
    }

    public async Task<HomeworkDto> CreateAsync(HomeworkInput input, CancellationToken cancellationToken = default)
    {
        var values = await ValidateAsync(input, cancellationToken);

        var homework = new Homework
        {
            Id = Guid.CreateVersion7(),
            Title = values.Title,
            Status = HomeworkStatus.Active,
            CreatedAtUtc = clock.UtcNow
        };
        values.ApplyTo(homework);

        context.Homeworks.Add(homework);
        await context.SaveChangesAsync(cancellationToken);

        // the homework is stored either way, the student also finds it with /homework
        await notifier.NotifyAssignedAsync(homework.Id, cancellationToken);

        return (await HomeworkRow.FindAsync(context, homework.Id, cancellationToken))!.ToDto(clock.Today);
    }

    public async Task<HomeworkDto?> UpdateAsync(Guid id, HomeworkInput input, CancellationToken cancellationToken = default)
    {
        var homework = await context.Homeworks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (homework is null)
            return null;

        var values = await ValidateAsync(input, cancellationToken);
        values.ApplyTo(homework);
        await context.SaveChangesAsync(cancellationToken);

        return (await HomeworkRow.FindAsync(context, id, cancellationToken))!.ToDto(clock.Today);
    }

    public async Task<HomeworkDto?> SetStatusAsync(Guid id, HomeworkStatus status, CancellationToken cancellationToken = default)
    {
        var homework = await context.Homeworks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (homework is null)
            return null;

        if (homework.Status != status)
        {
            homework.Status = status;
            homework.CompletedAtUtc = status == HomeworkStatus.Completed ? clock.UtcNow : null;
            await context.SaveChangesAsync(cancellationToken);
        }

        return (await HomeworkRow.FindAsync(context, id, cancellationToken))!.ToDto(clock.Today);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        // the practice marks go with it (cascade)
        await context.Homeworks.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>
    /// Checks the input against the database too: the student and the score must exist.
    /// </summary>
    private async Task<HomeworkValues> ValidateAsync(HomeworkInput input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var student = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(x => x.Id == input.StudentId)
            .Select(x => new { x.IsActive, x.IsParent })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
            errors[nameof(HomeworkInput.StudentId)] = ["The student does not exist."];
        else if (student.IsParent)
            errors[nameof(HomeworkInput.StudentId)] = ["This subscriber is marked as a parent; give homework to the child."];
        else if (!student.IsActive)
            errors[nameof(HomeworkInput.StudentId)] = ["The student is not subscribed in the bot (pressed Stop or never pressed Start)."];

        string? scoreTitle = null;
        if (input.ScoreId is { } scoreId)
        {
            scoreTitle = await context.Scores.Where(x => x.Id == scoreId).Select(x => x.Title).FirstOrDefaultAsync(cancellationToken);
            if (scoreTitle is null)
                errors[nameof(HomeworkInput.ScoreId)] = ["The score does not exist (anymore)."];
        }

        var title = Normalize(input.Title) ?? scoreTitle;
        if (title is null)
            errors[nameof(HomeworkInput.Title)] = ["Give a title, or pick a score from the library."];
        else if (title.Length > HomeworkDto.TitleMaxLength)
            errors[nameof(HomeworkInput.Title)] = [$"The title exceeds {HomeworkDto.TitleMaxLength} characters."];

        var instructions = Normalize(input.Instructions?.Replace("\r\n", "\n"));
        if (instructions?.Length > HomeworkDto.InstructionsMaxLength)
            errors[nameof(HomeworkInput.Instructions)] = [$"The instructions exceed {HomeworkDto.InstructionsMaxLength} characters."];

        if (input.FromBar is < 1 or > HomeworkDto.MaxBar)
            errors[nameof(HomeworkInput.FromBar)] = [$"Bars are 1 to {HomeworkDto.MaxBar}."];
        if (input.ToBar is < 1 or > HomeworkDto.MaxBar)
            errors[nameof(HomeworkInput.ToBar)] = [$"Bars are 1 to {HomeworkDto.MaxBar}."];
        else if (input is { FromBar: { } firstBar, ToBar: { } lastBar } && firstBar > lastBar)
            errors[nameof(HomeworkInput.ToBar)] = ["The last bar comes before the first one."];

        var tempoRange = $"The tempo is {HomeworkDto.MinTempoPercent} to {HomeworkDto.MaxTempoPercent} percent of the original.";
        if (input.StartTempoPercent is < HomeworkDto.MinTempoPercent or > HomeworkDto.MaxTempoPercent)
            errors[nameof(HomeworkInput.StartTempoPercent)] = [tempoRange];
        if (input.TargetTempoPercent is < HomeworkDto.MinTempoPercent or > HomeworkDto.MaxTempoPercent)
            errors[nameof(HomeworkInput.TargetTempoPercent)] = [tempoRange];
        else if (input is { StartTempoPercent: { } start, TargetTempoPercent: { } target } && start > target)
            errors[nameof(HomeworkInput.TargetTempoPercent)] = ["The target tempo is below the starting one."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        return new HomeworkValues(input, title!, instructions);
    }

    /// <returns>The trimmed text, null when empty.</returns>
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record HomeworkValues(HomeworkInput Input, string Title, string? Instructions)
    {
        public void ApplyTo(Homework homework)
        {
            homework.StudentId = Input.StudentId;
            homework.ScoreId = Input.ScoreId;
            homework.Title = Title;
            homework.Instructions = Instructions;
            homework.FromBar = Input.FromBar;
            homework.ToBar = Input.ToBar;
            homework.StartTempoPercent = Input.StartTempoPercent;
            homework.TargetTempoPercent = Input.TargetTempoPercent;
            homework.DueDate = Input.DueDate;
        }
    }
}
