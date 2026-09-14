# Business Rules

> These rules are binding. Code that violates a rule here is a defect regardless of tests.
> Changing a rule requires an ADR in `docs/decisions/`.

---

## 1. Scope of mediation (BR-SCOPE)

**BR-SCOPE-01** SeniorConnect mediates only **non-medical, non-nursing, unpaid** everyday help
and social activities.

**BR-SCOPE-02** The following categories are permanently blocked and can never be created
as a `HelpRequest`:

```
personal hygiene / Körperpflege
medication administration
wound care, injections, any clinical procedure
lifting or transferring a person
any task offered or requested for payment between private individuals
overnight / 24-hour care
```

**BR-SCOPE-03** When a user's request text or chosen category triggers a blocked topic,
the system responds with a **referral** (contact details of professional services in the
region) and does **not** create a request. This event is logged as
`BlockedCategoryReferral` for product learning.

**BR-SCOPE-04** Any request describing symptoms of an acute medical emergency triggers the
**Emergency path**: display 144 / 112 prominently, offer to call, notify trusted contacts
if configured. It is never converted into a normal help request. This applies to voice
input in later phases as well.

**BR-SCOPE-05** Emergency ≠ Safety Alert. "Emergency" routes to official services.
"Safety Alert" (senior did not arrive, volunteer raised a concern, family requested a
welfare check) is an internal workflow and must never be presented as an emergency service.

---

## 2. Identity, trust and capability (BR-TRUST)

**BR-TRUST-01** Three concepts are separate and must never be merged in code:

```
Identity  — is this person who they claim to be?
Trust     — how much has been verified about them?
Capability— what are they allowed to do right now?
```

**BR-TRUST-02** Trust levels:

| Level | Name | Achieved by |
| --- | --- | --- |
| 0 | Registered | account created |
| 1 | Contact Verified | email **and** phone verified |
| 2 | Identity Verified | identity confirmed via an approved provider or manual staff review |
| 3 | Address Verified | address confirmed (Meldebestätigung, postal code letter, or org confirmation) |
| 4 | Organization Verified | an organization vouches for this person |
| 5 | Safety Verified | background check recorded **and** required training completed **and** organization approval |

**BR-TRUST-03** Trust level is **computed server-side** from verification records. It is
never stored as a client-writable field and never sent by the client.

**BR-TRUST-04** The platform never asserts that a person is safe. Badges state
*what was verified, by whom, and when*. UI copy must reflect this.

**BR-TRUST-05** Verification documents (ID scans, Strafregisterbescheinigung) are
**not stored** by the platform in MVP. Only the outcome is stored:

```
verification_type, status, verified_by_org_id, verified_by_user_id,
verified_at, valid_until, external_reference
```

**BR-TRUST-06** Identity verification is behind `IIdentityVerificationProvider`.
MVP implementation: `ManualVerificationProvider` + `OrganizationVerificationProvider`.
`IdAustriaProvider` and `KycProvider` are added later with **zero business-logic change**.

**BR-TRUST-07** Verifications expire. An expired verification silently reduces the
computed trust level; the affected user is notified and their assignments above the new
level are flagged for the coordinator — **never auto-cancelled without a human seeing it**.

---

## 3. Safety levels (BR-SAFETY)

**BR-SAFETY-01** Every activity and help-request category has a **required safety level**,
determined **exclusively server-side** by `IActivitySafetyPolicy`. The client sends only
the category and context.

**BR-SAFETY-02** Reference mapping (configurable, not hardcoded):

| Level | Description | Example | Minimum trust |
| --- | --- | --- | --- |
| 1 | Public, group, no isolation | walking group, café meetup | 1 |
| 2 | One-to-one, public space | shopping together, accompaniment to a shop | 2 |
| 3 | One-to-one, semi-private / institutional | doctor's appointment, Behördengang | 3 + org or platform review |
| 4 | Inside the person's home | home visit, repairs, help unpacking | 5 + org approval |
| 5 | Home visit with a person flagged as highly vulnerable | cognitive impairment, no family contact | 5 + org approval + named coordinator |

**BR-SAFETY-03** Matching invariant:

```
volunteer.effectiveTrustLevel >= request.requiredTrustLevel
AND volunteer has all required capabilities
AND organization policy permits
```
All three conditions are evaluated server-side. Failing any one excludes the candidate.

**BR-SAFETY-04** The same category can require different levels in different contexts
(e.g. "shopping" in public = L2, "collect medication and bring it into the flat" = L4).
`IActivitySafetyPolicy` receives the full context, not just the category enum.

