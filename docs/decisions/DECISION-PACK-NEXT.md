# SeniorConnect — Next Decision Pack

> Prepared: 2026-09-30 · Phase: Decision & Architecture Closure (post Pilot-Independence).
> Source of truth for every row: the canonical documents named in the Evidence column.
> This pack decides nothing. It orders what the owner must decide, and what each
> decision gates.

---

## 1. Current Project State

SeniorConnect is an **independent, open-source, production-oriented project**
(ADR-028, Accepted): all implementation phases 0–9 of the build checklist are
complete and verified — backend 586 tests passing / 0 failed (3 Postgres
integration tests skip locally without `SC_TEST_POSTGRES` and run in CI),
Flutter 906 tests passing, `flutter analyze` clean, 705 locale keys × 3 locales
consistent, 58 architecture tests including the 2-test Pilot Independence Guard.
No production runtime, config, or seed dependency on any named partner exists.
Seven decisions remain formally open; none of them is blocking the current,
partner-agnostic deployment.

## 2. Current Phase

**Canonical statement (AGENTS.md, .agents/rules/AGENTS.md, .agents/rules/GEMINI.md — identical):**

> CURRENT PHASE: 9 — Production Deployment & Public Readiness — CLOSED 2026-09
> (P9-01..P9-10 complete; all roadmap phases 0–9 implemented).
> Next: Phase 8 — Early Access & Real-World Validation, gated on a willing
> community + Legal Gate (docs/product/early-access-validation-plan.md).

**Evidence:** AGENTS.md L142–151; BUILD-CHECKLIST PHASE 9 closure note
("CLOSED 2026-09", P9-01…P9-10 all `[x]`); roadmap v2.1 headers (Phase 5/6
"SHIPPED (gate waived)", Phase 7 = Production & Public Readiness, Phase 8 =
Early Access & Real-World Validation, Phase 9 = Partnerships & Scale).

**Phase-number mapping note (verified consistent as of this audit):** the build
checklist's own block numbers differ from roadmap v2.1 numbering — checklist
PHASE 7/8/9 blocks = hardening / scale features (roadmap Phase 9) /
deployment & readiness docs (roadmap Phase 7 deliverables). Mapping notes were
added to the checklist blocks in this closure phase; no stale "next phase"
statement remains anywhere.

## 3. Completed Phases

| Phase (checklist block) | Status | Evidence |
| --- | --- | --- |
| PHASE 0 — Discovery & Project Brain | Complete (P0-01/02/08 real-world items intentionally open) | BUILD-CHECKLIST §0; GATE 0 partially open by design |
| PHASE 1 — Foundation | Complete — all tasks `[x]` | BUILD-CHECKLIST PHASE 1; gate items listed with tests |
| PHASE 2 — Coordinator Wedge | Complete (P2-03/P2-06 manual tasks open — manual cross-tenant attempts are per-release rituals) | BUILD-CHECKLIST PHASE 2 |
| PHASE 2.9 — Scope Generalization | Complete — per ADR-018 | BUILD-CHECKLIST PHASE 2.9 |
| PHASE 3 — Help & Matching | Complete | BUILD-CHECKLIST PHASE 3 |
| PHASE 4 — Trust & Safeguarding | Complete | BUILD-CHECKLIST PHASE 4 |
| PHASE 5 — Org Announcements & Events | Complete — gate WAIVED 2026-09 | BUILD-CHECKLIST header; roadmap banner |
| PHASE 6 — Family & Delegation | Complete — gate WAIVED 2026-09 | BUILD-CHECKLIST header; roadmap banner |
| PHASE 7 — Production Hardening & Test Gate 7 | Complete & archived | docs/plans/completed/2026-09-phase7-pilot-hardening.md |
| PHASE 8 block (scale features) | `[x]` P8-01…P8-08 — implementations verified present (voice notifier, ESG screen, flavor config, ID-Austria DTOs, PostGIS, HybridMatchingPolicy) | BUILD-CHECKLIST PHASE 8 block; code paths cited in audit |
| PHASE 9 — Production Deployment & Public Readiness | Complete — P9-01…P9-10 verified with evidence | BUILD-CHECKLIST PHASE 9 closure note; docs cited per task below |

