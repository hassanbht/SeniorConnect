# SeniorConnect Launch Readiness Report

> **Update 2026-10-02 (Release Candidate Freeze & External Activation Evidence):**
> this report is now two phases behind. The current engineering conclusion is in
> `docs/launch/RELEASE-CANDIDATE-ACTIVATION-REPORT.md`; the freeze conclusion is
> in `docs/release/RELEASE-CANDIDATE-FREEZE.md`; the candidate identity is in
> `docs/release/RELEASE-CANDIDATE-MANIFEST.md`. **No gate status changed and
> Early Access remains NOT STARTED.** One material correction: the release
> candidate recorded below as commit `6f9c5ec` is **not** the deployable
> candidate — that commit does not contain the S-2/S-3/S-4/B-1/B-2/ADR-026 or
> email-provider code (finding R-1). This document remains the
> deployment-integration readiness record.
>
> **Update 2026-10-02:** the launch-security & deployment-closure phase
> followed this report; the current engineering conclusion lives in
> `docs/launch/FINAL-ENGINEERING-HANDOFF.md`. This report remains the
> deployment-integration readiness record.

> **Update 2026-10-02:** the launch-security & deployment-closure phase
> followed this report; the current engineering conclusion lives in
> `docs/launch/FINAL-ENGINEERING-HANDOFF.md`. This report remains the
> deployment-integration readiness record.
>
> **Date:** 2026-10-01 · **Branch:** main
> **Scope:** deployment integration & early-access preparation phase.
> This report is a **readiness record**, not a launch authorization. It makes
> **no legal claim of any kind** (no GDPR-compliance, lawful-basis,
> lawyer-approval, insurance or DPIA-approval claims). The final launch
> decision belongs exclusively to the operating owner; this report only
> presents evidence. **The Early-Access program itself remains NOT STARTED.**

## Engineering

**PASS**

- Release build: `dotnet build backend/SeniorConnect.slnx -c Release` →
  **0 errors**.
- Backend regression with **real PostgreSQL** (re-run after every change of
  this phase): **612 passed / 0 failed / 0 skipped** (Architecture 59 ·
  HelpRequests 217 · Community 101 · Organizations 54 · Family 50 ·
  PilotHardening 45 · TrustSafety 48 · Identity 38), 0 leftover `sc_test_%`
  databases.
- Flutter: `flutter analyze` → **No issues found**; `flutter test` →
  **906/906 passed**.
- Localization: `python scripts/check_locales.py` → **OK — 713 keys,
  3 locales (de/en/fa), all consistent**.
- **HTTP synthetic dry run: PASS — 55/55 checks, 0 failures**
  (`scripts/http_dry_run.py` against the real API over HTTP; synthetic users,
  one synthetic organization, one synthetic help request; Development OTP
  delivery channel; no real personal data). This closes the previous phase's
  "narrative-only script" limitation at the HTTP level.
- Real defects found and fixed by that dry run this phase:
  1. DI resolution crash (`ISafeguardingCaseRaiser` resolved the concrete
     class) — B-2 conversion path was unreachable at runtime;
  2. DELETE endpoints with complex body records crashed route building at
     startup (explicit `[FromBody]` restores the Flutter contract);
  3. **S-4 appointment always failed on real PostgreSQL** —
     `ck_memberships_role` never listed `PlatformSafeguardingOfficer`;
     fixed via migration `20261001150947_AddPlatformSafeguardingOfficerRoleCheck`.
- Email/OTP provider boundary completed (§3 of the brief): real SMTP adapter
  (`SmtpEmailSender`), Development-only log fallback, explicit startup failure
  on incomplete configuration, `EMAIL_PROVIDER_NOT_CONFIGURED` outside
  Development; the fake success stub was removed; 14 new security tests.

## Security

**PASS**

- Authorization matrix, tenant isolation, object ownership, concurrency and
  audit coverage: green across all suites (0 failures).
- Architecture/security guards green in the final run (59/59): module
  boundaries, leak sweeps, **ADR-026 feature-gate guard**, location-privacy
  sweeps.
- Provider boundaries fail closed: email/SMS senders refuse to pretend
  delivery outside Development; geocoder `Provider: none` rejects rather than
  fabricates; no fake provider masquerades as production.
- Configuration hardening verified: `JWT_SECRET_KEY` required and the public
  dev key refused outside Development (`JwtSettings.EnsureSafeFor`); CORS
  locked to `https://app.seniorconnect.at` in production; per-caller rate
  limiting (auth 20/min, global 300/min); `ClockSkew=0`; debug/telemetry
  defaults production-safe.
