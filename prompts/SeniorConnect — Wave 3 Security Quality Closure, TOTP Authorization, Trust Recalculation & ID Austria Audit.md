# SENIORCONNECT — NEXT PHASE

# Wave 3 — Security Quality Closure, TOTP Authorization, Trust Recalculation & ID Austria Audit

## PURPOSE

W2-5 and W2-6 have been completed and validated.

Current verified result:

```text
Backend Build = 0 errors
Backend Tests = 664 PASS / 0 FAIL
Flutter Tests = 909 PASS / 0 FAIL
```

W2-5 completed:

- versioned PBKDF2 password format
- PBKDF2-HMAC-SHA256 = 600,000 iterations
- legacy password hashes remain readable
- `NeedsRehash` upgrades legacy hashes on successful login
- unreasonable PBKDF2 costs are rejected
- malformed records are rejected safely
- regression tests cover the above behavior

W2-6 completed:

- stable audit code replaces translated UI text
- client no longer infers `isStaff` from authentication method
- server exposes `canManageStaffSecurity`
- fail-safe current behavior is `false`

DO NOT redo W2-5 or W2-6 unless a direct regression is discovered.

The remaining work is:

### WAVE 3

1. Architecture test for unauthorized responses that bypass the established ProblemDetails contract.
2. Swagger/OpenAPI must be explicitly opt-in rather than enabled implicitly in every non-Production environment.
3. Validate and harden `X-Correlation-ID`.
4. Remove/resolve the remaining 16 warnings without suppressing real problems.

### ARCHITECTURE DECISIONS

5. Close the staff TOTP authorization boundary through an ADR and server-side capability resolution.
6. Close the Trust recalculation semantics through an ADR.
7. Audit `IdAustriaVerificationProvider` and determine whether it is genuinely dead code or intentionally retained infrastructure.

This phase is SECURITY / QUALITY / ARCHITECTURE CLOSURE.

It is NOT a product-feature phase.

It is NOT a repository migration phase.

It is NOT a production-deployment phase.

---

# 0. HARD RULES

1. Do NOT redo W2-5.

2. Do NOT redo W2-6.

3. Do NOT change the PBKDF2 algorithm/work factor unless a concrete defect is discovered.

4. Do NOT modify the legacy password compatibility behavior unless a concrete regression is demonstrated.

5. Do NOT weaken authentication or authorization to make tests pass.

6. Do NOT redesign TrustLevelCalculator's approved ladder.

7. Do NOT introduce a numeric trust score.

8. Do NOT merge:

   - verification
   - trust
   - safeguarding
   - ratings

9. Do NOT infer staff from:

   - login method
   - TOTP presence
   - authentication mechanism
   - client flags

10. Do NOT trust client-controlled security capabilities.

11. Do NOT make Identity directly depend on Organizations merely to solve TOTP authorization.

12. Do NOT activate ADR-026.

13. `VisitAddress:RequireGrant` MUST remain `false`.

14. Do NOT add new product features.

15. Do NOT add AI matching.

16. Do NOT add payments.

17. Do NOT add voice/IVR.

18. Do NOT add corporate volunteering.

19. Do NOT add a new authentication provider.

20. Do NOT delete `IdAustriaVerificationProvider` until repository-wide usage analysis proves it is genuinely dead code.

21. Do NOT introduce a new dependency merely to solve one of these issues.

22. Do NOT run global formatting.

23. Do NOT suppress compiler/analyzer warnings merely to obtain zero warnings.

24. Do NOT modify unrelated files.

25. Do NOT commit automatically.

26. Do NOT push automatically.

27. Do NOT change Git history.

28. Do NOT invent ADR numbers. Inspect the existing ADR sequence first.

---

# 1. RECONSTRUCT CURRENT STATE

Before changing anything:

```text
git status --short
git branch --show-current
git rev-parse HEAD
git log -10 --oneline
```

Inspect repository instructions:

```text
AGENTS.md
.agents/rules/AGENTS.md
.agents/rules/GEMINI.md
```

Inspect relevant security/architecture documentation:

```text
business-rules.md
docs/security/**
docs/decisions/**
docs/architecture/adr/**
docs/plans/BUILD-CHECKLIST.md
```

