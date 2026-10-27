using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Services;

public interface IRequestService
{
    Task<PagedResult<RequestDto>> GetPagedAsync(RequestQueryParameters parameters, CancellationToken cancellationToken);

    Task<RequestDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<RequestDto> UpdateStatusAsync(int id, UpdateStatusRequest request, CancellationToken cancellationToken);

    Task<PagedResult<StatusHistoryDto>> GetHistoryAsync(
        int requestId, StatusHistoryQueryParameters parameters, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken);

    Task<RequestStatsDto> GetStatsAsync(CancellationToken cancellationToken);
}
