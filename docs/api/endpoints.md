# Endpoints (target surface, by phase)

## Phase 1 — Identity & Profiles

```
POST   /auth/register
POST   /auth/login
POST   /auth/refresh
POST   /auth/logout
POST   /auth/verify-email          { token }
POST   /auth/request-phone-code
POST   /auth/verify-phone          { code }
POST   /auth/forgot-password
POST   /auth/reset-password

GET    /me
PATCH  /me
GET    /me/trust                    → level, verifications, what is missing and how to get it
GET    /me/capabilities
DELETE /me                          → deletion request workflow

GET    /me/senior-profile
PUT    /me/senior-profile
GET    /me/volunteer-profile
PUT    /me/volunteer-profile
GET    /me/availability
PUT    /me/availability

GET    /reference/interests
GET    /reference/languages
GET    /reference/skills
GET    /reference/help-categories
```

## Phase 2 — Community

```
GET    /groups?nearLat&nearLng&radiusKm&interests&scope&cursor
POST   /groups
GET    /groups/{id}
PATCH  /groups/{id}
POST   /groups/{id}:archive
POST   /groups/{id}/members            join or request to join
DELETE /groups/{id}/members/{userId}
POST   /groups/{id}/members/{userId}:approve

GET    /events?from&to&nearLat&nearLng&radiusKm&groupId&cursor
POST   /events
GET    /events/{id}
PATCH  /events/{id}
POST   /events/{id}:cancel             { reason, scope: occurrence|series }
POST   /events/{id}/registrations
DELETE /events/{id}/registrations/{userId}

GET    /me/activities?from&to          the "Meine Aktivitäten" feed
```

## Phase 3 — Help & Matching

```
POST   /help-requests                  → 201, or 200 with a referral payload if blocked
GET    /help-requests?status&scope&cursor
GET    /help-requests/{id}
PATCH  /help-requests/{id}             If-Match required
POST   /help-requests/{id}:cancel      { reasonCode, note }
POST   /help-requests/{id}:complete

GET    /help-requests/{id}/candidates  coordinator only; includes score breakdown
POST   /help-requests/{id}/offers      create offers to specific volunteers
POST   /help-requests/{id}:accept      volunteer accepts; atomic; If-Match required
POST   /help-requests/{id}:decline
POST   /help-requests/{id}:assign      coordinator manual assignment; reason required

POST   /help-requests/{id}/checkin     { lat, lng, accuracy }
POST   /help-requests/{id}/checkout    { lat, lng }

GET    /volunteer/feed?nearLat&nearLng&radiusKm&onlyEligible=true&cursor
GET    /me/hours?from&to

POST   /feedback                       { sourceType, sourceId, sentiment, note }
```

## Phase 4 — Family

```
POST   /family/invitations             { seniorIdentifier | createSeniorAccount }
POST   /family/invitations/{id}:accept
GET    /family/relationships
GET    /family/relationships/{id}/permissions
PUT    /family/relationships/{id}/permissions      senior (or authorised staff) only
DELETE /family/relationships/{id}

POST   /family/senior-accounts         family-led account creation → returns an access code
GET    /family/seniors/{id}/activities
GET    /family/seniors/{id}/help-requests

GET    /me/trusted-contacts
PUT    /me/trusted-contacts
GET    /me/access-log                  "who saw my data", in plain language
```

## Phase 5 — Trust & Safeguarding

```
GET    /verifications
POST   /verifications                  { type } → starts a flow
POST   /verifications/{id}:submit
POST   /verifications/{id}:approve     staff; reason required
POST   /verifications/{id}:reject      staff; reason required

GET    /trainings
POST   /me/trainings/{id}:complete

POST   /blocks                         { userId, reason }
DELETE /blocks/{userId}

# Restricted — policy "SafeguardingAccess"
POST   /safeguarding/concerns          { subjectUserId, category, severity, description }
GET    /safeguarding/cases?status&cursor
GET    /safeguarding/cases/{id}
POST   /safeguarding/cases/{id}:assign
POST   /safeguarding/cases/{id}/notes
POST   /safeguarding/cases/{id}/actions
POST   /safeguarding/cases/{id}:resolve
```

