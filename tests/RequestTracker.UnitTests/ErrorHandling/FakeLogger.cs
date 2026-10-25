using Microsoft.Extensions.Logging;

namespace RequestTracker.UnitTests.ErrorHandling;

public sealed class FakeLogger<T> : ILogger<T>
{
    public List<LogLevel> Levels { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
}
