# Decision Implementation Report

> Phase: Decision Implementation & Policy Alignment · Date: 2026-09-30
> Mandate: convert only EXPLICITLY APPROVED policy decisions into docs/tests/implementation.
> Headline: **no decision was approved** — therefore no decision was implemented. This report records the evidence, the verified untouched state, and the exact missing approvals.

## 1. Repository State

- Branch `main`, HEAD `6f9c5ec` ("P9-07 , P9-10"), 4 commits ahead of `origin/main` (not pushed — no push requested).
- Pre-existing uncommitted working-tree changes from the previous closure phase
  (verified via `git diff` to contain exactly the documented reconciliation edits):
  AGENTS.md, .agents/rules/AGENTS.md, .agents/rules/GEMINI.md, BUILD-CHECKLIST.md,
  roadmap.md, 00-PRODUCT-BRIEF.md, phase5-decision-matrix.md, deletion of the
  duplicated `docs/plans/active/2026-09-adr021-phase2-intake-forms-flutter.md`
  (byte-identical copy archived in `docs/plans/completed/`), plus untracked
  `docs/decisions/DECISION-PACK-NEXT.md`. None were overwritten or reverted.
- Phase statement identical across AGENTS.md L142, .agents/rules/AGENTS.md L142,
  .agents/rules/GEMINI.md L142: **Phase 9 — Production Deployment & Public
  Readiness — CLOSED 2026-09**; next = Phase 8, gated on willing community +
  Legal Gate.

## 2. Approval Evidence

Search performed across `docs/` (decisions, product, security, plans), repository
markers (`ratified`, `approved`, `owner decided/chose/selected`, `sign-off`,
`decision recorded`), and per-ID grep for all seven IDs. Only explicit repository
records count; implementation, tests, "recommended", and roadmap sequencing were
**not** treated as approval.

| Decision | Status | Evidence | Action |
|---|---|---|---|
| ADR-026 | **C — STILL PENDING** | `ADR-026-visit-address-sharing.md` L3: "Status: **Proposed (decision required from product and legal)**"; Decision section: "No option is chosen in this ADR"; matrix §2: "DECISION REQUIRED (Proposed)" | None — verified non-exposure instead (§9) |
| ADR-027 | **C — STILL PENDING** | `ADR-027-trust-level-definition.md` L3: "Status: **Proposed (decision required)**"; matrix V-3 rows: "PENDING (ADR-027)", "ratification required"; phase5-security-gate: "until ADR-027 is ratified"; no commit after `8fd66b1` touches the file | None — not ratified, not applied |
| S-2 | **C — STILL PENDING** | matrix §1 L55 "**PENDING**", §2 L311 "DECISION REQUIRED"; no approval record anywhere | None — documented boundary only |
| S-3 | **C — STILL PENDING** | matrix §1 L56, §2 L312 — "DECISION REQUIRED" | None |
| S-4 | **C — STILL PENDING** | matrix §1 L57, §2 L313; scoping policy lists options, no owner choice recorded | None — remains hard multi-org gate |
| B-1 | **C — STILL PENDING** (unassignment half; effects resolved by rule in 5B) | matrix §1 L58 "unassignment is undefined", §2 L314 "DECISION REQUIRED (unassignment)" | None |
| B-2 | **C — STILL PENDING** | matrix §1 L59 "flag only, as today", §2 L315 "DECISION REQUIRED" | None |

Count: **0 approved / 0 rejected / 7 pending / 0 approved-with-unclear-requirements.**

## 3. ADR-027

- **Approval status:** not approved → **not ratified**. Decision status left exactly
  as "Proposed (decision required)"; no wording applied to BR-TRUST-02; no code change.
- **Documentation state (verified consistent):** business-rules.md's known-discrepancy
  note (~L669) already points to ADR-027 and states the computed ladder is what
  matching and `PerformSafetyLevelN` consume today; matrix and ADR index agree
  ("Ratification required"); phase5-security-gate lists the drift as open until
  ratification. No document claims trust = verification or trust = safeguarding;
  ADR-003 separation and BR-TRUST-01 keep the domains distinct.
- **Implementation state:** unchanged; `TrustLevelCalculator` remains the computed
  ladder consumed by matching (`RequiredTrustLevel`) and capability derivation.
