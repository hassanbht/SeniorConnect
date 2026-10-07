# Release-Critical File Inventory

Exactly what belongs in the first canonical release commit. Generated
outputs (`bin/`, `obj/`, `.dart_tool/`, `build/`), local credentials,
AI-agent private state (`.agents/`, `.freebuff/`, `.serena/`), and the
disposable-test database are EXCLUDED.

| Classification | Path | Reason |
| -------------- | ---- | ------ |
| A | `backend/SeniorConnect.Api/` (excl. `bin/`, `obj/`) | Release source (API host) |
| A | `backend/SeniorConnect.Application/`, `SeniorConnect.Domain/`, `SeniorConnect.Infrastructure/` (excl. `bin/`, `obj/`) | Release source (shared layers) |
| A | `backend/modules/*/` (excl. `bin/`, `obj/`) | Release source (11 modules) |
| A | `backend/Directory.Build.props`, `backend/SeniorConnect.slnx`, `backend/Dockerfile`, `backend/.dockerignore` | Build/release execution |
| A | `mobile/senior_connect/lib/`, `assets/`, `pubspec.yaml`, platform shells (`android/`, `ios/`, `web/`, `windows/`, `macos/`, `linux/` if present) | Release source (Flutter) |
| A | `docker-compose.yml` | Local/prod-shaped runtime topology |
| A | `database/` if schema artifacts are required by release policy | Database schema |
| B | `tests/` (excl. `bin/`, `obj/`, `TestResults/`) | Required validation |
| C | `backend/SeniorConnect.Infrastructure/Migrations/` (excl. designer-generated `*.Designer.cs`? NO — include: designers are required to build) | Database schema (19 migrations, head `20261001150947_*`) |
| D | `docs/decisions/`, `docs/plans/BUILD-CHECKLIST.md`, `docs/release/` | Traceability/gates (current-gate reconstructions, honestly labeled) |
| D | `AGENTS.md`, `backend/AGENTS.md`, `mobile/senior_connect/AGENTS.md`, `backend/README.md`, `00-PRODUCT-BRIEF.md`, `CONTRIBUTING.md`, `SECURITY.md` | Provenance/policy sources |
| E | `.github/workflows/ci.yml` | Release execution (presence only; not executed here) |
| E | `mobile/senior_connect/scripts/check_locales.py`, `analysis_options.yaml` | Quality gates |
| F | `.env`, `*.env.local`, local credential files, `TestResults/`, disposable DBs | Excluded |
| G | `bin/`, `obj/`, `.dart_tool/`, `build/`, `.flutter-plugins*` | Excluded (regenerable) |
| H | `prompts/` (phase instructions), `starter/` (bootstrap bundle), `skills/` | Excluded from runtime baseline; keep in repo history per project policy |

Reviewer note: `starter/` duplicates an early skeleton (incl. its own backend
sources and SQL). It is history, not release content — do not ship it as
part of the application baseline without a separate decision.