Also inspect:

- authentication architecture
- Identity/Application boundaries
- organization membership model
- staff capability model
- TrustLevelCalculator
- VerificationService
- TOTP implementation
- ProblemDetails/error handling
- Swagger/OpenAPI configuration
- correlation-ID middleware
- `IdAustriaVerificationProvider`

Use actual repository evidence.

Do not assume previous reports are current if the repository differs.

---

# 2. BASELINE RECONCILIATION

Record the current baseline before modifications:

```text
Backend Build
Backend Tests
Flutter Tests
Warnings
Architecture Tests
Security Tests
```

The known baseline is:

```text
Backend Build = 0 errors
Backend Tests = 664 PASS / 0 FAIL
Flutter Tests = 909 PASS / 0 FAIL
```

Treat those as previous-phase evidence.

If the current repository differs, report the difference.

Do not artificially rerun all large suites merely to duplicate evidence.

---

# 3. W2-5 / W2-6 REGRESSION GUARD

Perform a lightweight verification only.

Verify:

### Password hashing

- new hashes use the versioned format
- cost is encoded in the record
- legacy format remains readable
- malformed/high-cost records are rejected
- no plaintext password handling exists

### Audit

Verify:

```text
REACTIVATED_BY_COORDINATOR
```

remains a stable machine-readable code.

Verify no translated UI string is used as persistent audit data.

### Staff security

Verify:

- client does not infer staff from authentication method
- server capability remains authoritative

Do NOT redesign these completed changes.

---

# 4. WAVE 3-A — ProblemDetails ARCHITECTURE AUDIT

The current repository contains approximately 220 occurrences of:

```text
Results.Forbid()
Results.Unauthorized()
```

or equivalent authorization/error responses that may bypass the established ProblemDetails contract.

This task is NOT:

"replace every occurrence mechanically."

First determine the repository's canonical error contract.

Inspect:

- existing ProblemDetails middleware
- exception handling
- error DTOs
- API behavior
- endpoint conventions
- existing tests
- architecture rules

Classify each relevant occurrence:

```text
A = correctly emits canonical ProblemDetails
B = intentionally framework-native response
C = violates canonical API error contract
D = test-only / irrelevant
```

Only C requires implementation change.

For every changed endpoint:

- preserve correct HTTP status code
- preserve authorization semantics
- preserve tenant/security behavior
- preserve existing client expectations where repository evidence requires it
- emit the canonical error representation

Do not create multiple competing error formats.

---

# 5. PROBLEMDETAILS ARCHITECTURE GUARD

Create/update an architecture test so newly introduced endpoints cannot silently reintroduce the same violation.

The guard must be narrow and deterministic.

It must detect actual forbidden direct response patterns rather than legitimate framework usage.

False positives are unacceptable.

The architecture rule must document:

- what is forbidden
- what is allowed
- why
- any explicit exceptions

Do not weaken the rule merely to reduce the match count.

---

# 6. WAVE 3-B — SWAGGER POSITIVE-FLAG HARDENING

Current issue:

Swagger/OpenAPI is enabled implicitly in every non-Production environment, including Staging.

Change this to an explicit opt-in model.

Required semantics:

```text
Swagger/OpenAPI = OFF by default
```

Activation requires an explicit positive configuration flag.

Conceptually:

```text
Swagger:Enabled = false
```

or the repository's equivalent configuration convention.

Do NOT invent a second parallel configuration system.

Required behavior:

```text
Production:
OFF

Staging:
OFF unless explicitly enabled

Development:
OFF unless explicitly enabled

Local developer:
may explicitly enable it
```

Do not determine Swagger availability solely from:

```text
Environment != Production
```

Audit:

- Swagger endpoint
- Swagger UI
- OpenAPI JSON
- configuration binding
- CI/test environments
- container environment
- staging configuration
- documentation

Add tests proving:

1. default = OFF
2. explicit enable = ON
3. Production does not accidentally enable it
4. Staging does not accidentally enable it
5. no client can activate it remotely

Do not expose production secrets through OpenAPI configuration.

---

# 7. WAVE 3-C — X-CORRELATION-ID VALIDATION

