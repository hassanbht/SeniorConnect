# Release & Rollback Runbook

> **Status:** 2026-10-01 (deployment-integration phase).
> Built strictly from capabilities that exist in this repository. Where an
> operational capability does not exist, it is marked
> **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT**. No commands are
> invented; every command below is taken from this repository's scripts,
> compose file or CI workflow.

> **Update 2026-10-07 (Controlled Production Deployment phase): ROLLBACK
> READINESS = PENDING — EXTERNAL.** Nothing has ever been deployed, so no
> deployment exists to roll back and no rollback has been exercised against a
> real environment. Facts of record: application release revision =
> `9ccb5b66021a29c02ddeced4d9ae74d969c476ab` (previous known-good source
> ancestor `8bf2324`); mechanism-level capabilities below (redeploy previous
> build, `.env` rollback, feature-gate reset, `scripts/restore_db.sh`) are
> implemented but unproven in production; versioned artifacts/registry
> remain EXTERNAL. Decision authority: [Deployment Owner] — not appointed.

## 1. Capabilities inventory

| Capability | State | Source |
| --- | --- | --- |
| Containerized application build | Implemented | `backend/Dockerfile` (built in CI `container-checks`, `push: false`) |
| Composed deployment (api + postgres) | Implemented | `docker-compose.yml` (health-gated startup, `restart: unless-stopped`) |
| Configuration via environment | Implemented | `.env` for compose; `JWT_SECRET_KEY` **required** (`:?`), DB password, `EMAIL_*`, `Geocoding__ApiKey` |
| Database migration on startup | Implemented | `Program.cs` → `db.Database.MigrateAsync()` at boot |
| Health endpoints | Implemented | `/healthz/live` (liveness), `/healthz/ready` (incl. database check), `/health` (detailed) |
| Container health check | Implemented | compose `healthcheck` → `dotnet SeniorConnect.Api.dll --healthcheck` |
| Smoke test | Implemented | `scripts/verify_deployment.sh` (probes + locale check) + §4 below |
| Rollback of application | Implemented (mechanism) / **registry external** | Redeploy previous image or previous build — see §5 |
| Configuration rollback | Implemented | Restore previous `.env` values + restart |
| Feature-gate rollback | Implemented | `VisitAddress:RequireGrant` stays `false`; reset config if ever changed |
| Database restore | Implemented (tooling) | `scripts/restore_db.sh` + checksums from `scripts/backup_db.sh` |
| Image registry / versioned artifacts | **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT** | CI builds images without pushing; the deployment owner provides storage & tagging |
| Zero-downtime / blue-green | **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT** | compose model = single instance restart |
| Monitoring & alerting platform | **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT** | only container healthchecks exist |
| Backup scheduling & alerting | **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT** | see `docs/operations/backup-restore-requirements.md` |

## 2. Release procedure

1. **Preflight gates (all must be green):**
   ```bash
   dotnet build backend/SeniorConnect.slnx -c Release
   dotnet test backend/SeniorConnect.slnx -c Release      # with SC_TEST_POSTGRES (CI does this)
   (cd mobile/senior_connect && flutter analyze && flutter test)
   python scripts/check_locales.py
   ```
2. **Build the release artifact:**
   ```bash
   docker build -t seniorconnect-api:<version> ./backend
   ```
   (version = git commit SHA; storing it in an external registry is §1
   NOT IMPLEMENTED / EXTERNAL.)
3. **Configuration check:** confirm the deployment `.env` (never the
   repository) supplies `JWT_SECRET_KEY` (≥ 32 chars, not the dev key),
   DB credentials, and — when a provider is contracted — `EMAIL_*`. Confirm
   `VisitAddress:RequireGrant` is **not** set (gate OFF).
4. **Deploy:**
   ```bash
   docker compose up -d
   ```
   Migrations apply automatically at API startup; `depends_on` waits for the
   database healthcheck.
5. **Smoke test (§4).**
6. **Observe** container health (`docker compose ps`) and application logs
   for startup/configuration errors (an invalid email/JWT configuration fails
   startup explicitly by design).

## 3. Health check meanings

| Probe | Meaning |
| --- | --- |
| `GET /healthz/live` | process is up |
| `GET /healthz/ready` | database reachable (readiness gate; includes DB health check) |
| `GET /health` | detailed health entry output |

