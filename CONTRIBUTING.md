# Contributing to SeniorConnect

Thanks for helping build an independent, open-source platform for local
communities. This project has **no owning organization** (ADR-028) — decisions
are made through the ADR process documented here, and every contribution is
reviewed as code, not by rank.

## 1. Read first

1. [00-PRODUCT-BRIEF.md](00-PRODUCT-BRIEF.md) — product mission and independence note.
2. [docs/product/vision.md](docs/product/vision.md)
3. [docs/product/business-rules.md](docs/product/business-rules.md) — **binding** BR-* rules.
4. [AGENTS.md](AGENTS.md) — AI-agent guide; [docs/architecture/](docs/architecture/) for the architecture.

## 2. Ground rules

- **No new partner branding in code.** The repository is partner-agnostic; architecture tests (`PilotIndependenceGuardTests`) enforce this. Historical prose in ADRs/plans stays as is.
- **No feature creep past the roadmap.** Check [docs/product/roadmap.md](docs/product/roadmap.md) and the [BUILD-CHECKLIST](docs/plans/BUILD-CHECKLIST.md); parked items (payment marketplace, paid care mediation, gamification) stay parked.
- **Unresolved decisions stay unresolved.** ADR-026, ADR-027, S-2/S-3/S-4, B-1/B-2 need a deliberate ADR decision, not a drive-by implementation.
- **Safety boundaries hold.** ADR-014: AI proposes, humans decide — no auto-assignment at Safety Level 3+. Safeguarding rules (BR-SG-*) are non-negotiable.

## 3. Workflow

1. Fork / branch from `main` (`feat/<topic>` or `fix/<topic>`).
2. Small, focused PRs. Reference the BR-* / ADR your change touches.
3. Every task must satisfy the per-task Definition of Done in the
   [BUILD-CHECKLIST](docs/plans/BUILD-CHECKLIST.md#per-task-definition-of-done):
   authorization matrix (401/403/404/200), concurrency, audit entries,
   ProblemDetails with stable codes, Flutter loading/empty/error states,
   de+en+fa locales, light+dark, text scale, accessibility (48/64 dp targets).
4. CI must pass: backend + architecture tests, Flutter matrix, locale
   consistency (`scripts/check_locales.py`), vulnerability scan, Docker build.

## 4. Testing expectations

| Area | Requirement |
| --- | --- |
| Domain logic | Unit tests with real business-rule assertions (not "returns 200") |
| Endpoints | 401 without auth, 403 without capability, 404 cross-tenant, 200 happy path |
| Concurrency | Two-writer scenarios for any row users can touch simultaneously |
| Flutter | Widget tests incl. RTL (fa), senior-mode text-scale runs |
| Architecture | Guard tests stay green; add a case if you extend a rule |

## 5. Commits & style

- Imperative, descriptive messages; one logical change per commit.
- German user-facing strings for the default locale; translations for en/fa must
  be added in the same PR (CI checks key parity).
- Docs live in `docs/`; ADRs in `docs/decisions/` with sequential numbers and an
  index row in [docs/decisions/README.md](docs/decisions/README.md).

## 6. Governance

Decision-making, roles, and the ADR process: [docs/governance.md](docs/governance.md).
By contributing you agree your contributions are licensed under the project
license ([LICENSE](LICENSE)).