- **Secret scan (§8, executed this phase), classified:**
  - **A — actual secret committed: 1 finding (working tree remediated).**
    Two `sk-…` API keys in tracked `opencode.json` (developer LLM-relay
    tooling with `localhost` base URLs — outside the SeniorConnect runtime;
    no SeniorConnect service is affected). The launch-security & deployment-
    closure phase **removed the secret values from the working tree**
    (`{env:…}` references, `.gitignore` hardened,
    `docs/security/credential-exposure-remediation.md`). The old values remain
    in git history; **external rotation is required** (gate matrix C3 =
    **PENDING**, scope: tooling credential, not platform runtime).
  - **B — configuration placeholders:** compose
    `POSTGRES_PASSWORD:-seniorconnect_dev_password` dev fallback (deployment
    must override); CI `seniorconnect_ci_password` (ephemeral service
    container); `JwtSettings.DevelopmentSecret` (refused outside Development).
  - **C — documentation/example:** docs references, test-only in-memory keys.
  - **D — false positives:** schema/column names (`password_hash`,
    `totp_secret`), DTO field names, UI strings, a test dummy token.
  - `.env` / `.env.example` (which contain a dev JWT key) are **untracked and
    gitignored** — nothing there is committed.
- No `appsettings*`, workflow, compose or script contains a real SeniorConnect
  credential.

## Safeguarding

**PASS**

- All five approved policy behaviors verified **end-to-end over HTTP this
  phase** (dry run steps 9–13):
  - **S-2** content-free `OperationalTask` on restriction imposition (surface
    answers, leak-free),
  - **S-3** roster `OperationalHold` boolean (content-free row verified),
  - **S-4** organization-less case visible **only** to the appointed platform
    safeguarding officer — appointment via the real audited API, org officer
    denied **404** (fail-closed),
  - **B-1** block → automatic assignment release → released volunteer gets
    **404**,
  - **B-2** escalated unsafe-behavior report → manual officer conversion
    (audit `RaiseConcern` access log written, BR-SG-06 counts > 0).
- The HTTP dry run exposed and this phase fixed the S-4 PostgreSQL constraint
  defect (see Engineering) — without it the platform-officer appointment could
  never succeed on a real database.
- Operational runbook `docs/operations/safeguarding-operational-runbook.md`
  reviewed for this phase: required role placeholders only —
  [Operating Organization Safeguarding Lead], [Platform Safeguarding Officer],
  [Operational Coordinator], [Platform Operator] — with S-2/S-3/S-4/B-1/B-2
  behavior matched to the implementation. No new safeguarding behavior added.
- Safeguarding case text stays outside `audit_entries` (isolated schema);
  per-action `safeguarding.access_logs` verified live.

## PostgreSQL

**PASS**

- Full suite executed against real PostgreSQL: **612/0/0** (empty-database-
  per-class fixture, migrations applied per run), 0 leftover test databases.
- Migration chain integrity: previous phase's empty-database rehearsal passed
  for the whole chain; this phase added and **applied**
  `20261001150947_AddPlatformSafeguardingOfficerRoleCheck` (drops/re-adds
  `ck_memberships_role` including `PlatformSafeguardingOfficer`), verified
  live against the disposable database; `Down()` restores the previous
  constraint; model snapshot regenerated and consistent (architecture tests
  green).
- The dry-run environment applies migrations automatically at API startup
  (`MigrateAsync`) — verified live.
- Backup/restore **mechanism** re-verified this phase (§14): disposable
  PostgreSQL 16 container (`sc_test_pg_phase`), synthetic data in
  `sc_dryrun_http` (11,500 rows across 4 tables including the quoted-identifier
  mixed-case table `"HelpRequests"`) → `pg_dump -Fc` → SHA-256
  (`57f060…88ad0`) → `pg_restore` into `sc_restore_drill` → full row-count
  comparison with quoted identifiers for **all** tables. All 4 tables match
  exactly (organizations 200, organization_memberships 5000, visit_address_grants
  1500, HelpRequests 2000). Container torn down after drill.
  The **real operational backup target remains external** (see Operational).

## CI/CD

**PASS**

- `.github/workflows/ci.yml` inspected this phase — release-critical steps
  all present, no changes needed:
  - backend: restore → Release build → **full test suite with a
    `postgres:16` service and `SC_TEST_POSTGRES` exported** → vulnerable-
    package scan (fails the job on findings);
  - mobile: locale check (de/en/fa) → `flutter analyze` → `flutter test`
    (matrix);
  - containers: Docker image build (`push: false`).
- Reproducibility: CI builds everything from source on a clean runner; no
  prebuilt binaries; images are tagged per run. Image **registry/versioned
  artifact storage is NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT**
  (CI does not push).

## Operational

**PENDING**

