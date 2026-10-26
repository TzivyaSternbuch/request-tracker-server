using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RequestTracker.Core.Exceptions;

namespace RequestTracker.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => ToValidationProblem(validation),
            NotFoundException => Problem(StatusCodes.Status404NotFound, exception.Message),
            ConcurrencyConflictException => Problem(StatusCodes.Status409Conflict, exception.Message),
            InvalidStatusTransitionException => Problem(StatusCodes.Status422UnprocessableEntity, exception.Message),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, detail: null);
        }

        await TypedResults.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }

    private static ProblemDetails Problem(int status, string? detail) => new() { Status = status, Detail = detail };

    private static ValidationProblemDetails ToValidationProblem(ValidationException exception)
    {
        // Keys use the JSON (camelCase) names, the same as model-binding errors.
        var errors = exception.Errors
            .GroupBy(e => ErrorKeys.ToJsonPath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            // A ValidationException thrown with only a message has no errors; the message is the explanation.
            Detail = errors.Count == 0 ? exception.Message : null,
        };
    }
}
