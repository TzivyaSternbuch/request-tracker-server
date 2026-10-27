using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Abstractions;

public interface IRequestRepository
{
    Task<PagedResult<RequestDto>> GetPagedAsync(RequestQueryParameters parameters, CancellationToken cancellationToken);

    Task<RequestDto?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, RequestDto>> GetByIdsAsync(
        IReadOnlyCollection<int> ids, CancellationToken cancellationToken);

    Task<PagedResult<StatusHistoryDto>> GetHistoryAsync(
        int requestId, StatusHistoryQueryParameters parameters, CancellationToken cancellationToken);

    Task<UpdateStatusResult> TryUpdateStatusAsync(
        int id,
        byte[] expectedVersion,
        RequestStatus previousStatus,
        RequestStatus newStatus,
        string changedBy,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken);

    Task<RequestStatsDto> GetStatsAsync(DateTimeOffset asOf, CancellationToken cancellationToken);
}
