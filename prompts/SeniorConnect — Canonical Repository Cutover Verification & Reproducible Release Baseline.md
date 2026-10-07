# SENIORCONNECT — NEXT PHASE

# Canonical Repository Cutover Verification & Reproducible Release Baseline

## PURPOSE

The previous phase is COMPLETE at the engineering baseline level.

Verified current engineering state:

```text
Backend + PostgreSQL = 711/711 PASS
Backend failures = 0
Backend skips = 0

Architecture = 87/87 PASS
Security Regression = PASS

Flutter Analyze = PASS
Flutter Tests = 909/909 PASS
Warnings = 0

Migration Chain = PASS
19 migrations
Migration Head = 20261001150947_*

ADR-029 = CONSISTENT
ADR-030 = CONSISTENT
Approved Policy Baseline = PASS

ADR-026 = OFF

Secret Hygiene = PASS

Source Integrity = PASS
Release Candidate Inventory = PASS

Documentation Provenance = PARTIAL
Historical documentation source = NOT IDENTIFIED
Current-gate documentation = honest/reconstructed

Repository Identity = NOT ESTABLISHED
No Git repository exists in the current workspace.
No remote exists.
No HEAD exists.

CI = NOT EXECUTED against a canonical committed revision.

Release Revision = PENDING HUMAN COMMIT
Release Traceability = PENDING
```

The current engineering release candidate is therefore:

```text
ENGINEERING RELEASE CANDIDATE
```

but NOT yet:

```text
CANONICAL RELEASE REVISION
```

The previous phase deliberately stopped before creating or committing Git\
history.

The human owner must now perform the repository cutover.

This phase begins only to VERIFY the result of that human cutover.

The purpose is:

```text
HUMAN CANONICAL REPOSITORY CUTOVER
        ->
VERIFY CANONICAL REMOTE
        ->
VERIFY COMMITTED RELEASE CANDIDATE
        ->
VERIFY CI ON EXACT REVISION
        ->
VERIFY SOURCE/REVISION TRACEABILITY
        ->
ESTABLISH REPRODUCIBLE RELEASE BASELINE
```

This is NOT a feature-development phase.

This is NOT a security redesign phase.

This is NOT a deployment phase.

This is NOT an Early Access phase.

---

# 0. ABSOLUTE HARD RULES

1. Do NOT add product features.

2. Do NOT redesign application architecture.

3. Do NOT revisit completed HelpRequests fixes.

4. Do NOT redesign Tenant Isolation.

5. Do NOT alter ADR-027.

6. Do NOT alter ADR-029.

7. Do NOT alter ADR-030.

8. Do NOT alter:

   - S-2
   - S-3
   - S-4
   - B-1
   - B-2

9. ADR-026 MUST remain:

```text
OFF
```

10. `VisitAddress:RequireGrant` MUST remain disabled.

11. Do NOT activate Early Access.

12. Do NOT perform production deployment.

13. Do NOT contact participants.

14. Do NOT contact organizations.

15. Do NOT onboard real users.

16. Do NOT create real production data.

17. Do NOT create a GitHub repository automatically.

18. Do NOT commit automatically.

19. Do NOT push automatically.

20. Do NOT tag automatically.

21. Do NOT force-push.

22. Do NOT rewrite Git history.

23. Do NOT delete or archive the old repository automatically.

24. Do NOT create a second repository if the human owner already created\
    the canonical repository.

25. Do NOT fabricate a commit SHA.

26. Do NOT fabricate CI evidence.

27. Do NOT claim a remote is canonical merely because it is named `origin`.

28. Do NOT claim CI PASS merely because workflows exist.

29. Do NOT claim deployment readiness from repository validation alone.

30. Do NOT claim legal/Data Protection approval.

31. Do NOT claim GDPR compliance.

32. Do NOT invent historical documentation.

33. Do NOT invent missing ADRs.

34. Do NOT fabricate the historical 157-task BUILD-CHECKLIST.

35. Do NOT treat reconstructed current-gate documentation as historical.

36. Do NOT print secrets.

37. Do NOT print token/API-key values.

38. Do NOT rotate credentials automatically.

