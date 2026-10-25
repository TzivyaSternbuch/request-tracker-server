using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;

namespace RequestTracker.Infrastructure.Persistence.Configurations;

public class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        builder.ToTable("RequestStatusHistory", table =>
        {
            table.HasCheckConstraint(
                "CK_RequestStatusHistory_PreviousStatus", CheckConstraints.EnumValues<RequestStatus>("PreviousStatus"));
            table.HasCheckConstraint(
                "CK_RequestStatusHistory_NewStatus", CheckConstraints.EnumValues<RequestStatus>("NewStatus"));
        });

        builder.Property(h => h.ChangedBy).HasMaxLength(RequestStatusHistory.ChangedByMaxLength);

        // Restrict: deleting a request must not silently erase its audit trail.
        builder.HasOne<Request>()
            .WithMany()
            .HasForeignKey(h => h.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.RequestId, h.ChangedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_RequestStatusHistory_RequestId_ChangedAt");
    }
}
