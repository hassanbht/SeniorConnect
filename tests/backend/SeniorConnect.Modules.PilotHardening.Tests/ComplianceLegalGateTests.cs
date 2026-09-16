using FluentAssertions;
using SeniorConnect.Modules.Reporting.Domain;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

/// <summary>
/// P7-15 / Legal Gate — compliance domain tests.
///
/// These tests verify the domain invariants of the Legal Gate entities
/// without a database. They are fast, deterministic, and prove that
/// the "empty until a human acts" contract is enforced by the domain itself.
///
/// Authorization (PlatformAdmin only) is verified separately at the endpoint
/// level; the gate.IsGateClear behaviour is verified here.
/// </summary>
public sealed class ComplianceLegalGateTests
{
    // ── LegalEntityProfile ────────────────────────────────────────────────

    [Fact]
    public void CreateEmpty_ReturnsProfileWithAllNullTimestamps_LegalGate()
    {
        var profile = LegalEntityProfile.CreateEmpty();

        profile.Id.Should().NotBeEmpty();
        profile.LegalName.Should().BeNull();
        profile.EstablishedAtUtc.Should().BeNull(
            "the Legal Gate 'entity established' field must start null until a human files the paperwork");
        profile.PrivacyPolicyLawyerReviewedAtUtc.Should().BeNull(
            "the Legal Gate 'privacy policy reviewed' field must start null until a lawyer signs off");
        profile.TermsLawyerReviewedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkEstablished_SetsTimestamp_LegalGate()
    {
        var profile = LegalEntityProfile.CreateEmpty();
        var establishedAt = DateTimeOffset.UtcNow.AddDays(-1);

        profile.MarkEstablished(establishedAt, Guid.NewGuid());

        profile.EstablishedAtUtc.Should().Be(establishedAt);
    }

    [Fact]
    public void RecordPrivacyPolicyReview_SetsVersionAndTimestamp_LegalGate()
    {
        var profile = LegalEntityProfile.CreateEmpty();
        var reviewedAt = DateTimeOffset.UtcNow;

        profile.RecordPrivacyPolicyReview("v1.0", reviewedAt, Guid.NewGuid());

        profile.PrivacyPolicyVersion.Should().Be("v1.0");
        profile.PrivacyPolicyLawyerReviewedAtUtc.Should().BeCloseTo(reviewedAt, TimeSpan.FromSeconds(1));
    }

    // ── DataProcessingAgreement ───────────────────────────────────────────

    [Fact]
    public void CreateDpa_StartsAsDraft_LegalGate()
    {
        var dpa = DataProcessingAgreement.Create(
            Guid.NewGuid(), ControllerRole.PlatformIsController,
            "Dr. Muster", "muster@fwz.at", Guid.NewGuid());

        dpa.Status.Should().Be(AgreementStatus.Draft,
            "a newly created DPA must be Draft until a human actually signs it");
        dpa.SignedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkDpaSigned_ChangesStatusToExecuted_LegalGate()
    {
        var dpa = DataProcessingAgreement.Create(
            Guid.NewGuid(), ControllerRole.PlatformIsController,
            null, null, Guid.NewGuid());

        dpa.MarkSigned(DateTimeOffset.UtcNow, "REF-2026-001", Guid.NewGuid());

        dpa.Status.Should().Be(AgreementStatus.Executed);
        dpa.DocumentReference.Should().Be("REF-2026-001");
        dpa.SignedAtUtc.Should().NotBeNull();
    }

    // ── ProcessingActivityRecord ──────────────────────────────────────────

    [Fact]
    public void CreateProcessingActivity_RequiresActivityName_LegalGate()
    {
        var result = ProcessingActivityRecord.Create(
            activityName: "",
            purposeDescription: "Volunteer hour logging",
            dataCategories: "Name, contact",
            dataSubjectCategories: "Volunteers",
            legalBasis: LegalBasis.Contract,
            retentionPeriodDescription: "5 years",
            recipientCategories: null,
            involvesThirdCountryTransfer: false,
            createdBy: Guid.NewGuid());

        result.IsSuccess.Should().BeFalse("an empty activity name must be rejected");
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Validation);
    }

    [Fact]
    public void CreateProcessingActivity_WithValidData_Succeeds_LegalGate()
    {
        var result = ProcessingActivityRecord.Create(
            activityName: "Volunteer Hour Logging",
            purposeDescription: "Track volunteer activity for impact reporting",
            dataCategories: "Name, contact details, hours worked",
            dataSubjectCategories: "Volunteers",
            legalBasis: LegalBasis.Contract,
            retentionPeriodDescription: "5 years after last activity",
            recipientCategories: "Funder organizations (aggregated, suppressed below 10)",
            involvesThirdCountryTransfer: false,
            createdBy: Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value!.ActivityName.Should().Be("Volunteer Hour Logging");
        result.Value.InvolvesThirdCountryTransfer.Should().BeFalse();
    }

    // ── DpiaRecord ────────────────────────────────────────────────────────

    [Fact]
    public void CreateDpia_StartsNotStarted_LegalGate()
    {
        var result = DpiaRecord.Create("Pilot DPIA", "Processing data of elderly and at-risk persons", Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApprovalStatus.Should().Be(DpiaApprovalStatus.NotStarted,
            "a new DPIA must start as NotStarted; only a human can approve it");
        result.Value.ConductedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApproveDpia_SetsApprovedStatus_LegalGate()
    {
        var result = DpiaRecord.Create("Pilot DPIA", "Processing data of elderly and at-risk persons", Guid.NewGuid());
        var dpia = result.Value!;
        var conductedAt = DateTimeOffset.UtcNow;

        dpia.Approve(conductedAt, "Dr. Datenschutzbeauftragter", Guid.NewGuid());

        dpia.ApprovalStatus.Should().Be(DpiaApprovalStatus.Approved);
        dpia.ConductedAtUtc.Should().Be(conductedAt);
        dpia.ConductedByName.Should().Be("Dr. Datenschutzbeauftragter");
    }

    // ── InsurancePolicy — the P0-02 field ────────────────────────────────

    [Fact]
    public void CreatePending_HasNullWrittenConfirmation_P0_02_LegalGate()
    {
        var policy = InsurancePolicy.CreatePending(
            InsurancePolicyType.VolunteerAccident, null, Guid.NewGuid());

        policy.WrittenConfirmationReceivedAtUtc.Should().BeNull(
            "P0-02: the legal gate reads this field — it must be null until the broker answers in writing");
    }

    [Fact]
    public void RecordWrittenConfirmation_SetsAllFields_P0_02_LegalGate()
    {
        var policy = InsurancePolicy.CreatePending(
            InsurancePolicyType.TransportLiability, null, Guid.NewGuid());
        var receivedAt = DateTimeOffset.UtcNow;

        policy.RecordWrittenConfirmation(
            "Niederösterreichische Versicherung", "POL-2026-99887",
            "Covers all volunteers up to EUR 2M per incident", receivedAt, Guid.NewGuid());

        policy.WrittenConfirmationReceivedAtUtc.Should().Be(receivedAt);
        policy.ProviderName.Should().Be("Niederösterreichische Versicherung");
        policy.PolicyNumber.Should().Be("POL-2026-99887");
    }

    // ── HostingAttestation ────────────────────────────────────────────────

    [Fact]
    public void CreateEmpty_HostingAttestation_HasNullContractDate_LegalGate()
    {
        var attestation = HostingAttestation.CreateEmpty();

        attestation.ContractSignedAtUtc.Should().BeNull(
            "the legal gate 'EU hosting verified contractually' item must start null");
    }

    [Fact]
    public void RecordContract_SetsAllHostingFields_LegalGate()
    {
        var attestation = HostingAttestation.CreateEmpty();
        var signedAt = DateTimeOffset.UtcNow;

        attestation.RecordContract("Hetzner Online GmbH", "EU-West (Frankfurt)", "DPA-HETZNER-2026", signedAt, Guid.NewGuid());

        attestation.HostingProviderName.Should().Be("Hetzner Online GmbH");
        attestation.DataCenterRegion.Should().Be("EU-West (Frankfurt)");
        attestation.ContractSignedAtUtc.Should().Be(signedAt);
    }
}
