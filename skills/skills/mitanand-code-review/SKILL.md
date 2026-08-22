---
name: SeniorConnect-code-review
description: Use to review a completed change in SeniorConnect before merge. Produces a classified findings list without modifying code.
---

# SeniorConnect Code Review

Review as a principal engineer who is accountable for a platform serving vulnerable people.

**Do not modify code during the review.** Produce findings first.

## Read first

```
AGENTS.md (root + the relevant nested one)
docs/product/business-rules.md
docs/architecture/authorization.md
docs/architecture/trust-safety.md
the plan in docs/plans/active/ for this change
```

## Review dimensions, in priority order

```
 1. Trust & safety     Can an unqualified person reach a vulnerable person through this?
 2. Authorization      Any bypass? Client-supplied authority? Missing negative tests?
 3. Tenant isolation   Can org A see org B? Is the query filter present?
 4. Safeguarding leak  Does restricted data reach any export, log, report or notification?
 5. Privacy            Personal data in logs? Documents stored? Health data introduced?
 6. Architecture       Module boundary violations, logic in controllers or widgets
 7. Duplicated rules   Same business rule in both the API and Flutter
 8. Concurrency        Two users, same row, same second — what happens?
 9. Validation         Missing, or client-side only
10. Error contract     Stable code? ProblemDetails? Does a 403 explain what is missing?
11. Audit              State transitions and sensitive reads recorded?
12. Accessibility      Touch targets, semantics, text scale, colour-only meaning
13. Localization       Hardcoded strings, missing locale keys, RTL breakage
14. Dark mode          Hardcoded colours, unreadable states
15. Tests              Negative authorization tests present? Business rules covered?
16. Performance        N+1, unpaginated list, missing index
```

## Output format

For each finding:

```
[CRITICAL|HIGH|MEDIUM|LOW]  <one-line title>
File:        path:line
Problem:     what is wrong
Why:         what happens in production if this ships
Minimal fix: the smallest change that resolves it
```

Severity guide:

```
CRITICAL  a safety, authorization, tenant-isolation or safeguarding-leak defect
HIGH      a business-rule violation, data-loss risk, or missing negative test
MEDIUM    an architecture violation, missing accessibility, missing localization
LOW       style, naming, minor duplication
```

Do not propose a refactor of unrelated code. Do not rewrite working code because you would
have written it differently. Every CRITICAL and HIGH must be resolved before merge.
