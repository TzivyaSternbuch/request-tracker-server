using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RequestTracker.Api.ErrorHandling;

namespace RequestTracker.UnitTests.ErrorHandling;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_RequestAborted_DoesNotLogError()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        var logger = new FakeLogger<GlobalExceptionHandler>();
        var problemDetails = new FakeProblemDetailsService();
        var handler = new GlobalExceptionHandler(problemDetails, logger);

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
        Assert.Equal(0, problemDetails.WriteCount);
    }
}
