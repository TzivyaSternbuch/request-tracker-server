using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Services;

public interface IRequestService
{
    Task<PagedResult<RequestDto>> GetPagedAsync(RequestQueryParameters parameters, CancellationToken cancellationToken);

    Task<RequestDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<RequestDto> UpdateStatusAsync(int id, UpdateStatusRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken);
}