**P9 evidence (spot-verified, not checkbox-trusted):** P9-01 multi-stage
chiseled non-root Dockerfile (`backend/Dockerfile` FROM sdk:10.0 →
aspnet:10.0-noble-chiseled, USER $APP_UID) + PostGIS 16 compose; P9-02
`appsettings.Production.json` strict CORS/log levels; P9-03 `.github/workflows/ci.yml`
(backend+Postgres, Flutter matrix, locale parity, vuln scan, Docker build);
P9-04 `backend/publish/` + `build/web/` artifacts; P9-05 archived plans in
`docs/plans/completed/`; P9-06 deployment runbook content in
`docs/operations/deployment-guide.md` + `backup-and-restore.md`; P9-07
`docs/onboarding/organization-onboarding-guide.md`; P9-08
`docs/product/early-access-validation-plan.md`; P9-09 operator runbook +
deployment guide + org-admin guide; P9-10 security overview, AGPL-3.0 LICENSE,
CONTRIBUTING.md, governance.md.

## 4. Open Decisions

Canonical inventory: `docs/product/phase5-decision-matrix.md` §2 (7 pending —
corrected in this audit; the previous count of 6 omitted B-2).

| ID | Topic | Status | Classification | Owner Action |
| --- | --- | --- | --- | --- |
| ADR-026 | Visit-address sharing (BR-COMM-04 + Phase 3 consent) | Proposed; **no option chosen** | **D — legal/privacy** (owner+legal) | Choose A/B/C; answer the 4 legal questions (§5) |
| ADR-027 | Trust-level definition (V-3) | Proposed; **ratification required** | **B — product+tech** | Ratify computed ladder as normative (or order code-alignment phase) |
| S-2 | Notification/workflow for restrictions | DECISION REQUIRED | **C — product+safeguarding lead** | Choose: officer notification / content-free coordinator task / silence |
| S-3 | Coordinator awareness of restrictions | DECISION REQUIRED | **C — product+safeguarding lead** | Choose: no disclosure (status quo) / neutral marker / full visibility (needs ADR) |
| S-4 | Org-less cases / platform-level officer | DECISION REQUIRED — **blocking before a 2nd organization joins** | **C — product+tech** | Name the owner of `OrganizationId == null` cases (options in scoping policy) |
| B-1 | Block vs. existing assignment | Security effects RESOLVED & tested; **unassignment undefined** | **C — product+coordinators** | Keep status quo / flag for attention / auto-unassign with replacement flow |
| B-2 | UnsafeBehavior report → officer surface | DECISION REQUIRED (flag-only today) | **C — product+safeguarding lead** | Choose: manual case-from-report / auto-create / officer digest |

Cross-check of repository-wide markers (`DECISION REQUIRED`, `unresolved`,
`Proposed`, `owner decision`, …) found **no open item outside this list**.
Resolved and closed as rule-determined in Phase 5B: V-1, V-2, V-4, S-1, and the
B-1 security effects (withholding of address/contact/location) — not re-opened.

## 5. ADR-026 — Decision Summary (no new options; owner must choose)

**Problem.** BR-COMM-04 reveals the help request's home address to the assigned
volunteer after assignment. Phase 3 introduced a separate, standing
precise-location consent. Treating them as one permission either breaks home
visits (Option B) or yields an implicit, irrevocable consent (Option A). The
HIGH privacy residual from Phase 3 stays open until a choice is made.

**Existing implementation (unchanged by any option until decided).** The
assigned volunteer of a **live** assignment (`Assigned`/`InProgress`) receives
the typed address; blocks and `CONTACT_BLOCKED`/`LOCATION_BLOCKED` restrictions
withhold it; cancelled/completed/no-show/expired assignments end access; staff
never receive it via these endpoints. Architecture tests pin that Option C is
**not** accidentally implemented (`Phase5PolicyClosureTests`,
`Phase5PolicySweepTests`).

**Options (verbatim from the ADR):**

- **Option A — entering the address counts as consent.** Matches BR-COMM-04 as
  written; no new UI step; home visits keep working. Against: implicit consent,
  family may enter the address (BR-HELP-04), not revocable before the visit.
- **Option B — require `PRECISE` location consent for the address.** One
  setting, one mental model. Against: merges two permissions; every existing
  senior is at `NONE` (Phase 3 migrated nobody), so home visits silently lose
  the address; pushes seniors to share exact coordinates — opposite of data
  minimisation.
- **Option C — dedicated per-assignment visit-address consent (the ADR's
  preferred direction; designed design-only in
  `ADR-026-implementation-plan.md`, guarded against early implementation).**
  Purpose-bound, revocable, minimised. Against: new step in the time-critical
  flow; a non-answering senior blocks the visit; needs a coordinator path for
  offline/IVR seniors; changes BR-COMM-04's wording.

**Security/privacy consequences.** Phase 4 hardening (live-assignment-only,
withholding on block/restriction) holds under every option. The open HIGH
residual is about the *lawfulness of the default disclosure*, not a code defect.

