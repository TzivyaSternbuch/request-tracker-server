using RequestTracker.Core.Dtos;

namespace RequestTracker.Core.Models;

// Request is set only when Outcome is Updated: it carries the new version the client needs.
public record UpdateStatusResult(UpdateStatusOutcome Outcome, RequestDto? Request);
