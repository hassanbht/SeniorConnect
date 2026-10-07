# SENIORCONNECT — NEXT PHASE

# Post-Wave 3 Failure Closure & Canonical Release Baseline Preparation

## PURPOSE

Wave 3 security/quality scope has been implemented and is green within its\
own scope.

The previous phase reported:

```text
ProblemDetails Audit = PASS
ProblemDetails Architecture Guard = PASS
Swagger Opt-In = PASS
Correlation-ID Validation = PASS
Warning Cleanup = PASS

Staff TOTP ADR = APPROVED
Staff TOTP Authorization = PASS
Staff TOTP Security Tests = PASS

Trust Recalculation ADR = APPROVED
Trust Recalculation = PASS
Trust Client Boundary = PASS

IdAustria Provider = RETAINED / INTENTIONALLY INACTIVE
ADR-026 = OFF
Approved Policy Baseline = PASS
Flutter Analyze = PASS
Flutter Tests = 909/909 PASS
Warnings = 0
```

However, the previous phase remains:

```text
BLOCKED — WAVE 3 NOT CLOSED
```

because of three repository-level blockers:

1. 7 pre-existing `HelpRequests.Tests` failures.
2. 1 pre-existing `ArchitectureTests.TenantIsolationTests` failure.
3. The snapshot contains no `docs/` tree, no `BUILD-CHECKLIST.md`, and no\
   canonical ADR documentation directory.

The purpose of this phase is therefore:

```text
CLOSE EXISTING BASELINE FAILURES
+
RESTORE CANONICAL DOCUMENTATION STRUCTURE
+
REVALIDATE SECURITY/ARCHITECTURE BASELINE
+
PREPARE FOR CANONICAL RELEASE BASELINE
```

This is NOT a feature-development phase.

This is NOT a Wave-4/5 product phase.

This is NOT a production deployment phase.

This is NOT an Early Access phase.

---

# 0. HARD RULES

1. Do NOT redo completed Wave 3 work unless a direct regression is found.

2. Do NOT redesign:

   - ADR-029 staff authorization
   - ADR-030 trust recalculation
   - ADR-027 trust ladder
   - S-2
   - S-3
   - S-4
   - B-1
   - B-2
   - ADR-026

3. ADR-026 MUST remain:

```text
OFF
```

4. Do NOT introduce:

   - AI matching
   - payments
   - voice/IVR
   - corporate volunteering
   - white-label
   - new verification systems
   - new product functionality

5. Do NOT fix failing tests by:

   - weakening assertions
   - deleting tests
   - skipping tests
   - marking inconclusive
   - increasing timeouts arbitrarily
   - changing expected results without domain evidence

6. Do NOT weaken tenant-isolation filters merely to make an architecture\
   test pass.

7. Do NOT weaken authorization to make HelpRequests tests pass.

8. Do NOT invent missing documentation structure.

9. Do NOT invent ADR numbers.

10. Restore documentation from actual repository history/source evidence\
    whenever possible.

11. Do NOT rewrite Git history.

12. Do NOT force-push.

13. Do NOT commit automatically.

14. Do NOT create a new repository automatically.

15. Do NOT deploy production.

16. Do NOT activate Early Access.

17. Do NOT activate VisitAddressGrant.

18. Do NOT expose secrets.

19. Do NOT create synthetic documentation pretending that historical files\
    existed if they cannot be proven to exist.

20. Do NOT perform global formatting.

21. Do NOT modify unrelated files.

---

# 1. RECONSTRUCT THE ACTUAL REPOSITORY STATE

Before changing anything:

```text
git status --short
git branch --show-current
git rev-parse HEAD
git remote -v
git log -10 --oneline
```

If the current directory is not a Git repository:

record that fact immediately.

Inspect the repository root.

Determine whether:

```text
docs/
AGENTS.md
.agents/
BUILD-CHECKLIST.md
```

exist anywhere relevant.

Search carefully for historical copies of:

```text
docs/decisions/
docs/architecture/adr/
docs/plans/
docs/security/
BUILD-CHECKLIST.md
ADR-029
ADR-030
```

