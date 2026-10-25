using Microsoft.Extensions.DependencyInjection;

namespace RequestTracker.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHybridCache();

        return services;
    }
}
