# ADR-025 — Community Platform: allowing eligible individual users to create groups

Status: Accepted  
Date: 2026-09-21  
Amends: ADR-020 §2, §4 and `BR-COMM-06`  
Refines: `docs/product/vision.md`, `docs/product/roadmap.md`, `docs/product/business-rules.md` §9, `docs/architecture/authorization.md`, `docs/architecture/data-model.md` §3

---

## Context

Since ADR-020 was accepted, two material facts have changed:

1. **The WeLocally partnership is no longer expected.** SeniorConnect must now provide its own community, group, local discovery, event and moderation capabilities rather than relying on an external platform.
2. **The original Phase 0 interview gate was satisfied with the anchor NGO partner only.** Real senior and family interviews were not conducted in the field; instead, the needs of seniors and families were treated as a design default based on the product vision and the NGO partner's operational context. The project is now in Phase 9 (Production & Public Readiness), and the Community feature is being added as part of the production-ready scope.

ADR-020 therefore needs to be amended in one specific place: the rule that **only organizations may publish public groups/pages**. The rest of ADR-020 (no public leaderboards, no personal pages, mandatory pre-moderation, MVP lockdown to the Coordinator Wedge) remains valid — the named-partner element of that lockdown is superseded by ADR-028.

---

## Decision

### 1. Individual users may create community groups, subject to capability-based eligibility

`BR-COMM-06` is amended as follows:

> Any active user who meets the eligibility criteria may create a community group. Eligibility is determined server-side by capability and trust level, never by a client-sent value or by a fixed role such as "Senior" or "Migrant". Organization-created groups retain their verified identity and scoping advantages, but they are no longer the only permitted creators.

Eligibility rules (enforced in the domain / authorization layer):

| Requirement | Rationale |
| --- | --- |
| Account status = `Active` | Blocked/suspended accounts cannot create groups. |
| Trust level >= 1 (contact verified) | Minimum bar for publishing anything visible to others. |
| Capability `CreateCommunityGroup` present | Capability is computed server-side; may be revoked by platform moderators. |
| No active community-wide ban | A user banned from any group is temporarily ineligible to create new groups. |
| No pending safeguarding case where the user is the subject | Prevents abuse of the platform during an open investigation. |

The client is never trusted to decide eligibility.

### 2. Group types remain purpose-driven, not role-driven

The following group types are supported as stable category codes:

- `senior_community`
- `migrant_language_community`
- `interest_group`
- `learning_group`
- `family_group`
- `volunteer_group`
- `neighborhood_group`
- `organization_community`
- `support_group`

A user is never required to declare themselves a "migrant", "senior", or any other identity to create or join a group. Group type is a self-selected category for discovery, not a user role.

### 3. Safety guardrails from ADR-020 remain mandatory

The following are **not** changed by this ADR:

- No public leaderboards, points, or influencer systems.
- No personal public profile pages for individuals.
- All content visible to more than the author and one recipient is screened before publication.
- Flagged content is held for human review, never silently deleted and never silently shown.
- Safeguarding cases remain in the separate `safeguarding` schema with the `SafeguardingAccess` policy.

### 4. Organization communities keep a distinct trust signal

Organization-created groups continue to display a verified identity badge ("Organization verified" / "Von Organisation verifiziert"). Individual-created groups start as `Unverified`; platform moderators may later mark a group as `PlatformVerified` after review. The badge must always explain what was verified, never imply generic safety.

### 5. Scope of "public" visibility is clarified

A group with `Visibility = Public` is discoverable by location/category/language. Exact member locations or home addresses are never exposed. Member counts for small or sensitive groups may be displayed as ranges (e.g., "<10") to preserve privacy.

---

## Consequences

**Positive:**
- SeniorConnect can now fulfill its own community function without depending on WeLocally or any external platform.
- The platform remains useful when no organization is present, consistent with `vision.md` principle #1.
- The capability-based model aligns with `docs/architecture/authorization.md` and avoids making "migrant" or "senior" mandatory roles.

**Negative, accepted:**
- Opening group creation to individuals increases moderation surface. This is mitigated by mandatory pre-moderation, reporting, and platform moderation capabilities.
- The original Phase 5 interview gate was not satisfied with live senior/family interviews. This risk is accepted because the project is now in Phase 9 and the feature is required for production readiness; user feedback will be collected during early access and fed back into the product.

---

## Affected artefacts

The following files must be updated to reflect this ADR:

- `docs/product/business-rules.md` — amend `BR-COMM-06`.
- `docs/decisions/ADR-020-recognition-org-pages-moderation-mvp-lockdown.md` — add a note that §2 is amended by ADR-025.
- `docs/plans/active/2026-09-community-platform-analysis-and-plan.md` — remove the ADR-020 blocker.
- `backend/modules/Community/Domain/CommunityGroup.cs` — remove the `OrganizationId` mandatory check for individual creators.
- `backend/modules/Community/Infrastructure/CommunityService.cs` — add capability/trust eligibility check before group creation.
- `tests/backend/SeniorConnect.Modules.Community.Tests/GroupManagementTests.cs` — update the test that expects org-only creation.

---

## Alternatives considered

**Keep ADR-020 unchanged and build community only for organizations.** Rejected: it leaves SeniorConnect without a community layer after the WeLocally partnership ended, contradicting the product vision of being useful without organizations.

**Allow any verified user to create a group with no trust threshold.** Rejected: trust level 1 (contact verified) is the minimum bar already required for Safety Level 1 activities; publishing a public group is at least as visible as joining a public activity.
