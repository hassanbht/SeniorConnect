# Block and report policy (Phase 4)

> Status: implemented 2026-09-25. Related: BR-COMM-03 (report and block on
> every screen), BR-SG-04 (one-tap concern), `verification-and-discovery.md`,
> `safeguarding-location-policy.md`.

## 1. Blocks

Blocks already existed (`UserBlock`, `ISafetyBoundaryReader`) but were only used
for matching and accepting. Phase 4 makes them a server-side input to every
normal path that shows a person, their requests or their location.

### API

| Route | Effect |
| --- | --- |
| `POST /api/v1/trust-safety/blocks` `{ blockedUserId, reason? }` | Block. Idempotent: a second call returns the existing block (unique index `ux_user_blocks_pair`; the migration removes older duplicates first). |
| `GET /api/v1/trust-safety/blocks` | Only the blocks **I created**. Who blocked me is never listed. |
| `DELETE /api/v1/trust-safety/blocks/{blockedUserId}` | Unblock. Only the blocker can lift their own block; for anyone else it is `404`, so the other block's existence is not confirmed. |

### Reverse direction

A block is **symmetric in effect and asymmetric in control**:

- Either person having blocked the other hides both from each other.
- Only the blocker can unblock.

### Where a block applies

| Path | Effect |
| --- | --- |
| `GET /discovery/nearby-volunteers` | blocked person not listed (either direction) |
| `GET /discovery/nearby-requests` | requests of a person who blocked me, or whom I blocked, are gone |
| `GET /help-requests` (open list) | the same; the list now filters by the caller |
| `GET /help-requests/{id}` (non-party) | `404` |
| `GET /help-requests/{id}/location` | disclosure `NONE` |
| Volunteer feed, matching candidates, accept | excluded or refused (as before, now also via eligibility) |
| Live assignment | address, phone and contact are withheld **in both directions**, and exact coordinates are `NONE` (ADR-026 hardening) |
| `LocationVisibilityService` | any owner–viewer pair with a block gets `NONE`, whatever the consent |

A block created during a live assignment does not cancel it: an automatic
cancellation would leave a senior without help and nobody informed. The
coordinator sees the assignment as before and decides; the parties simply no
longer receive each other's contact details. Decision **B-1**: whether a block
should also notify the coordinator or unassign automatically.

## 2. Reports

Phase 4 builds no new moderation system. Existing conventions are reused:

| What is reported | Mechanism | Categories | Who sees it |
| --- | --- | --- | --- |
| Community content or a group member (post, comment, event, message, member, group) | `POST /api/v1/community/reports` (existing) | `CommunityReportCategory`: Spam, Harassment, Hate, InappropriateContent, Impersonation, UnsafeBehavior, Other | the reporter's own DTO; the group's managers and platform moderators in the moderation queue |
| A person in an activity context (help request, visit) | `POST /api/v1/safeguarding/concerns` (existing, BR-SG-04) | `category`, severity | safeguarding officers only (BR-SG-02); the reporter is never shown to the subject |

- **Audit.** Creating a report writes an `AuditEntry` (`CREATE_COMMUNITYREPORT`)
  through the audit interceptor; safeguarding concerns write
  `safeguarding.access_logs` (T25).
- **No disclosure.** The reported person never sees the report, its
  description or the reporter. Their moderation queue does not contain it
  (T26).
- `UnsafeBehavior` reports keep the existing `EscalatedToSafeguarding` status.
  They do not automatically create a safeguarding case (decision **B-2**).

## 3. Not in scope

A full moderation engine, appeals, and the automatic creation of safeguarding
cases.