**Technical consequences.** A → none. B → mass behaviour change on existing
data. C → new `visit_address_grants` table + gate change + Flutter prompt +
BR re-wording (full plan exists, ready to implement on approval).

**The four legal questions (from the ADR, unanswered):**
1. Is BR-COMM-04 disclosure covered by a legal basis other than consent (e.g. Art. 6(1)(b))?
2. May a family member with `CanCreateHelpRequests` grant on the senior's behalf; may a coordinator, for app-less seniors?
3. What happens when the senior does not answer before the visit (reminder / coordinator call / automatic unassignment)?
4. Should a coordinator ever see the visit address (e.g. to send a replacement), under which audited reason?

**Exact owner action:** product + legal choose A, B or C **in a follow-up
revision of ADR-026**, record the four answers, then (if C) approve the
implementation plan. **This pack does not choose, upgrade, or implement.**

## 6. ADR-027 — Decision Summary (V-3)

**Status:** Proposed (decision required). **The divergence:** BR-TRUST-02's
binding table is an *assurance ladder* (Contact→Identity→Address→Organization→
Safety), while `TrustLevelCalculator` computes a *capability-gate ladder*
(1 = email OR phone, 2 = both, 3 = address, 4 = identity, 5 = background
check). Matching (`RequiredTrustLevel`) and `PerformSafetyLevelN` capabilities
consume the computed ladder; the disclosure layer does not read the number at
all — which is why the divergence has caused no security incident.

**Proposed resolution (the ADR asks for ratification, it does not apply it):**
the computed ladder becomes the platform-normative definition; BR-TRUST-02's
table is reworded accordingly (proposed wording included in the ADR);
organization-verification and training stay separate facts; no numeric trust
score is introduced. Zero code change.

**Implementation relationship (verified):** the code already assumes the
computed ladder (snapshots at every auth/refresh, capability derivation,
matching thresholds). Documentation already points BR-TRUST-02's divergence
note at ADR-027 (business-rules.md ~L669). Tests
(`Phase5PolicyClosureTests`, `Phase5PolicySweepTests`) prove the two ladders
never merge. **No unmanaged divergence exists** — the only gap is the missing
ratification itself. If the owner rejects the proposed direction instead, a
separate code-alignment phase (capability remap, snapshot backfill, threshold
review) is required — estimated as its own phase, not a gate fix.

**Exact owner action:** product + tech lead ratify (Proposed → Accepted,
apply the docs-only wording) or order the alignment phase.

## 7. Safeguarding Decisions — S-2 / S-3 / S-4

All three are **policy gaps, not security defects**; existing safeguarding
behaviour is implemented, tested and must not change without a decision.

| ID | Code does today | Gap | Security finding? |
| --- | --- | --- | --- |
| S-2 | Officer imposes/lifts restrictions via API; every action writes `SafeguardingAccessLog` (BR-SG-06); effects immediate for all readers; restrictions never auto-create cases | No rule defines notifying anyone when a restriction is imposed (BR-NOTIFY-03 hints officer notifications exist but never says when) | No |
| S-3 | Coordinators observe effects only (restricted candidates filtered from matching; assignment attempts fail like any ineligibility); no coordinator DTO mentions safeguarding; isolation tests assert no leakage | No rule says what a coordinator may learn | No |
| S-4 | `VisibleCasesAsync` returns the officer's organizations' cases **plus all org-less cases**; org cases strictly tenant-scoped (cross-tenant → 404); no platform-officer role exists; boundary pinned by `S4_OrganizationCasesAreTenantScoped_OrgLessCasesArePlatformLevel` | Nobody owns `OrganizationId == null` cases by rule; documented options live in `docs/security/safeguarding-scoping-policy.md` | **Latent gap only for multi-org deployments** — blocking before a 2nd organization joins; single-org deployments unaffected |

**Exact owner actions:** S-2 — choose officer-only notification, content-free
coordinator task, or silence. S-3 — choose no disclosure (status quo), a
neutral "handled by safeguarding" marker, or full visibility (contradicts
BR-SG-02; would need its own ADR). S-4 — name the owner of org-less cases
before multi-org rollout.

## 8. Block/Report Decisions — B-1 / B-2

**B-1 (block vs. existing assignment).** Today `BlockUserAsync` is idempotent,
stores a reason, notifies nobody, **unassigns nothing**. Effects are
rule-determined and tested: both parties hidden in discovery
(`SafetyBoundaryReader`), location → `None` (`LocationVisibilityService`),
contact/visit address withheld even on a live assignment
(`IsContactWithheldAsync`; pinned by
`B1_A_Block_WithholdsTheVisitAddress_ButNeverSilentlyUnassigns`), matching
filtered, future offers impossible both ways, existing messages stay readable
(BR-COMM-02). Location/contact data is **not** accessible to the blocked pair.
**Undefined:** only whether an existing assignment should additionally be
unassigned or flagged. The security requirement is met in every state — this is
a UX/operations choice for product + organization coordinators. Documented
accurately in the matrix; no doc correction needed.

