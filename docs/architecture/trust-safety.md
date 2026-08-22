# Trust & Safety Architecture

## 1. The core principle

> **No trust without verification, and no verification without purpose.**

Verification is demanded only when the activity requires it. Asking every volunteer for a
Strafregisterbescheinigung before they can join a walking group loses most of them and
protects nobody.

## 2. Trust Engine

```
                    ┌──────────────────┐
                    │  Verifications   │
                    │  email · phone · │
                    │  identity ·      │
                    │  address · org · │
                    │  training · bg   │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │ TrustLevelService│  pure function, server-side
                    │  (recomputed on  │
                    │   every change   │
                    │   and on expiry) │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │ CapabilityEngine │  trust + training + org policy
                    └────────┬─────────┘
                             │
            ┌────────────────┼────────────────┐
            ▼                ▼                ▼
      MatchingEngine    Authorization    UI badges & hints
```

`TrustLevelService.Compute(userId)` is deterministic and side-effect free. Its output is
snapshotted to `trust_level_snapshots` so a historical decision can always be explained.

## 3. Provider abstraction

```csharp
public interface IIdentityVerificationProvider
{
    string ProviderKey { get; }
    Task<VerificationStartResult> StartAsync(Guid userId, VerificationContext ctx, CancellationToken ct);
    Task<VerificationOutcome>     GetOutcomeAsync(string externalReference, CancellationToken ct);
    bool Supports(VerificationType type);
}
```

| Phase | Registered implementations |
| --- | --- |
| 1–5 | `ManualVerificationProvider`, `OrganizationVerificationProvider` |
| 7+ | `KycProvider` (a commercial eID/KYC vendor) |
| 8+ | `IdAustriaProvider` (OIDC, after Service-Provider registration and accreditation) |

Business logic never references a concrete provider. Adding ID Austria later must be a
DI registration change plus a config flag — nothing else.

> On ID Austria specifically: becoming a Service Provider is a formal registration and
> accreditation process, not a public API key. Design for it, do not block the pilot on it.
> Verify the current requirements yourself before planning around it.

## 4. Safety Levels

| Level | Situation | Required trust | Additional |
| --- | --- | --- | --- |
| 1 | Public group activity | 1 | — |
| 2 | One-to-one in public | 2 | — |
| 3 | Accompaniment to institutions | 3 | Safety Basics training |
| 4 | Inside the person's home | 5 | Background check + org approval |
| 5 | Home visit, flagged vulnerable person | 5 | + named coordinator + buddy for first 3 |

```csharp
public interface IActivitySafetyPolicy
{
    SafetyRequirement Evaluate(SafetyEvaluationContext context);
}

public sealed record SafetyEvaluationContext(
    Guid CategoryId,
    LocationType LocationType,
    bool SubjectIsFlaggedVulnerable,
    bool IsOneToOne,
    bool InvolvesEnteringHome,
    bool InvolvesMedication,
    Guid? OrganizationId);
```

Note `InvolvesMedication`: collecting a prescription from a pharmacy is Level 3;
bringing it into the flat and leaving it on the table is Level 4; **administering** it is
blocked entirely (BR-SCOPE-02).

The evaluation is server-side. The client sends the category and context, never a level.

## 5. Buddy System

A volunteer's first three activities at Safety Level 3 or above:

```
Option A  accompanied by a volunteer with >= 10 completed activities at that level
Option B  explicit waiver by a coordinator, with a recorded reason
```

The rule is enforced in the matching service and re-checked at assignment time, so it
cannot be bypassed through a direct API call.

## 6. First Meeting Protocol

Shown to both parties before a first Level-3+ meeting:

```
Vorher   ✓ Name und Zeit bestätigt
         ✓ Treffpunkt vereinbart
         ✓ Vertrauensperson informiert (falls gewünscht)
Dabei    ✓ Check-in
Danach   ✓ Check-out
         ✓ Alles in Ordnung?   Ja / Ich möchte etwas melden
```

## 7. Safeguarding subsystem

Design constraints, all non-negotiable:

1. Separate PostgreSQL schema (`safeguarding`), separate EF DbContext.
2. A single authorization policy guards every endpoint; `OrganizationAdmin` does not imply it.
3. Never surfaced in search, exports, reports, dashboards, or notifications to non-officers.
4. Append-only notes; cases are resolved, never deleted.
5. Every read is logged to `case_access_log`.
6. The reporter's identity is stored but never shown to the case subject.
7. A concern can be raised in at most two taps from any activity context.

```
Report a Concern
      ↓
Case created (Open)  ── notify safeguarding officers only
      ↓
Assigned
      ↓
UnderReview → ActionTaken → Resolved → Closed (archived, retained)
```

## 8. Reliability, not ratings

Public five-star ratings are the wrong instrument for a vulnerable-population platform:
they are gameable, they discourage honest cancellation, and a low score reads as a
character judgement.

Instead, `reliability_score` is computed from behaviour:

```
completed / (completed + late_cancelled + no_show)
adjusted by:  average response time
              cancellation lead time (a 5-day notice is not a 1-hour notice)
              activity volume (low-N scores are shown as "noch wenig Erfahrung", not as a number)
```

Coordinators see the components. Ordinary users see a qualitative label
("zuverlässig", "neu dabei") — never a number and never a comparison.

Optional private feedback (`activity_feedback`) exists for coordinators. It is not public
and not shown to the subject by default.

## 9. What the platform must never claim

| Never say | Say instead |
| --- | --- |
| "Verified — safe" | "Identität am 12.05.2026 bestätigt" |
| "100 % sicher" | "Diese Person wurde von Caritas Tirol bestätigt" |
| "Background check passed" | "Strafregisterbescheinigung wurde von der Organisation geprüft" |
| "Hilfe ist unterwegs" (SOS) | "Rufen Sie 144 an" + the button |

## 10. Insurance

`insurance_context` on every assignment:

- `OrganizationCovered` — an organization sponsors the activity and carries the cover
- `PrivateNeighbourly` — informal neighbourly help, no cover
- `Unknown` — must be resolved before a Level-3+ assignment

Level 3+ requires `OrganizationCovered`, or an explicit acknowledged disclaimer stored with
the assignment. **Get this reviewed by an Austrian insurance broker before the pilot.**