Possible evidence sources:

- current working tree
- repository history
- sibling clean candidate
- known canonical repository
- Git objects that are actually available
- previously created release candidate

Do NOT assume one source is authoritative without evidence.

---

# 2. BASELINE RECONCILIATION

Capture the actual current failures before modification.

Run the smallest useful test scope first.

At minimum identify:

### HelpRequests

The exact 7 failures.

For each record:

```text
Test
Expected
Actual
Stack trace/location
Production code involved
Likely root cause
```

### Tenant Isolation

Record:

```text
Test
Expected
Actual
Entity
Filter involved
Assertion logic
Security invariant
```

Do not modify tests yet.

Produce:

```text
BASELINE FAILURE INVENTORY
```

---

# 3. WAVE 3 REGRESSION CHECK

Before fixing unrelated failures, verify that the completed Wave 3\
security work remains intact.

Focused checks:

### W2-5

- PBKDF2 versioned format
- legacy compatibility
- rehash detection
- unreasonable-cost rejection

### W2-6

- stable audit code
- server-side staff capability
- no authentication-method inference

### Wave 3

- ProblemDetails guard
- Swagger opt-in
- Correlation-ID validation
- staff TOTP authorization
- TrustLevelReader recalculation
- IdAustria fail-closed behavior

Do NOT repeat every historical test unless required.

The objective is to ensure that fixing the baseline failures does not\
regress the newly completed security work.

---

# 4. HELPREQUESTS FAILURE CLOSURE — PRIMARY TASK

There are exactly seven known pre-existing failures.

Do NOT treat them as seven unrelated bugs until their common root cause\
has been investigated.

Group the seven tests by behavior.

Expected categories may include:

```text
Matching
Proposal acceptance
Safety-level filtering
Location/tenant authorization
Sweep/dispatch
Hybrid matching
Other
```

Use actual test names.

For each failing test determine:

```text
1. Is the production behavior wrong?
2. Is the test outdated?
3. Is the test fixture invalid?
4. Is the failure caused by a recent security rule?
5. Is the failure caused by a stale business-rule assumption?
6. Is the failure an infrastructure/setup issue?
```

Do not change anything until the cause is classified.

---

# 5. HELPREQUESTS — MATCHING SAFETY RULES

Pay particular attention to the reported failures involving:

```text
OfferSweep
AcceptHelpRequest
MatchingService
MatchingLocationHotfix
HybridProposals
HybridMatchingPolicy
```

Inspect:

- matching authorization
- request ownership
- tenant scope
- volunteer eligibility
- discovery eligibility
- safety restrictions
- location visibility
- assignment state
- block relationships
- verification requirements
- trust level consumption
- proposal rules

Do NOT revert the Wave 1–5 security architecture merely because old\
matching tests expect different behavior.

The question is:

```text
Is the current production behavior correct according to the approved
business/security rules?
```

---

# 6. HELPREQUESTS ROOT-CAUSE POLICY

For every failing test use one classification:

```text
A = Production bug

B = Outdated test

C = Invalid/obsolete fixture

D = Test infrastructure/setup problem

E = Business-rule mismatch requiring documented decision
```

Rules:

### A

Fix production code.

Then add/update regression coverage.

### B

Update the test ONLY when repository evidence proves that the expected\
behavior changed intentionally.

### C

Correct the fixture only when the fixture violates current domain rules.

### D

Fix the test infrastructure.

Do not change application behavior.

### E

STOP the affected behavior and mark:

```text
DECISION REQUIRED
```

Do not invent the business decision.

---

# 7. MATCHING SECURITY INVARIANTS

While resolving the failures, these MUST remain true:

1. No cross-tenant matching leakage.

2. No arbitrary HelpRequest lookup.

3. No arbitrary volunteer lookup.

4. No client-controlled tenant authorization.

5. No client-controlled TrustLevel.

6. No client-controlled VerificationLevel.

7. No client-controlled safeguarding state.

8. No exact private coordinate leakage.

9. No arbitrary lat/lng probing.

10. No blocked-pair bypass.

11. No safeguarding restriction bypass.

