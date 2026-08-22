# Personas

> A **User** may hold several personas at once. The data model reflects this:
> one `User` → optional `SeniorProfile`, optional `VolunteerProfile`,
> zero-or-more `FamilyRelationship`, zero-or-more `OrganizationMembership`.
> Never model these as mutually exclusive roles.

---

## P1 — Maria, 78, Innsbruck-Wilten · *Independent Senior*

Widowed, lives alone in a third-floor flat with no lift. Retired German teacher. Walks
with a stick since a hip operation. Has an iPhone her son gave her; uses WhatsApp and the
camera, nothing else. Cataract in the left eye — reads at ~150 % text size.

**Needs:** company on the way to the doctor; someone to carry shopping up the stairs;
people to talk to.
**Also offers:** she runs a German conversation circle for two Ukrainian neighbours. She
is a *provider*, not just a recipient.
**Fears:** being a burden; being scammed; someone unknown coming to her flat.
**Killer feature:** the four-button home screen and the fact that her son set it all up.
**Deal-breaker:** any screen with more than one main action, any English word, any timeout.

---

## P2 — Sabine, 51, Vienna · *Family Caregiver* — **the acquisition channel**

Maria's daughter. Full-time job, two teenagers, 480 km away. Checks her phone constantly.
She is the one who will find, install, configure and evangelise this app.

**Needs:** to know her mother is fine without calling every evening; to arrange the
Tuesday doctor's visit from her desk; to be notified if something is wrong.
**Fears:** her mother falling and nobody noticing; letting a stranger into the flat.
**Killer feature:** creating and fully setting up her mother's account from her own phone,
then handing over a phone that already works.
**Deal-breaker:** being blocked from doing anything useful "for privacy reasons" without a
clear, senior-controlled way to grant permission.

> Design consequence: **every senior-facing flow must have a "do this on behalf of" variant**,
> gated by explicit, revocable, per-permission consent from the senior.

---

## P3 — Anna, 34, Innsbruck · *Independent Volunteer*

Physiotherapist, works four days a week, wants to do something meaningful on Fridays.
Found the app through a Gemeinde poster. Not a member of any NGO.

**Needs:** to see what is needed near her, this week, that fits her Friday morning; to be
trusted quickly without a two-month bureaucratic process.
**Fears:** committing to something open-ended; awkward situations she is not trained for;
her own safety in a stranger's flat.
**Killer feature:** clear, bounded tasks with a start and an end, and a visible safety level.
**Deal-breaker:** having to upload a Strafregisterauszug before she can join a walking group.

> Design consequence: **risk-based verification.** Level 1 activities need almost nothing.
> Verification escalates only when the activity requires it.

---

## P4 — Thomas, 69, Hall in Tirol · *Senior Volunteer / Community Organizer*

Retired postman, fit, bored. Knows everyone. Drives.

**Needs:** something regular to do; to organise the Wednesday walking group he already
informally runs.
**Killer feature:** creating a recurring group event without asking anyone's permission.
**Why he matters:** P4 is the single most valuable user type for the cold-start problem.
Recruit five Thomases in the pilot Gemeinde and the platform has content on day one.

---

## P5 — Elisabeth, 43 · *Volunteer Coordinator, NGO branch* — **the buyer**

Coordinates 60 volunteers for a regional NGO branch. Works in Outlook, three Excel files
and her phone. Her annual review depends on numbers she reconstructs by hand each January.

**Discovery reality (F1, F2, F10, F12):** 60 volunteers on the roster, **22 genuinely
active**, tracked from memory. Two hours every Tuesday morning retyping hours from paper
and WhatsApp into Excel. Two full weeks each December to build the annual report. When
nobody can be found, she takes her own car. Volunteer onboarding takes 2–4 weeks and
nobody tells the applicant where they stand.

> Q10, "if I could fix one thing for you": *"Automatic, hassle-free hour logging without
> me chasing people."*

**Needs:** to see today's problems in one screen; to record volunteer hours without
re-typing; to produce an impact report in one click.
**Fears:** a safeguarding incident on her watch; GDPR non-compliance; another system to
maintain.
**Killer feature:** the "Needs attention today" dashboard and the one-click PDF impact report.
**Deal-breaker:** anything that requires her volunteers to be tech-savvy; any system where
she cannot see who did what.

---

## P6 — Dr. Wagner · *Safeguarding Officer*

Social worker at the same NGO. Handles concerns about vulnerable clients. Legally and
professionally accountable.

**Needs:** a case record separate from ordinary reports; restricted visibility; an audit trail.
**Hard requirement:** the Organization Admin must **not** automatically see safeguarding
cases. This is a distinct, separately-granted permission.

---

## P7 — Gemeinde Social Welfare Officer · *the Funder* — **revised after discovery**

Municipality employee responsible for social-welfare budgets and answerable to the
Gemeinderat.

**Critical correction (F7):** the Gemeinde does **not** employ volunteers. It funds
associations and receives aggregated reports from them. It is a **Funder**, not a tenant.
Modelling it as an organization would grant it a data surface it is legally not permitted
to have. See ADR-017.

> "We don't recruit volunteers directly, we fund the associations. We don't have
> transparent data and we don't know how many people are actually active."
>
> "Data must be fully anonymised. The municipality must never see the seniors' names."

**Needs:** a live, anonymised impact dashboard instead of quarterly PDFs arriving by post;
numbers she can put in front of the council to renew next year's subsidies.
**Fears:** a data-protection breach on her watch; an insurance dispute like the one that
already happened (F4); an unjustifiable line item in a small municipal budget.
**Killer feature:** aggregate figures, live, with no letter-writing.
**Deal-breaker:** any personally identifying data reaching her dashboard; unclear insurance;
**a recurring per-seat licence fee a small Gemeinde cannot defend to its council.**
**Why she matters:** the fastest first *paying* customer — a social-committee decision is a
smaller process than NGO procurement. But she buys a **dashboard**, not a workflow tool.

---

## P8 — Corporate CSR Manager *(Phase 8)*

Wants 200 employees to do a volunteer day and needs an ESG-reportable number afterwards.
Ignore until Phases 1–6 are live.

---

## Anti-persona — who this product is NOT for

- People needing **medical or nursing care** → referral only, never matching.
- People in an **acute emergency** → routed to 144 / 112, never queued as a help request.
- People seeking or offering **paid domestic work** → blocked; explain why.
- **Anyone under 16** → not a user of this platform.
