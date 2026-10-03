using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

/// <summary>
/// ADR-026 Option C storage: at most ONE live grant per
/// (help request, volunteer) — revocation history is kept.
/// </summary>
public sealed class VisitAddressGrantConfiguration : IEntityTypeConfiguration<VisitAddressGrant>
{
    public void Configure(EntityTypeBuilder<VisitAddressGrant> builder)
    {
        builder.ToTable("visit_address_grants");

        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.HelpRequestId).HasColumnName("help_request_id").IsRequired();
        builder.Property(g => g.VolunteerUserId).HasColumnName("volunteer_user_id").IsRequired();
        builder.Property(g => g.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        builder.Property(g => g.GrantedByRole).HasColumnName("granted_by_role").HasConversion<string>().IsRequired();
        builder.Property(g => g.GrantedAtUtc).HasColumnName("granted_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(g => g.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamptz");
        builder.Property(g => g.RevokedByUserId).HasColumnName("revoked_by_user_id");
        builder.Property(g => g.RevokeReason).HasColumnName("revoke_reason");

        builder.HasIndex(g => new { g.HelpRequestId, g.VolunteerUserId, g.RevokedAtUtc })
            .HasFilter("revoked_at_utc IS NULL")
            .IsUnique()
            .HasDatabaseName("ux_visit_address_grants_live");

        builder.HasIndex(g => g.HelpRequestId).HasDatabaseName("ix_visit_address_grants_request");
    }
}
