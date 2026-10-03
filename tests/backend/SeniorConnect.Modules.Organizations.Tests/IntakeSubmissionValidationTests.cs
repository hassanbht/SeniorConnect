using FluentAssertions;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;
using RequiredField = SeniorConnect.Modules.Organizations.Infrastructure.OrganizationService.RequiredField;

namespace SeniorConnect.Modules.Organizations.Tests;

/// <summary>P2-38 / BR-ORG-FORM-02/04/05.</summary>
public sealed class IntakeSubmissionValidationTests
{
    private static readonly RequiredField[] StandardRequired =
    [
        new("bereiche", "intake.field.bereiche", FieldType.MultiChoice),
        new(OrganizationService.CriminalClearanceFieldKey, "intake.field.strafrechtliche_unbescholtenheit", FieldType.Boolean),
        new(OrganizationService.GdprConsentFieldKey, "intake.field.gdpr_consent", FieldType.Boolean),
    ];

    private static SubmitIntakeFormRequest Request(string json, bool clearance = true, bool gdpr = true) =>
        new(json, clearance, gdpr, EventInvitationOptIn: false);

    private const string ValidJson =
        """{"bereiche":["Soziales"],"strafrechtliche_unbescholtenheit":true,"gdpr_consent":true}""";

    [Fact]
    public void Valid_submission_passes()
    {
        OrganizationService.ValidateSubmission(Request(ValidJson), StandardRequired).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Required_boolean_sent_as_false_is_rejected()
    {
        var json = """{"bereiche":["Soziales"],"strafrechtliche_unbescholtenheit":false,"gdpr_consent":true}""";

        var result = OrganizationService.ValidateSubmission(Request(json), StandardRequired);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Extensions!["field"].Should().Be(OrganizationService.CriminalClearanceFieldKey);
    }

    [Fact]
    public void Required_multi_choice_sent_empty_is_rejected()
    {
        var json = """{"bereiche":[],"strafrechtliche_unbescholtenheit":true,"gdpr_consent":true}""";

        var result = OrganizationService.ValidateSubmission(Request(json), StandardRequired);

        result.Error!.Extensions!["field"].Should().Be("bereiche");
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{not json")]
    [InlineData("[1,2]")]
    public void Null_or_invalid_json_is_a_validation_error_not_a_500(string json)
    {
        var result = OrganizationService.ValidateSubmission(Request(json), StandardRequired);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public void Mandatory_gdpr_consent_requires_the_consent_flag()
    {
        var result = OrganizationService.ValidateSubmission(Request(ValidJson, gdpr: false), StandardRequired);

        result.Error!.Extensions!["field"].Should().Be(OrganizationService.GdprConsentFieldKey);
    }

    [Fact]
    public void Mandatory_criminal_clearance_requires_the_declaration_flag()
    {
        var result = OrganizationService.ValidateSubmission(Request(ValidJson, clearance: false), StandardRequired);

        result.Error!.Extensions!["field"].Should().Be(OrganizationService.CriminalClearanceFieldKey);
    }

    [Fact]
    public void Flags_are_not_demanded_when_the_form_does_not_mark_them_mandatory()
    {
        var result = OrganizationService.ValidateSubmission(Request("{}", clearance: false, gdpr: false), []);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Blank_required_text_is_rejected()
    {
        RequiredField[] required = [new("name", "intake.field.name", FieldType.Text)];

        OrganizationService.ValidateSubmission(Request("""{"name":"   "}"""), required).IsFailure.Should().BeTrue();
        OrganizationService.ValidateSubmission(Request("""{"name":"Anna"}"""), required).IsSuccess.Should().BeTrue();
    }
}
