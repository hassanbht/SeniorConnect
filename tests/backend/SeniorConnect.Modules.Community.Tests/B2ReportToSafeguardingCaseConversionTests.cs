using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Contracts;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

/// <summary>
/// B-2 (approved Option A, 2026-09-30): an escalated UnsafeBehavior report
/// remains a flag on the report. Only a manual officer conversion creates a
/// safeguarding case and links it back (traceability, BR-SG-01: safeguarding
/// is not an extension of ordinary reporting).
/// </summary>
public sealed class B2ReportToSafeguardingCaseConversionTests
{
    private readonly SeniorConnectDbContext _db;
    private readonly RecordingSafeguardingService _safeguarding = new();
    private readonly CommunityService _service;

    private readonly Guid _officer = Guid.NewGuid();
    private readonly Guid _coordinator = Guid.NewGuid();
    private readonly Guid _subject = Guid.NewGuid();
    private readonly Guid _org = Guid.NewGuid();

    public B2ReportToSafeguardingCaseConversionTests()
    {
        _db = new SeniorConnectDbContext(
            new DbContextOptionsBuilder<SeniorConnectDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new DefaultTenantContext { IsPlatformScope = true });

        _service = new CommunityService(
            _db, new RuleBasedCommunityModerationService(), new OrganizationCoordinatorReader(_db),
            new FakeUserAccountStatusReader(), new FakeTrustLevelReader(), new FakeSafeguardingCaseReader(),
            _safeguarding);
    }

    private async Task<CommunityReport> SeedEscalatedUnsafeBehaviorReportAsync(
        CommunityReportTargetType targetType = CommunityReportTargetType.Member)
    {
        var reporter = Guid.NewGuid();
        var target = targetType == CommunityReportTargetType.Member ? _subject : Guid.NewGuid();
        var report = CommunityReport.Create(reporter, targetType, target,
            CommunityReportCategory.UnsafeBehavior, "unsafe behaviour in the group").Value!;
        report.EscalateToSafeguarding(_coordinator, "Auto-escalated based on report category.");
        _db.CommunityReports.Add(report);
        await _db.SaveChangesAsync();
        return report;
    }

    [Fact]
    public async Task Officer_Can_Convert_AnEscalatedUnsafeBehaviorReport_IntoALinkedCase()
    {
        var report = await SeedEscalatedUnsafeBehaviorReportAsync();

        var converted = await _service.ConvertReportToCaseAsync(
            report.Id, _officer, callerIsSafeguardingOfficer: true,
            new ConvertReportToCaseRequest("Escalated unsafe behaviour", "Medium", _org));

        converted.IsSuccess.Should().BeTrue();
        converted.Value!.Should().NotBeEmpty("the conversion returns the new case id");
        converted.Value!.Should().Be(_safeguarding.LastRaised!.Id, "the returned id is the raised case's id");
        _safeguarding.LastRaised!.SubjectUserId.Should().Be(_subject, "the member the report targets becomes the case subject");
        _safeguarding.LastRaised!.Category.Should().Be("community_report");
        _safeguarding.LastRaised!.OrganizationId.Should().Be(_org);

        _safeguarding.RaiseCalls.Should().Be(1, "exactly one case is raised per conversion");

        var reloaded = await _db.CommunityReports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
        reloaded.SafeguardingCaseId.Should().Be(converted.Value, "the report keeps a traceability link to the case");
        reloaded.ConvertedByOfficerUserId.Should().Be(_officer);
        reloaded.ConvertedAtUtc.Should().NotBeNull();
        reloaded.Status.Should().Be(CommunityReportStatus.EscalatedToSafeguarding,
            "the report stays escalated; conversion links, it does not resolve");
    }

