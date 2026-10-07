# Organization Onboarding Guide (P9-07)

> **Audience:** platform operators and self-onboarding organizations (NGOs, municipalities, volunteer centres, associations).
> **Scope:** how any organization becomes a tenant of a SeniorConnect deployment and configures it end-to-end. **No specific partner is required or named** (ADR-028).
>
> Related: [Organization & Community Onboarding Playbook](../operations/pilot-onboarding-playbook.md) (people-facing launch playbook),
> [Multi-tenancy — Organizations](../architecture/data-model.md#7-organizations), [Authorization](../architecture/authorization.md),
> [DPA template](../legal/dpa-template.md), [ADR-028 — Project Independence](../decisions/ADR-028-project-independence.md).

---

## 0. Model in one paragraph

SeniorConnect is multi-tenant: one deployment hosts many organizations. Each
organization is an `organizations` row with its own members, intake forms,
activities, help requests, and reporting. Roles are granted **within** an
organization; all list endpoints are tenant-scoped and a lookup in another
tenant's data returns `404`, not `403` (cross-tenant leakage is never confirmed).
The platform **MUST** also provide value with zero organizations present —
community mode is the default, organizations are an opt-in extension.

## 1. Roles

| Role | Grants | Typical holder |
| --- | --- | --- |
| Platform operator | Runs the deployment; creates orgs and org admins. Not a data role. | Server admin |
| Organization administrator | Manages the org's profile, coordinators, intake forms, reporting. | Org office |
| Coordinator | Rosters, verifications, attention flags, impact reports. | Org staff / volunteer lead |
| Volunteer | Activity participation, help assignments. Trust tiers 1–3 per the [onboarding playbook](../operations/pilot-onboarding-playbook.md) (§4 verification flow). | Member |
| Senior / family | Help requests, event participation, delegation. | End users |

## 2. Prerequisites (operator checklist)

- [ ] Deployment is running and healthy (`/healthz/live`, `/healthz/ready` — see [deployment guide](../operations/deployment-guide.md)).
- [ ] For EU personal-data usage: DPA signed between operator and org ([dpa-template.md](../legal/dpa-template.md)); processing record updated ([verarbeitungsverzeichnis.md](../legal/verarbeitungsverzeichnis.md)); DPIA check for the org's planned usage ([dpia.md](../legal/dpia.md)).
- [ ] Org appoints a safeguarding lead who will hold the `SafeguardingOfficer` capability (BR-SG-02) before Trust Level 3 verification is used.
- [ ] Insurance check for volunteer transport (BR-TRANSPORT, BR-SAFETY-06) — org's responsibility.
- [ ] Physical Zugangskarten stock if seniors without smartphones will be onboarded ([zugangskarte-spec.md](zugangskarte-spec.md)).

## 3. End-to-end onboarding walkthrough (the P9-07 test)

Generic worked example with a fictional org; the same steps apply to any
NGO, municipality, volunteer centre, or association.

**Step 1 — create the organization (platform operator):**

```http
POST /api/v1/organizations
Content-Type: application/json

{
  "name": "Verein Nachbarschaft Musterstadt",
  "legalName": "Verein Nachbarschaft Musterstadt e.V.",
  "registrationNumber": "AT-ZVR-123456789",
  "postalCode": "1010",
  "contactEmail": "office@nachbarschaft-musterstadt.example",
  "contactPhone": "+43 1 0000000"
}
```

**Step 2 — appoint an org administrator** (platform operator grants the
organization-admin capability to the org office account; consult
[authorization.md](../architecture/authorization.md) for the capability model).

**Step 3 — configure the org profile** (org administrator): logo, contact
channels, opening hours, region/postal-code coverage. These fields drive
proximity discovery and the public org page.

**Step 4 — activate the standard intake taxonomy** (org administrator):

```http
POST /api/v1/organizations/{formId:guid}:activate-template
```

This activates the **standard Austrian volunteer-centre intake taxonomy**
(BR-ORG-FORM-03) as the org's dynamic intake form. It is generic platform
functionality; per-organization customization is done via the form editor
(ADR-021 custom forms). The template is also available in the seeder for
local/demo environments (`SeedStandardTemplateAsync`).

**Step 5 — designate coordinators:** org administrator grants the coordinator
capability to staff. Coordinators manage rosters, verifications, attention
flags, and impact reports (see the daily routine in the
[playbook](../operations/pilot-onboarding-playbook.md#5-daily-coordinator-operational-routine)).

**Step 6 — invite members:** volunteers self-register (phone OTP) and are
verified through the three trust tiers; seniors are onboarded via family-led
path (J1) or coordinator-assisted path (J2) — both described in the playbook.

**Step 7 — verify tenant isolation end-to-end:** as a smoke test, confirm that
the new org's coordinator can see **only** its own roster, activities, and
reports, and that a second org on the same deployment returns 404 on cross-tenant
lookups. This is the acceptance criterion "a generic organization can be created
and configured end-to-end".

## 4. What the org gets / what stays with the operator

| Org configures | Operator maintains |
| --- | --- |
| Profile, intake forms, coordinators, reporting cadence | Deployment, backups ([backup-and-restore](../operations/backup-and-restore.md)), updates, EU hosting |
| Trust-tier verification decisions (BR-TRUST-*) | DB migrations at deploy time (`dotnet ef database update` / runbook §3) |
| Safeguarding cases in the dedicated schema (accessed by safeguarding lead only) | Incident response ([incident-response-runbook.md](../operations/incident-response-runbook.md)) |

## 5. Naming note

The standard intake template is *based on* the Austrian volunteer-centre intake
taxonomy (a public, generic taxonomy). It is **not** branded to any specific
organization. The seeder's sample org ("Musterverein Nachbarschaftshilfe (Demo)")
is clearly-labelled sample data for local environments and is never auto-created
in production.
