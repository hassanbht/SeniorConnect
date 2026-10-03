namespace SeniorConnect.SharedKernel;

/// <summary>
/// docs/architecture/privacy-gdpr.md §1.
///
/// Every property on a persisted entity or an outbound DTO carries a
/// classification. An architecture test fails the build for any unclassified
/// property, so a new field cannot be added without someone deciding what it is.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public sealed class DataClassAttribute(DataClass dataClass) : Attribute
{
    public DataClass DataClass { get; } = dataClass;
}

public enum DataClass
{
    /// <summary>First name, area, interests. Any authenticated user.</summary>
    PublicProfile = 0,

    /// <summary>Non-personal: IDs, counts, timestamps, enums.</summary>
    Operational = 1,

    /// <summary>Full name, address, phone, DOB, free text written by a user.</summary>
    PersonalData = 2,

    /// <summary>Verification outcomes, reliability components.</summary>
    SensitiveData = 3,

    /// <summary>Mobility need, vulnerability flag. Avoided by design.</summary>
    HealthRelated = 4,

    /// <summary>Concerns, cases, notes. Safeguarding Officers only.</summary>
    Safeguarding = 5,
}

/// <summary>
/// Marks a DTO reachable from the funder API namespace.
///
/// BR-FUNDER-02: there is no code path from a funder token to a name, address,
/// phone number, email, or any free text written by a user. An architecture
/// test asserts that every property of every IFunderVisible type is classified
/// PublicProfile or Operational, and that no property name matches an
/// identifying pattern.
/// </summary>
public interface IFunderVisible;