39. Do NOT perform history cleanup to hide historical credential exposure.

40. Do NOT modify unrelated source files merely to make the repository look\
    cleaner.

41. Do NOT rerun large green test suites without a material reason.

---

# 1. PHASE ENTRY CONDITION

This phase has a strict entry condition.

The human owner must first perform:

```text
1. Create the intended private canonical Git repository.
2. Initialize/prepare the local Git repository as appropriate.
3. Review the release-critical inventory.
4. Create the initial release commit.
5. Record the resulting commit SHA.
6. Add the canonical remote.
7. Push the commit to the canonical remote.
```

The Agent MUST NOT perform those human/external actions automatically.

If, at phase start, there is still:

```text
NO GIT REPOSITORY
```

or:

```text
NO CANONICAL REMOTE
```

STOP repository verification and report:

```text
BLOCKED — HUMAN CANONICAL REPOSITORY CUTOVER NOT YET COMPLETE
```

Do not initialize Git merely because this phase exists.

---

# 2. CURRENT STATE RECONSTRUCTION

Once a Git repository exists, run:

```text
git status --short
git branch --show-current
git rev-parse HEAD
git remote -v
git log -10 --oneline --decorate
git status --porcelain=v1
```

Record:

```text
Repository root
Remote URL
Branch
HEAD SHA
Commit date
Working tree state
Recent history
```

Do not infer canonical identity from documentation.

Use actual Git evidence.

---

# 3. CANONICAL REPOSITORY IDENTITY

Determine whether the configured remote is the intended canonical repository.

Verify:

- remote URL
- repository owner/organization
- repository visibility where externally observable
- branch
- remote HEAD
- local HEAD
- synchronization state

The canonical repository may be classified as:

```text
ESTABLISHED
```

only when objective evidence shows that the local release candidate and the\
intended remote repository are the same repository lineage.

Otherwise:

```text
NOT ESTABLISHED
```

Do not guess.

---

# 4. COMMITTED RELEASE CANDIDATE

The previous phase ended with:

```text
Release Revision = PENDING HUMAN COMMIT
```

Now determine whether a real commit exists that contains the COMPLETE\
release candidate.

The committed revision must include the intended:

- application source
- tests
- migrations
- security controls
- approved policy implementation
- Flutter source
- CI
- Docker/build configuration
- current release documentation
- required security documentation

Do NOT compare only file counts.

Use:

```text
git ls-tree -r --name-only HEAD
git show --stat --oneline HEAD
git show --name-status --oneline HEAD
```

Where necessary inspect:

```text
git show HEAD:<path>
```

for release-critical files.

---

# 5. SOURCE-TO-COMMIT EQUIVALENCE

Prove that the committed revision contains the same intended application\
content that produced the:

```text
711/711 backend
909/909 Flutter
87/87 architecture
PostgreSQL PASS
Security PASS
```

baseline.

The critical question is:

```text
Was the tested release candidate actually committed?
```

Compare the current committed revision against the previously validated\
candidate state.

Use available artifacts, checksums, file comparison, or repository evidence.

If material release-critical differences exist:

```text
RELEASE TRACEABILITY = BLOCKED
```

and identify exactly what changed.

Do NOT assume that "the commit was made from the same folder" proves\
equivalence.

---

# 6. RELEASE-CRITICAL CONTENT AUDIT

Read:

```text
docs/release/RELEASE-CRITICAL-FILE-INVENTORY.md
```

Verify that every intended A–E file is present in the committed revision.

Classification:

```text
A = application/release source
B = tests
C = migrations
D = documentation
E = CI/deployment
F = local/secret
G = generated
H = unrelated
```

Confirm:

```text
A–E = included where required
F–H = excluded unless explicitly justified
```

Do not silently add files because they appear useful.

---

# 7. WORKING-TREE CLEANLINESS

Verify:

```text
git status --short
```

Required for a frozen revision:

```text
CLEAN
```

If release-critical files remain modified after the commit:

```text
Release Freeze = FAIL
```

If only unrelated local developer files exist:

classify them explicitly.

Do not delete unrelated user work.

The release revision itself must still be clearly identifiable.

---

# 8. RELEASE DOCUMENT TRACKING

