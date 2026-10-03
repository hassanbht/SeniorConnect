namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

/// <summary>
/// ADR-026 Option C feature gate (approved 2026-09-30).
/// <see cref="RequireGrant"/> is FALSE by default: the BR-COMM-04 behaviour
/// stays in force until the Legal/Data Protection Gate is passed and an
/// operator explicitly enables the grant flow ("legal review pending" —
/// see docs/decisions/APPROVED-POLICY-IMPLEMENTATION-REPORT.md).
/// </summary>
public sealed class VisitAddressOptions
{
    public const string SectionName = "VisitAddress";

    /// <summary>When true, a live visit-address grant is required before the assigned volunteer sees the address.</summary>
    public bool RequireGrant { get; set; }

    /// <summary>Coordinator intervention deadline (hours) after which a non-granted visit may be unassigned by operations.</summary>
    public int CoordinatorInterventionHours { get; set; } = 24;
}