**BR-SAFETY-05** **Buddy rule.** A volunteer's **first three** Level-3+ activities must be
either accompanied by an experienced volunteer or explicitly waived by a coordinator, with
the waiver recorded and attributed.

**BR-SAFETY-06** Insurance context is recorded on every assignment:
`OrganizationCovered | PrivateNeighbourly | Unknown`. Level 3+ assignments require
`OrganizationCovered`, or an explicit acknowledged disclaimer that is stored with the
assignment.

---

## 4. Help request lifecycle (BR-HELP)

**BR-HELP-01** States:

```
Draft → Open → Matching → Offered → Assigned → InProgress → Completed
                    ↓         ↓         ↓          ↓
                 Cancelled  Cancelled Cancelled  Cancelled
                                                     ↓
                                                 NoShow
                          Open → Expired (no match before the date)
```

**BR-HELP-02** Only these transitions are legal. Any other transition attempt is a
`409 Conflict` with a machine-readable reason code.

**BR-HELP-03** Assignment is **atomic and race-safe**. Use a conditional update:

```sql
UPDATE help_requests
   SET assigned_volunteer_id = @v, status = 'assigned', row_version = row_version + 1
 WHERE id = @id AND status = 'offered' AND row_version = @rv;
```
`RowsAffected = 0` → someone else took it → return 409 with `HELP_ALREADY_ASSIGNED`.

**BR-HELP-04** A request created by a family member on behalf of a senior records **both**
`created_by_user_id` and `subject_user_id`, and requires the corresponding
`FamilyPermission.CanCreateHelpRequests`.

**BR-HELP-05** Cancellation always requires a reason from a fixed list plus optional free
text. Reasons feed reliability metrics.

**BR-HELP-06** `NoShow` can only be set at least 30 minutes after the scheduled start, by
the counterparty or a coordinator, and always notifies the affected person with a chance
to dispute. A disputed no-show does not count against reliability until resolved.

**BR-HELP-07** Completion records actual duration. Volunteer hours are derived from
completed activities only — never self-declared without a counterpart or coordinator
confirmation.

---

## 5. Family relationships (BR-FAMILY)

**BR-FAMILY-01** A family relationship grants **nothing** by default. Every permission is
granted individually.

**BR-FAMILY-02** Permission set:

```
CanViewActivities
CanViewHelpRequests
CanCreateHelpRequests
CanManageEvents
CanReceiveSafetyNotifications
CanManageProfile
CanBeEmergencyContact
```

**BR-FAMILY-03** Permissions are granted by the senior, or — where the senior cannot use
the app — by an authorised organization staff member, with the reason recorded.
Never by the family member themselves.

**BR-FAMILY-04** The senior can revoke any permission at any time, in one step, and the
revocation takes effect immediately.

**BR-FAMILY-05** A family member **never** sees: private messages, safeguarding cases,
verification documents, or any data about third parties (other seniors, volunteers'
personal details beyond what a senior sees).

**BR-FAMILY-06** Every access by a family member to senior data is written to the audit log.

---

## 6. Safeguarding (BR-SG)

**BR-SG-01** Safeguarding is a **separate subsystem** with its own authorization policy.
It is not an extension of the ordinary reporting feature.

**BR-SG-02** Access requires the explicit `SafeguardingOfficer` capability.
`OrganizationAdmin` does **not** imply it.

**BR-SG-03** A safeguarding case is never deleted, only resolved and archived. Retention
follows a defined policy, separately from ordinary data.

**BR-SG-04** Anyone involved in an activity can raise a concern in one tap. The reporter's
identity is stored but is **not shown** to the subject of the case.

**BR-SG-05** Case data must never appear in ordinary reports, exports, dashboards,
notifications, or search results.

**BR-SG-06** Every read of a safeguarding case is audited, including the reader, timestamp
and the case ID.

---

## 7. Multi-tenancy and scope (BR-TENANT)

**BR-TENANT-01** Scope model:

```
Platform      — visible to everyone
Community     — visible in a geographic area
Organization  — visible only within one organization
Private       — visible only to explicitly invited participants
```

**BR-TENANT-02** `OrganizationId` is **nullable** on Groups, Events and Help Requests.
A null organization is valid and means "independent / community".

**BR-TENANT-03** Organization A can never read Organization B's data. Enforced by a global
query filter **and** verified by an architecture test, not by developer discipline.

**BR-TENANT-04** Cross-organization data aggregation is allowed only for anonymised
platform-level statistics, and only where re-identification is not possible
(minimum cohort size 10).