Verify that the following are actually committed:

```text
docs/release/
```

At minimum inspect:

```text
RELEASE-CANDIDATE-MANIFEST.md
RELEASE-CANDIDATE-FREEZE.md
RELEASE-CRITICAL-FILE-INVENTORY.md
RELEASE-BASELINE-EVIDENCE.md
REPOSITORY-INTEGRITY-REPORT.md
DOCUMENTATION-PROVENANCE-REPORT.md
```

Use actual filenames in the repository.

Do not require files that do not exist merely because they were expected\
historically.

Confirm:

```text
git ls-files docs/release/
```

contains the intended release documents.

---

# 9. RELEASE MANIFEST RECONCILIATION

Update:

```text
docs/release/RELEASE-CANDIDATE-MANIFEST.md
```

ONLY after a real commit exists.

Replace:

```text
PENDING — HUMAN COMMIT
```

with the actual:

```text
Release Revision = <REAL SHA>
```

only after verifying it.

Record:

```text
Repository
Remote
Branch
HEAD SHA
Commit date
Application version
Backend target framework
.NET SDK
Flutter SDK
Dart SDK
Dependency state
Migration head
PostgreSQL validation
Architecture validation
Security validation
Flutter validation
Localization status
Warnings
ADR-026
Approved policy baseline
```

Use exact actual values.

Do NOT invent fields that do not exist.

---

# 10. RELEASE FREEZE RECONCILIATION

Update:

```text
docs/release/RELEASE-CANDIDATE-FREEZE.md
```

The document MUST state:

```text
Release Candidate Status = COMMITTED
```

only when the real commit contains the complete candidate.

Record:

```text
Frozen Revision = <REAL SHA>
```

The freeze is tied to the actual commit.

Do NOT freeze a branch name without a revision.

Do NOT freeze a working tree.

---

# 11. REPOSITORY INTEGRITY REPORT

Update:

```text
docs/release/REPOSITORY-INTEGRITY-REPORT.md
```

The report must state:

```text
Git repository = YES
Canonical remote = <actual URL>
Branch = <actual>
HEAD = <actual SHA>
Working tree = CLEAN/DIRTY
Release candidate committed = YES/NO
Release candidate traceable = YES/NO
Release documents tracked = YES/NO
Secret hygiene = PASS/FAIL
Canonical repository identity = ESTABLISHED/NOT ESTABLISHED
```

No symbolic or invented values.

---

# 12. DOCUMENTATION PROVENANCE

The previous phase established:

```text
Historical Documentation Source = NOT-IDENTIFIED
```

Do NOT attempt to reconstruct historical documentation merely because Git\
now exists.

Once the new Git repository is created, its history begins with the new\
canonical baseline.

Do NOT claim that this new history restores historical ADR-001...ADR-028.

Keep the distinction:

```text
NEW CANONICAL BASELINE DOCUMENTATION
```

vs

```text
HISTORICAL DOCUMENTATION
```

Historical documents remain:

```text
MISSING — NO AUTHORITATIVE SOURCE
```

unless actual evidence is discovered.

---

# 13. ADR-029 VERIFICATION

Verify the committed ADR-029 document and implementation.

Confirm:

```text
Status = Accepted
```

Confirm authorization is based on active Coordinator/Admin membership.

Confirm it does NOT depend on:

- TOTP presence
- authentication method
- client flags
- client capability claims

Verify associated tests.

Required:

```text
ADR-029 = CONSISTENT
```

Do not redesign it.

---

# 14. ADR-030 VERIFICATION

Verify the committed ADR-030 document and implementation.

Confirm:

```text
Effective trust =
live verification facts
at trust-consuming boundaries
```

Confirm login-time snapshot is audit evidence, not the authoritative trust\
source.

Verify associated tests.

Required:

```text
ADR-030 = CONSISTENT
```

Do not redesign the trust system.

---

# 15. APPROVED POLICY BASELINE

Verify the committed release contains:

```text
ADR-027 = Option A
S-2 = Option B
S-3 = Option B
S-4 = Option B
B-1 = Option B
B-2 = Option A
ADR-026 = Option C
```

Verify the actual implementation and tests correspond to these decisions.

