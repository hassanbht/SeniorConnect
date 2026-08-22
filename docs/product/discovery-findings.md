# Discovery Findings

> **Status: SIMULATED, NOT VERIFIED.**
> These findings come from three constructed personas, not from real interviews.
> They are plausible and internally consistent, which makes them useful for
> shaping v1 — and dangerous if treated as fact.
>
> Every finding below carries a confidence level and a verification cost.
> Anything marked ⚠️ **must** be confirmed with a real person before it drives
> more than two weeks of engineering.

Source: `Analyze` persona responses, three roles — NGO Volunteer Coordinator,
Active Volunteer, Gemeinde Social Welfare Officer.

---

## F1 — The #1 pain is hour capture and reporting, not matching ⚠️ HIGH IMPACT

Evidence, all three roles independently:

| Role | Quote (paraphrased from the persona answers) |
| --- | --- |
| Coordinator | "09:30–11:30: manually typing last week's hours from paper and SMS into Excel." |
| Coordinator | "Q10: If you could fix one thing — automatic, hassle-free hour logging without me chasing people." |
| Coordinator | "The annual report takes two full weeks in December, from messy Excel files." |
| Volunteer | "I note it in my phone calendar and send one SMS at the end of the month." |
| Gemeinde | "Q10: A transparent, live impact dashboard without repeated letter-writing." |

**Confidence: HIGH.** Three independent roles converge on the same artefact
(the hours number) from three directions. This is the strongest signal in the set.

**Implication:** my roadmap put hour capture and impact reporting in **Phase 6**.
That is wrong. It is the wedge. See ADR-015.

**Verification cost: one phone call.** Ask any coordinator: "show me the file you
use to track hours." If they open Excel, F1 is confirmed.

---

## F2 — Only 22 of 60 volunteers are real ⚠️

> "I have about 60 volunteers on the list, but in practice only 22 are genuinely
> active and dependable. I track it with a manual column in Excel and from memory."

**Confidence: HIGH.** This pattern is near-universal in volunteer organisations.

**Implications:**

1. The matching engine's candidate pool is **one third** of what the roster says.
   Matching sophistication matters far less than roster accuracy. This further
   demotes the matching engine as a differentiator.
2. **"Who is actually active?"** is a dashboard widget, computed from behaviour,
   and it is cheap. Active / Dormant (60–120 days) / Inactive (>120 days) /
   Never activated.
3. A "reactivate dormant volunteers" flow is probably worth more than any
   scoring improvement. It is also a great retention metric for the funder report.

---

## F3 — Both previous software failures were authentication and notification failures

> Coordinator: "A heavy web portal the older volunteers couldn't operate, and the
> login kept breaking. After two months we went back to Excel and WhatsApp."
>
> Volunteer: "An app that sent a lot of spam notifications and needed a complex
> password every time I opened it. I deleted it."

**Confidence: HIGH.** Two of two prior-software questions blame login and
notifications. Neither blames missing features.

**Implications — these are concrete design changes:**

1. **Passwordless by default.** Phone number + SMS code, or a magic link.
   Email + password becomes the *optional* path for staff, not the default.
   My Phase 1 spec had email + password as primary. That is now wrong. → ADR-016.
2. **Very long sessions.** 90 days on a personal device, with biometric re-auth
   rather than a password prompt. A volunteer opening the app once a week must
   never see a login screen.
3. **A notification budget, enforced in code.** See BR-NOTIFY. Default state:
   the app sends at most 2 pushes per assignment (T-24h, T-2h) and nothing else
   without an explicit opt-in.
4. Note that the volunteers who could not use the portal were themselves **older**.
   Senior Mode is therefore not only for care recipients — it is for volunteers.
   This validates the existing decision to make it a preference, not a role.

---

## F4 — Insurance ambiguity has already caused a real dispute ⚠️

