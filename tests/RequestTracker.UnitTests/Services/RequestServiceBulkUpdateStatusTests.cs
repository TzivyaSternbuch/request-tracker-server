using FluentValidation;
using Microsoft.Extensions.Logging;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;
using RequestTracker.Core.Services;
using RequestTracker.Core.Validation;
using RequestTracker.UnitTests.Fakes;

namespace RequestTracker.UnitTests.Services;

public sealed class RequestServiceBulkUpdateStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] CurrentVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] StaleVersion = [9, 9, 9, 9, 9, 9, 9, 9];

    private readonly List<string> _calls = [];
    private readonly FakeRequestRepository _repository;
    private readonly CapturingLogger<RequestService> _logger = new();
    private readonly RequestService _service;

    public RequestServiceBulkUpdateStatusTests()
    {
        _repository = new FakeRequestRepository(_calls);
        _service = new RequestService(
            _repository,
            new RequestQueryParametersValidator(),
            new UpdateStatusRequestValidator(),
            new BulkUpdateStatusRequestValidator(),
            new StatusHistoryQueryParametersValidator(),
            new FakeHybridCache(_calls),
            new FakeServiceScopeFactory(_repository),
            new FixedTimeProvider(Now),
            _logger);
    }

    private static RequestDto Row(int id, RequestStatus status = RequestStatus.New) =>
        new(id, "Title", "Org", status, RequestPriority.Medium, "bob", Now, Now, CurrentVersion);

    private static BulkStatusItem Item(int id, byte[]? version = null) =>
        new() { Id = id, Version = version ?? CurrentVersion };

    private static BulkUpdateStatusRequest Request(params BulkStatusItem[] items) => new()
    {
        Items = [.. items],
        Status = RequestStatus.InProgress,
        ChangedBy = "  alice  ",
    };

    private void ArrangeRows(params RequestDto[] rows)
    {
        foreach (var row in rows)
        {
            _repository.Rows[row.Id] = row;
        }

        _repository.NextResult = new UpdateStatusResult(UpdateStatusOutcome.Updated, Row(0));
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_MixedOutcomes_FillsSucceededAndFailed()
    {
        // Row 4 is already InProgress, so the target status is not an allowed transition for it.
        ArrangeRows(Row(1), Row(2), Row(4, RequestStatus.InProgress), Row(5));
        _repository.ResultsById[5] = new UpdateStatusResult(UpdateStatusOutcome.Conflict, null);
        var request = Request(Item(1), Item(2, StaleVersion), Item(99), Item(4), Item(5));

        var result = await _service.BulkUpdateStatusAsync(request, CancellationToken.None);

        Assert.Equal([1], result.Succeeded);
        Assert.Equal(
            [
                new BulkFailureDto(2, BulkFailureReason.Conflict),
                new BulkFailureDto(99, BulkFailureReason.NotFound),
                new BulkFailureDto(4, BulkFailureReason.InvalidTransition),
                new BulkFailureDto(5, BulkFailureReason.Conflict),
            ],
            result.Failed);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_RowDeletedAfterRead_ReportsNotFound()
    {
        ArrangeRows(Row(1));
        _repository.ResultsById[1] = new UpdateStatusResult(UpdateStatusOutcome.NotFound, null);

        var result = await _service.BulkUpdateStatusAsync(Request(Item(1)), CancellationToken.None);

        Assert.Equal([new BulkFailureDto(1, BulkFailureReason.NotFound)], result.Failed);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_StaleVersionAndInvalidTransition_ReportsConflict()
    {
        ArrangeRows(Row(1));
        var request = Request(Item(1, StaleVersion)) with { Status = RequestStatus.Completed };

        var result = await _service.BulkUpdateStatusAsync(request, CancellationToken.None);

        Assert.Equal([new BulkFailureDto(1, BulkFailureReason.Conflict)], result.Failed);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_ManyItems_ReadsRowsOnceAndWritesOnlyValidItems()
    {
        ArrangeRows(Row(1), Row(2));

        await _service.BulkUpdateStatusAsync(Request(Item(1), Item(2), Item(3)), CancellationToken.None);

        Assert.Equal(1, _repository.GetByIdsCalls);
        Assert.Equal([1, 2], _repository.UpdateCalls.Select(c => c.Id));
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_Success_PassesTrimmedChangedByPreviousStatusAndTime()
    {
        ArrangeRows(Row(1));

        await _service.BulkUpdateStatusAsync(Request(Item(1)), CancellationToken.None);

        var call = Assert.Single(_repository.UpdateCalls);
        Assert.Equal("alice", call.ChangedBy);
        Assert.Equal(RequestStatus.New, call.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, call.NewStatus);
        Assert.Equal(CurrentVersion, call.ExpectedVersion);
        Assert.Equal(Now, call.ChangedAt);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_SeveralSucceed_InvalidatesStatsOnceAfterTheWrites()
    {
        ArrangeRows(Row(1), Row(2));

        await _service.BulkUpdateStatusAsync(Request(Item(1), Item(2)), CancellationToken.None);

        Assert.Equal(["TryUpdateStatus", "TryUpdateStatus", "RemoveByTag:stats"], _calls);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_NothingSucceeds_DoesNotInvalidateStats()
    {
        ArrangeRows(Row(1));

        await _service.BulkUpdateStatusAsync(Request(Item(1, StaleVersion), Item(99)), CancellationToken.None);

        Assert.Empty(_calls);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_FailureAfterASuccess_StillInvalidatesStats()
    {
        ArrangeRows(Row(1), Row(2));
        _repository.OnTryUpdate = id =>
        {
            if (id == 2)
            {
                throw new OperationCanceledException();
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.BulkUpdateStatusAsync(Request(Item(1), Item(2)), CancellationToken.None));

        Assert.Contains("RemoveByTag:stats", _calls);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_Completed_LogsOneSummaryWithCountsOnly()
    {
        ArrangeRows(Row(1), Row(2));

        await _service.BulkUpdateStatusAsync(Request(Item(1), Item(2, StaleVersion), Item(99)), CancellationToken.None);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("1 succeeded", entry.Message);
        Assert.Contains("1 not found", entry.Message);
        Assert.Contains("1 conflicts", entry.Message);
        Assert.DoesNotContain("alice", entry.Message);
        Assert.DoesNotContain("99", entry.Message);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_TooManyItems_ThrowsValidationExceptionAndTouchesNothing()
    {
        var items = Enumerable.Range(1, BulkUpdateStatusRequest.MaxItems + 1).Select(i => Item(i)).ToArray();

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.BulkUpdateStatusAsync(Request(items), CancellationToken.None));

        Assert.Equal(0, _repository.GetByIdsCalls);
        Assert.Empty(_repository.UpdateCalls);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_MaxItems_IsAccepted()
    {
        var items = Enumerable.Range(1, BulkUpdateStatusRequest.MaxItems).Select(i => Item(i)).ToArray();

        var result = await _service.BulkUpdateStatusAsync(Request(items), CancellationToken.None);

        Assert.Equal(BulkUpdateStatusRequest.MaxItems, result.Failed.Count);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_MissingVersion_ThrowsValidationExceptionAndTouchesNothing()
    {
        var request = Request(Item(1), new BulkStatusItem { Id = 2, Version = null });

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.BulkUpdateStatusAsync(request, CancellationToken.None));

        Assert.Empty(_repository.UpdateCalls);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_DuplicateId_ThrowsValidationExceptionAndTouchesNothing()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.BulkUpdateStatusAsync(Request(Item(1), Item(1)), CancellationToken.None));

        Assert.Empty(_repository.UpdateCalls);
    }
}
