# Community Platform — Analysis & Implementation Plan

> Status: Draft — analysis and plan only. No code changes yet.  
> Date: 2026-09-21  
> Author: AI engineering assistant  
> Scope: Extend the existing Community module into a purpose-driven local community platform.
>
> **Status reconciliation 2026-09-30 (facts only, no new decisions):**
> §3.1's required action is already fulfilled — `ADR-025-community-platform-individual-creators`
> exists and is **Accepted** (amends ADR-020 §2/§4 and BR-COMM-06), so the individual-creator
> conflict no longer blocks this plan. §3.2's interview gate was **waived by founder
> decision 2026-09** (BUILD-CHECKLIST PHASE 5/6 headers; roadmap banners). §13's
> implementation order remains valid. Nothing here is approved for implementation until
> the owner explicitly says go — see `docs/decisions/DECISION-PACK-NEXT.md` §10.

---

## 1. Executive Summary

This plan describes how to evolve the existing `Community` module from its current MVP shape (organization-published groups + events + pre-moderated threads) into the purpose-driven community platform requested in the product brief. The goal is **not** to build a generic social network; it is to support local connection, mutual help, language exchange, and organization-led communities inside SeniorConnect's trust and safety architecture.

**Governance update (2026-09-21):**

1. **ADR-020 has been amended by ADR-025.** Eligible individual users may now create community groups under capability-based rules. See `docs/decisions/ADR-025-community-platform-individual-creators.md`.
2. **Phase positioning clarified in `AGENTS.md`:** the project is in Phase 9 (Production Deployment & Field Pilot Readiness). The Community Platform feature is being added as part of pilot readiness; the Phase 5 senior/family interview gate was satisfied through the operational context of the anchor NGO partner and the product vision, with live interviews continuing during the pilot.

Implementation can now proceed using this plan.

---

## 2. Current State of the Community Module

### 2.1 Existing entities

| Entity | Purpose | Gaps vs. request |
| --- | --- | --- |
| `CommunityGroup` | Group header (title, description, category, scope, join policy, org) | Missing: creator type, language, city/region, rules, member count as first-class, status enum, verification status, cover image support. |
| `GroupMembership` | Owner/Admin/Member + Active/Pending/Invited/Rejected/Left/Removed | Missing: `Muted`, `Banned`, `Unbanned` audit history, invitation token flow. |
| `CommunityEvent` | One-off or recurring event, optionally linked to a group | OK as a base; needs stronger group-event ownership rules and discovery integration. |
| `EventRegistration` | Going / Interested / Waitlisted / Cancelled | OK. |
| `MessageThread` / `ThreadMessage` | Contextual discussion (group/event) | Sufficient for MVP posts/comments; needs to be split into `CommunityPost` + `CommunityComment` + `CommunityAnnouncement` for clearer moderation and UX. |
| `CommunityEventOccurrenceCancellation` | Cancel a single occurrence of a recurring event | OK. |

### 2.2 Existing authorization

- `CapabilityService` already grants `CreateCommunityGroup` to every active user.
- However, `CommunityGroup.Create` rejects any group without an `OrganizationId` with `ORGANIZATION_REQUIRED` (ADR-020 / BR-COMM-06).
- Group update/approval checks are performed inside `CommunityService` by looking up the caller's membership role (`Owner` or `Admin`).
- There are **no ASP.NET Core authorization policies** for community actions yet; endpoints only call `RequireAuthorization()`.

### 2.3 Existing moderation

- `IMessageModerationService` / `LocalMessageModerationService` screens thread messages locally for: scam phrases, money/bank requests, harassment, phone numbers, IBANs, street addresses.
- Flagged messages are held (`IsFlaggedForModeration`) and only visible to their author.
- There is no confidence score, no version metadata, no audit record, and no human-escalation path for safeguarding.

### 2.4 Existing API surface

All under `/api/v1/community`:

- `POST /groups`, `GET /groups`, `GET /groups/{id}`, `PUT /groups/{id}`, `POST /groups/{id}/join`, `POST /groups/{id}/members/{userId}:approve`, `POST /groups/{id}/members/{userId}:reject`, `POST /groups/{id}:leave`
- `POST /events`, `GET /events`, `GET /events/{id}`, `PUT /events/{id}`, `POST /events/{id}:cancel`, `POST /events/{id}:cancel-occurrence`, `POST /events/{id}/register`, `POST /events/{id}/register:cancel`, `GET /my-schedule`
- `GET /threads/{contextType}/{contextId}`, `GET /threads/{threadId}/messages`, `POST /threads/{threadId}/messages`, `DELETE /threads/{threadId}/messages/{messageId}`, `POST /threads/{threadId}/messages/{messageId}:report`

### 2.5 Existing Flutter UI

- `CommunityFeedScreen` lists events with category/nearby filters.
- `EventDetailScreen`, `MyAppointmentsScreen`.
- No group creation, no group detail, no member management, no moderation UI.

---

## 3. Conflicts & Decisions Required Before Coding

### 3.1 Conflict with ADR-020

ADR-020 states:

> "only organizations get a public page. Individuals do not."
> "`BR-TENANT-01`'s `Community` scope (visible in a geographic area, created by anyone) is **not built** in v1."
> "the 'senior creates a group with no organization' feature ... is dropped from v1."

The requested feature explicitly requires individual users (senior, migrant/community, volunteer) to create communities. This is a direct conflict.

**Required action:** file a new ADR (e.g., `ADR-025-community-platform-individual-creators`) that:
- Revisits the rationale for org-only publishing in light of the WeLocally partnership ending.
- Defines eligibility criteria (trust level, capability, not role) under which individuals can create groups.
- Mandates moderation and safeguarding guardrails as prerequisites for opening group creation.
- Keeps ADR-020's safety rules (no public leaderboards, no personal pages, pre-moderation) intact.

### 3.2 Conflict with roadmap phase gating

`docs/product/roadmap.md` Phase 5:

> "Phase 5 and Phase 6 are blocked until the senior and family conversations happen."

The request asks to build Community now. Either:
- Obtain the gated interviews first, or
- Formally move Community out of the gated Phase 5 scope with a roadmap/ADR update and accept the evidence risk.

### 3.3 Naming / scope-generalization tension

ADR-018 generalized the platform beyond seniors. The request uses terms like "Senior Community" and "Migrant Community" as group types. The implementation must:
- Treat group types as categories, not user roles.
- Never make "migrant" a mandatory role or infer it from data.
- Use the existing `support_profiles` / `volunteer_profiles` model, not a new "migrant profile".

---

## 4. Proposed Domain Model

### 4.1 Entity changes

#### `CommunityGroup` (extended)

```csharp
public enum GroupType
{
    SeniorCommunity,
    MigrantLanguageCommunity,
    InterestGroup,
    LearningGroup,
    FamilyGroup,
    VolunteerGroup,
    NeighborhoodGroup,
    OrganizationCommunity,
    SupportGroup
}

public enum GroupVisibility { Public, Private, OrganizationPrivate }
public enum GroupStatus { Draft, Active, Paused, Archived, Blocked }
public enum GroupVerificationStatus { Unverified, OrganizationVerified, PlatformVerified }

public sealed class CommunityGroup : Entity, IAuditable, ISoftDeletable, IOrganizationScoped
{
    public Guid CreatorUserId { get; }
    public CreatorType CreatorType { get; } // IndividualSenior, IndividualCommunity, Volunteer, OrganizationStaff, VerifiedOrganization
    public Guid? OrganizationId { get; }
    public string Name { get; }
    public string Description { get; }
    public GroupType Type { get; }
    public string Category { get; } // stable code, backed by community_categories reference table
    public GroupVisibility Visibility { get; }
    public GroupStatus Status { get; }
    public string? City { get; }
    public string? Region { get; }
    public string? PostalCode { get; }
    public double? Latitude { get; }
    public double? Longitude { get; }
    public string? PrimaryLanguageCode { get; } // ISO 639-1, optional
    public string? CoverImageUrl { get; }
    public string Rules { get; }
    public int? MaxMembers { get; }
    public GroupVerificationStatus VerificationStatus { get; }
    // timestamps, created_by, updated_by retained
}
```

