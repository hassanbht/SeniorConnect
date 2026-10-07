# Authorization

## 1. The three-layer model

```
WHO           →  Identity        (authenticated user)
WHAT ROLE     →  Membership      (relationship to an organization / a senior)
WHAT ALLOWED  →  Capability      (a specific permitted action, computed server-side)
```

Never conflate them. "Anna is a Volunteer" is not an authorization statement.
"Anna may perform a Safety-Level-3 activity for Organization X until 2027-05-12" is.

## 2. Decision pipeline

Every protected operation runs the same pipeline, in this order:

```
1. Authenticated?                        → 401
2. Account active (not suspended)?       → 403 ACCOUNT_SUSPENDED
3. Tenant scope valid for this resource? → 404 (never 403 — do not leak existence)
4. Capability present and unexpired?     → 403 CAPABILITY_MISSING (+ how to obtain it)
5. Relationship check (family / member)? → 403 RELATIONSHIP_REQUIRED
6. Trust level >= required?              → 403 TRUST_LEVEL_INSUFFICIENT (+ what is missing)
7. Organization policy allows?           → 403 ORGANIZATION_POLICY
8. Domain invariant / state machine?     → 409 with a reason code
```

Rule: a cross-tenant miss returns **404, not 403**. A 403 confirms the resource exists.

## 3. Capabilities

```csharp
public enum Capability
{
    // Community
    CreateCommunityGroup, ManageOwnGroup, JoinPublicEvent, CreateEvent,

    // Help — seniors
    CreateHelpRequest, CreateHelpRequestOnBehalf,

    // Help — volunteers, by safety level
    PerformSafetyLevel1, PerformSafetyLevel2, PerformSafetyLevel3,
    PerformSafetyLevel4, PerformSafetyLevel5,

    // Family
    ViewSeniorActivities, ViewSeniorHelpRequests, ManageSeniorProfile,
    ReceiveSafetyNotifications,

    // Organization staff
    ViewOrganizationDashboard, ManageOrganizationVolunteers,
    ApproveVerification, AssignHelpRequest, ViewOrganizationReports,
    ManageOrganizationSettings,

    // Restricted
    AccessSafeguarding, ManageSafeguardingCase,

    // Platform
    PlatformAdmin
}
```

`PerformSafetyLevelN` is **derived**, not granted by hand:

```
PerformSafetyLevel1 ← trust >= 1
PerformSafetyLevel2 ← trust >= 2
PerformSafetyLevel3 ← trust >= 3 AND training "Safety Basics" AND (org approval OR platform review)
PerformSafetyLevel4 ← trust >= 5 AND background check verified AND org approval
PerformSafetyLevel5 ← PerformSafetyLevel4 AND named coordinator assigned
```

## 4. ASP.NET Core policies

```csharp
options.AddPolicy("SafeguardingAccess", p =>
    p.RequireAuthenticatedUser()
     .AddRequirements(new CapabilityRequirement(Capability.AccessSafeguarding)));

options.AddPolicy("OrganizationCoordinator", p =>
    p.RequireAuthenticatedUser()
     .AddRequirements(new CapabilityRequirement(Capability.AssignHelpRequest),
                      new OrganizationScopeRequirement()));
```

Resource-based authorization (`IAuthorizationService.AuthorizeAsync(user, resource, policy)`)
is used wherever the decision depends on the specific row — which is almost everywhere.

## 5. Explainable denial

A 403 must always tell the user what is missing and how to fix it. This is a product
requirement, not a nicety: an unexplained refusal in a volunteering app loses the volunteer.

```json
{
  "type": "https://SeniorConnect.at/errors/trust-level-insufficient",
  "title": "Für diese Tätigkeit fehlt noch eine Bestätigung.",
  "status": 403,
  "code": "TRUST_LEVEL_INSUFFICIENT",
  "detail": "Hausbesuche brauchen Sicherheitsstufe 4.",
  "requiredTrustLevel": 5,
  "currentTrustLevel": 3,
  "missing": [
    { "type": "BackgroundCheck", "status": "NotStarted", "actionKey": "trust.action.background_check" },
    { "type": "OrganizationApproval", "status": "Pending", "actionKey": "trust.action.await_org" }
  ]
}
```

The Flutter client renders `missing[]` as a checklist with actions. It never invents this
list locally.

## 6. What the client is never trusted with

```
userId · role · capabilities · trustLevel · safetyLevel
organizationId (when derivable from membership)
requiredTrustLevel · matchScore · isVerified
```

All of these arrive **from** the server and are never sent **to** it as authority.
A client-sent value in any of these fields is a security defect and must fail a test.

## 7. Authorization matrix (excerpt)

| Action | Senior (self) | Family +perm | Volunteer | Coordinator | Org Admin | Safeguarding Officer |
| --- | --- | --- | --- | --- | --- | --- |
| Create help request | ✓ | ✓ | ✓ (own) | ✓ (on behalf, logged) | ✗ | ✗ |
| View senior profile | ✓ | partial | after assignment only | ✓ (audited) | ✓ (audited) | ✓ (audited) |
| Accept help request | ✗ | ✗ | ✓ if eligible | ✗ | ✗ | ✗ |
| Assign a volunteer manually | ✗ | ✗ | ✗ | ✓ (reason required) | ✓ | ✗ |
| Approve a verification | ✗ | ✗ | ✗ | ✓ | ✓ | ✓ |
| Create a community group | ✓ | ✗ | ✓ | ✓ | ✓ | ✗ |
| **View safeguarding case** | ✗ | ✗ | ✗ | ✗ | **✗** | **✓** |
| Export organization report | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ |
| Export own data | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ |

The bolded row is the one everyone gets wrong. An Organization Admin managing volunteers
and events has **no** safeguarding access unless separately appointed.

## 8. Testing requirements

For every endpoint, there must be a test that:

1. an unauthenticated caller gets 401
2. an authenticated caller **without** the capability gets 403
3. a caller from another organization gets **404**
4. the happy path succeeds

Missing negative tests block a merge.
