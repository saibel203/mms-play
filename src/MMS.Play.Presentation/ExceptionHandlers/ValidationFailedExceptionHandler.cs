using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using MMS.Play.Contracts.Exceptions;

namespace MMS.Play.Presentation.ExceptionHandlers;

/// <summary>
/// Returns <see cref="ValidationFailedException"/> thrown by services as 400 problem details,
/// in the same shape as the automatic model validation response. Handled exceptions are not logged as errors.
/// </summary>
public class ValidationFailedExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationFailedException validationException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new HttpValidationProblemDetails(validationException.Errors.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest
            }
        });
    }
}
