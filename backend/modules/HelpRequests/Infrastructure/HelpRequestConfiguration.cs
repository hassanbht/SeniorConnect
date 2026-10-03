using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class HelpRequestConfiguration : IEntityTypeConfiguration<HelpRequest>
{
    public void Configure(EntityTypeBuilder<HelpRequest> builder)
    {
        builder.ToTable("HelpRequests", "public");

        builder.HasIndex(h => new { h.SeniorUserId, h.Status })
            .HasDatabaseName("ix_help_requests_senior_status");

        builder.HasIndex(h => new { h.AssignedVolunteerUserId, h.Status })
            .HasDatabaseName("ix_help_requests_volunteer_status");

        builder.HasIndex(h => new { h.OrganizationId, h.Status })
            .HasDatabaseName("ix_help_requests_org_status");

        builder.HasIndex(h => new { h.Status, h.ScheduledStartUtc })
            .HasDatabaseName("ix_help_requests_status_scheduled");

        builder.HasIndex(h => h.CategoryId)
            .HasDatabaseName("ix_help_requests_category_id");
    }
}

public sealed class HelpRequestStatusHistoryConfiguration : IEntityTypeConfiguration<HelpRequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<HelpRequestStatusHistory> builder)
    {
        builder.ToTable("HelpRequestStatusHistories", "public");

        builder.HasIndex(h => new { h.HelpRequestId, h.ChangedAtUtc })
            .HasDatabaseName("ix_help_request_history_request_time");
    }
}
