using FluentValidation;
using RequestTracker.Core.Abstractions;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Exceptions;
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

    public async Task<RequestDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var request = await repository.GetByIdAsync(id, cancellationToken);
        return request ?? throw new NotFoundException($"Request {id} was not found.");
    }
}
