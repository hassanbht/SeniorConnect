# SENIORCONNECT — NEXT PHASE

# Canonical Release Baseline, Repository Integrity & Evidence Traceability

## PURPOSE

The previous phase is CLOSED at the engineering-baseline level.

Verified previous result:

```text
Baseline Reconstruction = PASS

HelpRequests Failure Inventory = COMPLETE
HelpRequests Failures = 0
HelpRequests Root Cause Classification = PASS
HelpRequests Security Boundary = PASS

Tenant Isolation Failure = BRITTLE TEST
Tenant Isolation Security = PASS

Architecture Tests = 87/87 PASS

Backend Build = PASS
Backend Tests = 708 passed / 0 failed / 3 environment skips

Flutter Analyze = PASS
Flutter Tests = 909/909 PASS

Security Regression = PASS
Warnings = 0

ADR-029 Canonical Record = PASS
ADR-030 Canonical Record = PASS

BUILD-CHECKLIST = PARTIAL
Documentation Restored = PARTIAL

ADR-026 = OFF
Approved Policy Baseline = PASS

Working Tree Hygiene = PASS

Release Baseline Readiness =
READY FOR CANONICAL RELEASE BASELINE
```

The previous phase also established:

```text
PostgreSQL = NOT-RUN
```

because no PostgreSQL server was available in that environment.

Documentation provenance is:

```text
Historical Documentation Source = NOT-IDENTIFIED
Current-Gate Documentation = PARTIALLY RECONSTRUCTED
```

Therefore this phase is NOT another failure-closure phase.

This phase is:

```text
CANONICAL RELEASE BASELINE
+
REPOSITORY INTEGRITY
+
DOCUMENTATION PROVENANCE
+
FINAL POSTGRESQL GATE
+
RELEASE TRACEABILITY
```

This phase MUST NOT become:

- feature development
- matching redesign
- trust redesign
- safeguarding redesign
- deployment
- Early Access
- legal approval
- production activation

The objective is to establish exactly what the repository contains, exactly what was validated, exactly which documentation is authoritative, and exactly what revision is eligible to become the canonical release baseline.

---

# 0. HARD RULES

1. Do NOT modify the completed HelpRequests fixes unless a new direct regression is discovered.

2. Do NOT redesign Tenant Isolation.

3. Do NOT weaken or revert the behavioral TenantIsolation test.

4. Do NOT redesign:

   - ADR-027
   - ADR-029
   - ADR-030
   - S-2
   - S-3
   - S-4
   - B-1
   - B-2
   - ADR-026

5. ADR-026 MUST remain:

```text
OFF
```

6. `VisitAddress:RequireGrant` MUST remain disabled.

7. Do NOT add:

   - AI matching
   - payments
   - voice/IVR
   - corporate volunteering
   - white-label
   - new verification mechanisms
   - new safeguarding features
   - new product functionality

8. Do NOT reopen already-resolved product decisions.

9. Do NOT weaken tests to obtain a green baseline.

10. Do NOT skip tests merely to obtain a green baseline.

11. Do NOT delete tests.

12. Do NOT invent historical documentation.

13. Do NOT invent missing ADRs.

14. Do NOT invent historical BUILD-CHECKLIST entries.

15. Do NOT claim that reconstructed documentation is historical documentation.

16. Do NOT claim a Git revision is canonical unless its existence and content are proven.

17. Do NOT create a GitHub repository automatically.

18. Do NOT push automatically.

19. Do NOT commit automatically.

20. Do NOT rewrite Git history.

21. Do NOT force-push.

22. Do NOT delete or modify historical repositories merely to make the report clean.

23. Do NOT invent external infrastructure.

24. Do NOT invent production PostgreSQL evidence.

25. Do NOT claim PostgreSQL PASS unless the PostgreSQL-gated tests actually execute and pass.

26. Do NOT claim CI PASS merely because workflow files exist.

27. Do NOT claim release readiness from historical test results alone.

28. Do NOT silently replace current reconstructed documentation with speculative historical content.

29. Do NOT perform global formatting.

30. Do NOT modify unrelated files.

31. Do NOT expose secrets.

32. Do NOT print credential values, tokens or private keys.

33. Do NOT perform production deployment.

34. Do NOT activate Early Access.

35. Do NOT make legal/compliance claims.

