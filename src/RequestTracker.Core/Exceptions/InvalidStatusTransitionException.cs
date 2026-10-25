namespace RequestTracker.Core.Exceptions;

public sealed class InvalidStatusTransitionException(string message) : Exception(message);