Inspect the current correlation-ID implementation.

Determine:

- header name
- generation behavior
- propagation
- logging
- response header behavior
- tracing/telemetry integration

Implement or harden a single canonical policy.

Requirements:

1. If no correlation ID is supplied:

   - generate one server-side.

2. If a correlation ID is supplied:

   - validate it before use.

3. Validation must reject unsafe values such as:

   - CR/LF
   - control characters
   - excessive length
   - invalid character set according to repository convention

4. The value must not become a log-injection vector.

5. The correlation ID must not contain or encode:

   - secrets
   - authentication tokens
   - private coordinates
   - personal data

6. The server must not trust the client value for security authorization.

7. Response correlation ID must be safe and consistent.

Prefer an existing project-wide identifier format if one already exists.

Do not create multiple correlation-ID formats.

Add tests for:

- missing header
- valid header
- malformed header
- overlong header
- CR/LF injection
- control characters
- propagation
- response header
- logging safety

Choose the smallest behavior change compatible with existing API contracts.

---

# 8. WAVE 3-D — WARNING CLEANUP

There are approximately 16 remaining warnings.

Produce a warning inventory first.

For each warning classify:

```text
A = real code defect
B = obsolete code
C = nullability issue
D = analyzer/compiler warning
E = intentional and justified
F = generated/tooling warning
```

Resolve A/B/C/D where possible.

For F or E:

document the exact reason rather than suppressing it blindly.

Do NOT:

- add blanket warning suppression
- disable analyzers
- downgrade compiler settings
- perform global formatting
- make unrelated refactors

Target:

```text
0 unjustified warnings
```

The final report must list every warning that remains, if any.

---

# 9. W2-6 REMAINING — STAFF TOTP AUTHORIZATION ADR

This issue is now a real architecture boundary.

Current problem:

TOTP registration was previously available to all users because the endpoint was not actually restricted to staff.

The current fail-safe implementation returns:

```text
canManageStaffSecurity = false
```

This is safe but not complete.

The required architecture is:

AUTHENTICATION\
!=\
STAFF AUTHORIZATION

Do NOT make:

```text
Identity -> Organizations
```

a direct dependency merely to solve this.

First inspect existing application/domain boundaries.

Create the next appropriate ADR:

```text
Staff TOTP Management Authorization
```

The ADR must define:

- what "staff security management" means
- capability name
- who receives the capability
- how organization membership is resolved
- how tenant scope is enforced
- how org-less users are handled
- whether platform-level staff exist
- whether the capability applies to safeguarding/platform roles
- enrollment
- reset/recovery
- disable/remove
- audit requirements
- fail-safe behavior

Preferred architecture:

```text
Identity
   |
   | authenticated user ID
   v
Application authorization service
   |
   v
organization/membership/capability resolution
   |
   v
canManageStaffSecurity
```

Identity must remain an authentication boundary.

Do not make authentication method itself a source of staff privilege.

---

# 10. IMPLEMENT STAFF TOTP AUTHORIZATION

Only implement after documenting the ADR.

Replace the current unconditional fail-safe behavior with actual server-side capability resolution.

The client may continue to receive:

```text
canManageStaffSecurity
```

but this must be computed server-side.

The server must enforce authorization for the actual TOTP management endpoints.

Never rely only on UI hiding.

Required:

- registration/enrollment protected
- status protected where sensitive
- reset protected
- disable protected
- recovery paths protected
- audit events where appropriate

Do not expose TOTP secrets.

Do not log:

- secret
- OTP
- QR payload
- recovery code

Where QR provisioning is used, protect the enrollment endpoint itself.

---

# 11. STAFF TOTP TEST MATRIX

Add/update tests for:

### Positive

- authorized staff can manage staff TOTP
- capability is derived server-side
- same-tenant membership works when policy allows

### Negative

- ordinary user denied
- user from another organization denied
- unaffiliated user denied
- authentication method does not grant staff privilege
- client cannot submit `isStaff=true`
- client cannot submit `canManageStaffSecurity=true`
- client cannot select another user's identity
- staff capability cannot cross tenant boundaries

### Security