---

## 8. Audit (BR-AUDIT)

**BR-AUDIT-01** Auditable events (minimum): authentication, trust/verification changes,
help-request state transitions, assignment changes, family-permission changes, any read of
a senior profile by staff, any safeguarding access, data export, data deletion.

**BR-AUDIT-02** Each entry records: actor, action, subject, timestamp (UTC), organization
context, **and the reason where the action requires one**.

**BR-AUDIT-03** Audit entries are append-only. No update, no delete, ever.

---

## 9. Communication (BR-COMM)

**BR-COMM-01** No open private messaging in MVP. Conversations exist only in a context:
a help request, an event, or a group.

**BR-COMM-02** A conversation is closed 14 days after its context completes; content
remains readable to participants for the retention period but no new messages can be sent.

**BR-COMM-03** Report and Block are available in every conversation, on every screen.

**BR-COMM-04** Phone numbers and addresses are revealed only after an assignment is
confirmed, and only to the assigned counterparty — never in a public list.

---

## 10. Data protection (BR-GDPR)

**BR-GDPR-01** Data classes with separate access policies:
`PublicProfile` · `PersonalData` · `SensitiveData` · `HealthRelatedData` · `SafeguardingData`.

**BR-GDPR-02** Health-related data is **avoided by design**. If a limitation must be
recorded, store a functional need ("braucht Begleitung beim Gehen"), never a diagnosis.

**BR-GDPR-03** Every user can export their data (machine-readable) and request deletion
from within the app.

**BR-GDPR-04** Deletion is a two-tier process: personal data erased/anonymised; audit and
safeguarding records retained under the documented legal basis, with personal identifiers
pseudonymised where legally permissible.

**BR-GDPR-05** No third-party analytics, advertising or tracking SDKs. Ever.

**BR-GDPR-06** No real personal data enters any environment before a legal entity,
privacy policy, processing agreement and security review exist. This is a hard gate
before Phase 7.

---

# Rules added after discovery (v2)

> Source: `docs/product/discovery-findings.md`. Each rule cites the finding that
> produced it, so a future reader can see the evidence — or the lack of it.

## 11. Transport (BR-TRANSPORT) — *from F4*

**BR-TRANSPORT-01** Transporting a person in a **private vehicle** is a distinct
risk dimension, orthogonal to Safety Levels 1–5. Safety Levels model *access to a
person*; transport models *liability for a journey*. An activity can be Safety
Level 1 and still be a transport activity.

**BR-TRANSPORT-02** Every activity carries `involves_transport` (bool) and, when
true, `transport_mode`:

```
PublicTransportTogether   accompanying on a bus or train — not a transport activity
VolunteerPrivateVehicle   ← the regulated case
OrganizationVehicle
Taxi / ProfessionalService
```

**BR-TRANSPORT-03** `VolunteerPrivateVehicle` requires **all** of:

```
a recorded, unexpired driving-licence verification
insurance_context = OrganizationCovered, OR an explicitly acknowledged and
    stored disclaimer naming who carries the risk
the senior (or an authorised family member) informed before confirmation
```

**BR-TRANSPORT-04** An activity with `transport_mode = VolunteerPrivateVehicle`
and `insurance_context = Unknown` **cannot be confirmed**. Not a warning — a block.

**BR-TRANSPORT-05** The platform never states that a journey is insured. It states
what has been recorded, by whom, and until when — consistent with BR-TRUST-04.

*Rationale: F4 records a real dispute between a Gemeinde and an association after
a volunteer's car accident, where coverage was unclear. This is the most likely
way a pilot ends badly and publicly.*

---

## 12. Physical keys (BR-KEYS) — *from F5*

**BR-KEYS-01** Any handover of a physical key to a volunteer is recorded:

```
key_custody: subject_user_id · holder_user_id · organization_id ·
             description ("Wohnungstür, 1 Schlüssel, gelber Anhänger") ·
             handed_over_at · handed_over_by · returned_at · received_by ·
             status (Held | Returned | Reported Lost)
```

**BR-KEYS-02** A key handover requires the senior's (or an authorised family
member's) explicit confirmation, recorded.

**BR-KEYS-03** A volunteer whose status becomes `Inactive`, or whose verification
expires, while still holding a key triggers a coordinator task. It is never
resolved automatically.

**BR-KEYS-04** `Reported Lost` immediately notifies the coordinator and creates a
safeguarding-adjacent task. It is not a safeguarding case unless someone escalates it.

