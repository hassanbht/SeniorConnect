using FluentAssertions;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

public sealed class SafeguardingWorkflowTests
{
    [Fact]
    public void Raising_concern_creates_open_case_with_severity()
    {
        var reporterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var result = SafeguardingCase.Raise(
            reporterUserId: reporterId,
            subjectUserId: subjectId,
            summary: "Observed signs of self-neglect during grocery dropoff.",
            severity: SafeguardingSeverity.High,
            category: "vulnerability_welfare");

        result.IsSuccess.Should().BeTrue();
        var sCase = result.Value!;
        sCase.Status.Should().Be(SafeguardingStatus.Open);
        sCase.Severity.Should().Be(SafeguardingSeverity.High);
        sCase.Category.Should().Be("vulnerability_welfare");
        sCase.ReporterUserId.Should().Be(reporterId);
        sCase.SubjectUserId.Should().Be(subjectId);
    }

    [Fact]
    public void Empty_concern_summary_fails_validation()
    {
        var result = SafeguardingCase.Raise(
            reporterUserId: Guid.NewGuid(),
            subjectUserId: Guid.NewGuid(),
            summary: "   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Officer_can_assign_record_action_and_close_case()
    {
        var officerId = Guid.NewGuid();
        var sCase = SafeguardingCase.Raise(
            reporterUserId: Guid.NewGuid(),
            subjectUserId: Guid.NewGuid(),
            summary: "Volunteer reported unannounced third-party present.").Value!;

        // Assign to officer
        sCase.AssignToOfficer(officerId);
        sCase.AssignedOfficerUserId.Should().Be(officerId);
        sCase.Status.Should().Be(SafeguardingStatus.Assigned);

        // Record action
        sCase.RecordAction("Contacted family coordinator, situation clarified and safe.");
        sCase.Status.Should().Be(SafeguardingStatus.ActionTaken);
        sCase.ResolutionNotes.Should().Contain("situation clarified");

        // Close
        var closeResult = sCase.Close(officerId, "Case resolved safely.");
        closeResult.IsSuccess.Should().BeTrue();
        sCase.Status.Should().Be(SafeguardingStatus.Closed);
        sCase.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Adding_case_notes_appends_to_case()
    {
        var caseId = Guid.NewGuid();
        var officerId = Guid.NewGuid();

        var noteResult = SafeguardingCaseNote.Create(caseId, officerId, "Spoke with municipal social worker.");
        noteResult.IsSuccess.Should().BeTrue();
        var note = noteResult.Value!;
        note.NoteText.Should().Be("Spoke with municipal social worker.");
        note.CaseId.Should().Be(caseId);
        note.AuthorOfficerUserId.Should().Be(officerId);
    }
}
