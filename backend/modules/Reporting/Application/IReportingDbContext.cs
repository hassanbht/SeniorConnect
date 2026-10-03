using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Application;

public interface IReportingDbContext
{
    DbSet<Funder> Funders { get; }
    DbSet<FundingRelationship> FundingRelationships { get; }
    DbSet<FunderMembership> FunderMemberships { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<FunderMonthlyReportView> FunderMonthlyReports { get; }
    DbSet<VolunteerHoursView> VolunteerHours { get; }

    // P7-15 / Legal Gate — see Domain/ComplianceRecords.cs
    DbSet<LegalEntityProfile> LegalEntityProfiles { get; }
    DbSet<DataProcessingAgreement> DataProcessingAgreements { get; }
    DbSet<ProcessingActivityRecord> ProcessingActivityRecords { get; }
    DbSet<DpiaRecord> DpiaRecords { get; }
    DbSet<InsurancePolicy> InsurancePolicies { get; }
    DbSet<HostingAttestation> HostingAttestations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
