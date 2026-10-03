using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NoShowDisputeResolutionAndEnumCheckSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_decline_reason",
                schema: "public",
                table: "volunteer_applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                schema: "public",
                table: "volunteer_applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_application_steps_status",
                schema: "public",
                table: "volunteer_application_steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_application_steps_step",
                schema: "public",
                table: "volunteer_application_steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_type",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_verified_needs_date",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_password_only_for_password_auth",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_phone_auth_needs_phone",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_status",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_languages_proficiency",
                schema: "public",
                table: "user_languages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_capabilities_reason",
                schema: "public",
                table: "user_capabilities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_capabilities_source",
                schema: "public",
                table: "user_capabilities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_support_contact_method",
                schema: "public",
                table: "support_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_otp_channel",
                schema: "public",
                table: "otp_challenges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_organizations_status",
                schema: "public",
                table: "organizations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_organizations_type",
                schema: "public",
                table: "organizations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_status",
                schema: "public",
                table: "organization_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_intake_forms_type",
                schema: "public",
                table: "organization_intake_forms");

            migrationBuilder.DropCheckConstraint(
                name: "ck_submissions_status",
                schema: "public",
                table: "intake_form_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_intake_fields_type",
                schema: "public",
                table: "intake_form_fields");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funders_status",
                schema: "public",
                table: "funders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funders_type",
                schema: "public",
                table: "funders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funder_memberships_role",
                schema: "public",
                table: "funder_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funder_memberships_status",
                schema: "public",
                table: "funder_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consents_type",
                schema: "public",
                table: "consents");

            // Rows written by the old lowercase SQL scripts/seeds are normalised to the
            // enum names EF actually stores, so the re-created constraints can be added.
            migrationBuilder.Sql(@"
UPDATE public.volunteer_applications SET status = 'Open' WHERE status = 'open';
UPDATE public.volunteer_applications SET status = 'Approved' WHERE status = 'approved';
UPDATE public.volunteer_applications SET status = 'Declined' WHERE status = 'declined';
UPDATE public.volunteer_applications SET status = 'Withdrawn' WHERE status = 'withdrawn';
UPDATE public.volunteer_application_steps SET status = 'NotStarted' WHERE status = 'not_started';
UPDATE public.volunteer_application_steps SET status = 'InProgress' WHERE status = 'in_progress';
UPDATE public.volunteer_application_steps SET status = 'Completed' WHERE status = 'completed';
UPDATE public.volunteer_application_steps SET status = 'Blocked' WHERE status = 'blocked';
UPDATE public.volunteer_application_steps SET status = 'Skipped' WHERE status = 'skipped';
UPDATE public.volunteer_application_steps SET step = 'Interview' WHERE step = 'interview';
UPDATE public.volunteer_application_steps SET step = 'BackgroundCheck' WHERE step = 'background_check';
UPDATE public.volunteer_application_steps SET step = 'ConfidentialityAgreement' WHERE step = 'confidentiality_agreement';
UPDATE public.volunteer_application_steps SET step = 'Briefing' WHERE step = 'briefing';
UPDATE public.volunteer_application_steps SET step = 'Approval' WHERE step = 'approval';
UPDATE public.verifications SET status = 'NotStarted' WHERE status = 'not_started';
UPDATE public.verifications SET status = 'Pending' WHERE status = 'pending';
UPDATE public.verifications SET status = 'Submitted' WHERE status = 'submitted';
UPDATE public.verifications SET status = 'Verified' WHERE status = 'verified';
UPDATE public.verifications SET status = 'Rejected' WHERE status = 'rejected';
UPDATE public.verifications SET status = 'Expired' WHERE status = 'expired';
UPDATE public.verifications SET type = 'Email' WHERE type = 'email';
UPDATE public.verifications SET type = 'Phone' WHERE type = 'phone';
UPDATE public.verifications SET type = 'Identity' WHERE type = 'identity';
UPDATE public.verifications SET type = 'Address' WHERE type = 'address';
UPDATE public.verifications SET type = 'Organization' WHERE type = 'organization';
UPDATE public.verifications SET type = 'Training' WHERE type = 'training';
UPDATE public.verifications SET type = 'BackgroundCheck' WHERE type = 'background_check';
UPDATE public.users SET primary_auth_method = 'PhoneOtp' WHERE primary_auth_method = 'phone_otp';
UPDATE public.users SET primary_auth_method = 'EmailMagicLink' WHERE primary_auth_method = 'email_magic_link';
UPDATE public.users SET primary_auth_method = 'Password' WHERE primary_auth_method = 'password';
UPDATE public.users SET primary_auth_method = 'Google' WHERE primary_auth_method = 'google';
UPDATE public.users SET primary_auth_method = 'IdAustria' WHERE primary_auth_method = 'id_austria';
UPDATE public.users SET primary_auth_method = 'EmailPassword' WHERE primary_auth_method = 'email_password';
UPDATE public.users SET status = 'Active' WHERE status = 'active';
UPDATE public.users SET status = 'Suspended' WHERE status = 'suspended';
UPDATE public.users SET status = 'Deactivated' WHERE status = 'deactivated';
UPDATE public.users SET status = 'Deleted' WHERE status = 'deleted';
UPDATE public.user_languages SET proficiency = 'A1' WHERE proficiency = 'a1';
UPDATE public.user_languages SET proficiency = 'A2' WHERE proficiency = 'a2';
UPDATE public.user_languages SET proficiency = 'B1' WHERE proficiency = 'b1';
UPDATE public.user_languages SET proficiency = 'B2' WHERE proficiency = 'b2';
UPDATE public.user_languages SET proficiency = 'C1' WHERE proficiency = 'c1';
UPDATE public.user_languages SET proficiency = 'C2' WHERE proficiency = 'c2';
UPDATE public.user_languages SET proficiency = 'Native' WHERE proficiency = 'native';
UPDATE public.user_capabilities SET source = 'Derived' WHERE source = 'derived';
UPDATE public.user_capabilities SET source = 'Granted' WHERE source = 'granted';
UPDATE public.support_profiles SET preferred_contact_method = 'App' WHERE preferred_contact_method = 'app';
UPDATE public.support_profiles SET preferred_contact_method = 'Phone' WHERE preferred_contact_method = 'phone';
UPDATE public.support_profiles SET preferred_contact_method = 'Sms' WHERE preferred_contact_method = 'sms';
UPDATE public.support_profiles SET preferred_contact_method = 'Family' WHERE preferred_contact_method = 'family';
UPDATE public.otp_challenges SET channel = 'Sms' WHERE channel = 'sms';
UPDATE public.otp_challenges SET channel = 'Email' WHERE channel = 'email';
UPDATE public.otp_challenges SET purpose = 'Login' WHERE purpose = 'login';
UPDATE public.otp_challenges SET purpose = 'Registration' WHERE purpose = 'registration';
UPDATE public.otp_challenges SET purpose = 'PhoneChange' WHERE purpose = 'phone_change';
UPDATE public.otp_challenges SET purpose = 'EmailChange' WHERE purpose = 'email_change';
UPDATE public.otp_challenges SET purpose = 'Recovery' WHERE purpose = 'recovery';
UPDATE public.otp_challenges SET purpose = 'PhoneVerification' WHERE purpose = 'phone_verification';
UPDATE public.organizations SET status = 'Active' WHERE status = 'active';
UPDATE public.organizations SET status = 'Suspended' WHERE status = 'suspended';
UPDATE public.organizations SET status = 'Archived' WHERE status = 'archived';
UPDATE public.organizations SET type = 'Ngo' WHERE type = 'ngo';
UPDATE public.organizations SET type = 'Association' WHERE type = 'association';
UPDATE public.organizations SET type = 'Parish' WHERE type = 'parish';
UPDATE public.organizations SET type = 'Company' WHERE type = 'company';
UPDATE public.organizations SET type = 'Other' WHERE type = 'other';
UPDATE public.organization_memberships SET role = 'Staff' WHERE role = 'staff';
UPDATE public.organization_memberships SET role = 'Coordinator' WHERE role = 'coordinator';
UPDATE public.organization_memberships SET role = 'Admin' WHERE role = 'admin';
UPDATE public.organization_memberships SET role = 'SafeguardingOfficer' WHERE role = 'safeguarding_officer';
UPDATE public.organization_memberships SET role = 'Volunteer' WHERE role = 'volunteer';
UPDATE public.organization_memberships SET role = 'Client' WHERE role = 'client';
UPDATE public.organization_memberships SET status = 'Invited' WHERE status = 'invited';
UPDATE public.organization_memberships SET status = 'Active' WHERE status = 'active';
UPDATE public.organization_memberships SET status = 'Suspended' WHERE status = 'suspended';
UPDATE public.organization_memberships SET status = 'Left' WHERE status = 'left';
UPDATE public.organization_intake_forms SET form_type = 'Volunteer' WHERE form_type = 'volunteer';
UPDATE public.organization_intake_forms SET form_type = 'HelpSeeker' WHERE form_type = 'help_seeker';
UPDATE public.intake_form_submissions SET status = 'Draft' WHERE status = 'draft';
UPDATE public.intake_form_submissions SET status = 'Submitted' WHERE status = 'submitted';
UPDATE public.intake_form_submissions SET status = 'Approved' WHERE status = 'approved';
UPDATE public.intake_form_submissions SET status = 'Declined' WHERE status = 'declined';
UPDATE public.intake_form_fields SET field_type = 'Text' WHERE field_type = 'text';
UPDATE public.intake_form_fields SET field_type = 'Textarea' WHERE field_type = 'textarea';
UPDATE public.intake_form_fields SET field_type = 'SingleChoice' WHERE field_type = 'single_choice';
UPDATE public.intake_form_fields SET field_type = 'MultiChoice' WHERE field_type = 'multi_choice';
UPDATE public.intake_form_fields SET field_type = 'Boolean' WHERE field_type = 'boolean';
UPDATE public.intake_form_fields SET field_type = 'Date' WHERE field_type = 'date';
UPDATE public.intake_form_fields SET field_type = 'TimeSlots' WHERE field_type = 'time_slots';
UPDATE public.funders SET status = 'Active' WHERE status = 'active';
UPDATE public.funders SET status = 'Suspended' WHERE status = 'suspended';
UPDATE public.funders SET status = 'Ended' WHERE status = 'ended';
UPDATE public.funders SET type = 'Municipality' WHERE type = 'municipality';
UPDATE public.funders SET type = 'Foundation' WHERE type = 'foundation';
UPDATE public.funders SET type = 'PublicBody' WHERE type = 'public_body';
UPDATE public.funders SET type = 'Corporate' WHERE type = 'corporate';
UPDATE public.funder_memberships SET role = 'Viewer' WHERE role = 'viewer';
UPDATE public.funder_memberships SET role = 'Admin' WHERE role = 'admin';
UPDATE public.funder_memberships SET status = 'Invited' WHERE status = 'invited';
UPDATE public.funder_memberships SET status = 'Active' WHERE status = 'active';
UPDATE public.funder_memberships SET status = 'Suspended' WHERE status = 'suspended';
UPDATE public.funder_memberships SET status = 'Left' WHERE status = 'left';
UPDATE public.consents SET consent_type = 'Terms' WHERE consent_type = 'terms';
UPDATE public.consents SET consent_type = 'Privacy' WHERE consent_type = 'privacy';
UPDATE public.consents SET consent_type = 'NotificationsPush' WHERE consent_type = 'notifications_push';
UPDATE public.consents SET consent_type = 'NotificationsSms' WHERE consent_type = 'notifications_sms';
UPDATE public.consents SET consent_type = 'NotificationsEmail' WHERE consent_type = 'notifications_email';
UPDATE public.consents SET consent_type = 'ResearchStatistics' WHERE consent_type = 'research_statistics';
");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NoShowDisputeResolvedAtUtc",
                schema: "public",
                table: "HelpRequests",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NoShowDisputeResolvedByUserId",
                schema: "public",
                table: "HelpRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoShowDisputeUpheld",
                schema: "public",
                table: "HelpRequests",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_decline_reason",
                schema: "public",
                table: "volunteer_applications",
                sql: "status <> 'Declined' OR decline_reason IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                schema: "public",
                table: "volunteer_applications",
                sql: "status IN ('Open','Approved','Declined','Withdrawn')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_steps_status",
                schema: "public",
                table: "volunteer_application_steps",
                sql: "status IN ('NotStarted','InProgress','Completed','Blocked','Skipped')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_steps_step",
                schema: "public",
                table: "volunteer_application_steps",
                sql: "step IN ('Interview','BackgroundCheck','ConfidentialityAgreement','Briefing','Approval')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications",
                sql: "status IN ('NotStarted','Pending','Submitted','Verified','Rejected','Expired')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_type",
                schema: "public",
                table: "verifications",
                sql: "type IN ('Email','Phone','Identity','Address','Organization','Training','BackgroundCheck')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_verified_needs_date",
                schema: "public",
                table: "verifications",
                sql: "status <> 'Verified' OR verified_at_utc IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users",
                sql: "primary_auth_method IN ('PhoneOtp','EmailMagicLink','Password','Google','IdAustria','EmailPassword')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_password_only_for_password_auth",
                schema: "public",
                table: "users",
                sql: "(primary_auth_method IN ('Password','EmailPassword')) = (password_hash IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_phone_auth_needs_phone",
                schema: "public",
                table: "users",
                sql: "primary_auth_method <> 'PhoneOtp' OR phone IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                schema: "public",
                table: "users",
                sql: "status IN ('Active','Suspended','Deactivated','Deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_languages_proficiency",
                schema: "public",
                table: "user_languages",
                sql: "proficiency IN ('A1','A2','B1','B2','C1','C2','Native')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_capabilities_reason",
                schema: "public",
                table: "user_capabilities",
                sql: "source = 'Derived' OR reason IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_capabilities_source",
                schema: "public",
                table: "user_capabilities",
                sql: "source IN ('Derived','Granted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_support_contact_method",
                schema: "public",
                table: "support_profiles",
                sql: "preferred_contact_method IN ('App','Phone','Sms','Family')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_channel",
                schema: "public",
                table: "otp_challenges",
                sql: "channel IN ('Sms','Email')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges",
                sql: "purpose IN ('Login','Registration','PhoneChange','EmailChange','Recovery','PhoneVerification')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_organizations_status",
                schema: "public",
                table: "organizations",
                sql: "status IN ('Active','Suspended','Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_organizations_type",
                schema: "public",
                table: "organizations",
                sql: "type IN ('Ngo','Association','Parish','Company','Other')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships",
                sql: "role IN ('Staff','Coordinator','Admin','SafeguardingOfficer','Volunteer','Client')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_status",
                schema: "public",
                table: "organization_memberships",
                sql: "status IN ('Invited','Active','Suspended','Left')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_intake_forms_type",
                schema: "public",
                table: "organization_intake_forms",
                sql: "form_type IN ('Volunteer','HelpSeeker')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_submissions_status",
                schema: "public",
                table: "intake_form_submissions",
                sql: "status IN ('Draft','Submitted','Approved','Declined')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_intake_fields_type",
                schema: "public",
                table: "intake_form_fields",
                sql: "field_type IN ('Text','Textarea','SingleChoice','MultiChoice','Boolean','Date','TimeSlots')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funders_status",
                schema: "public",
                table: "funders",
                sql: "status IN ('Active','Suspended','Ended')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funders_type",
                schema: "public",
                table: "funders",
                sql: "type IN ('Municipality','Foundation','PublicBody','Corporate')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funder_memberships_role",
                schema: "public",
                table: "funder_memberships",
                sql: "role IN ('Viewer','Admin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funder_memberships_status",
                schema: "public",
                table: "funder_memberships",
                sql: "status IN ('Invited','Active','Suspended','Left')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consents_type",
                schema: "public",
                table: "consents",
                sql: "consent_type IN ('Terms','Privacy','NotificationsPush','NotificationsSms','NotificationsEmail','ResearchStatistics')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_decline_reason",
                schema: "public",
                table: "volunteer_applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                schema: "public",
                table: "volunteer_applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_application_steps_status",
                schema: "public",
                table: "volunteer_application_steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_application_steps_step",
                schema: "public",
                table: "volunteer_application_steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_type",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_verified_needs_date",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_password_only_for_password_auth",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_phone_auth_needs_phone",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_status",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_languages_proficiency",
                schema: "public",
                table: "user_languages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_capabilities_reason",
                schema: "public",
                table: "user_capabilities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_capabilities_source",
                schema: "public",
                table: "user_capabilities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_support_contact_method",
                schema: "public",
                table: "support_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_otp_channel",
                schema: "public",
                table: "otp_challenges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_organizations_status",
                schema: "public",
                table: "organizations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_organizations_type",
                schema: "public",
                table: "organizations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_status",
                schema: "public",
                table: "organization_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_intake_forms_type",
                schema: "public",
                table: "organization_intake_forms");

            migrationBuilder.DropCheckConstraint(
                name: "ck_submissions_status",
                schema: "public",
                table: "intake_form_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_intake_fields_type",
                schema: "public",
                table: "intake_form_fields");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funders_status",
                schema: "public",
                table: "funders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funders_type",
                schema: "public",
                table: "funders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funder_memberships_role",
                schema: "public",
                table: "funder_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funder_memberships_status",
                schema: "public",
                table: "funder_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consents_type",
                schema: "public",
                table: "consents");

            migrationBuilder.DropColumn(
                name: "NoShowDisputeResolvedAtUtc",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropColumn(
                name: "NoShowDisputeResolvedByUserId",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropColumn(
                name: "NoShowDisputeUpheld",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_decline_reason",
                schema: "public",
                table: "volunteer_applications",
                sql: "status <> 'declined' OR decline_reason IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                schema: "public",
                table: "volunteer_applications",
                sql: "status IN ('open','approved','declined','withdrawn')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_steps_status",
                schema: "public",
                table: "volunteer_application_steps",
                sql: "status IN ('not_started','in_progress','completed','blocked','skipped')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_steps_step",
                schema: "public",
                table: "volunteer_application_steps",
                sql: "step IN ('interview','background_check','confidentiality_agreement','briefing','approval')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications",
                sql: "status IN ('not_started','pending','submitted','verified','rejected','expired')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_type",
                schema: "public",
                table: "verifications",
                sql: "type IN ('email','phone','identity','address','organization','training','background_check')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_verified_needs_date",
                schema: "public",
                table: "verifications",
                sql: "status <> 'verified' OR verified_at_utc IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users",
                sql: "primary_auth_method IN ('phone_otp','email_magic_link','password','google','id_austria','email_password')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_password_only_for_password_auth",
                schema: "public",
                table: "users",
                sql: "(primary_auth_method = 'password') = (password_hash IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_phone_auth_needs_phone",
                schema: "public",
                table: "users",
                sql: "primary_auth_method <> 'phone_otp' OR phone IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                schema: "public",
                table: "users",
                sql: "status IN ('active','suspended','deactivated','deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_languages_proficiency",
                schema: "public",
                table: "user_languages",
                sql: "proficiency IN ('a1','a2','b1','b2','c1','c2','native')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_capabilities_reason",
                schema: "public",
                table: "user_capabilities",
                sql: "source = 'derived' OR reason IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_capabilities_source",
                schema: "public",
                table: "user_capabilities",
                sql: "source IN ('derived','granted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_support_contact_method",
                schema: "public",
                table: "support_profiles",
                sql: "preferred_contact_method IN ('app','phone','sms','family')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_channel",
                schema: "public",
                table: "otp_challenges",
                sql: "channel IN ('sms','email')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges",
                sql: "purpose IN ('login','registration','phone_change','email_change','recovery','phone_verification')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_organizations_status",
                schema: "public",
                table: "organizations",
                sql: "status IN ('active','suspended','archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_organizations_type",
                schema: "public",
                table: "organizations",
                sql: "type IN ('ngo','association','parish','company','other')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships",
                sql: "role IN ('staff','coordinator','admin','safeguarding_officer','volunteer','client')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_status",
                schema: "public",
                table: "organization_memberships",
                sql: "status IN ('invited','active','suspended','left')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_intake_forms_type",
                schema: "public",
                table: "organization_intake_forms",
                sql: "form_type IN ('volunteer','help_seeker')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_submissions_status",
                schema: "public",
                table: "intake_form_submissions",
                sql: "status IN ('draft','submitted','approved','declined')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_intake_fields_type",
                schema: "public",
                table: "intake_form_fields",
                sql: "field_type IN ('text','textarea','single_choice','multi_choice','boolean','date','time_slots')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funders_status",
                schema: "public",
                table: "funders",
                sql: "status IN ('active','suspended','ended')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funders_type",
                schema: "public",
                table: "funders",
                sql: "type IN ('municipality','foundation','public_body','corporate')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funder_memberships_role",
                schema: "public",
                table: "funder_memberships",
                sql: "role IN ('viewer','admin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funder_memberships_status",
                schema: "public",
                table: "funder_memberships",
                sql: "status IN ('invited','active','suspended','left')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consents_type",
                schema: "public",
                table: "consents",
                sql: "consent_type IN ('terms','privacy','notifications_push','notifications_sms','notifications_email','research_statistics')");
        }
    }
}
