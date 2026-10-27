using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Core.Abstractions;

namespace RequestTracker.UnitTests.Fakes;

// Every scope resolves the one fake repository, which is all the service asks a scope for.
public sealed class FakeServiceScopeFactory(IRequestRepository repository) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new Scope(repository);

    private sealed class Scope(IRequestRepository repository) : IServiceScope, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IRequestRepository) ? repository : null;

        public void Dispose()
        {
        }
    }
}
