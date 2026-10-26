using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Services;

public interface IRequestService
{
    Task<PagedResult<RequestDto>> GetPagedAsync(RequestQueryParameters parameters, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken);
}