> Gemeinde: "A damage claim from a volunteer's car accident while transporting a
> person with reduced mobility. Insurance coverage was unclear and it turned into
> a dispute between the municipality and the association."

**Confidence: HIGH** that this class of incident is common. This is the single
most likely way a pilot ends badly and publicly.

**Implications:**

1. `insurance_context` moves from Phase 5 to **Phase 2**. It must be present and
   visible on the very first recorded assignment.
2. **Transport in a private vehicle is its own risk dimension**, orthogonal to my
   Safety Levels 1–5. Safety Levels model *access to a person*. Driving models
   *liability for a journey*. A public walking group is Safety Level 1 but
   driving someone to that walk is a separate, higher risk. → BR-TRANSPORT.
3. The app must show, before acceptance, in one sentence: who carries the cover
   for this activity. If the answer is unknown, say "unknown" — do not hide it.

---

## F5 — Physical keys are an unmodelled liability

> Coordinator: "A volunteer lost a senior's house key. We had to replace the locks
> for the whole building, paid by the organisation's liability insurance, and we
> completely revised how keys are handled."

**Confidence: MEDIUM-HIGH.** Specific and expensive. Nothing in my data model
touches physical keys.

**Implication:** a small `key_custody` record — who holds which key, since when,
handed over by whom, returned when. Ten lines of schema, one screen. It directly
addresses an incident that already cost this organisation real money, and it is
the kind of concrete detail that makes a coordinator believe you understand
her job.

---

## F6 — Disputes with cognitively impaired clients are predictable

> Volunteer: "A senior with dementia thought I had returned the wrong change from
> the shopping. I called the organisation immediately to mediate."

**Confidence: MEDIUM-HIGH.** Every shopping-assistance programme has this story.

**Implication — a cheap, high-value feature:** an **expense record** on shopping
activities. Amount given, amount spent, change returned, optional photo of the
receipt, both parties confirm. It is not an accounting feature; it is a neutral
record that protects the volunteer and reassures the family.

It also belongs as a safeguarding category: `Vorwurf / Missverständnis`, which is
neither ordinary feedback nor an abuse concern.

---

## F7 — The Gemeinde is a funder, not a tenant ⚠️ ARCHITECTURE CHANGE

> "We don't recruit volunteers directly, we fund the associations. We don't have
> transparent data and we don't know how many people are actually active."
>
> "Data must be fully anonymised. The municipality must never see the seniors'
> names in an analytics dashboard."

**Confidence: HIGH.**

**Implication:** I modelled Gemeinde as an `Organization` with `type = Municipality`.
That is wrong. A Gemeinde:

- owns no users
- employs no volunteers
- funds one or more organizations
- may only ever see **aggregated, anonymised** data
- reports upward to a Gemeinderat

This is a distinct scope: **Funder**. It needs its own relationship
(`funding_relationships`), its own read-only anonymised API surface, and a hard
authorization rule that no personally identifying field can cross that boundary
under any circumstances. → ADR-017, BR-FUNDER.

This is also commercially significant: the Gemeinde is the **fastest first sale**
(F9) and it buys a dashboard, not a workflow tool.

---

## F8 — The volunteer deliberately does not want health data

> "I only know the address and phone number, and I prefer to know nothing about
> illnesses so that no legal responsibility arises for me."

**Confidence: MEDIUM-HIGH.** Validates BR-GDPR-02 from the demand side, not just
the compliance side.

**Implication:** what the volunteer sees must be phrased as an **operational
instruction**, never as a condition:

```
✓ "Dritter Stock, kein Aufzug. Bitte klingeln und warten — es dauert etwas."
✗ "Eingeschränkte Mobilität nach Hüftoperation."
```

Rewrite `mobility_note` in the UI as **„Was Sie wissen sollten"** and constrain it
with examples and a character limit, so it does not become a free-text medical field.

---

## F9 — Two very different buying processes, both slow, both budget-cycle bound