Do not reopen decisions that are already approved.

---

# 16. ADR-026 FINAL SAFETY GATE

Verify the committed revision contains:

```text
VisitAddress:RequireGrant = false
```

Confirm:

- default configuration is OFF
- no client can enable it
- no ordinary admin can enable it
- no release configuration enables it
- no CI configuration unintentionally enables it

Required:

```text
ADR-026 = OFF
```

Do not activate it.

---

# 17. SECRET HYGIENE — CURRENT REPOSITORY

Run a targeted scan against the committed source.

Inspect:

- configuration
- Docker
- CI
- scripts
- documentation
- appsettings
- `.env` patterns
- AI-tool configuration
- local developer configuration

Confirm:

```text
NO ACTUAL SECRET IN TRACKED RELEASE FILES
```

Never print secret values.

The historical credential exposure remains documented separately.

Do not treat a clean new repository as proof that historical exposure\
never occurred.

---

# 18. CI VERIFICATION — MANDATORY

The previous phase established:

```text
CI = NOT EXECUTED
```

Therefore this is the primary technical gate in this phase.

Inspect:

```text
.github/workflows/
```

Determine the canonical CI workflow.

Do not claim CI PASS merely because the YAML exists.

The human owner must push the canonical commit to the canonical remote.

Then verify actual CI execution for that exact revision.

At minimum verify CI covers, where configured:

- backend build
- backend tests
- PostgreSQL
- architecture tests
- security tests
- Flutter analyze
- Flutter tests
- localization
- Docker/build checks
- dependency vulnerability scanning if configured

Record:

```text
Workflow
Run ID
Commit SHA
Status
Passed jobs
Failed jobs
Skipped jobs
```

The most important relationship is:

```text
CI commit SHA == release HEAD SHA
```

If the CI run belongs to another commit:

```text
CI = NOT VALID FOR RELEASE REVISION
```

---

# 19. CI SECURITY VERIFICATION

Inspect CI configuration for:

- secret references
- masked secrets
- accidental `echo`
- shell tracing
- credential output
- unsafe pull-request secret exposure
- environment separation

Do not print secret values.

If a CI secret might be exposed:

mark:

```text
CI SECURITY = BLOCKED
```

and stop.

Do not modify CI merely to improve style.

Only fix a demonstrated security or release-blocking defect.

---

# 20. CI DEPENDENCY VULNERABILITY SCAN

The previous phase reported:

```text
Dependency Traceability = PASS
vulnerable-scan CI step = not executed here
```

If CI contains a dependency-vulnerability scan:

verify its actual result for the release revision.

Record:

```text
PASS
FAIL
NOT CONFIGURED
```

Do not introduce a new scanner in this phase merely because one is common.

If no scanner exists:

record:

```text
NOT CONFIGURED
```

Do not call the dependency state invalid solely for that reason unless\
repository policy requires it.

---

# 21. LOCALIZATION GATE

Previous result:

```text
Localization = NOT-RUN
```

because there was no locale-affecting change.

However CI contains the localization gate.

Therefore:

- if CI executes localization checks successfully on the exact release SHA:\
  record PASS
- if CI does not execute them:\
  record NOT VERIFIED
- if localization changed after the previous validation:\
  rerun the localization validation

Do NOT report PASS merely because no files appeared to change.

---

# 22. REPRODUCIBLE BUILD

Now that the source has a real revision, verify that the release can be\
rebuilt from that revision.

At minimum, where repository tooling supports it:

```text
dotnet restore
dotnet build backend/SeniorConnect.slnx -c Release
```

and use the repository's documented Flutter build/check mechanism.

Do not unnecessarily repeat the entire test matrix if the source is\
unchanged and CI has already run the authoritative suite against the exact\
SHA.

The purpose is:

```text
SOURCE REVISION
    ->
BUILD
    ->
VERIFIABLE ARTIFACT
```

Record the artifact identity if available.

---

# 23. ARTIFACT TRACEABILITY

If the repository has an artifact/build output:

determine:

```text
Artifact
Source SHA
Build configuration
Application version
Migration head
Build timestamp
```

The artifact is valid for this release only if:

```text
Artifact source SHA == release HEAD SHA
```

If source SHA cannot be established:

```text
Artifact Traceability = PENDING
```

Do not invent a mapping.

---

# 24. MIGRATION TRACEABILITY

Verify the committed release contains:

```text
Migration Head =
20261001150947_AddPlatformSafeguardingOfficerRoleCheck
```

or the actual current migration head if it has legitimately changed.

Verify:

```text
19 migrations
```

only if that remains true in the current repository.

Do not use historical counts if the repository changed.

If a new migration exists, update the manifest and explain why.

Do not create a migration merely for versioning.

---

# 25. POSTGRESQL EVIDENCE RECONCILIATION

The previous phase already passed:

```text
711/711 backend
0 failures
0 skips
```

including PostgreSQL-gated tests.

Do NOT rerun all PostgreSQL tests merely because Git now exists.

Instead verify whether the committed revision is demonstrably equivalent\
to the tested candidate.

If equivalence is proven and CI also passes PostgreSQL on the exact SHA:

```text
PostgreSQL Release Evidence = PASS
```

If a release-critical source/configuration change occurred after the\
validated tree:

rerun the affected PostgreSQL tests.

---

# 26. ENGINEERING REGRESSION POLICY

Do NOT blindly repeat:

```text
711 backend tests
909 Flutter tests
87 architecture tests
```

if:

- the committed revision exactly matches the validated candidate
- CI has executed those suites on the exact SHA
- no release-critical changes occurred afterward

Instead reference the authoritative CI evidence.

Rerun locally only where needed to diagnose or verify a discrepancy.

If the commit differs materially:

run the affected suites.

All actual failures remain failures.

---

# 27. RELEASE BASELINE EVIDENCE

Update:

```text
docs/release/RELEASE-BASELINE-EVIDENCE.md
```

The document MUST distinguish:

## Previous candidate validation

The uncommitted engineering candidate.

## Canonical revision validation

The committed release SHA.

## CI evidence

Actual workflow/run.

## Build evidence

Actual build.

## Artifact evidence

Actual artifact, if available.

This prevents historical test results from being incorrectly attributed\
to the new commit.

---

# 28. DOCUMENTATION PROVENANCE STATUS

Update:

```text
docs/release/DOCUMENTATION-PROVENANCE-REPORT.md
```

Current state should explicitly distinguish:

```text
Historical source:
NOT IDENTIFIED

Current-gate documentation:
RECONSTRUCTED FROM CURRENT CODE/TEST EVIDENCE

Canonical baseline documentation:
COMMITTED IN NEW REPOSITORY

Historical documentation:
NOT RESTORED
```

Do not turn the new repository's initial commit into "historical recovery."

---

# 29. RELEASE-CANDIDATE ACTIVATION REPORT

Update:

```text
docs/launch/RELEASE-CANDIDATE-ACTIVATION-REPORT.md
```

The report must distinguish:

```text
Engineering validation
Canonical repository
Release revision
CI
Artifact
Production deployment
External gates
Early Access
```

Current state before external deployment remains:

```text
Production = NOT DEPLOYED
Early Access = NOT STARTED
ADR-026 = OFF
```

---

# 30. PRODUCTION/EXTERNAL GATES

Do NOT attempt to close deployment gates merely because the repository is\
now canonical.

Keep separate:

```text
Credential Rotation
Authentication Provider
Production Infrastructure
Domain/TLS
Production PostgreSQL
Backup
Restore
Observability
Operational Ownership
Safeguarding Ownership
Data Protection / Legal
Participant Readiness
Owner Launch Authorization
```

Only update a gate when actual new evidence exists.

Do not infer external readiness from CI.

---

# 31. RELEASE TRACEABILITY FINAL MODEL

The release must now be representable as:

```text
Canonical Repository
        |
        v
Branch
        |
        v
Release HEAD SHA
        |
        v
CI Run for SAME SHA
        |
        v
Build/Test Evidence
        |
        v
Artifact Identity
        |
        v
Migration Head
        |
        v
Future Deployment
```

The final report must explicitly verify each link.

If one link is missing:

record the exact missing link.

Do not use vague "release ready" language.

---