36. If a canonical repository cannot be established from objective evidence, record:

```text
CANONICAL REPOSITORY = NOT ESTABLISHED
```

rather than guessing.

---

# 1. PHASE ENTRY BASELINE

Before changing anything, reconstruct the actual current state.

Run, where Git is available:

```text
git status --short
git branch --show-current
git rev-parse HEAD
git remote -v
git log -10 --oneline
git status --porcelain=v1
```

If the current directory is NOT a Git repository:

record explicitly:

```text
Git Repository = NOT PRESENT
```

Do NOT initialize Git merely to make this status look better.

Inspect the repository root and determine whether:

```text
docs/
docs/decisions/
docs/plans/
docs/release/
backend/
flutter/
.github/
Dockerfile
.gitignore
BUILD-CHECKLIST.md
```

exist.

Record exact paths that exist.

---

# 2. ESTABLISH REPOSITORY IDENTITY

The central question is:

```text
WHAT EXACT SOURCE TREE IS THE RELEASE CANDIDATE?
```

Investigate all objective local evidence for a repository identity:

### Source A — Current Git repository

If Git exists, inspect:

- remote
- branch
- HEAD
- repository root
- commit history
- working tree

### Source B — Sibling clean candidate

Search only for actual existing sibling candidates.

Do not invent one.

### Source C — Remote/canonical repository

If an authenticated remote is already configured or otherwise verifiably accessible, inspect:

- URL
- branch
- latest revision
- repository content
- documentation
- release state

### Source D — Git objects

If a local `.git` exists, inspect available objects and history.

### Source E — Other local project copies

Only use if actual copies exist and their relationship can be established.

For each source classify:

```text
AUTHORITATIVE
POSSIBLE
HISTORICAL
UNVERIFIED
NOT AVAILABLE
```

Do not promote a source from POSSIBLE to AUTHORITATIVE without evidence.

---

# 3. CANONICAL SOURCE DECISION

Produce:

```text
CANONICAL SOURCE ASSESSMENT
```

with:

```text
Current working tree:
Git repository:
Remote:
Candidate:
Canonical source:
Evidence:
Confidence:
```

The final result MUST be one of:

```text
CANONICAL SOURCE ESTABLISHED
```

or:

```text
CANONICAL SOURCE NOT ESTABLISHED
```

If not established:

DO NOT pretend the current folder is canonical.

The current source may still be classified as:

```text
ENGINEERING RELEASE CANDIDATE SOURCE
```

but not:

```text
CANONICAL RELEASE REPOSITORY
```

---

# 4. SOURCE-TREE INTEGRITY

If a Git repository is available:

compare:

```text
HEAD
```

against:

```text
CURRENT WORKING TREE
```

Determine whether release-critical content exists only in the working tree.

Inspect specifically:

- HelpRequests fixture corrections
- TenantIsolation behavioral-test change
- ADR-029
- ADR-030
- BUILD-CHECKLIST
- documentation reconstruction
- tests
- migrations
- backend source
- Flutter source
- CI
- Docker configuration

Produce:

```text
HEAD CONTENT
WORKING-TREE CONTENT
RELEASE-CANDIDATE CONTENT
```

For every difference classify:

```text
A = required release source
B = required test
C = required documentation
D = required migration
E = required CI/deployment
F = generated/local-only
G = unrelated
```

No G changes may become part of the release candidate.

---

# 5. CURRENT RELEASE CANDIDATE DEFINITION

Define the release candidate from the ACTUAL current source tree.

Do not define it from historical test counts.

The release candidate must consist of:

```text
CURRENT SOURCE
+
INTENTIONAL TEST FIXES
+
INTENTIONAL ARCHITECTURE TEST STABILIZATION
+
CURRENT REQUIRED DOCUMENTATION
+
CURRENT RELEASE METADATA
```

It must NOT include:

- unrelated local state
- temporary artifacts
- secret files
- generated outputs unless repository convention requires them
- abandoned experiments
- AI-agent private state
- build directories
- local credentials

Create:

```text
RELEASE CANDIDATE CONTENT INVENTORY
```

with exact paths.

---

# 6. DOCUMENTATION PROVENANCE — CRITICAL

The previous phase established:

```text
Historical Documentation Source = NOT-IDENTIFIED
```

and created only minimum current-gate documentation.

This phase MUST NOT fabricate the missing history.

