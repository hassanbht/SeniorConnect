# Release Candidate Manifest

> Actual values re-verified 2026-10-07 during the Canonical Repository Cutover
> Verification phase. `PENDING` / `NOT DEFINED` mark unavailable values
> honestly. Every value below comes from a executed git/CI/build command.

```text
Repository:            D:\Project\SeniorConnect (canonical working tree)
Canonical status:      ESTABLISHED — remote `SeniorConnect` →
                       https://github.com/hassanbht/SeniorConnect.git
                       (private: anonymous GitHub API returns 404 while
                       authenticated ls-remote succeeds); remote
                       refs/heads/main == local HEAD
Branch:                main
HEAD:                  8bf2324b3dea26dec72111567a2d7b35f83bb543
Commit date:           2026-10-07T19:21:09+02:00 (hassanbehtooei)
Working tree:          CLEAN — `git status --porcelain` empty (no modified,
                       no untracked). Ignored local state classified:
                       docs/, Document/, archive/, backend/.env, bin/, obj/,
                       .serena/project.local.yml, *.user (F/G/H classes —
                       EXCEPT docs/ which is release-class D and NOT TRACKED,
                       see REPOSITORY-INTEGRITY-REPORT.md finding RDOCS-1)
Application version:   1.0.0+1 (mobile/senior_connect/pubspec.yaml; backend
                       assembly sets no <Version>)
Backend target framework: net10.0 (Directory.Build.props, TreatWarningsAsErrors)
.NET SDK:              10.0.401 (executed)
Flutter SDK:           3.47.6 stable (executed; no FVM pin in repo)
Dart SDK:              3.13.5 (executed)
Dependency state:      `dotnet restore` OK; `flutter pub get` OK; CI
                       "Security & Dependency Vulnerability Scan (P7-11)"
                       executed on the release SHA: no vulnerable packages
Migration head:        20261001150947_AddPlatformSafeguardingOfficerRoleCheck
                       (19 migration classes — committed in release SHA)
Build configuration:   Release
PostgreSQL result:     PASS — CI run 37658411034 (postgres:16 service +
                       SC_TEST_POSTGRES) on release SHA: 0 failures, 0 skips
Architecture result:   PASS — 87/87 (SeniorConnect.ArchitectureTests executed
                       in the same CI run; local static count 49 [Fact] +
                       38 [InlineData] = 87)
Security result:       PASS — security suites green in CI run 37658411034
                       (EndpointSecurity, TenantIsolation, Phase5PolicySweep,
                       Safeguarding, Funder, StaffTotpAuthorization,
                       ReactivationAudit, …) plus the dedicated scan step
Flutter result:        analyze clean (CI on release SHA + local re-check
                       "No issues found"); tests 909/909 (CI on release SHA)
Localization result:   PASS — CI "Check Translation Locales (de/en/fa)" step
                       succeeded on the release SHA
Warnings:              0 (Release build, TreatWarningsAsErrors; local rebuild
                       from release SHA: 0 warnings / 0 errors)
ADR-026:               OFF — committed VisitAddress:RequireGrant=false (the
                       only committed definition); no override in
                       appsettings.Production.json; no CI/compose activation;
                       architecture guard Phase5PolicySweepTests green in CI
Approved policy baseline: CONSISTENT (ADR-027 / S-2 / S-3 / S-4 / B-1 / B-2 /
                       ADR-026 = Options as approved; implementation and
                       tests committed in the release SHA)
CI workflow:           SeniorConnect CI Pipeline (.github/workflows/ci.yml)
CI run:                37658411034 (event push, branch main)
CI commit SHA:         8bf2324b3dea26dec72111567a2d7b35f83bb543  (== HEAD)
CI conclusion:         success (2026-10-07T17:21:21Z; all 3 jobs success)
```

## Release Revision

```text
Release Revision = 8bf2324b3dea26dec72111567a2d7b35f83bb543   (VERIFIED)
```

Verified via: `git rev-parse HEAD` == `git ls-remote SeniorConnect refs/heads/main`
== CI run 37658411034 `head_sha`.
