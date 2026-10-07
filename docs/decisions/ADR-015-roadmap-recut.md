# ADR-015 — Re-cut the roadmap around the coordinator wedge

Status: Accepted
Date: 2026-08
Supersedes the phase ordering in the first version of `docs/product/roadmap.md`.

## Context

The original roadmap sequenced: Foundation → Community → Help & Matching →
Family → Trust → **Organizations & Impact Reporting (Phase 6)** → Hardening.

Discovery (`docs/product/discovery-findings.md`, F1) shows all three interviewed
roles converging on the same pain from three directions:

- the coordinator spends two hours a week retyping hours into Excel and two weeks
  each December building the annual report
- the volunteer's own answer to "fix one thing" is a simple calendar plus
  effortless logging
- the funder's answer to "fix one thing" is a live impact dashboard

Nobody mentioned matching quality. Nobody mentioned community groups. The
features I had placed first have the least evidence behind them; the feature I
had placed sixth is the only one three independent roles asked for.

Two further findings force movement:

- F2: only ~⅓ of a roster is genuinely active, so matching sophistication is
  worth less than roster accuracy
- F4: an insurance dispute has already occurred in the wild, so
  `insurance_context` cannot wait until Phase 5

## Decision

Re-cut the phases so that the **coordinator wedge** — roster truth, activity
logging, hours, verification records, and the impact report — becomes Phase 2,
immediately after Foundation.

```
1  Foundation                   (revised: passwordless, see ADR-016)
2  Coordinator Wedge            ← was Phase 6
3  Help & Matching
4  Trust & Safeguarding         ← was Phase 5, now before Community
5  Community
6  Family & Delegation          ← was Phase 4
7  Pilot Hardening
8  Scale & Intelligence
```

Rationale for the individual moves:

- **Wedge to 2** — it is the only evidenced pain, it produces the artefact that
  gets a pilot partner to sign, and it generates the real data that everything
  later depends on.
- **Trust before Community** — once Phase 3 creates real assignments between real
  people, verification recording and safeguarding cannot be four phases away.
  The coordinator already performs these checks manually (F11); we are digitising
  an existing process, not inventing one.
- **Community and Family later** — both currently have **zero** interview
  evidence. Seniors and family members were not interviewed. Building them now
  would be exactly the assumption-driven development Phase 0 exists to prevent.

This is a **sequencing** change, not a change of vision. The org-optional thesis
(ADR-007) stands: nothing in the wedge makes an organization mandatory for the
Community or Help modules that follow.

## Consequences

Positive:

- A sellable artefact exists at the end of Phase 2 instead of Phase 6.
- Real usage data arrives roughly four months earlier, so Community and Family
  can be designed against evidence rather than assumption.
- The riskiest legal exposure (insurance, verification records) is handled early.

Negative, and accepted:

- Phase 2 delivers value primarily to organizations, which temporarily makes the
  product look org-centric. Mitigation: the data model keeps `organization_id`
  nullable throughout, and an architecture test asserts it.
- The senior-facing app is thinner for longer. Accepted, because seniors are not
  the first buyer and there is currently no evidence about what they want.
- Some Phase 2 UI (the coordinator dashboard) is web, which means the staff web
  app starts earlier than planned.

## Alternatives considered

**Keep the original order.** Rejected: it front-loads the two pillars with no
supporting evidence and back-loads the one with three independent sources.

**Pivot fully to volunteer-management SaaS.** Rejected: that is a crowded market
with established incumbents, and it abandons the differentiator (the community
layer and the trust model). The wedge is a way in, not the product.

**Build the wedge as a throwaway prototype.** Rejected: the hour and verification
records are exactly the data the rest of the system needs. There is nothing to
throw away.
