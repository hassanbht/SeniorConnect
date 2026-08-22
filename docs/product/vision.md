# Product Vision

## Mission

SeniorConnect connects seniors, families, volunteers and social organizations so that everyday
help and social participation can be arranged locally, safely and verifiably.

## The problem, stated precisely

Three different problems, held by three different people:

1. **A senior** needs someone to come along to the doctor on Tuesday, and does not want to
   feel like a burden asking their daughter for the fourth time this month.
2. **A daughter living 200 km away** does not know whether her mother left the house this
   week, and has no way to organise help without taking a day off.
3. **A volunteer coordinator at an NGO** spends most of her week on the phone matching
   people, chasing no-shows, and re-entering hours into Excel — and cannot prove her
   programme's impact at the annual funding review.

Existing services (Besuchsdienst, Begleitdienst, Nachbarschaftshilfe) already exist and
work. What does not exist is a shared coordination and trust layer under them.

## What the platform enables

1. Seniors connect with other seniors and join or create local activities.
2. Seniors request everyday, non-medical assistance.
3. Independent volunteers offer help, with a verified and visible level of trust.
4. Family members support and coordinate remotely, within permissions the senior controls.
5. Organizations manage their own volunteers, activities and safeguarding — and can
   measure and report their impact.

## Non-negotiable principles

1. **The platform must be useful with zero organizations registered.**
   Organizations are an optional extension, never a dependency.
2. **A senior is not only a recipient.** Every senior can also be a volunteer, an
   organizer, or a mentor. The data model must not make "senior" mean "helpless".
3. **Safety before convenience.** If a safety rule and a UX improvement conflict, safety wins.
4. **Accessibility is architecture, not a feature.**
5. **Trust is computed server-side and is always explainable.** A user can always see
   *why* they are or are not allowed to do something.
6. **Non-medical, non-nursing, unpaid.** See `business-rules.md` §1. This boundary is
   enforced in code.
7. **The platform never claims a person is safe.** It states what was verified, by whom,
   and when.

## Explicit non-goals (v1–v3)

- Not a payment marketplace
- Not an emergency/ambulance service
- Not a medical or care-documentation system (no Pflegedokumentation)
- Not a general social network with a public feed
- Not a dating platform
- Not a replacement for professional care or for existing NGO services

## The success test

> A senior in the pilot Gemeinde who has never used an app before receives help she would
> otherwise not have asked for, from a person whose identity was verified, arranged in
> under 24 hours, recorded well enough that the coordinator can show it in a funding report.

If that sentence is true 100 times, the product works.