**BR-KEYS-05** The platform stores no key codes, no photographs of keys, and no
lock or alarm information.

*Rationale: F5 — a lost key led to replacing the locks of an entire building, paid
by the organisation's liability insurance.*

---

## 13. Expense records (BR-EXPENSE) — *from F6*

**BR-EXPENSE-01** Shopping and errand activities may carry an expense record:

```
amount_given · amount_spent · amount_returned · currency ·
receipt_image_id (optional) · confirmed_by_subject · confirmed_by_volunteer
```

**BR-EXPENSE-02** The record is **optional but strongly prompted** whenever money
changes hands. It is a neutral record protecting both parties, not an accounting
feature and not a payment feature. **No money moves through the platform, ever**
(BR-SCOPE-02).

**BR-EXPENSE-03** `amount_given - amount_spent` must equal `amount_returned`, or
the discrepancy must be explained in a required note.

**BR-EXPENSE-04** Either party may mark an expense record as disputed. A dispute
creates a coordinator task and, if it concerns a person's conduct or capacity, may
be escalated to a safeguarding case with category `Vorwurf / Missverständnis`.

**BR-EXPENSE-05** A receipt image is stored encrypted, is visible only to the two
parties and a coordinator, and is deleted after the retention period for ordinary
activity data — never longer.

*Rationale: F6 — a senior with dementia believed a volunteer had returned the
wrong change. This class of incident is predictable and the current answer is a
phone call to the coordinator.*

---

## 14. Funder scope (BR-FUNDER) — *from F7, see ADR-017*

**BR-FUNDER-01** A **Funder** (Gemeinde, foundation, public body) is not an
organization. It owns no users, employs no volunteers, and runs no activities.

**BR-FUNDER-02** A funder principal can reach **only** aggregate endpoints under
`/api/v1/funder/...`. There is no code path from a funder token to a name,
address, phone number, email address, or any free text written by a user.

**BR-FUNDER-03** **Minimum cohort size 10.** Any aggregate covering fewer than 10
individuals renders as `<10`. Suppression must also defeat inference by
subtraction between adjacent cells — suppress complementary cells as well.

**BR-FUNDER-04** A funder sees only the organizations it funds, only within the
`reporting_scope` and the date range of an active funding relationship.

**BR-FUNDER-05** Funder access is audited identically to any other access.

**BR-FUNDER-06** The funder surface is a separate API namespace with dedicated
aggregate queries. It is never the organization endpoints with a filter applied.
An architecture test asserts that no DTO reachable from that namespace contains a
field classified `PersonalData` or above.

*Rationale: F7 — "the municipality must never see the seniors' names in an
analytics dashboard." Modelling a Gemeinde as a tenant would grant it a data
surface it is legally not permitted to have.*

---

## 15. Authentication (BR-AUTH) — *from F3, see ADR-016 & ADR-021*

**BR-AUTH-01** Authentication provides a multi-provider tier for maximum reach:
- **Google Sign-In** (OAuth 2.0 / OpenID Connect)
- **Email + Password + Password Confirmation** (with email verification token)
- **ID Austria** (Austrian national eIDAS-compliant digital identity)
- **Phone number + SMS OTP**

**BR-AUTH-02** Email + Password registration requires identical password and password confirmation inputs, followed by a mandatory email verification link. The account status reflects `email_verified = false` until the link is verified.

**BR-AUTH-03** Password fields are permitted on the email registration/login screen, provided one-tap passwordless alternatives (Google Sign-In, ID Austria, and SMS OTP) remain clearly visible to accommodate users who struggle with complex passwords.

**BR-AUTH-04** Sessions last 90 days on a device marked personal, refreshed
silently. Sensitive screens re-authenticate with biometrics, device PIN, or fresh token.

**BR-AUTH-05** Re-authentication is required only for: changing the phone number,
changing family permissions, and any safeguarding screen.

**BR-AUTH-06** Changing a phone number invalidates every session, notifies the old
number and the email address, and is audited. *(SIM-swap mitigation.)*

**BR-AUTH-07** OTP requests are rate-limited per number and per IP, and OTP codes
expire in 5 minutes with a maximum of 5 verification attempts.

**BR-AUTH-08** **ID Austria Trust Elevation:** Logging in via ID Austria verifies the citizen's legal identity through the Austrian federal trust federation, automatically granting Trust Level 1/2 without requiring paper ID scans or manual coordinator verification.

