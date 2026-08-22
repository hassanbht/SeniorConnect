# SeniorConnect — Product Brief

> Status: v1.0 — Foundation document. Everything else in this repository derives from this file.
> Owner: Hassan (Architect / Product Owner)
> Last reviewed: 2026-08

---

## 1. One-line definition

**SeniorConnect is a trusted local platform where seniors, families, volunteers and social
organizations organise everyday help and shared activities — safely and verifiably.**

German elevator pitch (for Gemeinde / NGO / funding conversations):

> SeniorConnect ist eine digitale Plattform für Nachbarschaftshilfe und soziale Teilhabe.
> Ältere Menschen, Angehörige, Freiwillige und soziale Organisationen finden
> zueinander — mit überprüfter Identität, klaren Sicherheitsstufen und
> nachvollziehbarer Dokumentation.

---

## 2. What changed vs. the original idea (and why)

The five ideation documents contain a strong product, but they contradict themselves in
three places and miss three market realities. This brief resolves all six.

### 2.1 Contradiction: org-centric vs. org-optional

Documents 01–03 treat Caritas/Rotes Kreuz as the centre of the product.
Document 04 says the platform must work with no organization at all.

**Resolution — "Community-first core, organization-ready architecture, org-led distribution."**

| Layer | Decision |
| --- | --- |
| Domain model | Organization is **optional** (`OrganizationId` nullable, `Scope` concept). Nothing in the core breaks if no org exists. |
| Product value | A senior + a neighbour + a walking group must be a complete, useful experience. |
| Go-to-market | You still **launch with one anchor partner** (a Gemeinde, a Pfarre, a Seniorenbund chapter, or one NGO branch), because that is the only realistic way to get the first 100 users. |

The architecture is org-optional. The **launch** is not. Those are different questions and
the ideation documents kept confusing them.

### 2.2 Missing reality #1 — Seniors do not download apps

This is the single biggest risk to the whole project and none of the source documents
address it.

**The acquisition channel is the daughter, not the senior.**

The real funnel:

```
Adult child (45–60, smartphone-native, worried about parent)
        ↓ installs the app, creates the senior's account
Senior receives a pre-configured phone / tablet / simplified account
        ↓
Senior uses only "Senior Mode" — 4 buttons, no settings, no onboarding
```

Product consequences (all are now in the roadmap):

- **Family-led onboarding is a Phase 1 feature, not Phase 4.** The family member must be
  able to create and fully configure a senior's account from their own device.
- The senior's first screen after install must be **already populated** — name, contacts,
  groups already there. Empty states kill this product.
- A **paper/QR onboarding card** (printed by the Gemeinde or NGO) that a senior scans is a
  real, required artefact.
- A **web view for families** (PWA) matters more than a family mobile app.

### 2.3 Missing reality #2 — The legal line around care work

If SeniorConnect becomes a place where people arrange paid care work, it enters
Gewerbeordnung / Hausbetreuungsgesetz / Sozialbetrugsbekämpfung territory and
becomes uninsurable and unsellable.

**Hard product rule, enforced in the domain layer:**

```
SeniorConnect mediates NON-MEDICAL, NON-NURSING, UNPAID everyday help
and social activities only.
```

Explicitly out of scope forever (blocked at category level, not just discouraged):

- Körperpflege, Medikamentengabe, Wundversorgung, Injektionen
- Heben/Transfer of a person
- Anything billed as a service between two private individuals
- 24-Stunden-Betreuung mediation

The app's response when a user requests these is **referral, not matching**:
"Dafür brauchen Sie professionelle Pflege — hier sind die Stellen in Ihrer Nähe."

This is not a limitation. For an NGO buyer, having this boundary *codified* is a selling
point, because their liability officer will ask about it in the first meeting.

### 2.4 Missing reality #3 — Volunteer insurance

Austrian NGOs carry Freiwilligenversicherung for their volunteers. An independent
volunteer arranged through your platform has none.

Product consequence:

- `HelpRequest` carries an **`InsuranceContext`** field: `OrganizationCovered`,
  `PrivateNeighbourly`, `Unknown`.
- For anything above Safety Level 2, the platform requires an organizational sponsor
  (who provides the insurance) OR shows an explicit, logged disclaimer.
- This is a Phase 5 hard gate before any real pilot with real people.

*You must confirm the exact requirements with an Austrian insurance broker and a lawyer.
Treat every legal statement in this repository as a starting point for that conversation,
not as legal advice.*

