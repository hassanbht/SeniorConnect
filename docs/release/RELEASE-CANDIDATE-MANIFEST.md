# Release Candidate Manifest

> Actual values re-verified 2026-10-07 during the Final Canonical Release
> Closure & Production External Gate Validation phase (RDOCS-1 closed).
> `PENDING` / `NOT DEFINED` mark unavailable values honestly. Every value
> below comes from an executed git/CI/build command.

```text
Repository:            D:\Project\SeniorConnect (canonical working tree)
Canonical status:      ESTABLISHED — remote `SeniorConnect` →
                       https://github.com/hassanbht/SeniorConnect.git
                       (private); remote refs/heads/main == local HEAD
Branch:                main
Release SHA:           9ccb5b66021a29c02ddeced4d9ae74d969c476ab
Commit date:           2026-10-07T20:20:23+02:00 (hassanbehtooei)
                       subject: "Complete RDOCS-1: track docs/release/
                       release documents via narrow gitignore negation"
Lineage:               d0201b5 init → 8dbdffb clean-room → 8bf2324 Wave 3 →
                       b346a1d track documentation → 9ccb5b6 RDOCS-1 complete
Working tree:          CLEAN at verification (post-documentation-re-anchor
                       modifications to docs/release/* + docs/launch/* are
                       CLASSIFIED — see REPOSITORY-INTEGRITY-REPORT.md)
RDOCS-1:               CLOSED — .gitignore `docs/` rule removed (b346a1d) +
                       narrow `!docs/release/` negation vs `[Rr]elease/`
                       template rule (9ccb5b6); 114 docs files tracked;
                       .env / build output / local AI state still ignored
Application version:   1.0.0+1 (mobile pubspec; backend sets no <Version>)
Backend target framework: net10.0 (TreatWarningsAsErrors)
.NET SDK:              10.0.401 (executed)
Flutter SDK:           3.47.6 stable / Dart SDK 3.13.5 (executed)
Dependency lock state: no packages.lock.json/global.json (integrity finding
                       R-3 OPEN, owner decision); restore verified OK locally
                       and in CI on the release SHA
Migration head:        20261001150947_AddPlatformSafeguardingOfficerRoleCheck
Migration count:       19 (unchanged — documentation work did not touch it)
CI status:             PASS — SeniorConnect CI Pipeline run 37666080520,
                       head_sha == 9ccb5b6… (== Release SHA), conclusion
                       success, 3/3 jobs, started 2026-10-07T18:20:34Z
                       (prior doc commit b346a1d also green: run 37664705350)
Backend tests (CI):    711 passed / 0 failed / 0 skipped (8 assemblies,
                       PostgreSQL 16 service + SC_TEST_POSTGRES)
Architecture status:   PASS — 87/87 within the same exact-SHA run
Security status:       PASS — security suites green + dedicated scan step on
                       exact SHA; full tracked-tree secret scan NONE
Flutter status:        PASS — analyze "No issues found"; 909/909 (exact SHA)
Localization status:   PASS — check_locales.py (de/en/fa) on exact SHA
Dependency vuln status: PASS — P7-11 step on exact SHA (23 projects, none
                       vulnerable)
Docker status:         PASS — container image built in CI (push:false, not
                       published)
Build:                 dotnet restore OK + dotnet build -c Release from clean
                       HEAD → 0 warnings / 0 errors (2026-10-07T18:22:27Z)
Artifact status:       local Release DLL SHA-256
                       93D32267B0101854179939A86C0C9DE0BE83494E122E15B1A66AF3BB4A57A461
                       built from Release SHA (source == HEAD); NOT published
                       (no registry, no deployment) — external gate
ADR-026:               OFF — committed VisitAddress:RequireGrant=false only;
                       no Production override; guard green on exact SHA
Approved policy baseline: CONSISTENT (ADR-027/S-2/S-3/S-4/B-1/B-2/ADR-026;
                       implementation + tests unchanged and green)
```

## Release Revision

```text
Release Revision = 9ccb5b66021a29c02ddeced4d9ae74d969c476ab   (VERIFIED)
```

Verified via: `git rev-parse HEAD` == `git ls-remote SeniorConnect
refs/heads/main` == CI run 37666080520 `head_sha`. Application source is
byte-identical to the previously validated candidate (8bf2324..9ccb5b6 diff
over backend/mobile/tests/.github/database/scripts/Dockerfile = EMPTY;
commits b346a1d and 9ccb5b6 changed only `.gitignore`, `docs/**` and
`prompts/**`), and CI independently re-executed the full suite on this exact
SHA — no reliance on historical results.