- TOTP secret never appears in logs
- OTP never appears in logs
- QR provisioning payload is not leaked
- unauthorized reset is denied
- unauthorized disable is denied

---

# 12. TRUST RECALCULATION — ADR REQUIRED

ADR-027 already ratified the existing trust ladder.

DO NOT redesign the ladder.

The remaining question is:

WHEN is the trust value recalculated?

Do NOT interpret this as:

"run trust calculation on every HTTP request in the entire application."

Recommended model:

```text
TRUST-CONSUMING OPERATION
        |
        v
TrustLevelCalculator
        |
        +--> verification facts
        +--> approved trust inputs
        +--> current user/account state
        |
        v
effective TrustLevel
```

Trust is authoritative when read/calculated.

Do NOT accept a client-submitted TrustLevel.

Do NOT persist a separate stale authoritative TrustLevel unless an approved ADR explicitly requires it.

The new ADR must define:

- calculation trigger
- authoritative inputs
- invalidation behavior
- verification revocation effect
- account deactivation effect
- safeguarding separation
- ratings separation
- organization-verification separation
- caching policy, if any
- performance expectations
- deterministic behavior
- audit implications

The default recommendation is:

```text
recalculate on each trust-consuming read/operation
```

not:

```text
recalculate on every HTTP request
```

---

# 13. IMPLEMENT TRUST RECALCULATION

Only implement behavior after the ADR establishes the semantics.

Ensure:

1. server calculates effective trust
2. client cannot submit authoritative trust
3. verification revocation changes effective trust immediately
4. deactivated accounts are handled correctly
5. safeguarding restrictions remain separate
6. ratings remain separate
7. organization verification does not silently become identity verification
8. no numeric trust score is introduced

Do not rewrite the approved trust ladder.

Add regression tests around the exact trust-consuming operations.

---

# 14. ID AUSTRIA PROVIDER AUDIT

Audit:

```text
IdAustriaVerificationProvider
```

Do NOT delete it immediately.

Search for all references across:

- source code
- DI registration
- service registration
- factories
- reflection
- configuration
- feature flags
- environment settings
- tests
- fixtures
- documentation
- API contracts
- startup initialization
- dependency injection
- package references

Classify the provider as:

```text
A = actively used
B = registered but inactive
C = referenced only by tests/docs
D = genuinely dead code
E = future architecture intentionally retained
```

If D:

remove the provider and its dead registrations/configuration/tests/docs.

Also remove unused dependencies introduced solely for it.

Do not delete unrelated verification infrastructure.

If E:

retain it and document exactly why it remains.

If B:

determine whether inactive retention is intentional.

Do not convert an inactive provider into active functionality.

---

# 15. ID AUSTRIA SAFETY RULE

Do NOT implement ID Austria integration in this phase.

Do NOT create:

- OAuth integration
- eIDAS integration
- government API calls
- real identity verification

This phase only determines whether the existing provider implementation is:

USED\
or\
DEAD\
or\
INTENTIONALLY RETAINED

---

# 16. SECURITY BOUNDARY SWEEP

After the above changes, perform a focused security audit around:

```text
Authorization
TOTP
Trust
Verification
ProblemDetails
Swagger
Correlation ID
IdAustria
Tenant scope
```

Verify:

- authentication ≠ authorization
- client cannot submit authoritative security state
- TOTP capability is server-derived
- TrustLevel is server-derived
- tenant scope is server-enforced
- error responses do not leak sensitive details
- Swagger is not implicitly enabled
- correlation IDs cannot inject log/control characters
- unused identity provider code does not create hidden attack surface

---

# 17. API CONTRACT AUDIT

For every changed endpoint inspect:

- HTTP method
- route
- request model
- response model
- authorization
- tenant scope
- error contract
- audit behavior

Especially inspect:

- staff TOTP endpoints
- `/me` security fields
- trust-related endpoints
- authorization failures
- Swagger configuration surface

Do not break the Flutter client without updating the client in this phase.

---

# 18. FLUTTER CLIENT BOUNDARY

Verify:

- Flutter never sends `isStaff` as authoritative state
- Flutter never sends `canManageStaffSecurity` as authoritative state
- Flutter never sends TrustLevel as authoritative state
- Flutter never sends VerificationLevel as authoritative state

