# SENIORCONNECT — NEXT PHASE

# External Gate Evidence Intake, Production Provisioning Validation & Controlled Deployment

## PURPOSE

The SeniorConnect engineering/release baseline is already VERIFIED.

Current application release source:

`9ccb5b66021a29c02ddeced4d9ae74d969c476ab`

Current repository HEAD may be a later documentation commit.

Do NOT confuse:

RELEASE SOURCE SHA

with:

CURRENT REPOSITORY HEAD

The previous phase established:

- canonical repository = verified
- release documentation = tracked
- source drift = none
- CI exact-SHA = PASS
- backend = PASS
- PostgreSQL engineering validation = PASS
- architecture = PASS
- security = PASS
- Flutter = PASS
- Docker = PASS
- migration traceability = PASS
- ADR-026 = OFF
- Early Access = NOT STARTED

The previous phase stopped correctly because the real external production environment and associated operational evidence did not yet exist.

This phase therefore has a different purpose:

EXTERNAL EVIDENCE INTAKE\
+\
PRODUCTION PROVISIONING VALIDATION\
+\
DEPLOYMENT VALIDATION\
+\
FINAL OWNER HANDOFF

This is NOT a feature-development phase.

It is NOT a repository-cleanup phase.

It is NOT another historical Git phase.

It is NOT an Early Access launch phase.

---

# 0. HARD RULES

1. Do NOT add product features.

2. Do NOT refactor unrelated code.

3. Do NOT reopen completed product decisions.

4. Do NOT modify the approved policy baseline:

   ADR-027 = A\
   S-2 = B\
   S-3 = B\
   S-4 = B\
   B-1 = B\
   B-2 = A\
   ADR-026 = C

5. `VisitAddress:RequireGrant` MUST remain `false` unless explicit authorized external activation evidence exists.

6. Do NOT activate ADR-026 automatically.

7. Do NOT start Early Access.

8. Do NOT contact participants.

9. Do NOT contact organizations.

10. Do NOT onboard a real second organization.

11. Do NOT use real participant data for testing.

12. Do NOT invent infrastructure.

13. Do NOT invent a hosting vendor.

14. Do NOT invent an authentication/email provider.

15. Do NOT invent a database.

16. Do NOT invent DNS or TLS.

17. Do NOT invent backup infrastructure.

18. Do NOT invent monitoring.

19. Do NOT invent operational owners.

20. Do NOT invent legal approval.

21. Do NOT claim GDPR compliance.

22. Do NOT claim lawyer approval.

23. Do NOT claim DPIA approval.

24. Do NOT claim insurance or contractual approval.

25. Do NOT fabricate external evidence.

26. PASS requires authoritative evidence.

27. Missing external evidence remains PENDING or BLOCKED.

28. Do NOT print secrets, passwords, tokens, API keys, private keys or connection strings.

29. Never include complete credential values in reports.

30. Do NOT rotate credentials automatically unless an explicitly authorized deployment mechanism is already provided.

31. Do NOT rewrite Git history.

32. Do NOT force-push.

33. Do NOT delete the old repository.

34. Do NOT create another Git repository.

35. Do NOT create a release tag automatically.

36. Do NOT commit automatically.

37. Do NOT perform simulated production deployment and label it production.

38. Do NOT treat CI/test PostgreSQL as production PostgreSQL.

39. Do NOT treat a synthetic backup drill as a real operational backup.

40. Do NOT treat health endpoints as proof of observability.

41. Do NOT treat repository code as proof of legal or organizational readiness.

42. If no new external evidence exists, do not repeat the entire previous gate analysis. Reconcile only the state and STOP.

43. If a real environment exists, use only the actual authorized environment.

44. Human deployment-owner authorization is required before the actual production deployment step.

---

# 1. ENTRY STATE

Begin by verifying:

```text
git status --short
git rev-parse HEAD
git branch --show-current
git remote -v
git fetch --prune
git ls-remote --heads SeniorConnect main
git log -5 --oneline --decorate
git diff --check
```

Determine:

- current repository HEAD
- remote HEAD
- clean/dirty state
- application release SHA
- whether current source differs from `9ccb5b6`
- whether only documentation commits exist after the application release

The expected application release remains:

`9ccb5b66021a29c02ddeced4d9ae74d969c476ab`

unless a new release process has explicitly superseded it.

Do not silently redefine the release SHA.

---

# 2. CHECK WHETHER NEW EXTERNAL EVIDENCE EXISTS

Before doing deployment work, inspect whether any of the previously pending external gates now have actual evidence.

Evidence may include:

