using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;

namespace RequestTracker.Infrastructure.Persistence.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests", table =>
        {
            table.HasCheckConstraint("CK_Requests_Status", CheckConstraints.EnumValues<RequestStatus>("Status"));
            table.HasCheckConstraint("CK_Requests_Priority", CheckConstraints.EnumValues<RequestPriority>("Priority"));
        });

        builder.Property(r => r.Title).HasMaxLength(Request.TitleMaxLength);
        builder.Property(r => r.OrganizationName).HasMaxLength(Request.OrganizationNameMaxLength);
        builder.Property(r => r.AssignedTo).HasMaxLength(Request.AssignedToMaxLength);
        builder.Property(r => r.Version).IsRowVersion();

        // Each index ends in CreatedAt so a filtered list keeps the default sort without a sort step;
        // the clustered key Id is implicitly part of every index, which gives the tiebreaker order.
        // RequestRepository.IndexedWithCreatedAt relies on the (Field, CreatedAt) layout below to sort
        // status, priority and assignedTo from the index; change both together.
        builder.HasIndex(r => r.CreatedAt).HasDatabaseName("IX_Requests_CreatedAt");
        builder.HasIndex(r => new { r.Status, r.CreatedAt }).HasDatabaseName("IX_Requests_Status_CreatedAt");
        builder.HasIndex(r => new { r.Priority, r.CreatedAt }).HasDatabaseName("IX_Requests_Priority_CreatedAt");
        builder.HasIndex(r => new { r.AssignedTo, r.CreatedAt }).HasDatabaseName("IX_Requests_AssignedTo_CreatedAt");
        builder.HasIndex(r => r.OrganizationName).HasDatabaseName("IX_Requests_OrganizationName");
    }
}
