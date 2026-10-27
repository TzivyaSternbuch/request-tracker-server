namespace RequestTracker.Core.Models;

// Nullable so a missing JSON property is reported by the validator instead of silently defaulting.
public record BulkStatusItem
{
    public int? Id { get; init; }
    public byte[]? Version { get; init; }
}
