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

    // Runs while the statistics are being computed, to simulate an update committing at that moment.
    public Func<Task>? DuringStats { get; set; }

    public DateTimeOffset? LastStatsAsOf { get; private set; }

    public async Task<RequestStatsDto> GetStatsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        StatsCalls++;
        LastStatsAsOf = asOf;
        if (DuringStats is not null)
        {
            await DuringStats();
        }

        return Stats;
    }

    public Task<PagedResult<RequestDto>> GetPagedAsync(
        RequestQueryParameters parameters, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public PagedResult<StatusHistoryDto> NextHistory { get; set; } = new([], 1, 10, 0);

    public (int RequestId, StatusHistoryQueryParameters Parameters)? LastHistoryCall { get; private set; }

    public Task<PagedResult<StatusHistoryDto>> GetHistoryAsync(
        int requestId, StatusHistoryQueryParameters parameters, CancellationToken cancellationToken)
    {
        calls.Add("GetHistory");
        LastHistoryCall = (requestId, parameters);
        return Task.FromResult(NextHistory);
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
