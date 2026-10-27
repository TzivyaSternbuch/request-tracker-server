using FluentValidation;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;
using RequestTracker.Core.Services;
using RequestTracker.Core.Validation;
using RequestTracker.UnitTests.Fakes;

namespace RequestTracker.UnitTests.Services;

public sealed class RequestServiceHistoryTests
{
    private readonly List<string> _calls = [];
    private readonly FakeRequestRepository _repository;
    private readonly RequestService _service;

    public RequestServiceHistoryTests()
    {
        _repository = new FakeRequestRepository(_calls);
        _service = new RequestService(
            _repository,
            new RequestQueryParametersValidator(),
            new UpdateStatusRequestValidator(),
            new StatusHistoryQueryParametersValidator(),
            new FakeHybridCache(_calls),
            new FakeServiceScopeFactory(_repository),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch),
            new CapturingLogger<RequestService>());
    }

    [Fact]
    public async Task GetHistoryAsync_ValidParameters_ReturnsRepositoryPage()
    {
        var parameters = new StatusHistoryQueryParameters { Page = 2, PageSize = 5 };
        var expected = new PagedResult<StatusHistoryDto>([], 2, 5, 7);
        _repository.NextHistory = expected;

        var result = await _service.GetHistoryAsync(42, parameters, CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal((42, parameters), _repository.LastHistoryCall);
    }

    [Fact]
    public async Task GetHistoryAsync_InvalidPageSize_ThrowsBeforeQueryingRepository()
    {
        var parameters = new StatusHistoryQueryParameters { PageSize = 0 };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetHistoryAsync(42, parameters, CancellationToken.None));

        Assert.Empty(_calls);
    }
}
