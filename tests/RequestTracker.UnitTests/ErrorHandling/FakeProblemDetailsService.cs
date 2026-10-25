using Microsoft.AspNetCore.Http;

namespace RequestTracker.UnitTests.ErrorHandling;

public sealed class FakeProblemDetailsService : IProblemDetailsService
{
    public int WriteCount { get; private set; }

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        WriteCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
    {
        WriteCount++;
        return ValueTask.FromResult(true);
    }
}
