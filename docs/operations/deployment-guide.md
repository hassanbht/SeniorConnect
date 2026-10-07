# Deployment Guide (P9-09)

> **Audience:** whoever deploys and operates a SeniorConnect backend + client stack.
> **Scope:** a fresh, independent, partner-agnostic production deployment from the
> repository's own artifacts. Complements [operator-runbook.md](operator-runbook.md)
> (day-2 operations) and [backup-and-restore.md](backup-and-restore.md).
>
> Related: [docker-compose.yml](../../docker-compose.yml), [backend/Dockerfile](../../backend/Dockerfile),
> [appsettings.Production.json](../../backend/SeniorConnect.Api/appsettings.Production.json),
> [CI pipeline](../../.github/workflows/ci.yml), [ADR-028](../decisions/ADR-028-project-independence.md).

---

## 1. Architecture of a deployment

```
┌────────────┐     ┌─────────────────────┐     ┌──────────────────────┐
│ Flutter app │────▶│  SeniorConnect.Api  │────▶│ PostgreSQL 16 + PostGIS│
│ web/mobile │HTTP │  (container :8080)  │     │ (postgis/postgis:16-3.4)│
└────────────┘     └─────────────────────┘     └──────────────────────┘
```

- One `docker-compose.yml` stack: services `db` and `api` on `seniorconnect_net`.
- The API container is a chiseled .NET 10 runtime image, non-root, with the
  healthcheck `dotnet SeniorConnect.Api.dll --healthcheck`.
- Flutter Web bundle (`flutter build web` → `build/web`) is served by the
  deployment's static host or reverse proxy; mobile builds are distributed
  per app-store/APK process.

## 2. Fresh deployment (the P9-06/P9-09 test)

1. **Prereqs:** Docker Engine + Compose v2; a domain with TLS termination (reverse proxy in front of `api:8080`); the repo checkout.
2. **Secrets:** create `.env` next to `docker-compose.yml`:
   ```bash
   POSTGRES_PASSWORD=<strong random>
   JWT_SECRET_KEY=<random, >= 32 chars>          # API refuses to start without it
   IDENTITY_HASH_PEPPER=<random, >= 32 chars>   # HMAC key for stored OTP / phone / IP hashes
   ```
   The compose file fails fast (`${JWT_SECRET_KEY:?}`, `${IDENTITY_HASH_PEPPER:?}`)
   if either secret is missing. **Set `IDENTITY_HASH_PEPPER` before the first real
   deployment** and keep it in the secret store: rotating it later invalidates
   existing OTP challenges and every stored phone/email/IP hash.
3. **Start:** `docker compose up -d --build`. The `db` healthcheck (`pg_isready`) gates the `api` service.
4. **Migrations:** apply EF migrations before first use, e.g.
   `dotnet ef database update --project backend/SeniorConnect.Infrastructure --startup-project backend/SeniorConnect.Api`
   (or the operator's equivalent job). Reference SQL migrations live in `database/migrations/`.
5. **Verify:** `./scripts/verify_deployment.sh` (or `.ps1`), then check
   `GET /healthz/live` and `GET /healthz/ready` return 200.
   A fresh environment reachable, migrated, and health-checked satisfies
   "a fresh environment can be deployed from documented steps".
6. **First data:** production starts **empty** — no demo seeding. To try the
   platform locally, run the app in Development where the seeder creates the
   clearly-labelled sample org "Musterverein Nachbarschaftshilfe (Demo)"
   ([DataSeeder.cs](../../backend/SeniorConnect.Infrastructure/DataSeeder.cs)).
   Never point the seeder at a production database.

## 3. Configuration reference

| Setting | Where | Production value / rule |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | env / compose | Host `db`, Postgres 16 + PostGIS extension |
| `JWT_SECRET_KEY` | env | Required, ≥ 32 chars, rotated per incident process |
| `ASPNETCORE_ENVIRONMENT` | compose | `Production` |
| `ASPNETCORE_URLS` | compose | `http://+:8080` (TLS at the reverse proxy) |
| `Cors:AllowedOrigins` | appsettings.Production.json | Your app origin(s), e.g. `https://app.seniorconnect.at`; change for your own domain |
| `Geocoding:Provider` | appsettings.Production.json | `None` by default — enable a provider explicitly, with API key and a proper `UserAgent` |
| `Matching:Weights` | appsettings.Production.json | Defaults; ADR-014 boundary: AI proposes, humans decide, no auto-assign at Safety Level 3+ |
| Serilog / log levels | appsettings.Production.json | JSON logging, SQL command telemetry suppressed to `Warning` |

All secrets come from the environment, never from committed files.

## 4. EU-hosting checklist (real personal data)

- [ ] VM/container host physically in the EU (Austria preferred for community deployments); provider DPA on file.
- [ ] Postgres + backups on EU storage, encrypted at rest (see [backup-and-restore](backup-and-restore.md): AES-256, SHA-256 checksums, 30-day rotation).
- [ ] TLS 1.2+ everywhere; HSTS at the proxy; no public exposure of `db` outside the compose network (remove the `5432` port mapping in production).
- [ ] Processing record ([verarbeitungsverzeichnis](../legal/verarbeitungsverzeichnis.md)) and DPIA ([dpia](../legal/dpia.md)) updated for this deployment before first real user data.
- [ ] DPA signed with every onboarded organization ([dpa-template](../legal/dpa-template.md)).
- [ ] Logs free of personal data payloads (log levels per appsettings.Production.json); log retention defined by the operator.
- [ ] Incident contacts reachable; [incident-response-runbook](incident-response-runbook.md) adapted with this deployment's real contacts.

## 5. Upgrades & rollback

1. Read the release notes / migration notes; check for breaking config keys.
2. `docker compose pull` / `docker compose build`, then **back up** (`scripts/backup_db.sh`) before applying migrations.
3. `docker compose up -d --build`, run `dotnet ef database update`, hit both health endpoints, run `scripts/verify_deployment.sh`.
4. Rollback = previous image tag + restore from the pre-upgrade dump (RTO ≤ 30 min per backup-and-restore).
5. Hotfixes follow [hotfix-matching-location](../security/hotfix-matching-location.md) where matching/location behaviour changes.

## 6. Client builds

| Target | Command | Artifact |
| --- | --- | --- |
| Web dashboard | `flutter build web --release` (in `mobile/senior_connect`) | `build/web/` → serve behind the same-origin or CORS-listed host |
| Android / iOS | standard `flutter build apk|appbundle|ipa` | Store or MDM distribution |
| API publish (non-container) | `dotnet publish backend/SeniorConnect.Api -c Release -o backend/publish` | Self-contained host possible; same config rules apply |

Localization consistency is enforced in CI (`scripts/check_locales.py`, de/en/fa).