#### `GroupMembership` (extended)

Add states:

```csharp
public enum GroupMembershipStatus
{
    Pending, Active, Rejected, Left, Removed, Muted, Banned
}
```

Add `MutedUntilUtc`, `BanReason`, `InvitedByUserId`, `InvitationToken` (for invite-only groups).

#### New entities

| Entity | Responsibility |
| --- | --- |
| `CommunityCategory` | Reference table: `Code`, `NameKey`, `GroupType`, `SortOrder`, `IsActive`. |
| `CommunityRule` | Per-group rules or platform default rules: `RuleText`, `CreatedBy`, `IsDefault`. |
| `CommunityPost` | Group discussion post: `AuthorUserId`, `GroupId`, `Content`, `PostType` (`Post`/`Announcement`), `IsPinned`, `Status` (`Visible`/`PendingReview`/`Hidden`/`Removed`/`Archived`), `ModerationStatus`. |
| `CommunityComment` | Optional comments on posts. Same status/moderation fields. |
| `CommunityAnnouncement` | Could be a `PostType.Announcement` on `CommunityPost`; separate table only if announcements need distinct permissions/visibility. **Recommendation:** start with `PostType` enum. |
| `CommunityReport` | User reports against group/member/post/comment/event: `ReporterUserId`, `TargetType`, `TargetId`, `Category`, `Description`, `Status`, `ModerationDecisionId`. |
| `CommunityModerationAction` | Audit record for creator/platform moderation: `ActorUserId`, `Action`, `TargetType`, `TargetId`, `GroupId`, `Reason`, `Timestamp`. |
| `CommunityInvitation` | Invite-only group invitations: `GroupId`, `InvitedByUserId`, `InvitedUserId` or `Email`/`Phone`, `Token`, `Status`, `ExpiresAtUtc`. |
| `CommunityJoinRequest` | Could reuse `GroupMembership` with `Pending` status + `RequestedAtUtc`. **Recommendation:** add `RequestedAtUtc` to `GroupMembership` rather than a new table. |

### 4.2 Moderation abstraction

Replace `IMessageModerationService` with `ICommunityModerationService`:

```csharp
public interface ICommunityModerationService
{
    Task<ModerationResult> EvaluateAsync(ModerationContext context, CancellationToken ct = default);
}

public sealed record ModerationContext(
    string ContentType, // "post", "comment", "announcement", "event_description", "group_description"
    string Content,
    Guid? AuthorUserId,
    Guid? GroupId);

public sealed record ModerationResult(
    ModerationDecision Decision,
    double Confidence,
    string Category,
    string? Reason,
    string Version,
    DateTimeOffset EvaluatedAtUtc);

public enum ModerationDecision { Allow, Flag, HideTemporarily, RateLimit, EscalateToHuman }
```

Implementations:
- `RuleBasedCommunityModerationService` (MVP): extends current local rules, returns confidence 0.0/1.0, never escalates to safeguarding automatically.
- `AiCommunityModerationService` (future): behind the same interface; no domain/API changes needed later.

**Critical rule:** safeguarding/child safety/serious abuse/criminal/medical emergency decisions must always return `EscalateToHuman` and never be auto-acted upon by the bot.

### 4.3 Capability model for community actions

Add to `Capability` enum (or as strings in `UserCapabilities`):

- `CreateCommunityGroup`
- `ManageOwnCommunityGroup`
- `ModerateCommunityGroup`
- `CreateCommunityEvent`
- `CreateCommunityPost`
- `CreateCommunityAnnouncement`
- `ReportCommunityContent`

Eligibility (server-side only):

