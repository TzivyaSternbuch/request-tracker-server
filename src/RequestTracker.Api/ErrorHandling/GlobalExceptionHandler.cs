using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RequestTracker.Core.Exceptions;

namespace RequestTracker.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The client is gone: nothing can be written, and it isn't a server fault.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {Path} was aborted by the client", httpContext.Request.Path);
            return true;
        }

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

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string? detail) => new() { Status = status, Detail = detail };

    private static ValidationProblemDetails ToValidationProblem(ValidationException exception)
    {
        // Keys use the JSON (camelCase) names, the same as model-binding errors.
        var errors = exception.Errors
            .GroupBy(e => ToJsonPath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest };
    }

    // "Items[0].Version" → "items[0].version": every segment, not only the first character.
    private static string ToJsonPath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