Search for real sources in this order:

1. Existing canonical repository
2. Existing clean candidate
3. Git history
4. Local Git objects
5. Actual sibling repository
6. Existing project artifact/archive
7. Only then current reconstruction

For every documentation family classify:

```text
HISTORICAL — PROVEN
CURRENT — PROVEN
RECONSTRUCTED — CURRENT-GATE ONLY
MISSING — SOURCE UNAVAILABLE
```

---

# 7. DOCUMENTATION PROVENANCE REPORT

Create or update:

```text
docs/release/DOCUMENTATION-PROVENANCE-REPORT.md
```

It must record:

## Historical source

Whether historical source was found.

## Proven files

Exact files whose historical existence/content is proven.

## Reconstructed files

Files reconstructed from current code/known decisions.

## Missing files

Files that cannot be restored because no authoritative source exists.

## Restrictions

Explicitly state that reconstructed files MUST NOT be described as historical documents.

## ADR-029

Source provenance.

## ADR-030

Source provenance.

## BUILD-CHECKLIST

Source provenance.

## Historical ADR-001…ADR-028

Only list as recovered if actual evidence exists.

Do NOT create placeholder ADR files simply to make numbering complete.

---

# 8. ADR-029 VERIFICATION

Verify:

```text
docs/decisions/ADR-029-*.md
```

and the actual implementation.

Confirm:

```text
Status = Accepted
```

and that the decision is still:

```text
Staff authorization is based on active Coordinator/Admin membership.
```

Verify it does NOT derive authorization from:

- authentication method
- TOTP presence
- client flags
- arbitrary client capability claims

Compare:

ADR document\
vs\
implementation\
vs\
tests

Required result:

```text
ADR-029 = CONSISTENT
```

If inconsistent:

STOP and classify the discrepancy.

Do not redesign the decision.

---

# 9. ADR-030 VERIFICATION

Verify:

```text
docs/decisions/ADR-030-*.md
```

and implementation.

Confirm:

```text
Effective trust is recalculated from live verification facts
at each trust-consuming boundary.
```

Confirm:

```text
Login-time trust snapshot = audit evidence only
```

Verify:

- revoked verification
- expired verification
- deactivated account
- live trust consumption
- client boundary

Compare:

ADR\
vs\
implementation\
vs\
tests

Required result:

```text
ADR-030 = CONSISTENT
```

Do not redesign the trust model.

---

# 10. APPROVED POLICY BASELINE VERIFICATION

Verify current implementation against:

```text
ADR-027 = Option A
S-2 = Option B
S-3 = Option B
S-4 = Option B
B-1 = Option B
B-2 = Option A
ADR-026 = Option C
```

The goal is NOT to re-implement these.

The goal is traceability:

```text
Decision
-> implementation
-> tests
-> documentation
```

For each record:

```text
Decision
Implementation file(s)
Test file(s)
Documentation
Current status
```

Required result:

```text
APPROVED POLICY BASELINE = CONSISTENT
```

unless objective contradiction exists.

---

# 11. POSTGRESQL — FINAL ENGINEERING BASELINE GATE

This is a mandatory final gate before calling the engineering release baseline fully validated.

The previous result was:

```text
Backend Tests = 708 passed / 0 failed / 3 skipped
PostgreSQL = NOT-RUN
```

The 3 skips were PostgreSQL-gated by repository contract.

Therefore:

## Objective

Execute the PostgreSQL-gated tests against a clean/disposable PostgreSQL environment using the repository-supported mechanism.

Prefer the existing repository mechanism.

Do not invent a new test architecture.

Do not replace PostgreSQL with InMemory or SQLite.

Do not weaken integration tests.

---

# 12. POSTGRESQL ENVIRONMENT

Determine the repository contract for:

```text
SC_TEST_POSTGRES
```

Determine:

- connection string format
- database lifecycle
- migration setup
- test seed/reset
- supported local infrastructure
- CI mechanism

If an existing disposable PostgreSQL mechanism is available:

use it.

If Docker is the established supported method and Docker is actually available:

a disposable PostgreSQL instance may be used.

Do not alter production configuration merely to execute tests.

---

# 13. POSTGRESQL TEST EXECUTION

Run the canonical backend test command with PostgreSQL enabled.

At minimum:

```text
dotnet test backend/SeniorConnect.slnx -c Release
```

