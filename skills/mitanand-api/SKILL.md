---
name: SeniorConnect-api
description: Use when creating or modifying an ASP.NET Core endpoint in the SeniorConnect backend. Enforces authorization, validation, error contracts and tests.
---

# SeniorConnect API

## Before writing the endpoint

```
1. Which actor calls this?
2. Which capability does it require?
3. What is the tenant/community scope?
4. Can an existing endpoint be extended instead?
5. Is this a read, a state change, or an action? (actions use :verb suffixes)
6. Does it need Idempotency-Key? (any state-changing POST does)
7. Does it need If-Match? (help requests, offers, safeguarding, verifications do)
```

## Structure

```csharp
// Endpoint: thin. Nothing else belongs here.
app.MapPost("/api/v1/help-requests/{id:guid}:accept", async (
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        AcceptHelpRequestCommand command,
        IMediator mediator,
        CancellationToken ct) =>
    {
        var result = await mediator.Send(command with { HelpRequestId = id, ETag = ifMatch }, ct);
        return result.ToHttpResult();
    })
    .RequireAuthorization("VolunteerCanAccept")
    .WithName("AcceptHelpRequest")
    .Produces<HelpRequestResponse>(200)
    .ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(412);
```

## Rules

```
✗ Business logic in the endpoint
✗ Exposing an EF entity
✗ Accepting userId / role / trustLevel / safetyLevel / capabilities from the client
✗ An unpaginated list
✗ A message string as the client's branching key — use the stable `code`
✓ FluentValidation on every request DTO
✓ RFC 7807 ProblemDetails with a stable code and, for 403, a `missing[]` array
✓ 404 (not 403) for cross-tenant misses
✓ Correlation ID in every response
```

## Error codes

Use SCREAMING_SNAKE_CASE, stable forever, documented in OpenAPI:

```
VALIDATION_FAILED · UNAUTHENTICATED · CAPABILITY_MISSING
TRUST_LEVEL_INSUFFICIENT · RELATIONSHIP_REQUIRED · ORGANIZATION_POLICY
HELP_ALREADY_ASSIGNED · INVALID_STATE_TRANSITION · CONCURRENCY_CONFLICT
CATEGORY_BLOCKED · EMERGENCY_REDIRECT · BUDDY_RULE_NOT_SATISFIED
RATE_LIMITED
```

## Required tests (all four, every endpoint)

```csharp
[Fact] Unauthenticated_Returns401
[Fact] WithoutCapability_Returns403_WithCode
[Fact] CrossTenant_Returns404          // NOT 403
[Fact] HappyPath_Returns200_AndAudits
```

Plus, where relevant: concurrent-access test, invalid-state-transition test,
idempotency-replay test.

## Finally

Update `docs/api/endpoints.md` and the OpenAPI description in the same change.
