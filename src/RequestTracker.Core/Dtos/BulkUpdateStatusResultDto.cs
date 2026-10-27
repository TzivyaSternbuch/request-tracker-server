namespace RequestTracker.Core.Dtos;

public record BulkUpdateStatusResultDto(IReadOnlyList<int> Succeeded, IReadOnlyList<BulkFailureDto> Failed);
