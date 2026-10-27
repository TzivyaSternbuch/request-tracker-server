using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Models;

public record UpdateStatusRequest
{
    // The size of a SQL Server rowversion.
    public const int VersionLength = 8;

    // Nullable so a missing JSON property is reported by the validator instead of silently defaulting.
    public RequestStatus? Status { get; init; }
    public byte[]? Version { get; init; }
    public string? ChangedBy { get; init; }
}