| | NGO | Gemeinde |
| --- | --- | --- |
| Decider | Abteilungsleiter + finance officer | Sozialausschuss → Gemeinderat |
| Gate | annual budget + GDPR compliance review | GDPR + committee + council vote |
| Cycle | annual | annual, council calendar |
| Red line | complex signup for seniors/volunteers; GDPR risk | non-compliance, unclear insurance, **unreasonable recurring licence cost for a small municipality** |

**Implications:**

1. **A GDPR compliance pack is a sales artefact, not a legal chore.** It is
   requested in both processes. Build it as a deliverable: processing register,
   data-flow diagram, DPA template, hosting attestation, deletion policy,
   pen-test summary.
2. **Timing matters more than features.** Budget decisions happen once a year.
   Find out the pilot Gemeinde's budget calendar in the first conversation.
3. **Pricing must not be a per-seat SaaS for small Gemeinden.** A flat annual fee
   with a genuinely small floor, or a one-off setup plus low maintenance. The
   red line was explicit.

---

## F10 — Onboarding takes 2–4 weeks and nobody tracks the waiting volunteer

Coordinator: 2–4 weeks (interview, Strafregister, confidentiality agreement,
briefing). Volunteer: "about 3 weeks."

**Confidence: HIGH**, and it is stated as normal by the coordinator and as long
by the volunteer. That gap is the finding.

**Implication:** an onboarding pipeline with a **visible timer and a status the
volunteer can see**. A volunteer who hears nothing for three weeks is lost. The
feature is not automating the check — it is making the wait legible:

```
Bewerbung eingegangen        ✓ 12.09.
Gespräch vereinbart          ✓ 18.09.
Strafregister angefordert    ⏳ seit 6 Tagen
Einführung                   —
Freigabe                     —
```

---

## F11 — Verification recording is already a manual daily task

Coordinator Q6 describes exactly the `verifications` table: interview,
Strafregisterbescheinigung, signed confidentiality agreement, briefing course.
She does this on paper and files it physically (Q1, 15:00–16:30).

**Implication:** the verification *record* (not automation, not ID Austria, not
KYC) is part of the wedge, not a later trust phase. Digitising the physical
consent-form filing cabinet is a same-day win.

---

## F12 — Escalation to the coordinator is real, and it is her car

> "If nobody can be found I call 3–4 people, or in the worst case I take my own
> car and go."

Validates the Tier-4 escalation in the matching engine. Also gives the impact
report a metric she will love: **"Wie oft musste die Koordination selbst einspringen?"**

---

## What did NOT appear, and that is informative

- **Nobody mentioned matching quality.** Not once, in three roles. The coordinator's
  problem is not "I picked the wrong volunteer", it is "I could not find anyone
  and then had to type it all into Excel."
- **Nobody mentioned community groups or social activities.** The seniors were not
  interviewed, so this is expected — but it means the community pillar currently
  has **zero** evidence behind it.
- **Nobody mentioned family caregivers.** Also not interviewed. The family-led
  onboarding thesis (Brief §2.2) remains my strongest unvalidated assumption.

**These two gaps are the priority for real Phase 0 work.** Interview five seniors
and five family members before Phase 4 and Phase 5 begin.

---

## Verification plan — the cheapest possible

| Finding | How to verify | Cost |
| --- | --- | --- |
| F1 hours | "Show me the file you track hours in." | 1 call |
| F2 roster | "Of your volunteers on the list, how many did something last month?" | same call |
| F3 login | "What made people stop using the last system?" | same call |
| F4 insurance | Ask the organisation's insurance broker directly | 1 call |
| F7 funder | "What exactly does the Gemeinde see from you?" | 1 call |
| F9 buying | "When is your budget decided, and who signs?" | same call |
| Family thesis | 5 real conversations with adult children | 5 calls ⚠️ |
| Community thesis | 5 real conversations with seniors | 5 calls ⚠️ |

Twelve conversations. Two of them are load-bearing for entire phases.
