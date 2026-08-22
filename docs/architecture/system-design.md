# System Design

## 1. High-level

```
┌──────────────┐   ┌──────────────┐   ┌────────────────┐
│ Flutter App  │   │  Staff Web   │   │  Org REST API  │
│ iOS/Android  │   │ (Blazor/React│   │  (Phase 6)     │
│ + Web (fam.) │   │  Phase 6)    │   │                │
└──────┬───────┘   └──────┬───────┘   └───────┬────────┘
       │                  │                   │
       └──────────────────┼───────────────────┘
                          ▼
        ┌─────────────────────────────────────┐
        │        ASP.NET Core 10 Web API       │
        │        (Modular Monolith)           │
        │                                     │
        │  Identity · Profiles · Family       │
        │  Community · Help · Matching        │
        │  TrustSafety · Safeguarding         │
        │  Organizations · Notifications      │
        │  Reporting · Audit                  │
        └───────┬──────────────────┬──────────┘
                │                  │
        ┌───────▼──────┐   ┌───────▼──────────┐
        │  PostgreSQL  │   │  Background Jobs │
        │  (+PostGIS   │   │  (Hangfire)      │
        │   from Ph.8) │   │  reminders,      │
        └──────────────┘   │  expiry, digests │
                           └──────────────────┘
        ┌───────────────────────────────────────┐
        │ External (all behind interfaces)      │
        │ SMS · Email · Push · Identity/KYC ·   │
        │ Geocoding · Object storage            │
        └───────────────────────────────────────┘
```

**Not microservices.** A modular monolith with enforced boundaries is correct for a
solo developer with a multi-year roadmap. Revisit only when a module has a genuinely
different scaling profile and you have the data to prove it. (ADR-001)

---

## 2. Backend structure

```
backend/
├── SeniorConnect.Api/                 # Composition root, controllers/endpoints, middleware
├── SeniorConnect.SharedKernel/        # Result<T>, DomainEvent, base entities, value objects
├── SeniorConnect.Infrastructure/      # EF Core, external adapters, jobs, cross-cutting
└── modules/
    ├── Identity/
    │   ├── Domain/ Application/ Infrastructure/ Endpoints/ Contracts/
    ├── Profiles/
    ├── Family/
    ├── Community/
    ├── Help/
    ├── Matching/
    ├── TrustSafety/
    ├── Safeguarding/
    ├── Organizations/
    ├── Notifications/
    ├── Reporting/
    └── Audit/
```

### Module boundary rules

1. A module may reference **only** another module's `Contracts` project — never its
   `Domain`, `Application` or `Infrastructure`.
2. Cross-module communication: in-process interfaces defined in `Contracts`, or domain
   events. No direct DbContext access across modules.
3. Each module owns its tables. A foreign key across modules is allowed only where the
   ADR permits it (in practice: everything may reference `users.id`).
4. **These rules are enforced by architecture tests** (NetArchTest), not by discipline.

### Layering inside a module

```
Endpoints    → thin. authenticate, authorize, validate, call, map, return.
Application  → use cases, orchestration, authorization decisions, transactions.
Domain       → entities, value objects, invariants, state machines. No EF, no DTOs.
Infrastructure → EF configurations, repositories, external adapters.
```

Business logic in a controller is a defect. Business logic duplicated in Flutter is a
defect.

---

## 3. Flutter structure

```
mobile/SeniorConnect/lib/
├── main.dart
├── bootstrap.dart
├── core/
│   ├── design_system/   app_colors · app_typography · app_spacing · app_radius
│   │                    app_breakpoints · app_theme · app_theme_mode
│   ├── accessibility/   senior_mode · text_scale · semantics helpers
│   ├── localization/    locale_provider · rtl helpers
│   ├── network/         api_client · interceptors · error_mapper
│   ├── storage/         secure_storage · preferences · drift (from Phase 7)
│   ├── di/              injectable setup
│   ├── routing/         go_router config · route guards
│   └── error/           failure types · error presenter
├── features/
│   ├── auth/            data/ domain/ application/ presentation/
│   ├── profile/
│   ├── community/
│   ├── help/
│   ├── family/
│   ├── trust/
│   ├── safeguarding/
│   ├── notifications/
│   └── settings/
└── shared/
    ├── widgets/         AppButton · AppTextField · AppCard · AppStatusChip …
    ├── senior/          SeniorScaffold · SeniorActionButton · SeniorConfirmSheet
    └── utils/
```

### Two navigation shells, one codebase

```dart
if (seniorMode) → SeniorShell   // 4–5 full-width actions, no tabs, no drawer
else            → StandardShell // bottom navigation, standard density
```

Senior Mode is **not** a role. A volunteer with poor eyesight may enable it. It is a
device-level accessibility preference that changes type scale, touch targets, navigation
depth and information density.

---

## 4. Key technical decisions

| Area | Decision | Rationale |
| --- | --- | --- |
| Backend | ASP.NET Core 10 | Your strongest stack. Excellent authz primitives. |
| Data | PostgreSQL 16 | Mature, EU-hostable, PostGIS path. |
| ORM | EF Core 10, code-first migrations | Global query filters give tenant isolation for free. |
| Geo | lat/lng + Haversine now; PostGIS later | Columns designed so `CREATE EXTENSION postgis` is additive. (ADR-005) |
| Auth | ASP.NET Identity + JWT access (15 min) + rotating refresh (30 d) | Refresh tokens stored hashed and revocable per device. |
| Authorization | Policy + capability based, evaluated server-side only | See `authorization.md`. |
| Concurrency | `xmin` as concurrency token + conditional UPDATE for assignment | (ADR-006) |
| Jobs | Hangfire (Postgres storage) | No extra infrastructure. |
| Errors | RFC 7807 ProblemDetails with a stable `code` field | Flutter maps `code`, never message text. |
| API style | REST, `/api/v1`, DTOs only, never entities | |
| Mobile state | Cubit + Freezed | Matches your existing expertise. |
| Mobile local DB | Drift (Phase 7) | Same. |
| i18n | easy_localization, de source of truth | Same. |
| Hosting | EU only (Hetzner / Exoscale / Azure Austria East) | GDPR + NGO procurement. |
| Analytics | Self-hosted, privacy-preserving, or none | BR-GDPR-05. |
| Observability | Serilog → Seq/Loki, OpenTelemetry, Sentry (self-hosted) | |

---

## 5. Cross-cutting concerns

### Multi-tenancy
`ITenantContext` resolved from the authenticated principal. EF global query filters on
every organization-scoped entity. **Nullable** `OrganizationId` where community scope is
valid. Architecture test asserts every `IOrganizationScoped` entity has a filter.

### Audit
An EF `SaveChangesInterceptor` writes audit rows for entities marked `IAuditable`.
Read-audit (staff viewing a senior profile) is written explicitly in the application layer,
because reads are not visible to the interceptor. Append-only table, no update/delete
permission on the DB role.

### Soft delete
`IsDeleted` + query filter on user-generated content. **Hard delete** for GDPR erasure,
which is a separate, deliberate, audited operation — not the same code path.

### Idempotency
Mutating endpoints accept an `Idempotency-Key` header. Critical for mobile clients on
poor connections; a senior tapping "Anfragen" three times must create one request.

### Feature flags
Simple config-backed flags per phase so half-finished modules can ship dark.

---

## 6. Environments

```
local    → docker-compose: postgres, mailhog, seq
dev      → EU VPS, seeded demo data, NO real personal data
staging  → mirrors prod, anonymised data
prod     → EU, backups + tested restore, restricted access
```

Real personal data exists only in `prod`, and only after the Phase 7 legal gate.