# 32. HUMAN TAGGING RULE

The Agent MUST NOT create a Git tag automatically.

After the release SHA and CI are verified, the human owner may optionally\
create a release tag.

The Agent may provide the exact safe command sequence, but MUST NOT execute\
it.

Example:

```text
git tag -a <human-selected-tag> <verified-release-sha>
git push <canonical-remote> <human-selected-tag>
```

Only if the owner explicitly chooses to tag.

Do not invent a tag name.

---

# 33. HUMAN RELEASE VERIFICATION PROCEDURE

Provide the exact safe sequence for the human owner:

```text
git status --short
git rev-parse HEAD
git remote -v
git fetch --prune
git branch --show-current
git log -1 --oneline --decorate

git diff --check

git show --stat --oneline HEAD
git ls-tree -r --name-only HEAD | <review>

verify release-critical inventory

verify secret scan

verify canonical remote

verify CI run for exact HEAD SHA

verify artifact source SHA

verify release manifest

git status --short
```

Do not execute commit/tag/push commands.

---

# 34. ACCEPTANCE CRITERIA

This phase is PASS only if ALL of the following are objectively true:

### Repository

1. A real Git repository exists.
2. A canonical remote exists.
3. The canonical repository identity is established.
4. The release candidate is committed.
5. The release SHA is real.
6. The working tree is clean or all remaining local state is explicitly\
   classified as non-release.

### Source

7. The committed source contains the intended release candidate.
8. No unexplained release-critical difference exists.
9. Release-critical inventory matches the committed tree.
10. Required migrations are present.

### Documentation

11. Release documents are tracked.
12. Release manifest contains the real SHA.
13. Freeze document contains the real SHA.
14. Documentation provenance remains honest.
15. Historical documentation is NOT fabricated.

### Security

16. Secret scan passes.
17. No actual secret exists in tracked release files.
18. Historical exposure remains documented.
19. ADR-026 remains OFF.
20. Approved policy baseline remains consistent.

### CI

21. CI executes on the canonical release SHA.
22. Backend tests pass.
23. PostgreSQL tests pass.
24. Architecture/security checks pass.
25. Flutter checks pass.
26. Localization gate is verified or explicitly documented as\
    not executed.
27. Dependency scan is verified or explicitly documented as not configured.
28. No CI secret exposure exists.

### Build

29. Release build is reproducible from the committed revision.
30. Artifact identity is known or explicitly pending.
31. Artifact source revision matches release SHA when an artifact exists.

### Traceability

32. The chain:

```text
Repository
-> SHA
-> CI
-> Build
-> Artifact
-> Migration
```

is complete or each missing link is precisely documented.

### External

33. Production is not falsely declared deployed.
34. Early Access remains NOT STARTED.
35. Legal/Data Protection is not falsely marked approved.
36. Deployment gates remain externally controlled.

---

# 35. REQUIRED FINAL REPORT

Return exactly:

```text
PHASE STATUS

Repository = PASS/BLOCKED
Canonical Remote = ESTABLISHED/BLOCKED
Canonical Repository Identity = ESTABLISHED/BLOCKED

Release Candidate Commit = PASS/BLOCKED
Release HEAD SHA = VERIFIED/PENDING
Working Tree = CLEAN/CLASSIFIED/BLOCKED

Source Equivalence = PASS/BLOCKED
Release-Critical Inventory = PASS/FAIL

Release Manifest = PASS/FAIL
Release Freeze = PASS/FAIL
Release Documents Tracked = PASS/FAIL
Documentation Provenance = PASS/PARTIAL/BLOCKED

Secret Hygiene = PASS/FAIL
Credential History Documentation = PASS/PENDING
ADR-029 = CONSISTENT/INCONSISTENT
ADR-030 = CONSISTENT/INCONSISTENT
Approved Policy Baseline = PASS/FAIL
ADR-026 = OFF/BLOCKED

Backend Build = PASS/FAIL
Backend Tests = PASS/FAIL
PostgreSQL CI = PASS/FAIL/NOT-VERIFIED
Architecture CI = PASS/FAIL/NOT-VERIFIED
Security CI = PASS/FAIL/NOT-VERIFIED

Flutter Analyze = PASS/FAIL/NOT-VERIFIED
Flutter Tests = PASS/FAIL/NOT-VERIFIED
Localization = PASS/FAIL/NOT-VERIFIED
Dependency Vulnerability Scan = PASS/FAIL/NOT-CONFIGURED/NOT-VERIFIED

Docker/Artifact Build = PASS/FAIL/NOT-VERIFIED
Artifact Traceability = PASS/PENDING/BLOCKED
Migration Traceability = PASS/FAIL

CI Exact-SHA Verification = PASS/FAIL
Release Traceability = PASS/PENDING/BLOCKED

Production Deployment = NOT STARTED
Early Access = NOT STARTED

External Gates = UNCHANGED/UPDATED

Final Result =
CANONICAL RELEASE BASELINE ESTABLISHED

or

CANONICAL RELEASE BASELINE VERIFIED — EXTERNAL DEPLOYMENT GATES REMAIN

or

BLOCKED — CANONICAL RELEASE BASELINE COULD NOT BE VERIFIED
```