- **Repository capabilities now documented from actuals:**
  - `docs/operations/release-and-rollback-runbook.md` (new) — release steps,
    health checks, rollback paths (application, configuration, feature gate,
    database restore, shutdown/restart), and explicit
    **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT** markers where a
    capability does not exist (registry, blue-green, monitoring, backup
    alerting);
  - smoke-test procedure included (runbook §4 + `scripts/verify_deployment.sh`);
  - `docs/operations/operational-ownership-checklist.md` (new) — 8 roles,
    every row **EXTERNAL ACTION REQUIRED** (no names invented);
  - `docs/operations/backup-restore-requirements.md` (new) — status
    **EXTERNAL ACTION REQUIRED** (mechanism proven, target does not exist);
  - existing runbooks reviewed: deployment guide, operator runbook,
    incident response, safeguarding operational runbook (updated), backup &
    restore concept.
- **What remains external (each blocks launch until done):** named role
  holders (checklist rows 1–8 incl. safeguarding lead & platform safeguarding
  officer), real backup target + recorded restore drill on it, hosting/domain/
  TLS, failure alerting, backup scheduling, contracted OTP/email provider
  credentials.

## Data Protection / Legal

**PENDING**

- `docs/legal/EXTERNAL-DATA-PROTECTION-REVIEW.md` (new) separates
  **repository evidence** (minimisation, tenant isolation, access control,
  auditing, location privacy, safeguarding isolation, visit-address gate,
  deletion/export, consent) from **external review required** — all 10
  external items are **PENDING / EXTERNAL ACTION REQUIRED** (controller/
  processor roles, legal bases, privacy information, retention, data-subject
  rights, DPA, transfers, DPIA sign-off, ADR-026 questions, hosting review).
- `docs/legal/dpia.md` §4 rewritten this phase: the previous wording
  ("Die Verarbeitung ist für den Produktionsbetrieb zulässig") read like an
  approval; it now states explicitly that the document is an internal working
  draft, constitutes **no** approval, and that external qualified review for
  the actual deployment is pending.
- **No compliance claim is made anywhere in this report or the documents it
  references.**

## Participant Readiness

**NOT STARTED**

- No participant, community, partner organization, volunteer, senior or family
  has been named, contacted or committed (hard rule of this phase).
- The readiness pack (`docs/product/early-access-readiness-pack.md`, updated
  this phase with the six-dimension summary) records: consent/onboarding flow
  implemented and localized; reporting/blocking paths live; emergency
  boundary (144/112, not an emergency service) implemented; stop conditions
  grounded in repository policy; **no KPIs or success percentages invented**.
- No second organization onboarded — multi-organization operation stays out of
  Early Access scope (engineering boundary already passed).

## ADR-026 Activation

**OFF**

- `VisitAddress:RequireGrant = false` remains the **only** definition across
  all committed configurations; nothing in `appsettings.Production.json`,
  compose or environment turns it on; architecture guard green in the final
  run (fails the build on any committed activation).
- Flutter clients, ordinary users and ordinary admins have **no path** to set
  it (server configuration only; no endpoint writes configuration).
- HTTP dry run verified the gate-off runtime state: live assignee sees the
  address under the unchanged BR-COMM-04 baseline, **zero rows** in
  `visit_address_grants`, exact address withheld from organization staff —
  4/4 checks PASS.
- Activation requires the external written procedure
  (`docs/security/visit-address-activation-gate.md` §4): legal answers to the
  four ADR-026 questions + an activation record + operator change procedure.
  **Final phase state: ENGINEERING IMPLEMENTED / ACTIVATION OFF. No
  activation during this phase.**

## Early Access

**READY-FOR-OWNER-GO** — *(the program itself: **NOT STARTED**; no
participant contacted, no real personal data processed, no launch executed.
The decision to launch belongs to the operating owner alone.)*

- Phase acceptance (§26 of the brief): state reconciled ✓ · PostgreSQL
  executed and passing ✓ · CI PostgreSQL integration verified ✓ · no
  release-critical security regression ✓ · provider boundary
  deployment-ready with the exact external blocker documented (contracted
  SMTP credentials — external) ✓ · HTTP dry run executed successfully on
  synthetic data ✓ · backup/restore operational requirement explicit ✓ ·
  operational ownership explicit ✓ · DP/Legal separated from engineering ✓ ·
  ADR-026 OFF ✓ · no legal claim invented ✓ · Early Access NOT STARTED ✓ ·
  no second organization ✓ · no real personal data ✓ · deployment/rollback
  documentation internally consistent ✓ · no unrelated feature ✓ · all
  executed tests 0 failures ✓ · working tree contains only intentional
  changes ✓.
- External gates still open (owner actions, tracked in
  `docs/launch/EXTERNAL-LAUNCH-GATE.md`): Legal/Data-Protection validation,
  operational appointments & real backup target, contracted provider,
  repository secret rotation (opencode.json), hosting.

---

**STOP.** Early Access NOT STARTED. No partner contacted, no second
organization onboarded, `VisitAddress:RequireGrant` unchanged at `false`, no
legal claim made, no unrelated feature added, next roadmap phase not begun.
The human owner remains the final launch authority.