with the required PostgreSQL environment.

Required:

```text
0 failures
0 PostgreSQL skips
```

Record actual counts.

Do not assume the result will be:

```text
711 passed
```

or any other number.

Use actual numbers only.

---

# 14. POSTGRESQL SECURITY COVERAGE

While PostgreSQL is available, verify at minimum:

### Tenant Isolation

- organization A
- organization B
- community/null-org rows

Verify:

```text
A sees A + community
A does not see B

B sees B + community
B does not see A
```

### HelpRequests

Verify:

- same-tenant access
- cross-tenant denial
- community visibility rules
- matching authorization

### Safeguarding

Verify:

- organization-scoped cases
- platform-level cases
- officer isolation
- ordinary-admin denial

### Trust

Verify live trust recalculation where PostgreSQL integration coverage exists.

### Audit

Verify security-sensitive audit behavior where applicable.

Do not add new product behavior.

---

# 15. MIGRATION BASELINE

Determine the actual current migration head.

Record:

```text
Migration Head
Database Provider
Schema State
```

Verify the migration chain can initialize a clean PostgreSQL database.

At minimum:

```text
empty database
    ->
all migrations
    ->
schema usable
    ->
required tests
```

Do not modify migrations unless a genuine migration defect is discovered.

Do not create a migration merely for release metadata.

---

# 16. FULL CURRENT ENGINEERING VALIDATION

After PostgreSQL succeeds, run the minimum full release baseline required by repository policy.

At minimum:

```text
dotnet build backend/SeniorConnect.slnx -c Release
dotnet test backend/SeniorConnect.slnx -c Release

flutter analyze
flutter test
```

Run architecture tests.

Run security regression tests.

Run localization only if the current files or localization configuration changed.

Use actual current numbers.

Do not substitute historical numbers such as:

```text
708
909
87
```

unless those are exactly what the current run produced.

---

# 17. WARNING BASELINE

Verify:

```text
Warnings = 0
```

for the release-critical backend/build path.

If warnings appear:

classify each:

```text
PRE-EXISTING
NEW
ENVIRONMENTAL
RELEASE-BLOCKING
```

Do not suppress warnings merely to get zero.

---

# 18. RELEASE TRACEABILITY

Create/update:

```text
docs/release/RELEASE-CANDIDATE-MANIFEST.md
```

It must record actual values:

```text
Repository:
Canonical status:
Branch:
HEAD:
Working tree:
Application version:
Backend target framework:
.NET SDK:
Flutter SDK:
Dart SDK:
Dependency state:
Migration head:
Build configuration:
PostgreSQL result:
Architecture result:
Security result:
Flutter result:
Localization result:
Warnings:
ADR-026:
Approved policy baseline:
```

For values unavailable:

use:

```text
NOT DEFINED
```

or:

```text
PENDING — EXTERNAL
```

Do NOT invent values.

---

# 19. COMMIT ANCHOR RULE

A release candidate becomes traceable only when a real Git commit contains it.

Therefore:

If the candidate is uncommitted:

```text
Release Revision = PENDING — HUMAN COMMIT
```

If Git is unavailable:

```text
Release Revision = PENDING — NO VERIFIED GIT REPOSITORY
```

Do NOT generate a fake SHA.

Do NOT use a parent/old SHA as the release SHA if it does not contain the candidate.

---

# 20. RELEASE FREEZE

Create/update:

```text
docs/release/RELEASE-CANDIDATE-FREEZE.md
```

Before human commit:

```text
CANDIDATE PREPARED — NOT COMMITTED
```

After a verified human commit:

```text
CANDIDATE COMMITTED
```

The freeze boundary is:

```text
THE FIRST VERIFIED COMMIT CONTAINING THE COMPLETE RELEASE CANDIDATE
```

Do not call the current working tree frozen.

---

# 21. RELEASE-CRITICAL FILE INVENTORY

Create:

```text
docs/release/RELEASE-CRITICAL-FILE-INVENTORY.md
```

Use:

| Classification | Path          | Reason                   |
| -------------- | ------------- | ------------------------ |
| A              | Application   | Release source           |
| B              | Tests         | Required validation      |
| C              | Migrations    | Database schema          |
| D              | Documentation | Traceability/gates       |
| E              | CI/Deployment | Release execution        |
| F              | Local/Secret  | Excluded                 |
| G              | Generated     | Excluded unless required |
| H              | Unrelated     | Excluded                 |