The Flutter app may:

```text
READ
DISPLAY
RENDER
```

but the server must:

```text
DECIDE
AUTHORIZE
CALCULATE
```

Preserve de/en/fa localization.

Do not redesign unrelated UI.

---

# 19. TEST STRATEGY

Do NOT automatically rerun the entire historical suite.

Run focused tests after each affected change.

Required minimum:

### Backend

- ProblemDetails tests
- Swagger configuration tests
- correlation-ID tests
- staff TOTP authorization tests
- trust recalculation tests
- IdAustria registration/activation tests where relevant
- architecture guards

### Flutter

- relevant security/client-boundary tests

### Full suites

Run full backend/Flutter suites only after all code/configuration changes are complete if repository release policy requires them.

The existing baseline:

```text
Backend = 664 PASS / 0 FAIL
Flutter = 909 PASS / 0 FAIL
```

must be treated as historical evidence until actually rerun.

Do not fabricate current-phase test counts.

---

# 20. BUILD / QUALITY GATE

At phase end, when source/configuration changes are complete:

Run:

```text
dotnet build backend/SeniorConnect.slnx -c Release
dotnet test backend/SeniorConnect.slnx -c Release
flutter analyze
flutter test
```

Run localization validation where relevant.

Run architecture/security guards.

If PostgreSQL integration infrastructure is available and a changed area depends on PostgreSQL:

run the affected PostgreSQL tests.

Required:

```text
0 failures
```

Do not convert failures into skips.

---

# 21. WARNING GATE

Final warning state must be explicitly reported.

Use:

```text
Warnings = 0
```

only if actually verified.

Otherwise:

```text
Warnings = N
```

and list each remaining warning with its classification and reason.

Do not hide warnings by disabling analyzers.

---

# 22. DOCUMENTATION

Create/update only the documents required by this phase.

Required:

- the new TOTP authorization ADR
- the new Trust recalculation ADR
- existing security/architecture docs where needed
- BUILD-CHECKLIST.md
- approved policy implementation report if behavior changed

For each ADR record:

- problem
- decision
- alternatives considered
- chosen architecture
- security implications
- compatibility implications
- tests
- implementation status
- remaining external constraints

Do not create duplicate policy documents.

---

# 23. APPROVED POLICY PRESERVATION

The following remain fixed:

```text
ADR-027 = Option A
S-2 = Option B
S-3 = Option B
S-4 = Option B
B-1 = Option B
B-2 = Option A
ADR-026 = Option C
```

Do NOT reinterpret these decisions.

In particular:

- Trust ladder remains the approved ADR-027 ladder.
- S-2 remains content-free.
- S-3 remains neutral.
- S-4 remains platform-officer scoped.
- B-1 remains automatic unassignment.
- B-2 remains officer-only manual conversion.
- ADR-026 remains implemented but OFF by default.

---

# 24. ADR-026 SAFETY CHECK

Verify:

```text
VisitAddress:RequireGrant = false
```

Check:

- default configuration
- test configuration
- development configuration
- staging configuration
- production configuration
- Flutter activation path

The phase MUST end with:

```text
ADR-026 = OFF
```

---

# 25. GIT HYGIENE

Before changes:

```text
git status --short
git diff --stat
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
A = security/architecture fix
B = tests/architecture guards
C = required documentation
D = unrelated
```

No D changes may be introduced.

Do not commit automatically.

---

# 26. DEFINITION OF DONE

This phase is COMPLETE only if:

1. ProblemDetails violations are identified and all actual violations are fixed.
2. Architecture guard prevents recurrence without false positives.
3. Swagger is explicitly opt-in.
4. Staging does not implicitly expose Swagger.
5. Correlation-ID is validated safely.
6. Remaining warnings are resolved or explicitly justified.
7. Staff TOTP authorization has an approved ADR.
8. Staff TOTP management is server-authorized by actual capability.
9. Authentication method no longer implies staff privilege.
10. TOTP negative tests pass.
11. Trust recalculation semantics have an approved ADR.
12. Trust is calculated server-side at trust-consuming boundaries.
13. Client cannot forge TrustLevel.
14. ID Austria provider usage is definitively classified.
15. Dead ID Austria code is removed only if proven dead.
16. No approved policy decision was changed.
17. ADR-026 remains OFF.
18. Backend build passes.
19. Backend tests pass.
20. Flutter analyze passes.
21. Flutter tests pass.
22. Architecture/security tests pass.
23. No unrelated files are modified.
24. No automatic commit occurs.

