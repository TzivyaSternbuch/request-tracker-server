using RequestTracker.Core.Abstractions;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;

namespace RequestTracker.UnitTests.Fakes;

public sealed class FakeRequestRepository(List<string> calls) : IRequestRepository
{
    public RequestDto? Current { get; set; }

    public UpdateStatusResult NextResult { get; set; } = new(UpdateStatusOutcome.NotFound, null);

    public TryUpdateStatusCall? LastUpdateCall { get; private set; }

    public RequestStatsDto Stats { get; set; } = new(
        DateTimeOffset.MinValue,
        new Dictionary<RequestStatus, int>(),
        new OpenRequestAgeDto(0, 0, 0),
        []);

    public int StatsCalls { get; private set; }

    public DateTimeOffset? LastStatsAsOf { get; private set; }

    public Task<RequestStatsDto> GetStatsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        StatsCalls++;
        LastStatsAsOf = asOf;
        return Task.FromResult(Stats);
    }

    public Task<PagedResult<RequestDto>> GetPagedAsync(
        RequestQueryParameters parameters, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<RequestDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Current);
    }

    public Task<UpdateStatusResult> TryUpdateStatusAsync(
        int id,
        byte[] expectedVersion,
        RequestStatus previousStatus,
        RequestStatus newStatus,
        string changedBy,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        calls.Add("TryUpdateStatus");
        LastUpdateCall = new TryUpdateStatusCall(id, expectedVersion, previousStatus, newStatus, changedBy, changedAt);
        return Task.FromResult(NextResult);
    }
}
