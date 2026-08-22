# API Conventions

## Base

```
Base URL     https://api.SeniorConnect.at/api/v1
Auth         Authorization: Bearer <JWT>
Content      application/json; charset=utf-8
Language     Accept-Language: de-AT | en | fa   (affects server-rendered messages)
Time         all timestamps ISO-8601 UTC with Z
IDs          UUID v7
Idempotency  Idempotency-Key: <uuid>  on all POST/PUT/PATCH that create or change state
Tracing      X-Correlation-Id echoed in every response
```

## Naming

```
/help-requests                 kebab-case, plural nouns
/help-requests/{id}/offers     nested only one level deep
/help-requests/{id}:accept     verb actions use a colon suffix, not a nested noun
```

## Standard responses

```
200 OK          read / update
201 Created     + Location header
202 Accepted    async work started
204 No Content  delete
400 Bad Request validation
401 Unauthorized
403 Forbidden   authorized-but-not-permitted, always with a machine-readable code
404 Not Found   also used for cross-tenant misses — do NOT leak existence
409 Conflict    state machine or concurrency violation
422 Unprocessable  semantically invalid but well-formed
429 Too Many Requests
500 / 503
```

## Errors — RFC 7807 with a stable `code`

```json
{
  "type": "https://SeniorConnect.at/errors/help-already-assigned",
  "title": "Diese Anfrage wurde bereits übernommen.",
  "status": 409,
  "code": "HELP_ALREADY_ASSIGNED",
  "detail": "Jemand anderes war schneller.",
  "instance": "/api/v1/help-requests/018f.../:accept",
  "correlationId": "…",
  "errors": { "fieldName": ["message"] }
}
```

The Flutter client switches on `code`, **never** on `title` or `detail` — those are
localised and may change.

## Pagination

```
GET /events?cursor=<opaque>&limit=20

{ "items": [...], "nextCursor": "…", "hasMore": true }
```

Cursor-based, not offset. Feeds change while a user scrolls.

## Concurrency

```
GET  → ETag: "W/\"1234\""
PUT  → If-Match: "W/\"1234\""   → 412 Precondition Failed on mismatch
```

Required on: help request updates, offer acceptance, safeguarding case updates,
verification decisions. Not required elsewhere.

## Filtering & sorting

```
?status=open,matching        comma-separated enums
?nearLat=47.26&nearLng=11.39&radiusKm=10
?from=2026-09-01T00:00:00Z&to=2026-09-30T23:59:59Z
?sort=-createdAt,title
```

## Versioning

URL-based (`/api/v1`). A breaking change means `/api/v2` with a documented deprecation
window. Additive fields are never breaking; clients must ignore unknown fields.

## Non-negotiables

1. Never expose an EF entity. Request and response DTOs only.
2. Never accept `userId`, `role`, `trustLevel`, `safetyLevel`, `capabilities` from a client.
3. Every list endpoint is paginated from day one.
4. Every mutating endpoint validates with FluentValidation before touching the domain.
5. Every endpoint has an OpenAPI description, an example, and its error codes documented.
6. Every endpoint has the four authorization tests (see `authorization.md` §8).