| Capability | Eligibility rule |
| --- | --- |
| `CreateCommunityGroup` | Active account + trust level >= 1 + no active community ban + account not suspended. |
| `ManageOwnCommunityGroup` | Active member with role `Owner` or `Admin/Moderator` of the specific group. |
| `ModerateCommunityGroup` | Same as `ManageOwnCommunityGroup`, plus platform moderators. |
| `CreateCommunityEvent` | Group owner/moderator, or organization staff for org groups. |
| `CreateCommunityAnnouncement` | Group owner/moderator only. |

The client must never decide eligibility.

---

## 5. API Changes

### 5.1 Groups

| Method | Path | Purpose | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/community/groups` | Discovery | Add query params: `city`, `region`, `nearLat`, `nearLng`, `radiusKm`, `category`, `type`, `language`, `interest`, `organizationId`, `q`, `cursor`, `limit`. |
| POST | `/api/v1/community/groups` | Create group | Requires `CreateCommunityGroup` capability. Remove org-only restriction for individual creators. |
| GET | `/api/v1/community/groups/{id}` | Group detail | Include member count, rules, my membership status, verification status. |
| PATCH | `/api/v1/community/groups/{id}` | Update group | Owner/moderator only. |
| POST | `/api/v1/community/groups/{id}:archive` | Archive group | Owner only. |
| POST | `/api/v1/community/groups/{id}:join` | Join public / request private | Existing `POST /groups/{id}/join` can be aliased or kept. |
| POST | `/api/v1/community/groups/{id}:leave` | Leave group | Existing. |
| GET | `/api/v1/community/groups/{id}/members` | List members | Privacy-aware member count; do not expose exact location. |
| POST | `/api/v1/community/groups/{id}/members/approve` | Approve pending request | Target user id in body. |
| POST | `/api/v1/community/groups/{id}/members/invite` | Invite user | Body: `userId` or `email`/`phone`. |
| POST | `/api/v1/community/groups/{id}/members/{userId}:mute` | Mute member | Duration + reason in body. |
| POST | `/api/v1/community/groups/{id}/members/{userId}:unmute` | Unmute member | |
| POST | `/api/v1/community/groups/{id}/members/{userId}:ban` | Ban member | Reason required. |
| POST | `/api/v1/community/groups/{id}/members/{userId}:unban` | Unban member | |
| POST | `/api/v1/community/groups/{id}/moderators` | Add co-moderator | Body: `userId`. |
| DELETE | `/api/v1/community/groups/{id}/moderators/{userId}` | Remove co-moderator | |

### 5.2 Posts / Announcements

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/v1/community/groups/{id}/posts` | List posts + announcements |
| POST | `/api/v1/community/groups/{id}/posts` | Create post |
| PATCH | `/api/v1/community/groups/{id}/posts/{postId}` | Edit own post / moderator hides |
| DELETE | `/api/v1/community/groups/{id}/posts/{postId}` | Remove post |
| POST | `/api/v1/community/groups/{id}/announcements` | Create announcement (moderator only) |
| POST | `/api/v1/community/groups/{id}/posts/{postId}:pin` | Pin post |

### 5.3 Events

Reuse existing `/api/v1/community/events` but enforce group ownership where `GroupId` is set. Add `GET /api/v1/community/groups/{id}/events` as a filtered view.

### 5.4 Reports

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/v1/community/reports` | Report group/member/post/comment/event |
| GET | `/api/v1/community/my-reports` | Reporter's own report history |

### 5.5 Moderation (platform / creator)

Creator moderation actions can be modeled as `POST /api/v1/community/groups/{id}/posts/{postId}:hide` etc. Platform moderation is a separate namespace:

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/v1/platform/moderation/queue` | Platform moderator queue |
| POST | `/api/v1/platform/moderation/decisions` | Human override / decision |

Safeguarding escalation remains under `/api/v1/safeguarding/concerns` (existing, restricted).

---

## 6. Authorization Changes

