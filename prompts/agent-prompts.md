# Ready-to-use Agent Prompts

Copy these verbatim. They work with Codex, Claude Code, Aider or OpenCode.

---

## P0 — Bootstrap the Project Brain (run once)

```
You are the principal software architect for SeniorConnect.

Do NOT write production code.

Read AGENTS.md and every document under docs/.

Your task:
1. Identify missing product decisions.
2. Identify contradictions between documents.
3. Identify business rules that are ambiguous or untestable.
4. Produce a list of OPEN QUESTIONS.
5. Verify the MVP boundary in docs/product/roadmap.md is coherent.
6. Propose the initial bounded contexts and module list.
7. Produce an implementation order for Phase 1.

Do not invent product requirements. Where information is missing,
mark it explicitly as OPEN QUESTION.

Output:
- Product gaps
- Contradictions found
- Architecture proposal
- Risks, ranked
- Recommended Phase 1 implementation order
```

---

## P1 — Start a feature

```
We are implementing a new feature for SeniorConnect.

Before writing any code:
1.  Read AGENTS.md and the nested AGENTS.md for the affected stack.
2.  Read the relevant product and architecture documents.
3.  Confirm this feature is in the CURRENT PHASE. If not, stop and say so.
4.  Identify the affected bounded contexts.
5.  Check the ADRs.
6.  Identify the business rules that apply, by BR-* ID.
7.  Identify authorization requirements.
8.  Identify trust and safety implications.
9.  Identify database, API and Flutter changes.

Then produce an implementation plan containing:
Goal · Non-goals · Actors · User journey · Business rules · Domain changes ·
Database changes · API contract · Authorization matrix · Failure cases ·
Concurrency concerns · Audit requirements · Tests · Open questions

Do not write implementation code until the plan is internally consistent.
If the feature conflicts with existing architecture, STOP and explain.
Do not silently change architecture.
```

---

## P2 — Implement one layer

```
Implement ONLY the domain model and its unit tests for <feature>.

Do not create the API, the migration or any Flutter code yet.

Constraints:
- No EF Core references in the domain layer.
- Encode every state transition as an explicit, tested method.
- Every invariant from docs/product/business-rules.md that applies must be
  enforced in the domain, not in the application layer.
- Reference the BR-* ID in a comment where the rule is non-obvious.

When done, list which business rules you enforced and which you deliberately
left to the application layer, and why.
```

Then, in separate turns:

```
Implement the application services and authorization for <feature>.
Implement the EF configuration and the migration for <feature>.
Implement the REST API and its integration tests for <feature>.
Implement the Flutter data, domain, cubit and presentation layers for <feature>.
```

Never combine these in one prompt.

---

## P3 — Code review

```
Review the current change as a principal engineer accountable for a platform
serving vulnerable people.

Use the SeniorConnect-code-review skill.

Do NOT modify code. Produce findings only.

Classify each finding as CRITICAL / HIGH / MEDIUM / LOW.
For each: File · Problem · Why it matters in production · Minimal fix.

Pay particular attention to:
- authorization bypasses and client-supplied authority
- tenant isolation
- safeguarding data leaking into any export, log, report or notification
- business rules duplicated between the API and Flutter
- race conditions on assignment
- accessibility, localization and dark-mode regressions
```

---

## P4 — Accessibility & theming audit (run at the end of every phase)

```
Audit every screen added or changed in this phase.

For each screen report a table:
Screen | Light | Dark | de | en | fa(RTL) | scale 1.0 | 1.5 | 2.0 |
touch targets | semantics | loading | empty | error

Flag every:
- hardcoded colour
- hardcoded user-visible string
- missing locale key in any of the four files
- non-directional padding (EdgeInsets.only(left/right))
- interactive element below 48dp (64dp in Senior Mode)
- interactive element without a semantic label
- use of TextScaler.noScaling
- colour used as the only carrier of meaning

Do not fix anything yet. Produce the list first.
```

---

## P5 — Phase gate

```
We are closing Phase <N>.

Read docs/product/roadmap.md, Phase <N> and its Test Gate.

For each Test Gate item, report:
  PASS / FAIL / NOT TESTED
and for each FAIL or NOT TESTED, the smallest change that would fix it.

Then answer:
1. What did we build that was NOT in the phase scope?
2. What is in the phase scope that we did not build?
3. What technical debt did this phase create, and what will it cost later?
4. What should move from the backlog into Phase <N+1>?

Do not start Phase <N+1> work.
```

---

## P6 — When the agent starts drifting

Use this the moment an agent proposes microservices, a new state library, or "let me just
refactor this while I'm here":

```
Stop.

Re-read AGENTS.md and the relevant ADRs.

Explain:
1. Which documented decision does your proposal contradict?
2. What problem are you solving that the current design does not solve?
3. What is the smallest change that solves it WITHIN the current architecture?

If you still believe the architecture should change, write an ADR proposal.
Do not implement anything until the ADR is accepted.
```
