# Early-Access Validation Plan (P9-08)

> **Purpose:** a structured plan for controlled real-world usage and feedback
> collection with **willing** organizations and communities — preparing the
> transition from "works in production, validated only by tests" to
> "validated by real usage". **No specific partner is required or named**
> (ADR-028); partners are possibilities, not dependencies (Phase 8 note).
>
> Related: [Roadmap Phase 8 — Early Access & Real-World Validation](roadmap.md),
> [Phase 5 decision matrix](phase5-decision-matrix.md) (closed 2026-09-30 — zero pending),
> [Go-to-market](go-to-market.md), [ADR-028](../decisions/ADR-028-project-independence.md).

---

## 1. Entry gate

Early access starts only when **all** of these hold:

1. Phase 9 deployment readiness is done (P9-01…P9-06: hardened containers, production config, CI/CD, EU-hosting checklist, backup/restore verified).
2. The [Legal Gate](../plans/BUILD-CHECKLIST.md) in the build checklist is consciously passed for the participating community: DPA in place with the operator, processing record and DPIA updated for real personal data.
3. A safeguarding lead with `SafeguardingOfficer` capability is appointed for the participating community (BR-SG-02).
4. The Phase 5 decision pack is **resolved and implemented** (owner decisions, 2026-09-30): ADR-027 = A (trust ladder ratified), S-2 = B, S-3 = B, S-4 = B, B-1 = B, B-2 = A, ADR-026 = C. All seven are implemented and tested; no further written risk acceptance is required from the operating organization.
5. **ADR-026 (visit-address sharing) stays feature-gated OFF during early access.** The `VisitAddress:RequireGrant` configuration ships `false` and an architecture guard fails the build if any committed configuration enables it. Production activation requires the Legal/Data Protection Gate below to be passed **in writing** for the deployment in question; until then BR-COMM-04 (no address disclosure) remains in force. Early access must not enable the gate.
6. A **platform safeguarding officer** (S-4) is named by the operator: organization-less cases are platform-owned and visible only to this explicit role (BR-SG-02, `docs/security/safeguarding-scoping-policy.md`).

## 2. Shape of the validation

| Dimension | Controlled approach |
| --- | --- |
| Scale | 1–3 communities, each ≤ 50 active seniors, ≤ 30 volunteers, ≤ 5 coordinators |
| Duration | 8–12 weeks per community, with a structured mid-point and end review |
| Scope | Full coordinator wedge (Phase 2) + help & matching (Phase 3) + trust & safeguarding (Phase 4); announcements/events as org chooses |
| Data | Real personal data only after gate 1.3; until then, `simulate_pilot_day.py` synthetic activity |
| Rollback | Data lives in one Postgres instance; nightly backups per [backup-and-restore](../operations/backup-and-restore.md); a community can be deactivated (org disabled) without data loss |

## 3. What we are validating (hypotheses & measures)

| # | Hypothesis | Measure | Target |
| --- | --- | --- | --- |
| H1 | Seniors ≥ 70 can complete the core request flow alone or with family help | Task completion rate, hesitations observed in guided sessions | ≥ 80% complete a picture-card request unassisted after week 2 |
| H2 | Trust tiers reduce coordinator workload while keeping safeguarding | Time-to-verify, incidents, silent-volunteer flags | Verification per volunteer ≤ 15 min; zero unmanaged safeguarding events |
| H3 | Matching quality beats manual assignment | Assignment acceptance rate, completion rate | ≥ 70% first-choice acceptance |
| H4 | Orgs can run daily operations from the dashboards | Coordinator attention-queue triage time | ≤ 10 min/day per coordinator |
| H5 | The platform provides value with no organization present | Community-mode activity share | Measured and reported, no target (per project mission) |

Targets are calibration instruments, not gate criteria for individuals; the
mid-point review may re-baseline them.

## 4. Feedback collection

- **Weekly:** coordinator 15-minute structured call (attention queues, false positives, missing views).
- **Bi-weekly:** senior+family session, observed, in the senior's home or the community space; note every hesitation ("Give the app to someone over 70 and say nothing" ritual from the [closing ritual](../plans/BUILD-CHECKLIST.md#per-phase-closing-ritual)).
- **Continuous:** in-app report/block signals feed [block-report-policy](../security/block-report-policy.md) metrics; crash reporting aggregated per release.
- **Monthly:** impact report export (P2-31) reviewed with the org — both as funder evidence and as validation data.

## 5. Roles in validation

| Role | Responsibility |
| --- | --- |
| Operating organization | Signs DPA, appoints safeguarding lead, owns its community's operational configuration (ADR-026 gate stays OFF unless the Legal Gate passed in writing) |
| Platform maintainers | Ship fixes, keep the [incident runbook](../operations/incident-response-runbook.md) current, run mid/end reviews |
| Community coordinators | Daily routine per [playbook](../operations/pilot-onboarding-playbook.md), feed structured feedback |
| Participants (seniors/families/volunteers) | Informed consent at registration; can leave at any time (data deletion per [privacy policy](../legal/privacy-policy.md)) |

## 6. Exit / decision points

- **End of each community cycle:** written review against §3 measures + a "keep / change / stop" recommendation for that community.
- **After the first three cycles:** consolidate findings into the roadmap; propose promotion of any early-access community to a production community, or wind-down with data deletion per retention policy ([data-retention-policy.md](../legal/data-retention-policy.md)).
- Any unresolved product/security decision that blocks real usage in a community is escalated to the decision matrix instead of being worked around silently.

## 7. Non-goals

- No paid mediation, no marketplace (permanently parked, per roadmap).
- No commitment to any future partner; participation is voluntary and revocable by either side.
- No AI auto-assignment at Safety Level 3+ (ADR-014 boundary holds).
