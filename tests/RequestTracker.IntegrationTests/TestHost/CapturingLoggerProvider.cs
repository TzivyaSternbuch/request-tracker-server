using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RequestTracker.IntegrationTests.TestHost;

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<LogLevel> Levels { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Levels);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<LogLevel> levels) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => levels.Enqueue(logLevel);
    }
}