**B-2 (escalated reports).** `CommunityService.FileReportAsync` marks
`UnsafeBehavior` reports `EscalatedToSafeguarding` **on the report row only** —
no `SafeguardingCase`, no officer notification; the flag's only consumer is the
moderation queue. So an escalated report creates **no usable officer surface**
beyond the moderation queue itself. Pinned by
`B2_AnUnsafeBehaviorReport_IsAFlag_OnTheReport_NotASafeguardingCase`; consistent
with BR-SG-01 (safeguarding is not an extension of reporting). **Exact owner
action:** choose manual "create case from report" (smallest fix), auto-create
per report (noise/retention cost), or an officer digest of escalated reports.

## 9. Real-World Gates — documentation vs. reality

| Gate | Documentation | Real-world status |
| --- | --- | --- |
| Legal Gate (Phase 7/roadmap) | Templates + registers **written**: privacy-policy, terms, DPA template, Verarbeitungsverzeichnis, DPIA, data-retention | **Not satisfied** — requires legal entity, lawyer review, signed DPAs, contractual EU hosting. No doc may be counted as compliance |
| P0-01 coordinator call · P0-02 insurance broker · P0-08 budget calendar | Open checklist items | **Not done** — real conversations only the owner can have |
| Real-World Gate (closing ritual: "give it to someone over 70") | Ritual documented | **Not executed** — belongs to Phase 8 early access |
| Phase 8 Early Access | `early-access-validation-plan.md` complete (entry gate, hypotheses, measures, exit) | **Not started** — deliberately: no recruited users, no partner |

## 10. Community Platform Plan

`docs/plans/active/2026-09-community-platform-analysis-and-plan.md` (Draft,
2026-09-21): analysis of the existing Community module plus a full
implementation plan (domain model, API, authorization, migrations, Flutter,
localization, tests). Reconciliation findings:

- **Its §3.1 conflict is already resolved**: it asked for "a new ADR (e.g.
  ADR-025…)" — **ADR-025 exists and is Accepted** (amends ADR-020 §2/§4 and
  BR-COMM-06, allowing eligible individual creators). The plan's stated
  blocker is gone; the plan text predates the ADR and was never updated.
- **Its §3.2 conflict is resolved the other way**: roadmap Phase 5/6 are now
  "SHIPPED (gate waived)", and the Community module already ships with the
  phase — so the "obtain interviews first" alternative is moot.
- **Scope overlap:** the platform's group/event/thread features exist today
  (ADR-020 §2/§3 implemented); the plan's delta is the individual-creator
  experience, member lifecycle deepening, and post/comment split.
- **Status:** valid, prepared future plan; **requires an owner go/no-go**
  before any implementation (per this phase's stop condition, none was
  started). Recommended housekeeping when the owner decides: refresh §1–§3 of
  the plan against ADR-025 and the current code, then move to
  `docs/plans/completed/` on completion or delete on rejection.

## 11. Recommended Decision Sequence (dependency order only)

1. **ADR-027 ratification** (product + tech lead) — zero risk, unblocks the
   BR-TRUST-02 wording; independent of everything else.
2. **S-2, S-3, B-2** (product + safeguarding lead) — small, self-contained
   policy choices; S-3's "full visibility" branch is the only one needing a new
   ADR.
3. **B-1 unassignment** (product + coordinators) — benefits from S-3 being
   settled (the "flag for coordinator attention" option interacts with what
   coordinators may learn).
4. **S-4 platform-officer ownership** (product + tech lead) — required before
   any second organization joins a deployment; sequence with onboarding plans.
5. **ADR-026 (product + legal)** — the legal prerequisite: the four questions
   need an external legal review; everything else can proceed without it
   because BR-COMM-04 remains in force and guarded.
6. **Community Platform plan go/no-go** (owner) — refresh the plan against
   ADR-025 first; implementation only after an explicit go.
7. **Legal Gate execution** (owner + lawyer) — entity, DPA, insurance answers
   (P0-01/02/08); hard prerequisite for real personal data.
8. **Enter Phase 8 — Early Access** — only after 7, per
   `early-access-validation-plan.md`'s entry gate; stays organization-neutral.

---

*Prepared by the Decision & Architecture Closure audit. No decision in this
pack has been pre-made; every "owner action" is left to the named owners.*