    [Fact]
    public async Task NonOfficer_Cannot_Convert_AndNoCaseIsRaised()
    {
        var report = await SeedEscalatedUnsafeBehaviorReportAsync();

        var result = await _service.ConvertReportToCaseAsync(
            report.Id, Guid.NewGuid(), callerIsSafeguardingOfficer: false,
            new ConvertReportToCaseRequest("Escalated unsafe behaviour", "Medium"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        _safeguarding.RaiseCalls.Should().Be(0, "no safeguarding case is raised without the officer capability");
    }

    [Fact]
    public async Task Converting_Twice_IsAConflict_AndRaisesOnlyOneCase()
    {
        var report = await SeedEscalatedUnsafeBehaviorReportAsync();
        var first = await _service.ConvertReportToCaseAsync(
            report.Id, _officer, callerIsSafeguardingOfficer: true,
            new ConvertReportToCaseRequest("Escalated unsafe behaviour", "Medium"));
        first.IsSuccess.Should().BeTrue();

        var second = await _service.ConvertReportToCaseAsync(
            report.Id, _officer, callerIsSafeguardingOfficer: true,
            new ConvertReportToCaseRequest("Duplicate conversion", "Medium"));

        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("REPORT_ALREADY_CONVERTED");
        second.Error.Kind.Should().Be(ErrorKind.Conflict);
        _safeguarding.RaiseCalls.Should().Be(1, "the duplicate conversion must not raise a second case");
    }

    [Fact]
    public async Task Only_EscalatedUnsafeBehaviorReports_AreConvertible()
    {
        // Open (not escalated) unsafe-behavior report.
        var openReport = CommunityReport.Create(Guid.NewGuid(), CommunityReportTargetType.Member, _subject,
            CommunityReportCategory.UnsafeBehavior, "open unsafe behaviour").Value!;
        _db.CommunityReports.Add(openReport);
        // Escalated but different category (the escalation rule would never
        // produce this; the conversion guard must refuse it anyway).
        var escalatedSpam = CommunityReport.Create(Guid.NewGuid(), CommunityReportTargetType.Member, _subject,
            CommunityReportCategory.Spam, "spam").Value!;
        escalatedSpam.EscalateToSafeguarding(_coordinator);
        _db.CommunityReports.Add(escalatedSpam);
        await _db.SaveChangesAsync();

        (await _service.ConvertReportToCaseAsync(
                openReport.Id, _officer, callerIsSafeguardingOfficer: true,
                new ConvertReportToCaseRequest("summary", "Low")))
            .Error!.Kind.Should().Be(ErrorKind.Validation, "an open report is a flag, not a case candidate");

        (await _service.ConvertReportToCaseAsync(
                escalatedSpam.Id, _officer, callerIsSafeguardingOfficer: true,
                new ConvertReportToCaseRequest("summary", "Low")))
            .Error!.Kind.Should().Be(ErrorKind.Validation, "only unsafe-behaviour reports convert");

        _safeguarding.RaiseCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItemTargeted_Reports_AreNotConvertible_ToUserCases()
    {
        var report = await SeedEscalatedUnsafeBehaviorReportAsync(CommunityReportTargetType.Post);

        // A post/comment target carries no user subject; the conversion refuses
        // loudly (the endpoint would map this to a problem response) instead of
        // inventing a subject.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConvertReportToCaseAsync(
                report.Id, _officer, callerIsSafeguardingOfficer: true,
                new ConvertReportToCaseRequest("summary", "Medium")));

        _safeguarding.RaiseCalls.Should().Be(0);
    }

    /// <summary>
    /// Test double of the B-2 boundary port (TrustSafety.Contracts). Records
    /// calls and mirrors RaiseCaseAsync's fail-closed severity parsing.
    /// </summary>
    private sealed class RecordingSafeguardingService : ISafeguardingCaseRaiser
    {
        public int RaiseCalls { get; private set; }

        public RaisedCase? LastRaised { get; private set; }

        public sealed record RaisedCase(Guid Id, Guid SubjectUserId, string Category, Guid? OrganizationId);

        public Task<Result<Guid>> RaiseCaseAsync(
            Guid reporterUserId,
            Guid subjectUserId,
            string summary,
            string severity,
            string category,
            Guid? organizationId,
            CancellationToken ct = default)
        {
            if (!Enum.TryParse<SafeguardingSeverity>(severity, ignoreCase: false, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Task.FromResult<Result<Guid>>(Error.Validation(
                    "Severity must be one of: Low, Medium, High, Critical."));
            }

            RaiseCalls++;
            LastRaised = new RaisedCase(Guid.CreateVersion7(), subjectUserId, category, organizationId);
            return Task.FromResult(Result<Guid>.Success(LastRaised.Id));
        }
    }
}