This inventory must answer:

```text
Exactly what belongs in the first canonical release commit?
```

---

# 22. SECRET INTEGRITY CHECK

Perform a targeted scan against the current release candidate.

Check:

- `.env`
- configuration
- appsettings
- Docker
- CI
- scripts
- documentation
- AI tooling configuration
- private-key markers
- credential patterns

Classify findings.

Do not output values.

Required result:

```text
SECRET HYGIENE = PASS
```

only when actual evidence supports it.

Do not import historical secret findings from older phases unless they exist in the current source/history being examined.

---

# 23. .GITIGNORE / TRACKING INTEGRITY

If Git exists, verify:

```text
git check-ignore -v docs/release/RELEASE-CANDIDATE-MANIFEST.md
```

The intended release documentation MUST be trackable.

Also verify that local secret patterns remain ignored.

Do not introduce broad negation rules.

Do not expose:

- `.env`
- local credentials
- local AI secrets
- private configuration

---

# 24. DOCUMENTATION STRUCTURE

Do NOT attempt to recreate the full historical docs tree without evidence.

The current release tree must contain, where justified:

```text
docs/decisions/
docs/plans/
docs/release/
```

and required launch/security records.

The minimum canonical current-gate documentation must accurately cover:

- ADR-029
- ADR-030
- current BUILD-CHECKLIST
- approved policy baseline
- security state
- release candidate
- repository provenance

Anything historically absent remains explicitly marked:

```text
HISTORICAL SOURCE UNAVAILABLE
```

---

# 25. BUILD-CHECKLIST FINALIZATION

Verify:

```text
docs/plans/BUILD-CHECKLIST.md
```

It must distinguish:

```text
CURRENT VERIFIED GATES
```

from:

```text
HISTORICAL / UNVERIFIED ITEMS
```

Do not fabricate the historical 157-task checklist.

Do not state that historical tasks were completed unless evidence exists.

At minimum, current-gate sections must represent:

- build
- backend tests
- PostgreSQL
- architecture
- security
- Flutter
- localization
- tenant isolation
- HelpRequests matching
- TOTP authorization
- trust recalculation
- IdAustria state
- ADR-026
- release integrity
- documentation provenance

---

# 26. RELEASE BASELINE EVIDENCE PACKAGE

Create:

```text
docs/release/RELEASE-BASELINE-EVIDENCE.md
```

Include:

## Engineering

Actual current validation results.

## PostgreSQL

Exact execution result.

## Security

Actual tests executed.

## Architecture

Actual tests executed.

## Flutter

Actual tests executed.

## Documentation

Provenance status.

## Repository

Repository identity status.

## Release Candidate

Traceability status.

## ADR-026

OFF.

## External Gates

Clearly separate:

```text
ENGINEERING BASELINE
```

from:

```text
EXTERNAL DEPLOYMENT GATES
```

---

# 27. REPOSITORY INTEGRITY REPORT

Create:

```text
docs/release/REPOSITORY-INTEGRITY-REPORT.md
```

It must answer:

```text
1. Is this a Git repository?
2. What is the repository URL?
3. What branch?
4. What HEAD?
5. Is the working tree clean?
6. Is the release candidate committed?
7. Can the release candidate be uniquely identified?
8. Is docs/release tracked?
9. Are release-critical files identifiable?
10. Are secrets excluded?
11. Is canonical repository identity established?
12. If not, exactly what evidence is missing?
```

Use only objective evidence.

---

# 28. EXTERNAL GATES MUST NOT BE REOPENED

This phase is not responsible for:

- production hosting
- production DNS
- production TLS
- SMTP provider
- production backup target
- monitoring provider
- operational appointments
- legal approval
- participant recruitment
- Early Access

Record current state only.

Do not invent evidence.

Do not generate simulated production evidence.

---

# 29. ADR-026 FINAL SAFETY CHECK

Verify in the current release candidate:

```text
VisitAddress:RequireGrant = false
```

Verify:

- configuration default = OFF
- Flutter cannot activate it
- ordinary user cannot activate it
- ordinary admin cannot activate it
- release documentation says OFF

Required:

```text
ADR-026 = OFF
```

---

# 30. VERSION / TOOLCHAIN TRACEABILITY