- **Tests:** `Phase5PolicyClosureTests` / `Phase5PolicySweepTests` still pin that the
  assurance ladder and capability ladder never merge — preserved untouched.

## 4. S-2

- **Decision:** pending (product + safeguarding lead).
- **Implementation:** unchanged — officer imposes/lifts restrictions via API; every
  action writes `SafeguardingAccessLog` (BR-SG-06); `SafeguardingRestrictionReader`
  is queried fresh so effects are immediate; no notification dispatch, no coordinator
  task exists.
- **Security boundary:** restriction facts stay inside the safeguarding schema;
  nothing leaks to non-officers; `SafeguardingIsolationTests` /
  `SafeguardingLeakSweepTests` keep the boundary.
- **Tests:** all existing safeguarding suites pass; no new tests because no approved
  behaviour exists to test.

## 5. S-3

- **Decision:** pending.
- **Implementation:** unchanged — coordinators observe effects only (filtered
  matching candidates; assignment attempts fail like any ineligibility); no
  coordinator DTO mentions safeguarding.
- **Visibility boundary:** intact and test-pinned; the options (silence / neutral
  marker / full visibility) remain recorded in the matrix; the "full visibility"
  branch still contradicts BR-SG-02 and would require its own ADR.
- **Tests:** unchanged and passing.

## 6. S-4

- **Decision:** pending → **remains a hard gate before second-organization
  onboarding** (matrix: "blocking before a second organization joins").
- **Implementation:** unchanged — `SafeguardingService.VisibleCasesAsync` returns
  the officer's organizations' cases **plus all org-less cases**; org cases are
  strictly tenant-scoped (cross-tenant → 404); no speculative platform-level role
  was invented.
- **Organization-less case ownership:** undefined by rule; the option space is
  documented in `docs/security/safeguarding-scoping-policy.md`
  (`PLATFORM_LEVEL` vs `ORGANIZATION_SCOPED`); pinned by
  `S4_OrganizationCasesAreTenantScoped_OrgLessCasesArePlatformLevel`.
- **Multi-org readiness:** NOT ready — this decision plus an owner-assigned
  platform officer is required first.

## 7. B-1

- **Decision:** pending (unassignment question only). The security effects were
  rule-determined and closed in Phase 5B — that closure is *not* re-opened.
- **Assignment behavior (verified, unchanged):** `BlockUserAsync` is idempotent,
  stores a reason, notifies nobody, **unassigns nothing**; existing assignments stay
  `Assigned`/`InProgress`.
- **Security behavior (verified, unchanged):** both parties hidden from discovery
  (`SafetyBoundaryReader`), location → `None` (`LocationVisibilityService`),
  contact/visit address withheld even on a live assignment
  (`IsContactWithheldAsync`), matching filtered, future offers impossible in both
  directions, existing messages readable (BR-COMM-02).
- **Tests:** `T12_T23`, `T24`, `T24_T37`, `T30`,
  `B1_A_Block_WithholdsTheVisitAddress_ButNeverSilentlyUnassigns` — all passing.

## 8. B-2

- **Decision:** pending.
- **Escalation workflow (verified, unchanged):** `CommunityService.FileReportAsync`
  marks `UnsafeBehavior` reports `EscalatedToSafeguarding` **on the report row only**;
  no `SafeguardingCase`, no officer notification; the flag's only consumer is the
  moderation queue / report listing.
- **Officer visibility:** today an officer sees the flag only through the ordinary
  moderation queue — no dedicated officer surface exists. The three candidate
  models (manual case-from-report / auto-create / digest) remain unimplemented
  pending the owner's choice.
- **Tests:** `Phase4VenueAndReportTests.T25_T26`,
  `B2_AnUnsafeBehaviorReport_IsAFlag_OnTheReport_NotASafeguardingCase` — passing.

## 9. ADR-026

- **Legal decision status:** **NOT resolved** — no option selected; the four legal
  questions remain open and unanswered (legal basis beyond consent; family/coordinator
  consent on the senior's behalf; no-answer path; coordinator access under audited
  reason).