- credential rotation confirmation
- revoked old credential confirmation
- real SMTP/provider configuration
- real deployment host
- real container runtime
- real secret store
- real registry/image
- real domain
- real TLS endpoint
- real PostgreSQL
- actual backup target
- restore target
- monitoring/alerting
- appointed operational roles
- appointed safeguarding officer
- external Data Protection / Legal review
- owner deployment authorization

Do not infer evidence from:

- repository files alone
- documentation alone
- local Docker
- CI PostgreSQL
- synthetic tests
- local backup scripts
- health endpoints

If NO material external evidence exists:

update/reconcile the relevant readiness documentation only if necessary and return:

`NO NEW EXTERNAL EVIDENCE — PRODUCTION REMAINS BLOCKED`

Then HARD STOP.

Do not repeat large validation suites.

---

# 3. CREDENTIAL SECURITY GATE

Inspect:

`docs/security/credential-exposure-remediation.md`

Determine whether the historically exposed credentials have actually been:

- revoked
- rotated
- replaced
- verified unusable

Accept only authoritative evidence.

Never print values.

If no evidence exists:

`Credential Rotation = PENDING`

If verified:

`Credential Rotation = PASS`

Preserve:

- historical exposure
- current repository remediation
- history rewrite status

Do not rewrite Git history automatically.

---

# 4. AUTHENTICATION / EMAIL PROVIDER

Determine whether a REAL provider is now provisioned.

Required evidence:

- provider identity
- sender identity
- production configuration
- secret injection
- TLS
- timeout
- retry/failure behavior
- rate limiting
- ownership

If unavailable:

`Authentication Provider = PENDING`

If available, use synthetic accounts and validate:

```text
register
→ issue OTP/magic-link
→ provider delivery
→ authenticate
→ expired token rejected
→ replay rejected
→ invalid token rejected
→ transient provider failure handled safely
```

Verify:

- OTP never logged
- token never logged
- provider credential never logged

Do not send messages to real participants.

---

# 5. PRODUCTION INFRASTRUCTURE

Determine whether an actual authorized deployment environment exists.

Required evidence:

- host/platform
- runtime
- container deployment capability
- secret store
- network path
- deployment access
- registry/image source
- health endpoints
- logging
- rollback path

If absent:

`Production Infrastructure = BLOCKED`

Do not construct a fake production environment.

---

# 6. ARTIFACT REGISTRY

Verify whether the release artifact is actually retained in a registry.

Current historical CI behavior used:

`push:false`

Therefore prior Docker PASS does NOT prove publication.

A valid artifact requires:

- registry
- repository/image name
- immutable digest or equivalent
- source SHA mapping
- retention
- access control

If no registry exists:

`Artifact Publication = PENDING`

If available:

verify that the artifact corresponds to application release SHA:

`9ccb5b66021a29c02ddeced4d9ae74d969c476ab`

Do not silently rebuild from a different source revision and call it the same release.

---

# 7. DOMAIN / TLS

Only if an actual environment exists:

verify:

- DNS
- expected hostname
- HTTPS
- certificate validity
- certificate chain
- reverse proxy routing
- redirect behavior
- CORS
- HSTS where configured

The repository's configured production origin is:

`https://app.seniorconnect.at`

Do not treat that string as proof DNS/TLS exists.

If unavailable:

`Domain/TLS = PENDING`

---

# 8. PRODUCTION POSTGRESQL

Only if a real production database exists:

verify:

- actual production environment
- server/database identity
- PostgreSQL version
- connection security
- secret injection
- migration state
- schema state
- access control
- application connectivity

Do NOT treat:

- CI PostgreSQL
- local PostgreSQL
- disposable Testcontainers

as production.

If unavailable:

`PostgreSQL Deployment = PENDING`

---

# 9. PRODUCTION MIGRATION

Current migration head:

`20261001150947_AddPlatformSafeguardingOfficerRoleCheck`

Current migration count:

`19`

If a real production database exists:

verify:

```text
backup
→ migration
→ startup
→ health
→ smoke validation
```

Record:

- before version
- after version
- migration result
- timestamp
- environment

Do not create a new migration unless a real deployment defect requires one.

Do not modify historical migrations.

---

# 10. BACKUP

Determine whether the actual operational backup target exists.

Required evidence:

- target
- schedule
- retention
- encryption
- access control
- monitoring
- owner

If absent:

`Backup = BLOCKED`

If present:

perform a safe synthetic backup using non-production data where authorized.

Record evidence without exposing data.

A repository backup script alone is NOT operational PASS.

---

# 11. RESTORE

If an authorized restore target exists:

perform:

```text
backup
→ restore
→ schema verification
→ migration compatibility
→ application startup
→ health
→ synthetic smoke
```

Verify critical structures exist, including:

- organizations
- memberships
- HelpRequests
- assignments
- audit entries
- safeguarding cases
- safeguarding access logs
- operational tasks
- visit-address grant records

Do not expose row contents.

If no target exists:

`Restore = PENDING`

---

# 12. OBSERVABILITY

Determine whether the actual deployment has operational monitoring.

At minimum verify detection of:

- application outage
- database outage
- authentication failure
- email/provider failure
- migration failure
- safeguarding workflow failure
- deployment failure

Verify:

- alert destination
- alert routing
- owner
- evidence of alert delivery where safely possible

Application logging alone is insufficient.

Health endpoints alone are insufficient.

If absent:

`Observability = BLOCKED`

---

# 13. OPERATIONAL OWNERSHIP

Verify actual appointments for:

```text
Platform Owner
Deployment Owner
Safeguarding Lead
Platform Safeguarding Officer
Operational Coordinator
Support Contact
Incident Escalation Contact
Backup Owner
```

A role definition in documentation is NOT an appointment.

For every role:

- appointed/not appointed
- evidence source
- acknowledgement where available
- backup

Never invent names.

---

# 14. SAFEGUARDING OPERATIONAL GATE

Verify actual ownership for:

- S-2 OperationalTask
- S-3 OperationalHold
- S-4 PlatformSafeguardingOfficer
- B-1 automatic unassignment
- B-2 officer-only report conversion

Confirm the operating team knows the supported workflow.

Do not introduce new safeguarding behavior.

If no real safeguarding owner exists:

`Safeguarding Operational Ownership = PENDING`

---

# 15. DATA PROTECTION / LEGAL

Read:

`docs/legal/EXTERNAL-DATA-PROTECTION-REVIEW.md`

Do not perform legal analysis.

Only record authoritative external evidence for:

- controller/processor structure
- purposes
- legal bases
- transparency
- retention
- deletion
- rights handling
- processor arrangements where applicable
- transfer considerations where applicable
- DPIA assessment where applicable
- ADR-026 review

No evidence:

`Data Protection / Legal = PENDING`

Do not write a legal conclusion.

---

# 16. ADR-026

Default remains:

`VisitAddress:RequireGrant = false`

Verify:

- repository configuration
- deployment environment
- environment variables
- startup configuration
- client cannot enable it
- ordinary admins cannot enable it

Only report:

`READY-FOR-AUTHORIZED-ACTIVATION`

when:

1. engineering remains verified
2. legal/data-protection authorization exists
3. deployment owner is identified
4. explicit activation authority exists

Even then:

DO NOT activate automatically.

---

# 17. RELEASE TRACEABILITY

Maintain the separation:

```text
Application Release SHA = 9ccb5b6...
Repository HEAD = current repository HEAD
Artifact = actual retained artifact
Deployment = actual environment + timestamp
```

Never claim that a documentation commit is a new application build.

If a new application source revision is introduced:

a new release validation process is required.

---

# 18. PRODUCTION DEPLOYMENT AUTHORIZATION

Do NOT deploy simply because all technical gates happen to pass.

Before deployment verify:

- authorized deployment environment
- authorized deployment owner
- artifact exists
- migration plan exists
- backup exists
- rollback exists
- observability exists
- authentication exists
- legal/data-protection gate is addressed as required
- safeguarding ownership exists
- explicit human deployment authorization exists

Without explicit human authorization:

`Deployment = NOT AUTHORIZED`

Stop.

---

# 19. DEPLOY EXACT RELEASE

Only after authorization:

deploy the exact release artifact corresponding to the approved application release SHA.

Record:

- environment
- artifact digest
- release SHA
- deployment timestamp
- migration state
- deployment result

No feature changes during deployment.

---

# 20. PRODUCTION SMOKE TEST

Only against the real authorized environment.

Run controlled synthetic checks:

```text
health
→ authentication
→ authorization
→ tenant isolation
→ synthetic user
→ synthetic organization
→ discoverability
→ help request
→ matching
→ proposal
→ assignment
→ block
→ B-1 validation
→ report
→ B-2 validation
→ S-2
→ S-3
→ S-4
→ audit
→ cleanup
```

Verify:

- no cross-tenant leakage
- no exact unauthorized coordinates
- no address leakage
- no safeguarding leakage
- no secret logging
- ADR-026 remains OFF

Do not use real participant data.

---

# 21. ROLLBACK VALIDATION

If deployment is performed:

verify:

- application rollback path
- artifact rollback
- configuration rollback
- database recovery strategy
- migration failure handling
- service shutdown
- service restart
- observability during rollback

Do not pretend a documented procedure has been operationally tested if it was not.

---

# 22. EARLY ACCESS

Remain:

