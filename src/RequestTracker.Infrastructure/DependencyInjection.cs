using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Infrastructure.Persistence;
using RequestTracker.Infrastructure.Seeding;

namespace RequestTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RequestTracker")
            ?? throw new InvalidOperationException("Connection string 'RequestTracker' is not configured.");
        services.AddDbContext<RequestTrackerDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<DataSeeder>();
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }

    // Replaces the whole database, schema included, with freshly generated test data.
    public static async Task SeedDatabaseAsync(
        this IServiceProvider services,
        int requestCount = DataSeeder.DefaultRequestCount,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(requestCount, cancellationToken);
    }

    public static async Task SeedDatabaseIfEmptyAsync(
        this IServiceProvider services,
        int requestCount = DataSeeder.DefaultRequestCount,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedIfEmptyAsync(requestCount, cancellationToken);
    }
}
