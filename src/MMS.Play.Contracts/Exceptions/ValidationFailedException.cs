namespace MMS.Play.Contracts.Exceptions;

/// <summary>
/// Input rejected by a service. The API returns it as 400 validation problem details,
/// <see cref="Errors"/> keys are the names of the invalid request fields.
/// </summary>
public class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public ValidationFailedException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
