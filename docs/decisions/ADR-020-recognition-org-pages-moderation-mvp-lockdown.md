# ADR-020 — Recognition without public scores, org-only pages, message pre-moderation, MVP lockdown to the Coordinator Wedge

Status: Accepted
Date: 2026-09
Refines: vision.md, roadmap.md (Phase 5), business-rules.md §6/§9, personas.md (P4),
trust-safety.md §8, ADR-018

> **Implementation status (2026-09):** §2 (org-only group publishing,
> `BR-COMM-06`) and §3 (message pre-moderation, `BR-COMM-05`) are implemented
> in `backend/modules/Community/` — `CommunityGroup.Create` now rejects group
> creation without an `organizationId`, and a new `LocalMessageModerationService`
> screens every thread message before `GetMessagesAsync` returns it to anyone
> but its own sender. All 178 backend tests pass. §1 (private recognition
> instead of a public leaderboard) and §4 (MVP lockdown) are documentation/
> roadmap decisions, not yet reflected in a coordinator-facing screen.

## Context

Four new inputs arrived after ADR-018/019 and after `PHASE-AUDIT-2026-08.md`:

1. The founder's own new ideas (`note.txt`): points/leaderboard per city for
   volunteers and helped persons, sponsor-funded prizes for top scorers, and a
   message-moderation bot for group chat.
2. A real NGO contact ("Sebastian Mayer", not the pilot partner) declined
   deeper engagement, explicitly citing: audience reach is unproven, the
   long-term financing model is unanswered, and the GitHub scope tries to
   serve too many audiences and offerings at once — recommending one sharply
   scoped problem for one target group first (`uer-unsat.txt`).
3. **Freiwilligenzentrum Innsbruck-Land** — the actual, cooperating pilot
   partner — supplied real material: its current paper intake form
   ("Interesse für Freiwilligentätigkeit"), its GDPR consent form, its 2025
   stats (304 Freiwillige, 77 in the pool, 261 Vermittlungen, 450 Versicherte),
   and a real recruitment flyer. This partner told the founder directly they
   do not want individual users designing their own public profile pages.
4. A round of structured interview answers across coordinator, Gemeinde,
   social-worker, three volunteer, senior and family personas
   (`Antworten.txt`, `Antworten1.txt`, PDFs) — treated as **corroborating,
   not a replacement for**, the still-open live interviews P0-01–P0-04.

These four inputs conflict with existing locked decisions in ways that need a
written ruling, not a silent edit.

## Decisions

### 1. No public leaderboard, no public points. Reliability rule stands.

`trust-safety.md` §8 and `roadmap.md`'s "permanently parked" list already
reject public ratings and gamification, for a stated reason: gameable,
discourages honest cancellation, reads as a character judgement for a
vulnerable population. The new leaderboard/points idea is rejected for the
same reason and is **not** an exception.

**What ships instead — reciprocity, kept private:**

- `reliability_score` (already spec'd) stays qualitative and non-public.
- A new, **coordinator-curated, non-numeric** recognition mechanism: an
  organization's staff can privately nominate a volunteer or helped person for
  a period's "Anerkennung" (recognition) — the same social function as an
  NGO's existing "Freiwilliger des Jahres" practice, just made easy to
  produce from activity data the platform already has. No public rank, no
  visible score, no cross-city comparison.
- **Sponsors fund the recognition event/prize, not a ranking.** A sponsor can
  fund a thank-you event or a prize pool; the organization's staff decide who
  receives it, exactly as they would today for an offline Ehrenamtstag. The
  platform's only job is to hand the coordinator the activity numbers to make
  that decision informed — never to compute or publish a winner itself.

This directly answers the founder's points/sponsor idea without reopening the
rejected public-score design.

### 2. Only organizations get a public page. Individuals do not.

Per the pilot partner's explicit request: a self-service public profile/page
is available **only** to entities with an `Organization` record (an NGO
branch, the Freiwilligenzentrum itself, a Gemeinde-recognized association) —
not to individual seniors, volunteers, or family members.

An organization's page can post announcements and events — the same shape as
`https://www.freiwillig-engagiert.at/`: a public bulletin of "we are looking
for a volunteer for X" (see `FW_Suche_Luftmaschenhäkeln.pdf` for the exact
real-world content this replaces) and event listings. Individuals can browse,
register interest, and apply — they do not get a personal page, a bio, a
portfolio, or a "create your own group" flow.