- **Selected option:** none (none approved).
- **Implementation state (verified this phase):**
  - No `visit_address_grants` / `VisitAddressGrant` artifact exists anywhere in
    `backend/`, `mobile/senior_connect/lib`, or `database/` — the only matches are
    the two design documents and the decision pack.
  - The Phase 4 gate is intact: address/phone/coordinates reach only the live
    assignee (`IsLiveAssignee`, `IsContactWithheldAsync` in
    `HelpRequestService.cs`); cancellation/completion/no-show/expiry end access;
    blocks and `CONTACT_BLOCKED`/`LOCATION_BLOCKED` restrictions withhold.
  - The separate standing location consent (`LocationSharingConsent`,
    `location_sharing_consents`) is untouched and is not silently used as address
    consent; the central location policy is not bypassed; auditability unchanged
    (guards `Phase5PolicyClosureTests.VisitAddress_ADR026IsProposed_…` and
    `Phase5PolicySweepTests.NoVisitAddressGrantArtifact_ExistsBeforeAdr026IsApproved`
    pass).
- **Remaining legal questions:** the four listed above, verbatim in the ADR §
  "Questions for product and legal". Owner + legal must answer them and choose
  A/B/C in a follow-up revision of the ADR; only then may the (already prepared,
  design-only) Option C plan be implemented.

## 10. Community / Early Access Gate

`docs/plans/active/2026-09-community-platform-analysis-and-plan.md` — a stale
in-paragraph inconsistency was corrected (documentation-only, supported by already
accepted decisions): §3.1's "required action" (file the ADR) is fulfilled —
ADR-025 is **Accepted** — and §3.2's interview gate was waived by founder decision
2026-09. A dated reconciliation note was added; the plan itself remains **Draft,
awaiting explicit owner go/no-go**; its §13 implementation order is unchanged and
NOT started.

Early-access readiness re-verified in
`docs/product/early-access-validation-plan.md`: organization-neutral (no named
partner, no mandatory community), entry gate still requires Legal Gate + willing
community + written risk acceptance of open decisions. **Legal Gate is NOT marked
passed; no community was invented; no rollout started.**

## 11. Validation

| Command | Result |
|---|---|
| `git diff --check` | clean (one transient trailing-whitespace introduced by the plan note was fixed in-phase) |
| `dotnet build backend/SeniorConnect.slnx -c Release` | 0 errors (16 pre-existing warnings) |
| `dotnet test backend/SeniorConnect.slnx -c Release --no-build` | **586 passed / 0 failed / 3 skipped** (Postgres integration, `SC_TEST_POSTGRES` unset locally; they RUN in CI — not claimed as executed here) |
| Architecture / Pilot Independence Guard | 58/58, guard 2/2 |
| `flutter analyze` | No issues found |
| `flutter test` | **906/906 passed** (known cosmetic `[Easy Localization]` warning untouched, per mandate §0.13) |
| `python scripts/check_locales.py` | OK — 705 keys × 3 locales consistent |

## 12. Remaining Blockers

All seven decisions remain owner-blocked (none is a code blocker today):

1. **ADR-026** — needs product+legal choice of A/B/C and written answers to the 4
   legal questions (external legal review).
2. **ADR-027 (V-3)** — needs product owner + tech lead ratification.
3. **S-2** — needs product + safeguarding lead choice.
4. **S-3** — needs product + safeguarding lead choice.
5. **S-4** — needs product + tech lead ownership decision; **hard gate before a
   second organization joins any deployment**.
6. **B-1** — needs product + coordinators decision on unassignment.
7. **B-2** — needs product + safeguarding lead choice of escalation model.

Additionally, before Early Access: Legal Gate execution (legal entity, lawyer
review, DPAs, EU hosting contract, insurance answers P0-01/02) and a willing
community — real-world actions outside this repository.

## 13. Scope Control

- **No speculative decision** was made or implemented; silence was not read as approval.
- **No new partner/pilot dependency**; no community invented; no real personal data added.
- **No unrelated feature**; no new endpoint, entity, workflow, or API contract change.
- **No security/safeguarding/privacy/tenant/authorization/audit boundary weakened** —
  all existing protection suites pass unmodified.
- **No legal claim invented**; ADR-026 remains unresolved and unimplemented.
- No D-class (unrelated) changes introduced; pre-existing uncommitted work from the
  previous phase was preserved verbatim and is listed in §1.