**BR-AUTH-09** **In-Profile Mobile Phone Verification:** Users can supply or update their mobile phone number in their Profile. An explicit "Verify Phone Number" button dispatches an SMS OTP challenge, setting `phone_verified_at_utc` upon successful confirmation.

**BR-AUTH-10** **Email Verification Enforcement:** Email accounts must complete email verification within 24 hours of registration. Unverified accounts cannot create community announcements, apply to organizations, or accept assignments.

*Rationale: F3 & ADR-021 — balancing zero-barrier login for seniors with mainstream Google OAuth, sovereign ID Austria verification, and standard email registration with strong email/phone verification guards.*

---

## 16. Notification budget (BR-NOTIFY) — *from F3*

**BR-NOTIFY-01** The platform has a **hard notification budget, enforced in code**.
Default state, per user:

```
Per assignment:        at most 2 pushes   (T-24h, T-2h)
Per week, everything else: at most 3 pushes
Marketing / engagement / "we miss you" pushes:  ZERO, permanently
```

**BR-NOTIFY-02** Anything above the budget is silently downgraded to in-app only.
Exceeding the budget is a bug, and a test asserts a simulated week stays inside it.

**BR-NOTIFY-03** Exempt from the budget, because they are user-initiated or safety
critical: a match found for a request the user created, a cancellation of something
they are attending today, a safety alert, a safeguarding notification to an officer.

**BR-NOTIFY-04** Quiet hours default to 21:00–08:00 and are respected by everything
except a safety alert.

**BR-NOTIFY-05** Every notification category can be turned off individually. There
is no category the user cannot disable except a safeguarding notification to an
appointed officer.

*Rationale: F3 — "an app that sent a lot of spam notifications … I deleted it."
Notification volume is an uninstall cause in this population, not an engagement lever.*

---

## 17. Roster status (BR-ROSTER) — *from F2*

**BR-ROSTER-01** A volunteer's activity status is **computed from behaviour**,
never set by hand:

```
NeverActivated  no completed activity, ever
Active          a completed activity in the last 60 days
Dormant         last completed activity 60–120 days ago
Inactive        no completed activity for more than 120 days
```

**BR-ROSTER-02** Thresholds are configuration, not constants.

**BR-ROSTER-03** All roster counts shown to a coordinator or reported to a funder
use `Active`, never total roster size. A report that says "60 volunteers" when 22
are active is a false statement to a funder.

**BR-ROSTER-04** Transition to `Dormant` triggers a reactivation prompt to the
volunteer and appears in the coordinator's "needs attention" list.

**BR-ROSTER-05** Status never changes a volunteer's permissions. It is
informational and drives outreach, not authorization.

*Rationale: F2 — "60 on the list, 22 genuinely active, tracked from memory."*

---

## 18. Onboarding pipeline visibility (BR-ONBOARD) — *from F10*

**BR-ONBOARD-01** A volunteer applicant can always see their own pipeline status
and how long each step has been open.

**BR-ONBOARD-02** A step open longer than its configured SLA (default 14 days)
appears in the coordinator's "needs attention" list.

**BR-ONBOARD-03** The platform automates none of the checks. It makes the wait
**legible**. The interview, the Strafregisterbescheinigung and the briefing remain
human, organizational processes.

*Rationale: F10 — the coordinator describes 2–4 weeks as normal; the volunteer
describes 3 weeks as long. A volunteer who hears nothing for three weeks is lost.*

---

## 19. What the volunteer sees about a person (BR-VISIBILITY) — *from F8*

**BR-VISIBILITY-01** Information shown to a volunteer about a senior is phrased as
an **operational instruction**, never as a condition or a diagnosis.

```
✓ "Dritter Stock, kein Aufzug. Bitte klingeln und etwas warten."
✗ "Eingeschränkte Mobilität nach Hüftoperation."
```

**BR-VISIBILITY-02** The field is labelled **„Was Sie wissen sollten"** in the UI,
has a character limit, and shows worked examples. It must not become a free-text
medical field by accident.

**BR-VISIBILITY-03** Address and phone number are revealed only after an assignment
is confirmed, only to the assigned counterparty (BR-COMM-04).

*Rationale: F8 — the volunteer actively does not want health information, for
liability reasons: "I prefer to know nothing about illnesses." Data minimisation
here is a user requirement, not only a compliance requirement.*

---

## 20. Austrian Geography, Address Geocoding & Proximity Discovery (BR-GEO) — *see ADR-021*