`Early Access = NOT STARTED`

Even after successful production deployment.

Do NOT:

- recruit
- contact participants
- contact organizations
- announce launch
- onboard real users

Early Access requires a separate explicit owner decision.

---

# 23. DOCUMENTATION

Update only documents affected by newly verified evidence.

At minimum reconcile:

```text
docs/launch/PRODUCTION-ACTIVATION-GATE.md
docs/launch/PRODUCTION-ACTIVATION-READINESS-REPORT.md
docs/launch/PRODUCTION-DEPLOYMENT-VALIDATION-REPORT.md
docs/launch/FINAL-ENGINEERING-HANDOFF.md
docs/operations/production-deployment-checklist.md
docs/operations/backup-restore-requirements.md
docs/operations/operational-ownership-checklist.md
docs/security/credential-exposure-remediation.md
docs/legal/EXTERNAL-DATA-PROTECTION-REVIEW.md
```

Do not create another release-document family.

Clearly label:

```text
REPOSITORY EVIDENCE
EXTERNAL EVIDENCE
HUMAN AUTHORIZATION
DEPLOYMENT EVIDENCE
NOT VERIFIED
PENDING
BLOCKED
```

---

# 24. GATE STATUS VOCABULARY

Use only:

```text
PASS
PENDING
BLOCKED
NOT STARTED
NOT AUTHORIZED
```

Do not use:

- almost ready
- probably ready
- looks good
- effectively ready
- assumed ready

---

# 25. ACCEPTANCE CRITERIA

This phase is successful only when the actual state is accurately established.

For every production gate:

- status exists
- evidence exists or explicit lack of evidence is recorded
- owner exists or is explicitly pending
- next action exists
- verification date exists

Production Deployment may be PASS only when:

1. real environment exists
2. real artifact exists
3. real PostgreSQL exists
4. migration completed
5. backup capability exists
6. restore capability is validated as required
7. authentication works
8. TLS works
9. observability works
10. operational ownership exists
11. safeguarding ownership exists
12. required legal/data-protection review is evidenced
13. deployment owner is authorized
14. smoke validation passes
15. rollback capability is acceptable

If any required condition is absent:

Do NOT deploy.

---

# 26. FINAL REPORT

Return exactly:

```text
PHASE STATUS

New External Evidence = YES/NO
Repository Integrity = PASS/FAIL
Application Release SHA = 9ccb5b6...
Source Drift = NONE/DETECTED

Credential Rotation = PASS/PENDING/BLOCKED
Authentication Provider = PASS/PENDING/BLOCKED
Production Infrastructure = PASS/PENDING/BLOCKED
Artifact Publication = PASS/PENDING/BLOCKED
Domain/TLS = PASS/PENDING/BLOCKED
PostgreSQL = PASS/PENDING/BLOCKED
Migration Deployment = PASS/PENDING/BLOCKED
Backup = PASS/PENDING/BLOCKED
Restore = PASS/PENDING/BLOCKED
Observability = PASS/PENDING/BLOCKED
Operational Ownership = PASS/PENDING
Safeguarding Ownership = PASS/PENDING
Data Protection / Legal = PASS/PENDING/BLOCKED
ADR-026 = OFF/READY-FOR-AUTHORIZED-ACTIVATION
Deployment Authorization = AUTHORIZED/NOT AUTHORIZED
Production Deployment = PASS/NOT STARTED/BLOCKED
Production Smoke Test = PASS/PENDING/BLOCKED
Rollback = PASS/PENDING/BLOCKED
Early Access = NOT STARTED

FINAL RESULT =
```

The final result MUST be exactly one of:

`NO NEW EXTERNAL EVIDENCE — PRODUCTION REMAINS BLOCKED`

`EXTERNAL GATES PARTIALLY VERIFIED — PRODUCTION REMAINS BLOCKED`

`PRODUCTION READY FOR EXPLICIT OWNER AUTHORIZATION`

`PRODUCTION DEPLOYMENT VERIFIED`

---

# 27. HARD STOP

After the final report:

STOP.

Do NOT:

- add product features
- start another engineering phase
- launch Early Access
- contact participants
- contact organizations
- onboard a second organization
- activate ADR-026 automatically
- rewrite Git history
- force-push
- create another repository
- invent external infrastructure
- invent legal approval
- invent operational owners
- fabricate production evidence

The purpose of this phase is:

EXTERNAL EVIDENCE INTAKE\
+\
PRODUCTION PROVISIONING VALIDATION\
+\
CONTROLLED DEPLOYMENT\
+\
FINAL OWNER HANDOFF

NOT:

FEATURE DEVELOPMENT

NOT:

AUTOMATIC EARLY ACCESS LAUNCH
