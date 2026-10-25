using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Infrastructure.Persistence;

namespace RequestTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RequestTracker")
            ?? throw new InvalidOperationException("Connection string 'RequestTracker' is not configured.");
        services.AddDbContext<RequestTrackerDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
