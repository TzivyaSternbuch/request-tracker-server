using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using RequestTracker.Core.Exceptions;

namespace RequestTracker.IntegrationTests.TestHost;

[ApiController]
[Route("test")]
public sealed class TestController : ControllerBase
{
    public const string NotFoundMessage = "Request 42 was not found.";
    public const string ConflictMessage = "Request 42 was modified by someone else.";
    public const string TransitionMessage = "Request 42 cannot move from Completed to New.";
    public const string ValidationMessage = "Status is required.";
    public const string NestedValidationMessage = "Version is required.";
    public const string RequestLevelValidationMessage = "Between 1 and 100 items are allowed.";
    public const string InternalMessage = "secret-internal-detail";

    [HttpGet("throw/{kind}")]
    public IActionResult Throw(string kind) => kind switch
    {
        "notfound" => throw new NotFoundException(NotFoundMessage),
        "conflict" => throw new ConcurrencyConflictException(ConflictMessage),
        "transition" => throw new InvalidStatusTransitionException(TransitionMessage),
        "validation" => throw new ValidationException([new ValidationFailure("Status", ValidationMessage)]),
        "validation-nested" => throw new ValidationException(
            [new ValidationFailure("Items[0].Version", NestedValidationMessage)]),
        "validation-message" => throw new ValidationException(RequestLevelValidationMessage),
        "validation-unnamed" => throw new ValidationException(
            [new ValidationFailure(null!, RequestLevelValidationMessage)]),
        "aborted" => Abort(),
        _ => throw new InvalidOperationException(InternalMessage),
    };

    private IActionResult Abort()
    {
        HttpContext.Abort();
        throw new OperationCanceledException(HttpContext.RequestAborted);
    }

    [HttpGet("json")]
    public TestPayload GetJson() => new() { SomeValue = "Value", Kind = TestKind.Medium };

    [HttpPost("json")]
    public TestPayload PostJson(TestPayload payload) => payload;
}