1. Add ASP.NET Core policies in `Program.cs`:
   - `CommunityMember`
   - `CommunityCreator`
   - `CommunityModerator`
   - `OrganizationCommunityManager`
   - `PlatformModerator`
   - Keep `SafeguardingAccess` separate.

2. Implement resource-based authorization handlers that load the group/membership and decide based on capability, not client-sent values.

3. Enforce the rule: community moderation permission does **not** grant access to `support_profiles` private data, family relationships, or safeguarding cases.

4. Add negative-authorization tests for every endpoint (401, 403, 404 cross-tenant, happy path) per `docs/architecture/authorization.md` §8.

---

## 7. Database Migration Plan

### 7.1 New migration: `AddCommunityPlatformV1`

**Tables to alter:**

- `community_groups`
  - Add `creator_type` (text/enum), `group_type` (text/enum), `visibility` (text/enum), `status` (text/enum), `city`, `region`, `primary_language_code`, `cover_image_url`, `rules`, `verification_status` (text/enum).
  - Rename `title` → `name`? **Recommendation:** keep `title` to avoid breaking existing DTOs/indexes; add `name` only if needed for consistency. Better: keep `title` and add display alias if required.
  - Add indexes on `(status, visibility, city)`, `(category)`, `(primary_language_code)`, `(organization_id, verification_status)`.
- `group_memberships`
  - Add `muted_until_utc`, `ban_reason`, `invited_by_user_id`, `invitation_token`, `requested_at_utc`.
  - Add check constraint on status values.

**Tables to create:**

- `community_categories` (reference)
- `community_posts`
- `community_comments`
- `community_reports`
- `community_moderation_actions`
- `community_invitations`

**Indexes:**

- `ix_community_posts_group_status_created`
- `ix_community_comments_post_status`
- `ix_community_reports_reporter_status`
- `ix_community_moderation_actions_group_action`
- `ix_community_invitations_token`

**Data migration:**

- Seed default `community_categories` for the nine group types.
- Map existing `GroupVisibilityScope` / `GroupJoinPolicy` values to new enums without data loss.

### 7.2 Concurrency

Add `xmin` concurrency tokens to `community_groups`, `group_memberships`, `community_posts`, `community_events` where race conditions are possible (member actions, moderation).

---

## 8. Flutter Changes

### 8.1 Feature structure

```
lib/features/community/
├── data/
│   ├── community_repository.dart
│   ├── community_moderation_repository.dart
│   └── community_report_repository.dart
├── domain/
│   ├── community_group.dart
│   ├── community_membership.dart
│   ├── community_post.dart
│   └── community_event.dart
├── application/
│   ├── community_discovery_notifier.dart
│   ├── community_group_detail_notifier.dart
│   ├── community_create_group_notifier.dart
│   ├── community_member_management_notifier.dart
│   ├── community_post_notifier.dart
│   └── community_moderation_notifier.dart
└── presentation/
    ├── pages/
    │   ├── community_discovery_page.dart
    │   ├── community_group_detail_page.dart
    │   ├── community_create_group_page.dart
    │   ├── community_members_page.dart
    │   └── community_moderation_page.dart
    └── widgets/
        ├── community_group_card.dart
        ├── community_post_card.dart
        ├── community_announcement_banner.dart
        └── community_filter_sheet.dart
```

### 8.2 Senior UX requirements

- Large tap targets (>= 48 dp, preferably 64 dp in Senior Mode).
- Simple language, one primary action per screen.
- Clear Join / Leave / Request-to-Join states.
- Large readable cards with high contrast.
- Confirmation sheets for destructive actions (leave, ban, remove post).

### 8.3 Accessibility / RTL / Responsive

- Use `EdgeInsetsDirectional`, `AlignmentDirectional`, `start/end`.
- Verify in German, English, Persian.
- Test text scales 1.0 / 1.5 / 2.0.
- Use `flutter_screenutil`, `LayoutBuilder`, `Flexible`, `Expanded`.

---

## 9. Localization Keys

Add to `assets/translations/{de,en,fa}.json` under `community.*`:

```json
{
  "community": {
    "create": {
      "title": "Gruppe gründen",
      "name_label": "Name der Gruppe",
      "description_label": "Beschreibung",
      "type_label": "Art der Gruppe",
      "visibility_label": "Sichtbarkeit",
      "language_label": "Hauptsprache (optional)",
      "city_label": "Stadt / Region",
      "rules_label": "Regeln",
      "submit": "Gruppe erstellen"
    },
    "join": "Mitmachen",
    "request_join": "Anfrage senden",
    "leave": "Nicht mehr mitmachen",
    "members": "Mitglieder",
    "moderation": {
      "report": "Melden",
      "hide": "Ausblenden",
      "remove": "Entfernen",
      "mute": "Stumm schalten",
      "ban": "Sperren",
      "reason_required": "Bitte geben Sie einen Grund an."
    },
    "event": {
      "create": "Treffen planen",
      "edit": "Bearbeiten",
      "cancel": "Absagen"
    },
    "announcement": {
      "create": "Ankündigung veröffentlichen",
      "pinned": "Wichtig"
    },
    "discovery": {
      "search_hint": "Gruppen oder Treffen suchen",
      "filter_city": "Stadt",
      "filter_language": "Sprache",
      "filter_category": "Kategorie",
      "filter_interest": "Interesse"
    },
    "status": {
      "active": "Aktiv",
      "paused": "Pausiert",
      "archived": "Archiviert",
      "blocked": "Gesperrt"
    },
    "verification": {
      "unverified": "Nicht verifiziert",
      "organization_verified": "Von Organisation verifiziert",
      "platform_verified": "Von Plattform verifiziert"
    }
  }
}
```

Run `scripts/check_locales.py` after adding keys.

---

## 10. Testing Plan

### 10.1 Domain tests

- Group creation eligibility by trust level and capability.
- Membership state machine (join, approve, reject, leave, remove, mute, ban, unban, rejoin after ban).
- Post/announcement visibility and moderation status transitions.
- Event ownership and group-event linking.

### 10.2 Authorization tests

For every new endpoint:
- 401 unauthenticated.
- 403 without capability.
- 404 cross-tenant / unrelated group.
- Happy path.

Additional:
- Creator cannot manage another group.
- Normal member cannot moderate.
- Org staff cannot access unrelated groups.
- Community moderator cannot access safeguarding.

### 10.3 Privacy tests

- Exact address not exposed in group/member APIs.
- Private group membership not leaked.
- Sensitive profile fields not included in group DTOs.
- Member count thresholds (e.g., `<10` for small sensitive groups).

### 10.4 Moderation tests

- Spam / scam rule flags content.
- Report creation and escalation to safeguarding when configured.
- Bot decision logging (decision, confidence, reason, version).
- Human override of bot decision.
- Bot never auto-resolves safeguarding cases.

### 10.5 API integration tests

- Happy paths, invalid input, unauthorized access, concurrency (`xmin` / 409).

### 10.6 Flutter tests

- Loading, empty, error states.
- Join / leave / request-to-join state changes.
- Moderation actions.
- Localization (all three locales).
- RTL layout.
- Responsive layout (small/large phone, tablet, landscape).

---

## 11. Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| ADR-020 not revised | High — implementation would violate accepted architecture. | File new ADR and get explicit approval before coding. |
| Phase gating ignored | High — building Community before senior/family interviews contradicts roadmap. | Resolve phase conflict; do not start implementation until governance is clear. |
| Scope creep into social network | Medium — adding followers, likes, feed algorithms. | Strictly enforce anti-scope list in §12. |
| Moderation bot makes sensitive decisions | High — legal/safeguarding liability. | Bot returns `EscalateToHuman` for safeguarding/child safety/serious abuse; never auto-terminates accounts. |
| Privacy leak via discovery | High — exact locations or sensitive demographics exposed. | Discovery uses approximate location; no exact addresses; no inference of nationality/residency status. |
| Performance of discovery queries | Medium — unbounded geo + text search. | Cursor pagination, composite indexes, optional PostGIS in Phase 8. |
| Breaking existing tests | Medium — `GroupManagementTests` expects org-only creation. | Update tests after ADR revision; keep existing event/thread tests green. |

