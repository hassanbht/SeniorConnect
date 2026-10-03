---
name: SeniorConnect-feature
description: Use when implementing or modifying any feature in the SeniorConnect repository. Enforces the plan-before-code workflow, the layer order, and the Definition of Done.
---

# SeniorConnect Feature Development

## Step 1 — Understand (no code)

Read, in this order:

```
00-PRODUCT-BRIEF.md
docs/product/vision.md
docs/product/business-rules.md
docs/product/user-journeys.md
docs/architecture/system-design.md
docs/architecture/authorization.md
docs/architecture/trust-safety.md
relevant ADRs
```

Then answer, in writing:

```
1. Which journey in user-journeys.md does this serve?
2. Which actors are involved?
3. Which business rules (BR-*) apply?
4. What are the trust and safety implications?
5. Which modules are affected?
6. What data changes?
7. What API changes?
8. What Flutter changes?
9. What is explicitly NOT in scope?
```

If the feature is not in the **current phase** (see the bottom of `AGENTS.md`), stop and
say so.

## Step 2 — Plan

Create `docs/plans/active/YYYY-MM-<feature>.md`:

```md
# Feature
## Goal
## Non-goals
## Actors
## User journey
## Business rules applied (BR-*)
## Domain changes
## Database changes (+ migration name)
## API contract (endpoints, DTOs, error codes)
## Authorization matrix
## Trust / safety implications
## Failure cases
## Concurrency concerns
## Audit requirements
## Test plan
## Open questions
```

Do not write implementation code until the plan is internally consistent and the open
questions are either answered or explicitly accepted as risks.

## Step 3 — Implement, in this order

```
1. Domain (entities, invariants, state machine) + unit tests
2. Application (use cases, authorization) + unit tests
3. Infrastructure (EF configuration, migration, repositories)
4. API (endpoints, DTOs, validation) + integration tests
5. Flutter (data → domain → cubit → presentation)
6. Localization keys in all three locale files
```

Do not skip a layer. Do not start with the UI.

## Step 4 — Verify before claiming done

```
[ ] Business rules respected and referenced by ID in the code comments where non-obvious
[ ] Authorization tested: 401 / 403 / 404 cross-tenant / happy path
[ ] Concurrency handled where two users can act on the same row
[ ] Audit entries written for state transitions
[ ] ProblemDetails with a stable code for every error path
[ ] Flutter: loading / empty / error states
[ ] Flutter: de + en + fa, LTR and RTL
[ ] Flutter: light + dark
[ ] Flutter: text scale 1.0 / 1.5 / 2.0
[ ] Flutter: semantic labels and touch target sizes
[ ] docs/plans/active/ → docs/plans/completed/
[ ] Documentation updated where behaviour changed
```

## Step 5 — Report

Summarise:
```
What changed · What you deliberately did NOT do · Assumptions made ·
Follow-ups you recommend · Anything that felt like it violated an architecture rule
```
