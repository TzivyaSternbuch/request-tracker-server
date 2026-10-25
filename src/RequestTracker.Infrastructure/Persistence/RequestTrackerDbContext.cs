using Microsoft.EntityFrameworkCore;
using RequestTracker.Core.Entities;

namespace RequestTracker.Infrastructure.Persistence;

public class RequestTrackerDbContext(DbContextOptions<RequestTrackerDbContext> options) : DbContext(options)
{
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestStatusHistory> RequestStatusHistory => Set<RequestStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RequestTrackerDbContext).Assembly);
}
