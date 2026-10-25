using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;
using RequestTracker.Infrastructure.Persistence;

namespace RequestTracker.Infrastructure.Seeding;

public class DataSeeder(RequestTrackerDbContext db, TimeProvider timeProvider, ILogger<DataSeeder> logger)
{
    public const int DefaultRequestCount = 100_000;

    // Dates end on a fixed day, not today, so every run produces identical data.
    public static readonly DateTimeOffset EndDate = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private const int RandomSeed = 20261001;
    private const int BatchSize = 5_000;
    private const int CreatedWithinDays = 730;
    private const int MaxDaysUntilUpdate = 60;

    // Startup seeding: a database that already has requests is left as it is, so restarts keep changes made through the app.
    // Startup has just migrated the database, so it is filled in place rather than recreated.
    public async Task SeedIfEmptyAsync(int requestCount = DefaultRequestCount, CancellationToken cancellationToken = default)
    {
        if (await db.Requests.AnyAsync(cancellationToken))
        {
            return;
        }

        await InsertRequestsAsync(requestCount, cancellationToken);
    }

    public async Task SeedAsync(int requestCount = DefaultRequestCount, CancellationToken cancellationToken = default)
    {
        // Recreating the database (not deleting rows) also resets the identity columns, so Ids start at 1 on every run.
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        await InsertRequestsAsync(requestCount, cancellationToken);
    }

    private async Task InsertRequestsAsync(int requestCount, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();

        // All or nothing: a run stopped halfway would otherwise leave a partly filled table that startup treats as seeded.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var random = new Random(RandomSeed);
        var organizations = CreateOrganizations(random);

        for (var inserted = 0; inserted < requestCount; inserted += BatchSize)
        {
            var batchSize = Math.Min(BatchSize, requestCount - inserted);
            for (var i = 0; i < batchSize; i++)
            {
                db.Requests.Add(CreateRequest(random, organizations));
            }

            await db.SaveChangesAsync(cancellationToken);
            // Tracked entities would otherwise pile up and slow every later batch.
            db.ChangeTracker.Clear();
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {RequestCount} requests in {ElapsedSeconds:F1} s",
            requestCount,
            timeProvider.GetElapsedTime(started).TotalSeconds);
    }

    // Every name/type combination, in a shuffled order, so the largest organizations aren't alphabetically first.
    private static string[] CreateOrganizations(Random random)
    {
        var organizations = SeedData.OrganizationNames
            .SelectMany(name => SeedData.OrganizationTypes.Select(type => $"{name} {type}"))
            .ToArray();
        random.Shuffle(organizations);
        return organizations;
    }

    private static Request CreateRequest(Random random, string[] organizations)
    {
        var status = PickWeighted(random, SeedData.StatusWeights);
        var createdAt = EndDate.AddSeconds(-random.NextInt64(CreatedWithinDays * 86_400L));
        var updatedAt = status == RequestStatus.New
            ? createdAt
            : Min(createdAt.AddSeconds(random.NextInt64(MaxDaysUntilUpdate * 86_400L)), EndDate);

        return new Request
        {
            Title = CreateTitle(random),
            // Squaring skews the pick toward the start of the array: the first organizations get thousands of
            // requests, the last ones a few hundred, so organization filters return result sets of different sizes.
            OrganizationName = organizations[(int)(organizations.Length * Math.Pow(random.NextDouble(), 2))],
            Status = status,
            Priority = PickWeighted(random, SeedData.PriorityWeights),
            AssignedTo = SeedData.Assignees[random.Next(SeedData.Assignees.Length)],
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }

    // About a third of the titles have no context, so the same action appears both alone and with a place or audience.
    private static string CreateTitle(Random random)
    {
        var action = SeedData.TitleActions[random.Next(SeedData.TitleActions.Length)];
        var contextIndex = random.Next(SeedData.TitleContexts.Length + SeedData.TitleContexts.Length / 2);
        return contextIndex < SeedData.TitleContexts.Length ? $"{action} {SeedData.TitleContexts[contextIndex]}" : action;
    }

    private static T PickWeighted<T>(Random random, (T Value, int Weight)[] weights)
    {
        var roll = random.Next(weights.Sum(w => w.Weight));
        foreach (var (value, weight) in weights)
        {
            if (roll < weight)
            {
                return value;
            }

            roll -= weight;
        }

        throw new InvalidOperationException("Unreachable: the roll is always below the total weight.");
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