Then provide exactly:

## 1. CANONICAL REPOSITORY

Repository:\
Remote:\
Branch:\
HEAD:\
Commit date:\
Working tree:\
Canonical identity evidence:

## 2. RELEASE REVISION

Release SHA:\
Candidate source:\
Commit contents:\
Source equivalence:\
Freeze status:

## 3. CI

Workflow:\
Run ID:\
Commit SHA:\
Backend:\
PostgreSQL:\
Architecture:\
Security:\
Flutter:\
Localization:\
Dependency scan:\
Docker:

## 4. BUILD / ARTIFACT

Backend build:\
Flutter build/check:\
Docker/artifact:\
Artifact identity:\
Artifact source SHA:

## 5. MIGRATION

Migration head:\
Migration count:\
Clean-database evidence:\
Current status:

## 6. SECURITY

Secret scan:\
Historical credential exposure:\
ADR-029:\
ADR-030:\
ADR-026:\
Approved policy baseline:

## 7. DOCUMENTATION

Release documents:\
Documentation provenance:\
Historical documentation:\
BUILD-CHECKLIST:

## 8. TRACEABILITY CHAIN

Repository:\
SHA:\
CI:\
Build:\
Artifact:\
Migration:

For each link use:

```text
VERIFIED
PENDING
BLOCKED
```

## 9. EXTERNAL GATES

Only statuses that changed or remain relevant.

Do not repeat the full previous external-gate analysis.

## 10. HUMAN ACTIONS REQUIRED

Only actions that genuinely require human/external access.

Typical examples:

- create/verify canonical private repository
- verify GitHub repository ownership/visibility
- verify CI results
- optional release tag
- credential-rotation confirmation
- future production deployment

## 11. REMAINING BLOCKERS

Only concrete blockers.

Do NOT list historical documentation absence as a blocker unless a current\
release policy explicitly requires it.

## 12. NEXT PHASE

If repository and CI baseline are fully verified:

```text
PRODUCTION DEPLOYMENT / EXTERNAL GATE VALIDATION
```

If canonical repository remains unverified:

```text
HUMAN CANONICAL REPOSITORY CUTOVER
```

Do NOT start another feature-development phase.

---

# 36. HARD STOP

STOP after:

- canonical repository verification
- release SHA verification
- CI verification
- source equivalence verification
- artifact traceability
- migration traceability
- release-document reconciliation
- final report

DO NOT:

- deploy production
- activate Early Access
- activate ADR-026
- create/delete GitHub repositories
- commit automatically
- push automatically
- tag automatically
- rewrite history
- force-push
- delete the old repository
- rotate credentials automatically
- invent historical documentation
- invent legal approval
- invent infrastructure
- add features
- redesign architecture
- start AI matching
- add payments
- add voice/IVR
- add corporate volunteering

The goal is:

```text
CANONICAL SOURCE
+
REAL COMMIT
+
EXACT CI EVIDENCE
+
REPRODUCIBLE BUILD
+
TRACEABLE ARTIFACT
+
DOCUMENTED MIGRATION
=
CANONICAL RELEASE BASELINE
```

Nothing more.
