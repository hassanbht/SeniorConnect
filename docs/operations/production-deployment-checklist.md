# Production Deployment Checklist

> **Status:** 2026-10-02 (launch-security & deployment-closure phase).
> This checklist separates **what the repository provides and proves**
> (`REPOSITORY READY`) from **what must be provisioned by an external
> deployment owner** (`INFRASTRUCTURE NOT PROVISIONED`). No hosting vendor,
> domain, certificate authority or registry is chosen or invented here.
> Nothing in this document claims that production is deployed.

## 1. Capability matrix

| Area | Repository state | Evidence | Infrastructure state |
| --- | --- | --- | --- |
| ASP.NET Core application | **READY** | `backend/SeniorConnect.Api` (.NET 10), Release build 0 errors. | NOT PROVISIONED — no host chosen. |
| Container image | **READY** | `backend/Dockerfile` multi-stage build, non-root distroless/chiseled runtime. | NOT PROVISIONED — no registry server chosen. |
| PostgreSQL | **READY** | EF Core 10 + Npgsql; `postgis/postgis:16-3.4` in `docker-compose.yml`; migrations applied at startup. | NOT PROVISIONED — no managed/self-hosted DB chosen. |
| Configuration / secrets | **READY** | Everything secret comes from env/secret store; startup refuses dev JWT key outside Development; `Email` fails closed. | NOT PROVISIONED — secret store instance external. |
| Health checks | **READY** | `/healthz/live`, `/healthz/ready` (DB check), `/health`; container `healthcheck`. | — |
| Database migrations | **READY** | `db.Database.MigrateAsync()` on startup (disable with `DB_MIGRATE_ON_STARTUP=false` for out-of-band migration). | — |
| Logs | **READY (stdout)** | Standard ASP.NET console logging; structured messages. | NOT PROVISIONED — no log aggregation/sink chosen. |
| TLS / HTTPS | **READY (app-level)** | `UseHttpsRedirection()` + `UseHsts()` outside Development; `EnableSsl=true` SMTP. | NOT PROVISIONED — no certificate or terminator chosen. |
| Domain | **READY (config)** | Production CORS locked to `https://app.seniorconnect.at`. | NOT PROVISIONED — DNS/host external. |
| Reverse proxy | **READY (compatible)** | App binds `http://+:8080`; standard proxy/forwarding works. | NOT PROVISIONED — proxy instance external. |
| Artifact registry / versioning | **NOT IMPLEMENTED in repo** | CI builds images with `push: false`. | NOT PROVISIONED — registry + tagging external. |
| Monitoring / alerting | **NOT IMPLEMENTED in repo** | Only container healthchecks exist. | NOT PROVISIONED — platform external. |
| Zero-downtime / blue-green | **NOT IMPLEMENTED in repo** | compose model = single-instance restart. | NOT PROVISIONED — strategy external. |

## 2. Reproducible deployment (repository-proven)

```bash
# 1. Build the image (context = ./backend)
docker build -t seniorconnect-api:<version> ./backend

# 2. Provide deployment secrets out-of-band (never in the repo), then:
docker compose up -d
```

- `docker-compose.yml` requires `JWT_SECRET_KEY` (`:?` — compose refuses to
  start without it) and reads the DB password from `POSTGRES_PASSWORD`.
- The `api` service waits on the `db` healthcheck, then applies migrations at
  startup.
- Health: `docker compose ps` shows the container healthcheck result
  (`dotnet SeniorConnect.Api.dll --healthcheck`).

## 3. Required deployment configuration (external)

| Setting | Source | Rule |
| --- | --- | --- |
| `JWT_SECRET_KEY` | secret store | ≥ 32 chars, **not** the public dev key; startup refuses the dev key outside Development. |
| `POSTGRES_PASSWORD` / `DB_*` | secret store | Never the compose dev fallback. |
| `EMAIL_USERNAME` / `EMAIL_PASSWORD` | secret store | See `docs/operations/email-provider-deployment.md`. |
| `Email:Provider` / `Host` / `SenderAddress` | deploy config | `none` until a provider is contracted. |
| `Geocoding__Provider` / `Geocoding__ApiKey` | deploy config | `None` until a geocoder is chosen; `RequireApiKey` default true. |
| `ASPNETCORE_ENVIRONMENT` | deploy config | `Production` (enables HSTS, hides OpenAPI/Scalar, refuses dev secrets). |
| `Cors__AllowedOrigins__*` | deploy config | Must match the real front-end origin; production default is `https://app.seniorconnect.at`. |
| `VisitAddress__RequireGrant` | — | **Must remain `false`.** Never set it in deployment config (see `docs/security/visit-address-activation-gate.md`). |

## 4. TLS / domain requirements (§12)

These are **requirements**, not provisions. No certificate is created here.

- **Domain:** a stable, operator-controlled HTTPS origin for the API and the
  Flutter web/app front end. The production CORS allow-list
  (`https://app.seniorconnect.at`) must be replaced with the real origin if it
  differs.
- **HTTPS:** mandatory. The app enables `UseHttpsRedirection()` and
  `UseHsts()` outside Development, so the deployment must terminate TLS (proxy
  or app) with a valid, publicly-trusted certificate.
- **Certificate management:** an external requirement — issuance and renewal
  (e.g. via the chosen proxy/ACME setup) are the operator's responsibility.
  The repository ships no certificate.
- **Reverse proxy:** the app exposes plain HTTP on `:8080` and expects the
  proxy to terminate TLS and forward the standard forwarded headers. The
  deployment must not expose `:8080` directly to the internet without TLS.
- **Security headers:** the app already sets `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy` and HSTS outside Development. Any
  additional header policy (e.g. CSP) is an operator/proxy decision.
- **CORS:** explicitly controlled via `Cors:AllowedOrigins`; production is
  locked to a single origin. Wildcards are not used.
- **Sender domain:** SPF/DKIM/DMARC for the SMTP sender identity (see the email
  provider doc).

**TLS / domain status: EXTERNAL ACTION REQUIRED** — no real domain, DNS record
or certificate exists in this repository, and none is invented.

## 5. Artifact / registry requirements (external)

- CI proves the image builds; it does **not** publish it.
- The deployment owner must provide versioned artifact storage (image registry)
  and a tagging convention (git SHA suggested) so rollback to a previous image
  is possible (`docs/operations/release-and-rollback-runbook.md` §5).

## 6. External provisioning checklist

- [ ] Choose & provision the host / runtime environment (not chosen here).
- [ ] Choose & provision PostgreSQL (managed or self-hosted); supply DB
      credentials + `DB_SSL_MODE` for the environment.
- [ ] Provision DNS, domain and a trusted TLS certificate.
- [ ] Configure a reverse proxy to terminate TLS and forward to `:8080`.
- [ ] Provision the secret store and load all §3 values.
- [ ] Provision versioned image storage (registry) and a tagging convention.
- [ ] Provision log aggregation and monitoring/alerting.
- [ ] Confirm `VisitAddress__RequireGrant` is **not** set anywhere in the
      deployment configuration.
- [ ] Run `scripts/verify_deployment.sh <BASE_URL>` and the runbook smoke test.

**Overall status: REPOSITORY READY / INFRASTRUCTURE NOT PROVISIONED.**