---

# 27. REQUIRED FINAL REPORT

Return exactly:

PHASE STATUS

W2-5 Regression = PASS/FAIL\
W2-6 Regression = PASS/FAIL

ProblemDetails Audit = PASS/FAIL\
ProblemDetails Architecture Guard = PASS/FAIL\
Swagger Opt-In = PASS/FAIL\
Correlation-ID Validation = PASS/FAIL\
Warning Cleanup = PASS/FAIL

Staff TOTP ADR = APPROVED/PENDING\
Staff TOTP Authorization = PASS/FAIL/BLOCKED\
Staff TOTP Security Tests = PASS/FAIL

Trust Recalculation ADR = APPROVED/PENDING\
Trust Recalculation = PASS/FAIL/BLOCKED\
Trust Client Boundary = PASS/FAIL

IdAustria Provider Classification = ACTIVE/INACTIVE/DEAD/RETAINED\
IdAustria Cleanup = PASS/NOT-REQUIRED/BLOCKED

Tenant Isolation = PASS/FAIL\
API Security Boundary = PASS/FAIL\
Flutter Boundary = PASS/FAIL

Backend Build = PASS/FAIL\
Backend Tests = PASS/FAIL\
Flutter Analyze = PASS/FAIL\
Flutter Tests = PASS/FAIL\
Architecture Tests = PASS/FAIL\
Security Tests = PASS/FAIL\
Localization = PASS/FAIL/NOT-RUN\
Warnings = 0/N

ADR-026 = OFF\
Approved Policy Baseline = PASS/FAIL\
Working Tree Hygiene = PASS/FAIL

Final Result =

WAVE 3 COMPLETE — SECURITY / QUALITY / ARCHITECTURE CLOSED

or

BLOCKED — WAVE 3 NOT CLOSED

Then provide:

## 1. WAVE 3 FINDINGS

## 2. PROBLEMDETAILS

## 3. SWAGGER

## 4. CORRELATION-ID

## 5. WARNINGS

## 6. STAFF TOTP

ADR:\
Authorization model:\
Files changed:\
Tests:

## 7. TRUST

ADR:\
Calculation boundary:\
Files changed:\
Tests:

## 8. ID AUSTRIA

Provider:\
Usage classification:\
Action taken:\
References removed/retained:

## 9. SECURITY VALIDATION

Only actual tests executed in this phase.

## 10. DOCUMENTS CHANGED

## 11. FILES CHANGED

## 12. REMAINING BLOCKERS

Only genuine blockers.

## 13. NEXT PHASE RECOMMENDATION

If this phase is fully green, recommend moving to:

RELEASE CANDIDATE / REPOSITORY INTEGRITY / CANONICAL RELEASE BASELINE

Do NOT begin another feature-development wave unless a real blocker is discovered.

---

# 28. HARD STOP

STOP after:

- Wave 3 fixes
- TOTP ADR and implementation
- Trust ADR and implementation
- IdAustria audit/cleanup
- focused security validation
- final regression
- documentation
- final report

DO NOT:

- add product features
- implement ID Austria
- activate ADR-026
- enable VisitAddress
- add AI matching
- add payments
- add voice/IVR
- add corporate volunteering
- perform Git history rewrite
- commit automatically
- push automatically
- deploy production
- launch Early Access

The purpose of this phase is:

WAVE 3 SECURITY CLOSURE\
+\
TOTP AUTHORIZATION BOUNDARY\
+\
TRUST RECALCULATION DECISION\
+\
IDAUSTRIA CODE AUDIT

NOT:

FEATURE DEVELOPMENT

NOT:

PRODUCTION DEPLOYMENT

NOT:

EARLY ACCESS LAUNCH