**Consequence for Phase 5 ("Community"):** the pillar is renamed **"Org
Announcements & Events"** and redefined — see roadmap.md. `P4-Thomas`'s
killer feature ("create a recurring group without asking anyone's
permission") is **out of v1 scope**: an independent organizer without an
`Organization` record cannot publish a public page. His Wednesday walking
group can still exist as an org-sponsored event once an org lists it, or as a
plain `Activity`/`HelpRequest` between individuals — just not as a
self-published public group page. `personas.md` is annotated accordingly.

This is a real scope cut, not a relabelling: `BR-TENANT-01`'s `Community`
scope (visible in a geographic area, created by anyone) is **not built** in
v1. Only `Platform` (org-authored, public) and `Private`
(help-request/event-context threads) scopes ship.

### 3. Message pre-moderation is a mandatory gate, not a nice-to-have

Any feature that lets one message reach more than its author and one
recipient (an org announcement's public comments, an event's group thread —
should either ship) requires an automated content check **before** the
message becomes visible to anyone but its author:

- A **local, lightweight** classifier (not a cloud LLM call on personal
  data) screens for: scam/phishing patterns, requests for money or banking
  details (already forbidden by BR-SCOPE-02/BR-EXPENSE-02), harassment, and
  personal data a user pasted by mistake (phone numbers, addresses in a
  public context).
- A flagged message is held for a coordinator/moderator, never silently
  deleted and never silently shown — the sender sees "wird geprüft", the
  moderator sees the flag reason.
- This is a **blocking prerequisite** for Phase 4/5's public-comment or
  group-thread feature specifically, per the pilot partner's stated priority.
  It does **not** block Phase 3's one-to-one, context-bound assignment
  threads (`BR-COMM-01`), which are already low-broadcast-risk by design —
  though a lighter version of the same scam/PII check is cheap to apply there
  too and is recommended, not mandatory.

New rule: **BR-COMM-05** (see `business-rules.md`).

### 4. MVP locks down to the Coordinator Wedge for one named partner

Per the external, disinterested signal in `uer-unsat.txt` — scope is too
broad, financing is unanswered — and independent of it, per the founder's own
priority (the charity is the first supporter; the software's priority is
covering that organization's need):

- **v1 = Phase 0 through Phase 4** (Foundation, Coordinator Wedge, Help &
  Matching, Trust & Safeguarding) for **one named pilot partner:
  Freiwilligenzentrum Innsbruck-Land**. This was already `roadmap.md`'s
  sequencing (Phase 2 first); this ADR makes the audience-narrowing explicit
  and closes the door on building Phase 5+ speculatively.
- Phase 5 ("Org Announcements & Events", redefined above) and Phase 6
  (Family & Delegation) stay **gated** on real interviews (`P0-03`, `P0-04`)
  exactly as before — the interview-answer documents received
  (`Antworten.txt`, `Antworten1.txt`, the PDF sets) are useful corroborating
  material and may inform question design, but do **not** themselves satisfy
  P0-03/P0-04, because `uer-unsat.txt`'s central point — that reach and
  adoption are unproven — is a claim about live behaviour, not something a
  written Q&A pack can establish.
- General local-community features (open groups, events discovery, general
  "local offers" browsing) that overlap with **WeLocally**
  (`welocally.txt`) are **not built** in v1 or v2. If a future integration
  makes sense, it is an API/data-exchange relationship with WeLocally, not a
  parallel rebuild — this was already the founder's own stated preference to
  WeLocally's team, and it is also the cheapest way to honour Sebastian
  Mayer's "pick one problem" feedback.
- **Financing**: `go-to-market.md` §6 already has an answer (free for
  individuals forever; flat annual fee banded by active volunteers for
  organizations; flat fee for a Gemeinde/funder dashboard). This ADR adds
  nothing new to the SaaS pricing model — it only clarifies that a sponsor's
  money, if one is found, pays for **recognition/prizes** (§1 above), not for
  hosting or development, which stay on the pricing model already defined.
  State this pricing model explicitly the next time a partner org asks who
  pays long-term — it was already the answer, it just was not repeated back
  to `uer-unsat.txt`'s author.

## Consequences

**Positive:** removes a direct contradiction between new ideas and the
existing safety architecture before any code is written against the
conflicting version. Gives the pilot partner exactly the two things they
asked for (no personal pages, moderated group content) without inventing new
architecture — both slot into rules and modules that already exist
(`Organization`, `BR-TENANT`, `BR-COMM`).

**Negative, accepted:** the "senior creates a group with no organization"
feature and its Test Gate 5 assertion are dropped from v1. `P4-Thomas` needs
re-validation once real senior interviews happen — he may simply not be
representative of the pilot partner's actual population, which is fine; that
is what P0-03 is for.

## Alternatives considered

**Build the public leaderboard as an opt-in, per-organization toggle.**
Rejected: an "opt-in" public ranking still creates the discouragement and
gaming dynamics the rule exists to prevent for whichever users are opted in,
and it doubles the UI/testing surface for a feature that is permanently
parked for a documented reason.

**Let any verified user create a public page, restrict only "business-style"
profiles.** Rejected as the default: the pilot partner's own wording was
categorical ("nobody should be able to design a personal page"), and a
narrower reading is a guess this ADR should not make on their behalf. If the
partner later clarifies they meant something narrower, this ADR is cheap to
revise — file a follow-up ADR rather than silently reinterpreting theirs.