**BR-GEO-01** **Austrian Administrative Hierarchy Master Dataset:**
The platform maintains an official directory of all Austrian administrative divisions:
- 9 Federal States (*Bundesländer*: Wien, Tirol, Vorarlberg, Salzburg, Kärnten, Steiermark, Oberösterreich, Niederösterreich, Burgenland)
- 94 Districts (*Bezirke* and *Statutarstädte*)
- 2,093 Municipalities (*Gemeinden*)
- All official Postal Codes (*Postleitzahlen / PLZ*) and localities (*Ortschaften*).

**BR-GEO-02** **Smart Address Completion:**
Profile address entry provides cascading dropdowns and autocomplete driven by the Austrian administrative hierarchy. A user selecting a PLZ or typing a Gemeinde gets immediate canonical validation.

**BR-GEO-03** **Address Geocoding & Map Confirmation:**
Users can trigger address resolution via an "Address Lookup" action (`POST /reference/geocode-address`), which resolves latitude and longitude coordinates and renders an interactive map pin for confirmation before persisting.

**BR-GEO-04** **Spatial Proximity Computation:**
Distances between users, organizations, and activities are computed server-side via spatial math (Haversine/PostGIS) to dynamically determine the closest towns/cities and match individuals within configurable radii (default 5 km, 10 km, 25 km or same Gemeinde/Bezirk).

**BR-GEO-05** **Mutual Local Discovery:**
- **Citizens & Seniors:** Can browse nearby social organizations/charities and independent/free volunteers (*Freiwillige*) operating within their geographic perimeter.
- **Independent Volunteers:** Can browse nearby registered social organizations and active community help requests within their preferred distance radius (`max_distance_km`).

**BR-GEO-06** **Privacy Location Fuzzing:**
Exact street numbers and coordinate precision are fuzzed in public and discovery views. Unassigned users see only the locality name, postal code, and approximate distance. Precise address coordinates are strictly unmasked only after mutual confirmation of an assignment.

---

## 21. Organization Custom Intake Forms & Volunteer Categorization (BR-ORG-FORM) — *see ADR-021*

**BR-ORG-FORM-01** **Custom Membership & Intake Form Builder:**
Every social organization can configure distinct digital intake forms for:
- Volunteer applicants (*Freiwilligen-Aufnahme*)
- Help-seekers / community members (*Hilfesuchende / Mitglieder-Aufnahme*)

**BR-ORG-FORM-02** **Field-Level Requirement Control:**
Every section and field within an organization's custom form can be independently flagged by the coordinator as:
- **Mandatory** (`Pflichtfeld`)
- **Optional** (`Freiwillig`)

**BR-ORG-FORM-03** **FWZ Innsbruck-Land Standard Taxonomy:**
Organizations can activate or adapt the standard Austrian Freiwilligenzentrum intake template (`Interesse für Freiwilligentätigkeit`), featuring:
1. **Activity Domains (*Bereiche*):** *Soziales*, *Natur*, *E-Volunteering*, *Klima und Nachhaltigkeit*, *Handwerkliches / Kreatives*, *Kunst und Kultur*, *Freiwilligenpool*, *Lernbetreuung*.
2. **Target Groups (*Personengruppen*):** *Geflüchtete / Personen mit Migrationshintergrund*, *Familien*, *Senior:innen*, *Menschen mit Behinderung*, *Kinder und Jugendliche*, *Sonstige*.
3. **Time Commitment & Flexibility (*Zeitaufwand*):** *einmalig*, *regelmäßig (pro Woche, pro Monat, pro Jahr)*, *Stundenanzahl*, *Tageszeit*, *Wochentag(e)*, *Flexibel*, *WhatsApp-Zustimmung zur Kontaktaufnahme*.
4. **Skills & Free Notes (*Fähigkeiten / Anmerkungen*).**

**BR-ORG-FORM-04** **Criminal Clearance Declaration (*Strafrechtliche Unbescholtenheit*):**
Volunteer intake forms include a mandatory legal affirmation:
*"Ich erkläre, dass gegen mich keinerlei strafgerichtliche Verurteilungen, die noch nicht getilgt sind, bestehen und, dass gegen mich derzeit keine strafgerichtlichen Ermittlungen laufen."*
Accepted as a timestamped boolean in `intake_form_submissions` (no physical certificate scanned or stored, upholding BR-GDPR-01 & ADR-008).

**BR-ORG-FORM-05** **GDPR Consent (*Einwilligung zur Datenverarbeitung*):**
The intake flow incorporates explicit, unbundled GDPR consent checkboxes for data processing by the local volunteer center and coordinating state office, plus an optional consent for event and training invitations.

