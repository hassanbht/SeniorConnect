# Glossary

Use these words consistently in code, in documentation and in the UI. Inconsistent
vocabulary is how a solo project loses its own model.

| Term | Meaning | Do not confuse with |
| --- | --- | --- |
| **User** | one account, one person | Role |
| **Senior** | a user with a `SeniorProfile` | "elderly person" — a senior may also be a volunteer |
| **Volunteer** | a user with a `VolunteerProfile` | "helper", "Helfer:in" in code |
| **Family Caregiver** | a user with a confirmed `FamilyRelationship` | Trusted Contact |
| **Trusted Contact** | someone notified on defined events; may not have an account | Family Caregiver, Emergency Contact |
| **Organization** | an NGO, Gemeinde, association or company tenant | Branch |
| **Branch** | a regional unit of an organization | Organization |
| **Capability** | a specific permitted action | Role, Trust Level |
| **Trust Level** | 0–5, computed from verifications | Reliability, Rating |
| **Safety Level** | 1–5, required by an activity | Trust Level |
| **Reliability** | computed from behaviour | Rating, Trust |
| **Help Request** | a request for concrete, bounded, unpaid, non-medical help | Event, Concern |
| **Offer** | a volunteer's proposal or the system's proposal to a volunteer | Assignment |
| **Assignment** | a confirmed volunteer ↔ help request pairing | Offer |
| **Community Group** | a persistent group of people with a shared interest | Event |
| **Event** | a single occurrence with a time and place | Group, Activity |
| **Activity** | the umbrella term for anything a user participates in | Event |
| **Check-in / Check-out** | time-bounded, activity-scoped location + time record | tracking (which we do not do) |
| **Concern** | a raised worry about a person's wellbeing | Feedback, Report, Rating |
| **Safeguarding Case** | the restricted record created from a concern | Concern, Report |
| **Emergency** | routes to official services (144/112) | Safety Alert |
| **Safety Alert** | an internal workflow notification | Emergency |
| **Verification** | a recorded outcome of a check | the document, which we never store |
| **Scope** | Platform / Community / Organization / Private | Tenant |
| **Senior Mode** | a device accessibility preference | a role; and it is labelled „Große Ansicht" in the UI |

## German UI vocabulary (fixed — do not vary)

| Concept | German string |
| --- | --- |
| Help request | **Anfrage** (verb: „Hilfe anfragen") |
| Volunteer | **Freiwillige:r** |
| Community group | **Gruppe** |
| Event | **Termin** (in Senior Mode) / **Veranstaltung** (elsewhere) |
| My activities | **Meine Aktivitäten** |
| Trust badges | **Bestätigungen** |
| Senior Mode | **Große Ansicht** |
| Report a concern | **Etwas melden** |
| Check-in | **Angekommen** |
| Check-out | **Fertig** |