### 2.5 Missing reality #4 — No-show is the real operational pain

Every volunteer coordinator's actual daily problem is not matching. It is:

- a volunteer who does not show up
- a senior who forgot the appointment
- 40 minutes of phone calls to reschedule

The product should lead with this, because it is measurable and immediately valuable:

- automatic reminders (push + SMS fallback) at T-24h and T-2h
- one-tap confirm / cancel-with-reason
- Reliability score computed from behaviour, never from star ratings
- a coordinator dashboard whose first widget is **"Needs attention today"**

### 2.6 Simplification: cut the 8 pillars to 3 for MVP

Document 02 defines 8 product pillars. That is a 3-year product. The MVP has three:

```
1. TRUST     — who is this person, and what are they allowed to do?
2. HELP      — request → match → do it → record it
3. COMMUNITY — groups and recurring activities
```

Everything else (Impact/ESG, Corporate, Marketplace, AI, IVR) is built on these three.

---

## 3. Positioning

**We are not:**

- a new Caritas (they have 100 years of trust — you cannot buy that)
- Uber for seniors (transactional framing kills NGO partnerships)
- a social network for seniors (that market is Facebook and it is already lost)
- a care marketplace (legal minefield, see 2.3)

**We are:**

> The coordination and trust layer for local everyday help.
> We make existing volunteering *organisable, safe and measurable*.

The measurable part is what you sell to organizations. The safe part is what you sell to
families. The organisable part is what makes seniors use it.

---

## 4. Business model (add this to every funding conversation)

| Revenue line | Who pays | When |
| --- | --- | --- |
| Free forever | Seniors, families, independent volunteers | Always |
| Gemeinde licence | Municipality, per inhabitant/year | Phase 6 |
| Organization SaaS | NGO, per active coordinator seat + per branch | Phase 6 |
| Impact & ESG reporting module | NGO / Corporate | Phase 7 |
| Corporate volunteering | Companies, per employee/year | Phase 8 |
| White-label | Large NGO | Phase 8 |

Non-dilutive funding worth investigating in Austria (verify current programmes yourself —
these change frequently):

- FFG (Basisprogramm / Impact Innovation)
- aws (Preseed / Seedfinancing — Innovative Solutions)
- Sozialministerium / Land Tirol digital-social calls
- EU AAL / Horizon social-innovation calls
- Netidee (Internet Foundation Austria)

---

## 5. The proof you need before any serious meeting

Do not walk into Caritas with a demo. Walk in with a table:

```
Pilot Gemeinde X, 3 months

  34 seniors onboarded          (28 of them by a family member)
  22 volunteers, 19 verified
 187 help requests created
  91% matched within 24 h
   6% no-show rate  (baseline before pilot: 23%)
 412 volunteer hours recorded
  74 group participations
   2 safeguarding concerns raised, both resolved < 48 h
```

The last two lines are what make the difference. Everyone can show signups. Almost nobody
can show a *working safeguarding process with real data*.

---

## 6. Top risks, ranked

| # | Risk | Mitigation |
| --- | --- | --- |
| 1 | Cold start — nobody uses an empty local platform | Anchor partner + family-led onboarding + launch in ONE Gemeinde only |
| 2 | A safety incident in the pilot | Safety Levels + org approval for L3+ + Buddy System for first visits + Safeguarding from day 1 |
| 3 | Building 40 features and shipping none | Freeze MVP scope (Phase 1–3). Every new idea goes to `docs/plans/backlog.md`, not into the sprint |
| 4 | GDPR / data-controller ambiguity in the pilot | No real personal data until legal entity + DPA + privacy policy exist (Phase 7 gate) |
| 5 | Solo-founder burnout on a 3-year scope | Phases are independently shippable. Phase 3 alone is a sellable product |
| 6 | Building on assumptions instead of interviews | Phase 0 is mandatory: 3 NGOs, 1 social worker, 5 seniors, 5 volunteers, before Phase 1 code |

---

## 7. How to use this repository

```
Read in this order:
  00-PRODUCT-BRIEF.md          ← you are here
  docs/product/vision.md
  docs/product/personas.md
  docs/product/business-rules.md
  docs/architecture/system-design.md
  docs/product/roadmap.md      ← what to build, in order
  docs/design/design-system.md ← colors, dark mode, typography
  AGENTS.md                    ← how the AI agent must behave in this repo
```

Then copy the whole tree into an empty Git repository and start Phase 0.
