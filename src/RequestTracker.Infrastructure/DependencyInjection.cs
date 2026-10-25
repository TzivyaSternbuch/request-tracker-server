using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Infrastructure.Persistence;

namespace RequestTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Read when the context is first resolved, so configuration added by the test host is already in place.
        services.AddDbContext<RequestTrackerDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("RequestTracker")
            ?? throw new InvalidOperationException("Connection string 'RequestTracker' is not configured.")));
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