## 4. Smoke test procedure (minimal)

Run `scripts/verify_deployment.sh <BASE_URL>`; it asserts:

- application starts & probes answer (`/healthz/live`, `/healthz/ready`, `/health`),
- locale consistency (`scripts/check_locales.py`).

Then the four manual checks it does not cover:

1. **migrations applied** — startup log shows "No migrations were applied /
   Database is already up to date", or `__EFMigrationsHistory` matches the
   assembly,
2. **authentication endpoint responds** — `POST /api/v1/auth/request-email-link`
   with a test address answers 200 (delivery only via the configured provider),
3. **core API responds** — e.g. `GET /api/v1/activities/categories` with a
   valid token returns 200,
4. **gate state** — confirm `VisitAddress:RequireGrant` is OFF for the
   deployment.

For a full synthetic end-to-end (registration → matching → assignment →
safety checks → gate OFF → cleanup) against a **disposable** environment, use
`scripts/http_dry_run.py` (Development channel; synthetic data only). Do not
run it against production data.

## 5. Rollback procedure

Ordered from least to most invasive; the operator picks by failure class:

1. **Application rollback:** stop the new container, start the previous
   build/image (`docker compose up -d` with the previous tag; versioned
   artifact storage is the external requirement in §1). Record what was
   rolled back and why.
2. **Configuration rollback:** restore the previous `.env`, `docker compose
   up -d` to recreate. Startup fails loudly on invalid required
   configuration — read the log before proceeding.
3. **Feature-gate rollback:** set/keep `VisitAddress:RequireGrant: false`
   and restart. The architecture guard fails the build if a committed
   configuration ever enables it.
4. **Database rollback:** EF migrations support `Down()`, but rolling back a
   migration on production data is **not** an automated procedure here —
   assess per migration; if data loss is possible, prefer:
   ```bash
   ./scripts/restore_db.sh /path/to/backup.dump <target_db>
   ```
   after verifying `sha256sum -c` on the dump. Restoring overwrites everything
   after the backup point — decision by [Platform Operator] + [Backup Owner].
5. **Shutdown / restart:**
   ```bash
   docker compose down       # stop (data volume persists)
   docker compose up -d      # start
   ```

## 5a. Failure scenario index

Each scenario maps to the procedures above; no command is invented.

| Scenario | First action | Procedure |
| --- | --- | --- |
| **Failed deployment** (new image will not start / fails smoke test) | Stop the new container, start the previous image. | §5.1 application rollback. |
| **Failed migration** (startup `MigrateAsync` throws, `/healthz/ready` never green) | Do not force it; read the log, take a backup, decide restore vs forward-fix. | §5.4 database rollback + §3 probes. |
| **Configuration error** (invalid/missing required config; startup fails by design) | Restore the previous `.env`/secret values and recreate. | §5.2 configuration rollback. |
| **Security incident** (suspected bypass, cross-tenant read, exposed secret) | Follow the incident runbook; restrict operation and prepare a rollback. | `docs/operations/incident-response-runbook.md` + §5.1–§5.3. |
| **Feature-gate rollback** (`VisitAddress:RequireGrant` found `true`) | Reset to `false` and restart. | §5.3 feature-gate rollback. |
| **Application shutdown** (planned maintenance or emergency stop) | `docker compose down` / `docker compose stop api`. | §5.5 shutdown. |
| **Database recovery** (corruption / data loss) | Preserve state, take a backup, restore a verified dump. | §5.4 + `docs/operations/backup-restore-requirements.md`. |

## 6. Roles

- Executes releases/rollbacks: **[Platform Operator]** (the Deployment Owner).
- Database restore decision: **[Platform Operator]** with **[Backup Owner]**.
- Safeguarding-affecting incidents during rollback (e.g. lost audit rows):
  escalate per `docs/operations/incident-response-runbook.md`.

## 7. Not implemented (external deployment requirements)

- Versioned artifact registry and retention (CI does not push images).
- Deployment automation to a server (SSH/CD pipeline) — deployment happens
  where the operator runs docker compose.
- Blue-green/canary strategy and automated health-gated promotion.
- Centralized monitoring/alerting and backup-failure alerting.
