using FluentValidation;
using RequestTracker.Core.Abstractions;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Services;

public sealed class RequestService(
    IRequestRepository repository,
    IValidator<RequestQueryParameters> queryValidator) : IRequestService
{
    public async Task<PagedResult<RequestDto>> GetPagedAsync(
        RequestQueryParameters parameters, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(parameters, cancellationToken);
        return await repository.GetPagedAsync(parameters, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken) =>
        repository.GetAssigneesAsync(cancellationToken);
}
