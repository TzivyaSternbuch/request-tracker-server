using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace RequestTracker.Api.ErrorHandling;

internal static class InvalidModelStateResponse
{
    // Binding errors on query parameters are keyed by the C# property name ("SortBy"). Clients read the
    // camelCase name they sent, which is also how validator and JSON body errors are keyed.
    public static IActionResult Create(ActionContext context)
    {
        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, context.ModelState);

        var errors = problem.Errors
            .GroupBy(e => ErrorKeys.ToJsonPath(e.Key))
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.Value).ToArray());
        problem.Errors.Clear();
        foreach (var (key, messages) in errors)
        {
            problem.Errors.Add(key, messages);
        }

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
