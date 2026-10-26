using System.Linq.Expressions;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Entities;

namespace RequestTracker.Core.Mapping;

public static class RequestMapping
{
    public static readonly Expression<Func<Request, RequestDto>> ToDto = r => new RequestDto(
        r.Id,
        r.Title,
        r.OrganizationName,
        r.Status,
        r.Priority,
        r.AssignedTo,
        r.CreatedAt,
        r.UpdatedAt,
        r.Version);
}
