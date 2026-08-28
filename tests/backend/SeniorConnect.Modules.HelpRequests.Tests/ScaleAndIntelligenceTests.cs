using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Matching.Infrastructure;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Reporting.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class ScaleAndIntelligenceTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    private readonly DbContextOptions<SeniorConnectDbContext> _dbOptions = new DbContextOptionsBuilder<SeniorConnectDbContext>()
        .UseInMemoryDatabase(databaseName: $"ScaleIntelTests_{Guid.NewGuid()}")
        .Options;

    [Fact]
    public async Task VoiceRequestParser_DetectsEmergency_AndRecommendsOfficialNotruf()
    {
        var parser = new VoiceRequestParser();
        var request = new VoiceParseRequest("Ich habe starke Brustschmerzen und brauche sofort Hilfe!");

        var result = await parser.ParseTranscriptAsync(request);

        Assert.True(result.IsSuccess);
        var val = result.Value!;
        Assert.True(val.IsEmergency);
        Assert.Contains("144", val.EmergencyMessage);
        Assert.Equal("emergency", val.DetectedCategoryCode);
    }

    [Fact]
    public async Task VoiceRequestParser_DetectsBlockedNursing_AndProvidesReferralMessage()
    {
        var parser = new VoiceRequestParser();
        var request = new VoiceParseRequest("Könnte jemand vorbeikommen und mir die Strümpfe anziehen?");

        var result = await parser.ParseTranscriptAsync(request);

        Assert.True(result.IsSuccess);
        var val = result.Value!;
        Assert.False(val.IsEmergency);
        Assert.True(val.IsBlockedCategory);
        Assert.Contains("Pflegedienste", val.BlockedReferralMessage);
    }

    [Fact]
    public async Task VoiceRequestParser_ExtractsValidShoppingRequest()
    {
        var parser = new VoiceRequestParser();
        var request = new VoiceParseRequest("Ich brauche morgen Hilfe beim Lebensmitteleinkauf beim SPAR.");

        var result = await parser.ParseTranscriptAsync(request);

        Assert.True(result.IsSuccess);
        var val = result.Value!;
        Assert.False(val.IsEmergency);
        Assert.False(val.IsBlockedCategory);
        Assert.Equal("shopping", val.DetectedCategoryCode);
        Assert.True(val.ProposedDurationMinutes > 0);
    }

    [Fact]
    public async Task HybridMatchingPolicy_SafetyLevel3Plus_RequiresManualCoordinatorApproval_ADR014()
    {
        using var db = new SeniorConnectDbContext(_dbOptions, new TestTenantContext());
        var seniorUserId = Guid.CreateVersion7();
        var volunteerUserId = Guid.CreateVersion7();

        var category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, Guid.CreateVersion7());
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Code))!.SetValue(category, "doctor");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.NameKey))!.SetValue(category, "help.category.doctor");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.DefaultSafetyLevel))!.SetValue(category, 3);
        db.ActivityCategories.Add(category);

        var helpRequestResult = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorUserId,
            createdByUserId: seniorUserId,
            categoryId: category.Id,
            safetyLevel: 3,
            trustLevel: 3,
            scheduledStartUtc: DateTimeOffset.UtcNow.AddHours(2),
            scheduledEndUtc: DateTimeOffset.UtcNow.AddHours(4),
            durationMinutes: 120,
            locationType: LocationType.SeniorHome,
            notes: "Fahrt zum Facharzt",
            latitude: 47.8095,
            longitude: 13.0550);

        Assert.True(helpRequestResult.IsSuccess);
        var helpRequest = helpRequestResult.Value!;
        db.HelpRequests.Add(helpRequest);

        var volunteerProfile = VolunteerProfile.Create(
            userId: volunteerUserId,
            bio: "Freiwillige Begleitung",
            postalCode: "5020",
            latitude: 47.8100,
            longitude: 13.0560,
            maxDistanceKm: 15);

        volunteerProfile.UpdateReliability(0.95m);
        db.VolunteerProfiles.Add(volunteerProfile);
        db.TrustLevelSnapshots.Add(SeniorConnect.Modules.Identity.Domain.TrustLevelSnapshot.Create(volunteerUserId, 3, "{}"));
        await db.SaveChangesAsync();

        var trustReader = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var matchingService = new MatchingService(db, db, trustReader, safetyReader);
        var hybridResult = await matchingService.GetHybridProposalsAsync(new HybridMatchingRequest(helpRequest.Id));

        Assert.True(hybridResult.IsSuccess);
        var proposals = hybridResult.Value!;
        Assert.NotEmpty(proposals);

        var first = proposals[0];
        Assert.Equal(volunteerUserId, first.CandidateVolunteerUserId);
        Assert.True(first.RequiresManualCoordinatorApproval, "Per ADR-014: AI proposes, humans decide for Safety Level 3+.");
        Assert.True(first.CombinedScore > 0.50);
        Assert.NotEmpty(first.AiRecommendationReason);
    }

    [Fact]
    public async Task IdAustriaVerificationProvider_ReturnsVerifiedOutcome_WithoutPersistingDocuments_BRTRUST05()
    {
        var provider = new IdAustriaVerificationProvider();
        var userId = Guid.CreateVersion7();

        var result = await provider.VerifyIdentityAsync(new IdentityVerificationRequest(
            UserId: userId,
            ProviderType: VerificationProviderType.IdAustria,
            ExternalTokenReference: "ID_AUSTRIA_MOCK_TOKEN_98765"));

        Assert.True(result.IsSuccess);
        var val = result.Value!;
        Assert.True(val.IsSuccess);
        Assert.Equal(userId, val.UserId);
        Assert.Contains("ID Austria", val.ProviderName);
        Assert.NotEmpty(val.VerificationReferenceHash);
        Assert.Null(val.FailureReason);
    }

    [Fact]
    public async Task EsgReportingService_GeneratesValidCorporateSummary_AndPdfCertificate()
    {
        using var db = new SeniorConnectDbContext(_dbOptions, new TestTenantContext());
        var companyOrgId = Guid.CreateVersion7();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var esgService = new EsgReportingService(db);
        var summaryResult = await esgService.GetCorporateEsgSummaryAsync(companyOrgId, from, to);

        Assert.True(summaryResult.IsSuccess);
        var summary = summaryResult.Value!;
        Assert.Equal(companyOrgId, summary.CompanyOrganizationId);
        Assert.NotEmpty(summary.SdgsImpacted);

        var pdfResult = await esgService.ExportEsgCertificatePdfAsync(companyOrgId, from, to);
        Assert.True(pdfResult.IsSuccess);
        Assert.NotEmpty(pdfResult.Value!);
    }

    [Fact]
    public async Task ReportingService_GeneratesValidSpreadsheetXlsxReport()
    {
        using var db = new SeniorConnectDbContext(_dbOptions, new TestTenantContext());
        var orgId = Guid.CreateVersion7();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var reportingService = new ReportingService(db);
        var xlsxResult = await reportingService.ExportImpactXlsxAsync(orgId, from, to);

        Assert.True(xlsxResult.IsSuccess);
        var bytes = xlsxResult.Value!;
        Assert.NotEmpty(bytes);

        var content = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("xml version=\"1.0\"", content);
        Assert.Contains("urn:schemas-microsoft-com:office:spreadsheet", content);
        Assert.Contains("Wirkungsbericht", content);
    }
}
