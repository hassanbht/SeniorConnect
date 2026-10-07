# P2-25 redefinition: Organization page, built in Flutter (web + mobile)

Status: Approved for planning
Date: 2026-09-11
Supersedes: `P2-25 — Staff web app shell` framing in `docs/plans/BUILD-CHECKLIST.md` (line ~495)

## Problem

`BUILD-CHECKLIST.md` marks `P2-25` as an `L`-sized, unstarted gap: "Blazor or
React" staff web app shell. No such project exists anywhere in the repo.

The actual near-term need, per the product owner, is narrower and concrete:
a per-organization page where an org's staff/coordinator can publish news
and events, and any logged-in app user can view that org's page by tapping
the organization. It must work both as a web page and inside the mobile
app — not two separate frontends.

## Decisions

1. **No new frontend project.** Add the `web` platform to the existing
   `mobile/senior_connect` Flutter app (`flutter create --platforms=web .`).
   One codebase, two build targets (mobile app + browser build for staff on
   desktop). This replaces the "Blazor or React" framing in the checklist.
2. **No new content entity.** "News" and "Events" are both
   `CommunityEvent` (`backend/modules/Community/Domain/CommunityEvent.cs`),
   which already has `OrganizationId`, `Title`, `Description`, `Category`,
   `StartsAtUtc`/`EndsAtUtc`, and is already filterable by
   `organizationId` via `GET /api/v1/community/events`. A "news" post is a
   `CommunityEvent` with `Category == "news"`; the client hides date/RSVP
   chrome for that category. No schema change, no migration.
3. **Visibility**: the entire `/api/v1/community` endpoint group already
   requires authentication (`RequireAuthorization()`). "Only visible to
   logged-in users" is already satisfied — no change needed.
4. **Security gap found and must be closed as part of this work**:
   `CommunityService.CreateGroupAsync`, `CreateEventAsync`, and
   `UpdateGroupAsync` accept any `organizationId` from any authenticated
   caller with no check that the caller actually staffs that organization.
   This contradicts `BR-COMM-06` ("only an Organization may publish") and
   must be fixed before a staff posting UI ships, or any logged-in user
   could post as any organization.

## Non-goals

- Coordinator operational dashboard (volunteer roster, hours confirmation
  queue, "needs attention today" widget — `P2-26`/`P2-27`/`P2-28`). Those
  remain separate, later tickets. This spec only covers the org
  news/events page.
- `CommunityGroup` (membership-based clubs) — out of scope. Only
  `CommunityEvent` is touched.
- Public/anonymous access. This is an authenticated-app feature only, not
  a public marketing page.
- Rich media, comments, or reactions on news/events.

## Backend changes

All changes live in `backend/modules/Community/` and
`backend/modules/Organizations/`, following the existing cross-module
pattern already used by `Matching` (`ProjectReference` to another module +
inject a `Contracts` interface — see
`backend/modules/Organizations/Contracts/OrganizationsContracts.cs`).

### 1. Cross-module reference

Add to `backend/modules/Community/SeniorConnect.Modules.Community.csproj`:
```xml
<ProjectReference Include="..\Organizations\SeniorConnect.Modules.Organizations.csproj" />
```

### 2. Authorization check (closes the security gap)

Inject `IOrganizationCoordinatorReader`
(`backend/modules/Organizations/Contracts/OrganizationsContracts.cs`,
implementation `OrganizationCoordinatorReader`) into `CommunityService`.

In `CreateEventAsync`, `UpdateEventAsync` (new), `CancelEventAsync` (new),
and `CreateGroupAsync`/`UpdateGroupAsync` (existing, currently
unprotected): when `request.OrganizationId` is set, call
`GetActiveCoordinatorUserIdsAsync(organizationId, ct)` and require
`callerUserId` is in the result. Otherwise return
`Error.Forbidden("Only an active coordinator or admin of this organization may publish on its behalf.")`.
This reuses the exact reader already built for the `P3-13` escalation path
— no new interface needed.

### 3. Event edit/cancel (currently missing from the service)

`CommunityEvent.Cancel(reason, hostUserId)` already exists on the domain
but is never called from `ICommunityService`/an endpoint. Add:

- `CommunityEvent.UpdateDetails(title, description, category, locationAddress, locationPostalCode, capacity, updatedByUserId)` —
  a new small domain method, same shape as the existing `Reschedule`.
- `ICommunityService.UpdateEventAsync(Guid eventId, Guid userId, UpdateCommunityEventRequest request, CancellationToken ct)`
- `ICommunityService.CancelEventAsync(Guid eventId, Guid userId, string reason, CancellationToken ct)`
- Endpoints in `CommunityEndpoints.cs`:
  `PUT /api/v1/community/events/{id:guid}`,
  `POST /api/v1/community/events/{id:guid}:cancel`
  (mirrors the existing group update / event register endpoint shapes).
- Both go through the same authorization check as #2 (organizer must be
  the event's org staff, or the original `HostUserId`).

### 4. "Which org do I staff" endpoint

No existing endpoint tells a client which organization(s) the current
user administers. `OrganizationCoordinatorReader` only answers the
reverse question (org → coordinator ids). Add the mirror query:

- `IOrganizationCoordinatorReader.GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct)`
  returning `(Guid OrganizationId, MembershipRole Role)[]`, filtered to
  `Status == Active`.
- Endpoint: `GET /api/v1/me/organizations` (new file or added to
  `ProfileEndpoints.cs`'s `/api/v1/me` group) → list of
  `{ organizationId, organizationName, role }` for orgs where the caller
  is `Coordinator` or `Admin`. Used by the client to decide whether to
  show a "manage this org" entry point at all — the server-side check in
  #2 remains the real gate.

### 5. Org header data

`GET /api/v1/organizations/{id}` already exists
(`OrganizationEndpoints.cs`) and returns `OrganizationDto` (name, legal
name, type, status, contact). Reused as-is for the page header — no
change.

## Frontend changes (Flutter)

### Platform

Run `flutter create --platforms=web .` inside `mobile/senior_connect`.
Verify the API client's base URL / CORS works from a browser origin
(check `backend/SeniorConnect.Api/Program.cs` CORS policy — add the local
web dev origin if missing).

### Screens

1. **`organization_profile_screen.dart`**
   (`lib/features/organizations/presentation/`) — view-only, any
   authenticated user. Header: org name/type/contact from
   `GET /organizations/{id}`. Body: two sections, "News" (events with
   `category == "news"`) and "Events" (everything else), both from
   `GET /community/events?organizationId={id}`, reusing the card layout
   already in `community_feed_screen.dart`. Tapping an item opens the
   existing `event_detail_screen.dart` unchanged.
   New route: `AppRoutes.organizationProfile = '/organizations/:id'` in
   `app_router.dart`.
2. **Entry points into the new screen**: from `GET /organizations` (org
   directory — new lightweight list screen if one doesn't already exist)
   and from `community_feed_screen.dart` (each event card can show its
   org name as a tappable chip → org profile). Both are additive; no
   existing screen behavior changes.
3. **`organization_post_form_screen.dart`**
   (`lib/features/organizations/presentation/`) — staff-only. One form
   for both news and event (a category toggle switches whether the
   date/time and capacity fields are shown). Calls
   `POST /community/events` (create) or `PUT /community/events/{id}`
   (edit) / `POST /community/events/{id}:cancel`. Reachable only from the
   org profile screen, and only when `GET /me/organizations` lists the
   current user as staff for that org. The 403 from the server (if the
   client's local state is stale) surfaces as a normal error toast — the
   server call, not the client check, is the actual authorization
   boundary.
4. **Responsive layout**: both screens use `LayoutBuilder` to switch from
   a single-column mobile layout to a two-column layout above ~840px
   (matches Material breakpoint conventions already implicit in
   `app_tokens.dart`). No separate desktop-only screen files.

## Data flow

```
Staff (web or mobile) → organization_post_form_screen
  → POST/PUT /api/v1/community/events[...]
  → CommunityService checks IOrganizationCoordinatorReader
  → 403 if not active Coordinator/Admin of that org, else saved

Any user (web or mobile) → organization_profile_screen
  → GET /api/v1/organizations/{id}            (header)
  → GET /api/v1/community/events?organizationId={id}  (news + events list)
```

## Error handling

- Server 403 on unauthorized publish attempts (new — closes the gap).
- Server 404 (`Error.NotFound`) for a deleted/nonexistent org id or event
  id, per the existing cross-tenant convention noted in `Result.cs`
  ("cross-tenant misses use NotFound, never Forbidden").
- Client: standard `AppEmptyState`/`AppLoading` widgets already used in
  `community_feed_screen.dart`, reused as-is.

## Testing

Backend (`tests/backend/SeniorConnect.Modules.Community.Tests/`):
- A non-member cannot create/update/cancel an event for an org (403).
- An `Active` `Coordinator` or `Admin` of the org can.
- A `Suspended` or `Invited` (not yet `Active`) membership cannot.
- `GET /community/events?organizationId=` returns only that org's events,
  correctly split by category for the news/events UI assumption.
- `GET /me/organizations` returns only `Active` `Coordinator`/`Admin`
  memberships.

Flutter:
- Widget test: the "manage" entry point on `organization_profile_screen`
  is hidden when `/me/organizations` doesn't include the viewed org, and
  shown when it does.
- Widget test: `organization_post_form_screen` toggles date/capacity
  fields correctly between the news and event categories.
- One integration smoke test running the web build, confirming the org
  profile screen loads and renders the events list.

## Rollout phases (input to the implementation plan)

A. Backend authorization fix + `UpdateEventAsync`/`CancelEventAsync` +
   `GET /me/organizations` — this alone is worth shipping first, it fixes
   a real security hole independent of any UI.
B. Enable Flutter web platform, verify CORS/build.
C. `organization_profile_screen` (view-only) + org directory entry point.
D. `organization_post_form_screen` (staff create/edit/cancel).
E. Tests above, then tick `P2-25` in `docs/plans/BUILD-CHECKLIST.md` and
   update the "MISSING" note at the top of that file.

## Open risks

- CORS/base-URL config for the web build is unverified — Phase B should
  confirm before building screens on top of it.
- No existing "org directory" screen was found in the mobile app; Phase C
  may need a minimal list screen (`GET /organizations`) in addition to
  the profile screen itself, sized as part of that phase, not a new spec.