12. No unauthorized proposal creation.

13. No unauthorized proposal acceptance.

14. No unauthorized assignment mutation.

15. No security-sensitive state derived from client flags.

If any fix would weaken one of these:

STOP and classify it as a security blocker.

---

# 8. TENANT ISOLATION FAILURE

Investigate:

```text
ArchitectureTests.TenantIsolationTests
Query_filters_admit_rows_with_a_null_organization
```

The previous report suggests the production filter may intentionally allow\
null-organization `CommunityEvent` rows and that the test is relying on\
EF `ToString()` rendering.

Do NOT assume this is true.

Prove it.

Inspect:

- `SeniorConnectDbContext.cs`
- the actual `CommunityEvent` entity
- its OrganizationId semantics
- query filters
- generated SQL/expression tree where possible
- the architecture test
- the security invariant

Determine whether:

A. actual tenant isolation is violated

or:

B. the test is brittle/version-dependent but the security invariant is\
intact.

---

# 9. TENANT ISOLATION DECISION

If A:

Fix the production filter.

Add regression coverage.

Run cross-tenant integration tests.

If B:

Do NOT weaken the tenant filter.

Replace only the brittle test mechanism with a stable semantic assertion.

The test should verify a real invariant, for example:

```text
organization-scoped entity
+
different organization
+
query
=
row not visible
```

Do not rely on:

```text
EF ToString()
```

or another implementation-detail string unless absolutely necessary.

The architecture test must remain meaningful.

---

# 10. CROSS-TENANT SECURITY REGRESSION

After the TenantIsolation decision, execute focused PostgreSQL tests covering:

### Organization A

- ordinary user
- staff
- coordinator
- admin

### Organization B

- ordinary user
- staff
- coordinator
- admin

Verify:

```text
A -> A = allowed where authorized
A -> B = denied where not authorized
B -> A = denied where not authorized
```

Also verify platform-level safeguarding behavior:

```text
Organization officer -> platform case = DENIED
Platform officer -> platform case = ALLOWED
```

Do not use only unit tests for this gate if PostgreSQL integration tests are\
available.

---

# 11. DOCUMENTATION RESTORATION — CRITICAL

The previous snapshot had:

```text
No docs/
No BUILD-CHECKLIST.md
No canonical ADR directory
```

Do NOT create arbitrary documentation from scratch.

Search for authoritative historical/canonical copies.

Priority:

1. NEW canonical repository if already available
2. clean candidate
3. Git history if accessible
4. sibling working tree
5. known project artifact/source
6. only then create the minimum missing documents

The restored documentation should include at minimum the current\
architecture/security/release structure required by the repository.

---

# 12. ADR-029

The current Wave 3 implementation uses ADR-029 as an in-code decision\
record.

Restore or create the canonical ADR document ONLY if repository evidence\
establishes that this is the next appropriate ADR number.

Document:

```text
Title:
Staff TOTP Management Authorization
Status:
Accepted

Decision:
Staff authorization is based on active Coordinator/Admin membership.

Not authoritative:
- authentication method
- TOTP presence
- client flags
```

Record:

- problem
- decision
- alternatives
- security boundary
- module dependency rule
- tests
- implementation references

Do not alter the accepted decision.

---

# 13. ADR-030

Restore or create the canonical ADR document ONLY if ADR numbering is\
proven.

Document:

```text
Title:
Effective Trust Recalculation
Status:
Accepted
```

Decision:

```text
Effective trust is calculated from live verification facts at each
trust-consuming boundary.

Login-time snapshot is audit evidence only.
```

Record:

- calculation boundary
- approved ladder
- invalidation behavior
- revoked verification
- expiry
- deactivated accounts
- safeguarding separation
- ratings separation
- client boundary
- tests

Do not change the approved ladder.

---

# 14. BUILD-CHECKLIST RESTORATION

Restore:

```text
docs/plans/BUILD-CHECKLIST.md
```

Only from authoritative repository evidence.

It MUST contain the current security/release gates.

At minimum ensure the current state can record:

- Wave 3
- ProblemDetails
- Swagger
- Correlation-ID
- TOTP authorization
- Trust recalculation
- IdAustria state
- ADR-026
- tenant isolation
- HelpRequests matching
- backend
- Flutter
- PostgreSQL
- architecture
- security
- localization
- release integrity

Do not fabricate historical checklist entries.

---

# 15. DOCUMENT CONSISTENCY

Once the documentation tree is restored:

Check for contradictions between:

- ADR-027
- ADR-029
- ADR-030
- business rules
- safeguarding policy
- security policies
- BUILD-CHECKLIST
- release documents

Do not silently resolve contradictions.

Classify each:

```text
CONSISTENT
STALE
CONFLICTING — DECISION REQUIRED
```

Only correct documentation when repository evidence establishes the\
correct state.

---

# 16. NO FEATURE EXPANSION

If a matching test failure reveals a missing feature rather than a bug:

DO NOT implement it here.

Record:

```text
FUTURE PRODUCT WORK
```

unless it is a direct violation of an already-approved security invariant.

Do not let the seven failing tests turn into a new matching-system project.

---

# 17. REQUIRED VALIDATION AFTER FIXES

After all justified fixes:

### Backend

```text
dotnet build backend/SeniorConnect.slnx -c Release
dotnet test backend/SeniorConnect.slnx -c Release
```

Required:

```text
0 errors
0 failures
0 skips
```

unless the repository's test contract explicitly allows a documented\
non-test artifact, in which case report it precisely.

### Flutter

```text
flutter analyze
flutter test
```

Required:

```text
0 failures
```

### Architecture

Run all architecture tests.

Required:

```text
0 failures
```

### Security

Run all relevant security regression tests.

Required:

```text
0 failures
```

### PostgreSQL

Run the clean PostgreSQL integration suite.

Required:

```text
0 failures
```

Do NOT turn failures into skipped tests.

---

# 18. FULL BASELINE REVALIDATION

Only after the focused failures are fixed, run the broader release baseline\
required by repository policy.

Do not rely on historical:

```text
612 backend
906 Flutter
55 HTTP
```

as current PASS evidence when the repository has changed.

Report actual current numbers.

At minimum validate:

```text
Backend
Architecture
Security
PostgreSQL
Flutter
Localization
Build
```

HTTP synthetic validation should be rerun only if matching/authentication/API\
inputs changed.

---

# 19. RELEASE-INTEGRITY PRE-CHECK

If all current tests are green, prepare the repository for the next phase.

Record:

```text
Repository
Branch
HEAD
Working tree
Application version
.NET SDK
Flutter SDK
Dart SDK
Dependency state
Migration head
ADR-026
Warnings
Test baseline
```

Do NOT commit automatically.

Do NOT push automatically.

Do NOT create a release tag.

---

# 20. RELEASE-READINESS GATE

The final output of this phase should answer:

```text
Can the current repository enter
CANONICAL RELEASE BASELINE
without known engineering failures?
```

The answer can only be:

```text
YES
```

if:

- backend = green
- architecture = green
- security = green
- PostgreSQL = green
- Flutter = green
- docs restored
- no unresolved Critical/High regression
- no unresolved tenant-isolation failure
- no unexplained HelpRequests failures

Otherwise:

```text
NO — BLOCKED
```

with exact blockers.

---

# 21. GIT HYGIENE

Before changes:

```text
git status --short
```

After changes:

```text
git status --short
git diff --stat
git diff --check
git diff --name-status
```

Classify changes:

```text
A = matching/security fix
B = test correction
C = architecture-test stabilization
D = documentation restoration
E = required release metadata
F = unrelated
```

No F changes.

Do not commit.

---

# 22. ACCEPTANCE CRITERIA

This phase is COMPLETE only if:

1. All 7 HelpRequests failures are explained and resolved, or explicitly\
   blocked by a documented product decision.

2. No test is skipped merely to obtain green status.

3. Matching authorization remains secure.

4. No cross-tenant matching leak exists.

5. No block/safeguarding/location bypass exists.

6. TenantIsolation failure is conclusively classified as:

   - real production defect, or
   - brittle test defect.