---

## 12. Anti-Scope Reminder

Do **not** implement:

- Payment or marketplace.
- Advertising.
- AI matching / AI voice assistant.
- Follower system / influencer metrics.
- Complex recommendation engine.
- Full social-media feed.
- Unrestricted direct messaging.
- Complex media/video platform.
- Public leaderboards or gamification.

---

## 13. Implementation Order

Once the governance conflicts in §3 are resolved:

1. **ADR** — file `ADR-025` (or equivalent) revising individual group creation.
2. **Domain** — extend `CommunityGroup`, `GroupMembership`, add `CommunityPost`, `CommunityComment`, `CommunityReport`, `CommunityModerationAction`, `CommunityInvitation`, `CommunityCategory`.
3. **Moderation abstraction** — introduce `ICommunityModerationService` + `RuleBasedCommunityModerationService`; keep `LocalMessageModerationService` as a thin wrapper or replace it.
4. **Application layer** — update `ICommunityService` / `CommunityService` with new use cases.
5. **Authorization** — add policies, resource-based handlers, capability eligibility.
6. **Database migration** — `AddCommunityPlatformV1`.
7. **API** — add/update endpoints.
8. **Backend tests** — domain, authorization, privacy, moderation, API integration.
9. **Flutter data/domain** — repositories, models, notifiers.
10. **Flutter UI** — discovery, group detail, create group, members, posts, moderation.
11. **Localization** — de/en/fa keys.
12. **Accessibility / RTL / Responsive** validation.
13. **Security review** — audit logging, safeguarding separation, privacy checks.
14. **Documentation** — update API spec, data model, business rules if changed.

---

## 14. Acceptance Criteria (from request, mapped to reality)

| Criterion | Status / Note |
| --- | --- |
| Seniors can create eligible communities | Requires ADR-020 revision + capability rule. |
| Eligible migrant/community users can create communities | Same as above; "migrant" is not a role. |
| Organizations can create official communities | Already partially supported; extend verification badge. |
| Discover communities by location/category/language | New discovery endpoint + indexes. |
| Join public groups | Existing; extend for new membership states. |
| Private group membership requires approval | Existing; add invite-only flow. |
| Creators can manage their own groups | Add resource-based auth. |
| Moderators have limited community permissions | Add `CommunityModerator` policy. |
| Moderation cannot bypass platform security | Separate platform vs. group moderation. |
| Posts/announcements/events work | New `CommunityPost` + announcement type. |
| Users can report content | New `CommunityReport` entity + endpoint. |
| Moderation bot abstraction exists | `ICommunityModerationService`. |
| Rule-based moderation works in MVP | `RuleBasedCommunityModerationService`. |
| AI moderation can be added later | Yes, via interface swap. |
| Sensitive reports escalate to Safeguarding | Route to existing `/safeguarding/concerns`. |
| Audit logging works | Use existing `AuditEntries` + `CommunityModerationAction`. |
| German, English, Persian supported | Add keys to all three files. |
| RTL works | Use directional widgets; test Persian. |
| Responsive UI works | LayoutBuilder + screenutil. |
| Senior UX works with large text | Senior Mode + text scale tests. |
| PostgreSQL migration works | `AddCommunityPlatformV1`. |
| Unit / integration / Flutter tests pass | See §10. |
| CI passes | Update build/test pipeline if needed. |
| Documentation updated | API spec, data model, business rules, ADRs. |

---

## 15. Next Step

Governance decisions are now recorded:

- `ADR-025` amends ADR-020 for individual community creators.
- `AGENTS.md` clarifies Phase 9 scope including Community Platform.
- `business-rules.md` now contains `BR-COMM-06` for capability-based group creation.

**Next:** begin implementation in the order defined in §13, starting with the domain model changes and the `ICommunityModerationService` abstraction.
