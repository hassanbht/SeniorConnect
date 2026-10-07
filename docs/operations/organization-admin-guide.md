# Organization Administrator Guide (P9-09)

> **Audience:** administrators and coordinators of an organization using a
> SeniorConnect deployment (NGO, municipality, volunteer centre, association).
> **Scope:** everything an org admin does after the org is onboarded
> ([onboarding guide](../onboarding/organization-onboarding-guide.md)) —
> no deployment access needed.
>
> Related: [Organization & Community Onboarding Playbook](pilot-onboarding-playbook.md),
> [Authorization](../architecture/authorization.md), [ADR-021 custom forms](../decisions/ADR-021-austria-auth-geo-custom-forms.md).

---

## 1. Your capabilities

As organization administrator you manage, **within your organization only**:

- the org profile (name, contact channels, service region/postal codes, logo);
- coordinators (grant/revoke the coordinator capability);
- the dynamic intake form — use the standard template or customize it (ADR-021);
- impact reporting exports (Excel/PDF) for funders and your own board;
- the attention queues that coordinators work from.

Everything is tenant-scoped: data of other organizations on the same deployment
is invisible to you (lookups return `404`).

## 2. Setting up your intake form

1. Open the organization profile in the app (or call `POST /api/v1/organizations/{formId}:activate-template`).
2. **Activate standard template** — loads the standard Austrian volunteer-centre
   intake taxonomy (BR-ORG-FORM-03); the Flutter action is
   `activateStandardTemplate` (translations key `activate_standard_template`).
3. Customize: add/adjust form fields to your local needs; versions are kept so
   past submissions stay interpretable.
4. Share the intake link with volunteers and members.

## 3. Coordinators and the daily routine

Grant the coordinator capability to staff who will run rosters and verifications.
Their daily routine (attention queues, verifications, reporting) is described in
the [playbook §5](pilot-onboarding-playbook.md#5-daily-coordinator-operational-routine):

- Morning sweep: expiring verifications, unconfirmed hours.
- Weekly: silent volunteers (> 60 days), broadcast check-ins.
- Monthly: Excel impact report → funders / municipal council (P2-31).

## 4. Volunteer trust tiers (BR-TRUST-*)

| Tier | Requirement | Unlocks |
| --- | --- | --- |
| 1 — Community | Phone OTP verified | Public events, non-home tasks |
| 2 — Basic Verified | Coordinator in-person interview | Home dropoff, shopping help |
| 3 — Safeguarded | Strafregisterbescheinigung (Kinder- und Jugendfürsorge) verified | In-home accompaniment, key custody |

Appoint one person as safeguarding lead (holds `SafeguardingOfficer`,
BR-SG-02) **before** any Tier-3 verification; safeguarding cases live in a
separate schema only this role can access ([safeguarding-scoping-policy](../security/safeguarding-scoping-policy.md)).

## 5. Seniors & families

- **Family-led (J1):** a family member sets up the senior's account and prints a
  Zugangskarte code — see [zugangskarte-spec](../onboarding/zugangskarte-spec.md).
- **Coordinator-assisted (J2):** phone OTP sign-in together with the senior;
  the app switches to Senior Mode (64 dp targets, high contrast, simplified
  navigation) and shows a first-time tutorial.

## 6. Reporting

- Excel export: `GET /api/v1/reporting/organizations/{orgId}/export.xlsx` —
  volunteer hours and activity metrics for funding reconciliation.
- Annual report view: neutral organization title, no invented official figures
  (reported figures come from real recorded activity only).

## 7. Leaving the platform

An organization can stop using a deployment at any time (independence works in
both directions, ADR-028): export your reports, request deletion of org data per
the operator's process and the [data-retention-policy](../legal/data-retention-policy.md).
Participant accounts remain under the participants' own consent and deletion
rights ([privacy policy](../legal/privacy-policy.md)).