7. If brittle, the test is stabilized without weakening tenant isolation.

8. Canonical documentation source is identified.

9. `docs/` tree is restored from authoritative evidence where possible.

10. ADR-029 has a canonical location if its existence is verified.

11. ADR-030 has a canonical location if its existence is verified.

12. BUILD-CHECKLIST is restored from authoritative evidence.

13. No new product feature is introduced.

14. ADR-026 remains OFF.

15. All current release-critical validation passes.

16. Working tree contains only intentional changes.

17. The repository is ready for the next:\
    `CANONICAL RELEASE BASELINE`\
    phase.

---

# 23. REQUIRED FINAL REPORT

Return exactly:

PHASE STATUS

Baseline Reconstruction = PASS/FAIL

HelpRequests Failure Inventory = COMPLETE/INCOMPLETE

HelpRequests Failures = 0/N

HelpRequests Root Cause Classification = PASS/BLOCKED

HelpRequests Security Boundary = PASS/FAIL

Tenant Isolation Failure = REAL DEFECT/BRITTLE TEST/UNRESOLVED

Tenant Isolation Security = PASS/FAIL

Architecture Tests = PASS/FAIL

Backend Build = PASS/FAIL

Backend Tests = PASS/FAIL

PostgreSQL = PASS/FAIL

Security Regression = PASS/FAIL

Flutter Analyze = PASS/FAIL

Flutter Tests = PASS/FAIL

Localization = PASS/FAIL/NOT-RUN

Warnings = 0/N

Documentation Source = IDENTIFIED/NOT-IDENTIFIED

Documentation Restored = PASS/PARTIAL/BLOCKED

ADR-029 Canonical Record = PASS/PENDING/BLOCKED

ADR-030 Canonical Record = PASS/PENDING/BLOCKED

BUILD-CHECKLIST = PASS/PENDING/BLOCKED

ADR-026 = OFF

Approved Policy Baseline = PASS/FAIL

Working Tree Hygiene = PASS/FAIL

Release Baseline Readiness = READY/BLOCKED

Final Result =

BASELINE CLOSED — READY FOR CANONICAL RELEASE BASELINE

or

BLOCKED — BASELINE FAILURES REMAIN

Then provide:

## 1. BASELINE FAILURES

## 2. HELPREQUESTS ROOT CAUSES

For each of the seven failures:

Test:\
Classification:\
Root cause:\
Fix:\
Validation:

## 3. TENANT ISOLATION

Security invariant:\
Test issue or production issue:\
Fix:\
Validation:

## 4. DOCUMENTATION RESTORATION

Source:\
Files restored:\
ADR-029:\
ADR-030:\
BUILD-CHECKLIST:

## 5. SECURITY VALIDATION

Only actual tests executed in this phase.

## 6. FULL VALIDATION

Actual current counts only.

## 7. FILES CHANGED

## 8. REMAINING BLOCKERS

Only genuine blockers.

## 9. NEXT PHASE

If fully green:

CANONICAL RELEASE BASELINE / REPOSITORY INTEGRITY

If not:

DEDICATED FAILURE CLOSURE

---

# 24. HARD STOP

STOP after:

- seven HelpRequests failures are resolved/classified
- TenantIsolation issue is resolved/classified
- canonical documentation is restored
- ADR-029/030 are anchored where justified
- BUILD-CHECKLIST is restored
- full current baseline is validated
- final report is produced

DO NOT:

- add product features
- redesign matching
- redesign Trust
- redesign safeguarding
- activate ADR-026
- deploy production
- create GitHub repository
- push
- commit automatically
- tag automatically
- launch Early Access
- begin another feature-development wave

If the final baseline is fully green, the ONLY recommended next phase is:

```text
CANONICAL RELEASE BASELINE
/
REPOSITORY INTEGRITY
/
RELEASE CANDIDATE TRACEABILITY
```

The goal of this phase is:

```text
ZERO UNEXPLAINED BASELINE FAILURES
+
CANONICAL DOCUMENTATION
+
RELEASE-READY ENGINEERING BASELINE
```

Nothing more.
