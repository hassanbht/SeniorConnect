using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Application;

public interface IOrganizationsDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationBranch> OrganizationBranches { get; }
    DbSet<OrganizationMembership> OrganizationMemberships { get; }
    DbSet<OrganizationPolicy> OrganizationPolicies { get; }
    DbSet<OrganizationIntakeForm> OrganizationIntakeForms { get; }
    DbSet<IntakeFormSection> IntakeFormSections { get; }
    DbSet<IntakeFormField> IntakeFormFields { get; }
    DbSet<IntakeFormSubmission> IntakeFormSubmissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