## Phase 6 — Organizations

```
GET    /organizations/{id}
PATCH  /organizations/{id}
GET    /organizations/{id}/branches
POST   /organizations/{id}/branches
GET    /organizations/{id}/members
POST   /organizations/{id}/members
PATCH  /organizations/{id}/members/{userId}      role changes
GET    /organizations/{id}/policies
PUT    /organizations/{id}/policies

GET    /organizations/{id}/dashboard             "needs attention today"
GET    /organizations/{id}/volunteers?status
GET    /organizations/{id}/verification-queue
GET    /organizations/{id}/reports/impact?from&to
POST   /organizations/{id}/reports/impact:export { format: pdf|csv|xlsx }
GET    /organizations/{id}/audit?subjectType&subjectId&cursor
```

## Phase 7 — Platform

```
GET    /notifications?cursor
POST   /notifications/{id}:read
GET    /me/notification-preferences
PUT    /me/notification-preferences
POST   /devices                        push token registration
DELETE /devices/{id}

GET    /me/consents
POST   /me/consents                    { type, version, granted }
POST   /me/data-export                 → 202, delivered asynchronously
GET    /me/data-export/{id}

GET    /health · /health/ready · /health/live
```

## Phase 4 — Verification, discovery, blocks, safeguarding restrictions (implemented)

```
GET    /me/verification                        badges: what was verified, by whom, when
GET    /me/discoverability
PUT    /me/discoverability                     { isDiscoverable }

POST   /organizations/{orgId}/volunteers/{userId}/verifications
                                               staff of orgId; { type: ADDRESS|ORGANIZATION,
                                               method: DOCUMENT_SIGHTED|ORGANIZATION_RECORD|
                                               ORGANIZATION_MEMBERSHIP, validUntilUtc? }
POST   /organizations/{orgId}/verifications/{id}:revoke    { reason }

GET    /me/location-sharing                    Phase 3 consent (NONE/APPROXIMATE/PRECISE)
PUT    /me/location-sharing                    { level, version }

POST   /trust-safety/blocks                    { userId, reason? }  idempotent
GET    /trust-safety/blocks                    own blocks only
DELETE /trust-safety/blocks/{blockedUserId}    404 whether missing or reverse-blocked

POST   /safeguarding/concerns                  any authenticated user (BR-SG-04)
GET    /safeguarding/cases?organizationId       officer only
GET    /safeguarding/cases/{id}                 officer only, per-read audit
POST   /safeguarding/cases/{id}/notes           officer only
POST   /safeguarding/cases/{id}:assign          officer only
POST   /safeguarding/cases/{id}:close           officer only
GET    /safeguarding/cases/{id}/restrictions    officer only
POST   /safeguarding/cases/{id}/restrictions    { counterpartUserId?, blocksDiscovery,
                                               blocksLocation, blocksContact,
                                               blocksAssignment }
POST   /safeguarding/restrictions/{id}:lift     { reason }
```

Contract notes (full audit: `docs/security/phase5-security-gate.md` §5):

- No endpoint accepts a client-specified `verificationLevel`, `trustLevel`,
  `safetyLevel`, `organizationVerified` or `addressVerified`; all such state is
  computed server-side from recorded facts.
- Discovery/location disclose at most locality and approximate distance
  (BR-GEO-06); exact coordinates require the Phase 3 `PRECISE` consent and a
  live assignment.
- Errors are ProblemDetails with stable codes
  (`SAFEGUARDING_OFFICER_REQUIRED`, `VERIFICATION_TYPE_INVALID`,
  `VERIFICATION_METHOD_INVALID`, `VERIFICATION_NOT_A_MEMBER`,
  `LOCATION_LEVEL_INVALID`, …).
- Every safeguarding read/write writes a `SafeguardingAccessLog` row (BR-SG-06);
  case data never appears outside officer surfaces (BR-SG-05).