Inspect actual:

```text
global.json
*.csproj
Directory.Build.*
pubspec.yaml
pubspec.lock
FVM configuration if present
Dockerfile
CI workflows
```

Record:

- target framework
- .NET SDK
- Flutter SDK
- Dart SDK
- package/dependency lock state
- application version
- migration head

If not explicitly pinned:

```text
NOT EXPLICITLY PINNED
```

Do NOT modify versions solely to make traceability look cleaner.

---

# 31. SOURCE-TO-RELEASE EQUIVALENCE

Prove that:

```text
TESTED SOURCE
```

equals:

```text
RELEASE CANDIDATE SOURCE
```

except for documented release metadata/provenance files.

At minimum compare:

- backend source
- tests
- migrations
- Flutter source
- localization
- security docs
- ADRs
- release docs
- CI
- Docker configuration

If the tested source and release candidate differ materially:

the release baseline is:

```text
BLOCKED
```

until the candidate is revalidated.

---

# 32. HUMAN COMMIT PACKAGE

The agent MUST NOT commit.

Prepare an exact human procedure.

If Git is available:

```text
git status --short

git diff --stat
git diff --name-status
git diff --check

review RELEASE-CRITICAL-FILE-INVENTORY.md

perform final secret scan

git add <ONLY REVIEWED RELEASE FILES>

git diff --cached --check
git diff --cached --stat
git diff --cached --name-status

git commit -m "<human-selected canonical release baseline message>"

git rev-parse HEAD
git log --oneline --decorate -5
git status --short
```

Do not execute the commit.

Do not provide force-push commands.

Do not provide history-rewrite commands.

---

# 33. CANONICAL REPOSITORY CUTOVER — ONLY IF NECESSARY

If the current repository is already demonstrably canonical:

do NOT create another repository.

If it is not canonical and a separate clean candidate is required:

prepare the candidate only.

Do NOT create a GitHub repository automatically.

The report must state:

```text
CANONICAL REPOSITORY =
ESTABLISHED
```

or:

```text
CANONICAL REPOSITORY =
NOT ESTABLISHED — HUMAN/EXTERNAL CUTOVER REQUIRED
```

---

# 34. NO PRODUCT / APPLICATION CHANGES

If the work reveals a product feature gap:

record:

```text
FUTURE PRODUCT WORK
```

Do NOT implement it.

If the work reveals a genuine security or release-integrity defect:

only then make the smallest required fix and rerun the affected tests.

---

# 35. FINAL ACCEPTANCE CRITERIA

This phase is COMPLETE only if:

1. Canonical source identity is established OR the exact external blocker is documented.

2. Current release candidate content is fully inventoried.

3. No unexplained release-critical source difference exists.

4. PostgreSQL-gated tests have actually executed and passed, OR the environment blocker is explicitly documented.

5. Backend tests are green.

6. Architecture tests are green.

7. Security regression is green.

8. Flutter analyze is clean.

9. Flutter tests are green.

10. Warnings are zero or precisely classified.

11. Migration head is identified.

12. Toolchain is identified.

13. Dependency state is identified.

14. ADR-029 is consistent.

15. ADR-030 is consistent.

16. Approved policy baseline is consistent.

17. ADR-026 remains OFF.

18. Release documents are trackable.

19. Documentation provenance is explicit.

20. No historical documentation is fabricated.

21. No actual credential is present in the release candidate.

22. Release-critical files are inventoried.

23. The candidate is uniquely identifiable by a real commit OR explicitly marked as awaiting human commit.

24. External production gates remain unchanged unless new evidence exists.

25. Early Access remains NOT STARTED.

26. No deployment occurs.

27. No product feature is added.

28. No automatic commit occurs.

29. No Git history rewrite occurs.

---

# 36. REQUIRED FINAL REPORT

Return exactly:

```text
PHASE STATUS

Repository Identity = ESTABLISHED/NOT-ESTABLISHED
Canonical Source = PASS/PENDING/BLOCKED

Source Integrity = PASS/FAIL
Release Candidate Inventory = PASS/FAIL

PostgreSQL = PASS/BLOCKED/NOT-RUN
Backend Build = PASS/FAIL
Backend Tests = PASS/FAIL
Architecture Tests = PASS/FAIL
Security Regression = PASS/FAIL
Flutter Analyze = PASS/FAIL
Flutter Tests = PASS/FAIL
Localization = PASS/NOT-RUN
Warnings = 0/N

Migration Traceability = PASS/PENDING
Toolchain Traceability = PASS/PENDING
Dependency Traceability = PASS/PENDING

ADR-029 = CONSISTENT/INCONSISTENT
ADR-030 = CONSISTENT/INCONSISTENT
Approved Policy Baseline = PASS/FAIL

Documentation Provenance = PASS/PARTIAL/BLOCKED
BUILD-CHECKLIST = PASS/PARTIAL/BLOCKED
Release Documentation Tracking = PASS/FAIL

Secret Hygiene = PASS/FAIL
ADR-026 = OFF

Release Revision = COMMITTED/PENDING HUMAN COMMIT
Release Traceability = PASS/PENDING
Working Tree Hygiene = PASS/FAIL

External Gates = UNCHANGED/UPDATED
Early Access = NOT STARTED

Final Result =
CANONICAL RELEASE BASELINE READY

or

CANONICAL RELEASE BASELINE PREPARED — HUMAN REPOSITORY/COMMIT ACTION REQUIRED

or

BLOCKED — CANONICAL RELEASE BASELINE NOT ESTABLISHED
```

Then provide exactly:

## 1. REPOSITORY IDENTITY

Repository:\
Remote:\
Branch:\
HEAD:\
Working tree:\
Canonical status:\
Evidence:

## 2. RELEASE CANDIDATE

Source:\
Candidate definition:\
Release-critical files:\
Committed revision:\
Human action required:

## 3. POSTGRESQL

Environment:\
Command:\
Actual result:\
Passed:\
Failed:\
Skipped:\
Migration result:

## 4. ENGINEERING VALIDATION

Backend:\
Architecture:\
Security:\
Flutter:\
Localization:\
Warnings:

Use only actual current results.

## 5. DOCUMENTATION PROVENANCE

Historical source:\
Proven documents:\
Reconstructed documents:\
Missing documents:\
ADR-029:\
ADR-030:\
BUILD-CHECKLIST:

## 6. TRACEABILITY

Application version:\
Backend framework:\
.NET SDK:\
Flutter SDK:\
Dart SDK:\
Dependencies:\
Migration head:\
Artifact:\
Release revision:

## 7. SECURITY

Secret scan:\
Tenant isolation:\
HelpRequests:\
ADR-026:\
Other security evidence:

## 8. FILES CHANGED

Exact paths only.

## 9. EXTERNAL GATES

Only gates relevant to this phase.

Do not repeat unchanged deployment analysis.

## 10. HUMAN ACTIONS REQUIRED

Only actions that cannot be performed safely/authoritatively by the agent.

At minimum, where applicable:

1. human review of release-critical file inventory
2. human initial/canonical commit
3. canonical remote/repository verification
4. CI execution against the committed revision

## 11. REMAINING BLOCKERS

Only genuine blockers.

## 12. NEXT PHASE

If fully complete:

```text
RELEASE CANDIDATE VERIFICATION / CANONICAL REPOSITORY CUTOVER
```

If canonical repository is not yet established:

```text
HUMAN CANONICAL REPOSITORY CUTOVER
```

Do NOT recommend another feature-development phase.

---

# 37. HARD STOP

STOP after:

- canonical source assessment
- release candidate inventory
- PostgreSQL gate
- documentation provenance
- ADR-029/030 consistency verification
- release traceability
- release documentation
- repository-integrity report
- final report

DO NOT:

- add product features
- redesign matching
- redesign trust
- redesign safeguarding
- modify approved policy decisions
- activate ADR-026
- deploy production
- launch Early Access
- contact participants
- contact organizations
- onboard users
- create a GitHub repository automatically
- commit automatically
- push automatically
- tag automatically
- rewrite history
- force-push
- invent historical documentation
- invent historical ADRs
- fabricate PostgreSQL evidence
- fabricate CI evidence
- fabricate release SHA
- claim legal approval

The goal of this phase is:

```text
EXACT RELEASE CONTENT
+
EXACT VALIDATION EVIDENCE
+
EXACT DOCUMENTATION PROVENANCE
+
EXACT REPOSITORY IDENTITY
+
EXACT RELEASE TRACEABILITY
```

Nothing more.
