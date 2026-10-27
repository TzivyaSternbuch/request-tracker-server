using FluentValidation;
using Microsoft.Extensions.Logging;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Exceptions;
using RequestTracker.Core.Models;
using RequestTracker.Core.Services;
using RequestTracker.Core.Validation;
using RequestTracker.UnitTests.Fakes;

namespace RequestTracker.UnitTests.Services;

public sealed class RequestServiceUpdateStatusTests
{
    private const int RequestId = 42;
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] CurrentVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly List<string> _calls = [];
    private readonly FakeRequestRepository _repository;
    private readonly FakeHybridCache _cache;
    private readonly CapturingLogger<RequestService> _logger = new();
    private readonly RequestService _service;

    public RequestServiceUpdateStatusTests()
    {
        _repository = new FakeRequestRepository(_calls) { Current = CurrentRow() };
        _cache = new FakeHybridCache(_calls);
        _service = new RequestService(
            _repository,
            new RequestQueryParametersValidator(),
            new UpdateStatusRequestValidator(),
            new BulkUpdateStatusRequestValidator(),
            new StatusHistoryQueryParametersValidator(),
            _cache,
            new FakeServiceScopeFactory(_repository),
            new FixedTimeProvider(Now),
            _logger);
    }

    private static RequestDto CurrentRow()
    {
        return new RequestDto(
            RequestId, "Title", "Org", RequestStatus.New, RequestPriority.Medium, "bob", Now, Now, CurrentVersion);
    }

    private static UpdateStatusRequest ValidRequest()
    {
        return new UpdateStatusRequest
        {
            Status = RequestStatus.InProgress,
            Version = [1, 2, 3, 4, 5, 6, 7, 8],
            ChangedBy = "  alice  "
        };
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidInput_ThrowsValidationExceptionAndWritesNothing()
    {
        var request = ValidRequest() with { Status = null };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateStatusAsync(RequestId, request, CancellationToken.None));

        Assert.Null(_repository.LastUpdateCall);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownId_ThrowsNotFound()
    {
        _repository.Current = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateStatusAsync(RequestId, ValidRequest(), CancellationToken.None));

        Assert.Null(_repository.LastUpdateCall);
    }

    [Fact]
    public async Task UpdateStatusAsync_StaleVersion_ThrowsConflictBeforeTransitionCheck()
    {
        var request = ValidRequest() with { Status = RequestStatus.Completed, Version = [9, 9, 9, 9, 9, 9, 9, 9] };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => _service.UpdateStatusAsync(RequestId, request, CancellationToken.None));

        Assert.Null(_repository.LastUpdateCall);
    }

    [Fact]
    public async Task UpdateStatusAsync_DisallowedTransition_ThrowsInvalidTransition()
    {
        var request = ValidRequest() with { Status = RequestStatus.Completed };

        var exception = await Assert.ThrowsAsync<InvalidStatusTransitionException>(
            () => _service.UpdateStatusAsync(RequestId, request, CancellationToken.None));

        Assert.Contains("New", exception.Message);
        Assert.Contains("Completed", exception.Message);
        Assert.Null(_repository.LastUpdateCall);
    }

    [Fact]
    public async Task UpdateStatusAsync_SameStatus_ThrowsInvalidTransition()
    {
        var request = ValidRequest() with { Status = RequestStatus.New };

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(
            () => _service.UpdateStatusAsync(RequestId, request, CancellationToken.None));

        Assert.Null(_repository.LastUpdateCall);
    }

    [Fact]
    public async Task UpdateStatusAsync_RepositoryConflict_ThrowsConflictAndLogsWarningWithoutChangedBy()
    {
        _repository.NextResult = new UpdateStatusResult(UpdateStatusOutcome.Conflict, null);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => _service.UpdateStatusAsync(RequestId, ValidRequest(), CancellationToken.None));

        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("alice"));
        Assert.DoesNotContain(_calls, c => c.StartsWith("RemoveByTag"));
    }

    [Fact]
    public async Task UpdateStatusAsync_RepositoryNotFound_ThrowsNotFound()
    {
        _repository.NextResult = new UpdateStatusResult(UpdateStatusOutcome.NotFound, null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateStatusAsync(RequestId, ValidRequest(), CancellationToken.None));

        Assert.DoesNotContain(_calls, c => c.StartsWith("RemoveByTag"));
    }

    [Fact]
    public async Task UpdateStatusAsync_Success_ReturnsUpdatedDtoAndInvalidatesStatsAfterWrite()
    {
        var updated = CurrentRow() with { Status = RequestStatus.InProgress, Version = [1, 2, 3, 4, 5, 6, 7, 9] };
        _repository.NextResult = new UpdateStatusResult(UpdateStatusOutcome.Updated, updated);

        var result = await _service.UpdateStatusAsync(RequestId, ValidRequest(), CancellationToken.None);

        Assert.Same(updated, result);
        Assert.Equal(["TryUpdateStatus", "RemoveByTag:stats"], _calls);
    }

    [Fact]
    public async Task UpdateStatusAsync_Success_PassesTrimmedChangedByStatusAndTime()
    {
        var request = ValidRequest();
        _repository.NextResult = new UpdateStatusResult(UpdateStatusOutcome.Updated, CurrentRow());

        await _service.UpdateStatusAsync(RequestId, request, CancellationToken.None);

        var call = Assert.IsType<TryUpdateStatusCall>(_repository.LastUpdateCall);
        Assert.Equal(RequestId, call.Id);
        Assert.Equal("alice", call.ChangedBy);
        Assert.Equal(RequestStatus.New, call.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, call.NewStatus);
        Assert.Equal(request.Version, call.ExpectedVersion);
        Assert.Equal(Now, call.ChangedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_CancelledAfterCommit_StillInvalidatesStats()
    {
        using var requestAborted = new CancellationTokenSource();
        _repository.NextResult = new UpdateStatusResult(UpdateStatusOutcome.Updated, CurrentRow());

        await _service.UpdateStatusAsync(RequestId, ValidRequest(), requestAborted.Token);

        var token = Assert.IsType<CancellationToken>(_cache.LastRemoveByTagToken);
        Assert.False(token.CanBeCanceled);
    }
}
