# Matching Engine

## 1. Two stages, always

```
Stage 1 — HARD FILTERS   (a candidate either qualifies or does not; no scoring)
Stage 2 — SCORING        (rank the qualified candidates)
```

Never express a safety rule as a low score. A volunteer who lacks the required trust level
must be **absent** from the candidate set, not ranked 47th.

## 2. Stage 1 — hard filters

```
volunteer.status == Active
volunteer.is_accepting_requests
volunteer.effectiveTrustLevel   >= request.requiredTrustLevel
volunteer has Capability.PerformSafetyLevel{N}
distance                        <= volunteer.max_distance_km
no user_block in either direction
no scheduling conflict
weekly assignments              <  volunteer.max_activities_per_week
buddy rule satisfied            (see trust-safety.md §5)
organization policy allows      (if request.organization_id is not null)
volunteer.user_id               != request.subject_user_id
```

## 3. Stage 2 — scoring

```
score = w_distance      * f_distance
      + w_availability  * f_availability
      + w_skills        * f_skills
      + w_language      * f_language
      + w_trust         * f_trust
      + w_reliability   * f_reliability
      + w_continuity    * f_continuity
      - p_workload
```

Default weights — **configuration, never constants in code**:

```json
{
  "distance": 0.30, "availability": 0.25, "skills": 0.15,
  "language": 0.10, "trust": 0.05, "reliability": 0.05,
  "continuity": 0.10, "workloadPenaltyPerOpenAssignment": 0.03
}
```

Component functions:

| Factor | Definition |
| --- | --- |
| `f_distance` | `max(0, 1 - km / max_distance_km)` |
| `f_availability` | 1.0 exact slot match · 0.6 same day, adjacent slot · 0.2 flexible request · 0 otherwise |
| `f_skills` | share of the request's required skills that the volunteer holds (verified skills count double) |
| `f_language` | 1.0 if a shared language exists at B1+, else 0.2 |
| `f_trust` | `min(1, (trust - required) / 2)` — a small bonus for headroom, not a ranking driver |
| `f_reliability` | the reliability score, normalised; **new volunteers default to 0.7, not 0** |
| `f_continuity` | 1.0 if this pair has worked together before and the feedback was positive |

`f_continuity` matters more than it looks. Seniors overwhelmingly prefer the same person
again. Optimising purely for distance produces a rotating cast of strangers, which is the
opposite of what this product is for.

`f_reliability` defaulting to 0.7 avoids the cold-start trap where a new volunteer can
never get a first assignment and therefore never builds a score.

## 4. Interfaces

```csharp
public interface IVolunteerMatchingService
{
    Task<IReadOnlyList<VolunteerCandidate>> GetCandidatesAsync(
        Guid helpRequestId, int take, CancellationToken ct);
}

public interface IMatchingPolicy
{
    string PolicyKey { get; }
    MatchScore Score(VolunteerContext volunteer, HelpRequestContext request, MatchingWeights weights);
}

public sealed record VolunteerCandidate(
    Guid VolunteerUserId,
    decimal Score,
    IReadOnlyDictionary<string, decimal> Breakdown,   // explainability, always populated
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Concerns);
```

Implementations over time: `RuleBasedMatchingPolicy` (Phase 3) →
`HybridMatchingPolicy` (Phase 8) → `AiAssistedMatchingPolicy`.
The API contract never changes.

## 5. Where the work happens

```
PostgreSQL     candidate set (hard filters + distance) — set-based, indexed
     ↓
Application    scoring, explanation, policy application
     ↓
API            ranked, explained candidates
```

Do not put the scoring logic in SQL. Filtering in SQL is right (it is set reduction);
scoring in SQL is unmaintainable and untestable, and it makes swapping the policy later
impossible.

## 6. Offer strategy

Do **not** broadcast to everyone. Broadcasting produces a race, a flood of notifications,
and one winner plus nine annoyed volunteers.

```
Tier 1: top 3 candidates          → 4-hour exclusive window
Tier 2: next 7                    → if no acceptance, next 8 hours
Tier 3: all eligible in the area  → if still open
Tier 4: escalate to a coordinator → if organization-scoped
Expire: if the requested date passes
```

For an urgent request (< 24 h), compress to 30 min / 2 h / remainder.

## 7. Explainability requirement

Every candidate carries a `Breakdown`. Every match decision shown to a coordinator must
be explainable in one sentence:

> „Anna: 2,3 km entfernt, Dienstag frei, spricht Deutsch, hat Maria schon zweimal begleitet."

When AI matching arrives in Phase 8, this requirement does not relax — it tightens.
**AI proposes, a human decides.** No AI system in this product may auto-assign a
Safety-Level-3-or-above activity.

## 8. Testing

```
[ ] A volunteer one trust level short never appears — asserted at the service level
[ ] Weights are read from configuration; changing them changes the ranking in a test
[ ] Two simultaneous acceptances: exactly one succeeds
[ ] A blocked user never appears in either direction
[ ] The buddy rule cannot be bypassed by calling accept directly
[ ] A candidate list of 0 returns a helpful reason, not an empty page
[ ] Score breakdown sums to the total score (within rounding)
```
