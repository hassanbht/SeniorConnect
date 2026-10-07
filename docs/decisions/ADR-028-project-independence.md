# ADR-028 — Project independence: from a named-partner pilot to an independent open-source project

Status: Accepted
Date: 2026-09
Amends: ADR-020 §4 (named-partner MVP lockdown), ADR-021 §1/§2.5 (partner-branded intake template)
Relates to: `00-PRODUCT-BRIEF.md`, `docs/product/roadmap.md`, `docs/plans/BUILD-CHECKLIST.md`

## Context

The v1 scope was locked down around a **single named launch partner**
(ADR-020 §4), and ADR-021 described the platform as starting with a "flagship
pilot partner" whose paper intake form seeded a partner-branded template
(`FWZ Innsbruck-Land Standard Template`).

That framing made a specific organization look like a **prerequisite** for the
project: an owner, operator and validation source. It is not. SeniorConnect is
independently developed, has no external owner, funder or operator, and its
core architecture is organization-optional and multi-tenant (ADR-002, ADR-007).
Nothing in the platform requires a particular NGO, volunteer centre,
municipality, sponsor or tenant.

At the same time, the repository holds generic capabilities that must **not** be
removed or weakened: organization onboarding, multi-tenancy, organization
profiles and verification, volunteer coordination, dashboards, reporting,
safeguarding, moderation, and tenant isolation. Pilot and early-access
deployments remain a supported, generic use of the platform.

## Decision

**SeniorConnect is an independent, open-source, production-oriented community
and volunteering platform.**

1. **Ownership and operation.** There is currently no external organization that
   owns, manages, finances, administers or officially operates the project. It
   is not the official project of any NGO, municipality, volunteer centre, pilot
   partner or funding organization.
2. **ADR-020 §4 is superseded.** The named-partner MVP lockdown ("v1 = Phases 0–4
   for one named partner: Freiwilligenzentrum Innsbruck-Land") no longer holds.
   v1 remains the Phases 0–4 core, but **no specific partner is required** for
   the project to be developed, deployed or completed.
3. **ADR-021 §1/§2.5 naming is amended.** The partner-branded intake template is
   renamed to a neutral **standard Austrian volunteer-centre intake template**.
   The authentication, geography, geocoding and custom-form decisions themselves
   are unchanged.
4. **Organizations are future possibilities, not dependencies.** NGOs,
   municipalities, volunteer centres, community organizations, research/project
   partners and deployment organizations may become implementation, early-access,
   validation or funding partners in the future — but these are possibilities,
   not current requirements.
5. **Validation is not partner-bound.** Validation may happen through
   self-hosted testing, community testing, early-access users, NGO partners,
   municipalities, volunteer centres or other organizations. No particular
   organization is required.
6. **Generic pilot/early-access capability is preserved.** Organization
   onboarding, multi-tenant configuration and early-access workflows stay in
   place. The migration removes the dependency on *one* partner, not the
   capability to support future ones.
7. **Product independence.** The platform must remain functional without a
   specific NGO, volunteer centre, municipality, sponsor or pilot tenant. No
   production configuration, seed or runtime path may require a named partner.

## Explicitly out of scope of this decision

This ADR records **only** the independence decision. It does **not** change, and
must not be read as resolving:

- **ADR-026** (visit-address sharing) — remains **Proposed / DECISION REQUIRED**;
- **ADR-027** (trust-level definition, V-3) — remains **Proposed / ratification required**;
- **S-2, S-3, S-4** (restriction notifications, coordinator awareness, org-less
  safeguarding cases) — remain unresolved;
- **B-1, B-2** (block/unassignment semantics, escalated report workflow) — remain
  unresolved.

It also does not change safeguarding policy, location consent, visit-address
sharing, trust or verification levels, restriction notifications, officer
access, block/unassign semantics, or any security, privacy or tenant-isolation
behaviour.

## Consequences

**Positive**

- The project can be understood, run and released without any external partner.
- Documentation, roadmap, configuration and seed no longer imply a partner is
  responsible for ownership, financing, management or operation.
- Generic multi-tenant and early-access capability stays intact for real future
  deployments.

**Negative, accepted**

- The concrete anchor that ADR-020 §4 provided for go-to-market is gone; the
  launch-community discussion becomes an open, future go-to-market question
  rather than a locked assumption.
- Historical documents that named the previously considered partner must be
  read as historical context, not as active dependencies.

## Alternatives considered

**Keep the named-partner lockdown.** Rejected: it presents one organization as
the project's owner/operator/validation source, which is not true, and blocks
the project from being understood as an independent open-source project.

**Remove pilot/early-access concepts entirely.** Rejected: this would delete
generic, useful capability. The goal is to remove dependency on one partner, not
support for future ones.

**Replace the named partner with a different named organization.** Rejected: it
substitutes one dependency for another and makes an unresolved business decision.
