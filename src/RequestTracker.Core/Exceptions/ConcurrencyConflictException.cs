namespace RequestTracker.Core.Exceptions;

public sealed class ConcurrencyConflictException(string message) : Exception(message);
