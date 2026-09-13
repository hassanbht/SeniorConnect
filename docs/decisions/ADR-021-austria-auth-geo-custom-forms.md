# ADR-021 — Multi-Provider Authentication, Austrian Administrative Geography, Map Geocoding, and Dynamic Organization Intake Forms

Status: Accepted  
Date: 2026-09  
Amends: ADR-016 (Passwordless authentication), ADR-005 (Geo representation), ADR-013 (Identity provider)

---

## 1. Context

SeniorConnect is designed to empower Austrian local communities, starting with its flagship pilot partner **Freiwilligenzentrum Innsbruck-Land (FWZ)** in Tyrol. Real-world requirements, partner intake documents, and user registration flows have highlighted four essential platform enhancements:

1. **Authentication Evolution:**
   While ADR-016 successfully eliminated password barriers for older seniors, relying exclusively on SMS OTP introduced high telecom friction, carrier costs, and onboarding bottlenecks. Furthermore, mainstream users and volunteers expect Google Sign-In, and modern Austrian citizens increasingly use **ID Austria** (the official eIDAS-compliant national digital identity). An option for email + password (with strict email verification and confirmation) is necessary to support web users and those without active Austrian SIM cards.

2. **Austrian Administrative Geography & Spatial Resolution:**
   Help requests and volunteer connections are fundamentally local. In Austria, administrative structure is organized into:
   - 9 Federal States (*Bundesländer*)
   - 94 Political Districts (*Bezirke* and *Statutarstädte*)
   - 2,093 Municipalities (*Gemeinden*)
   - Postcodes (*Postleitzahlen / PLZ*) and local villages/subdivisions (*Ortschaften*)
   Without structured administrative reference data, users mistype addresses and cannot be reliably matched to nearby volunteers or local institutions.

3. **Geocoding & Interactive Map Verification:**
   Users need visual confirmation of their home or activity location. Entering an address must allow looking up coordinates (geocoding via official Austrian BEV address registry / OpenStreetMap Nominatim), previewing a pin on a privacy-respecting map, and persisting verified latitude/longitude coordinates.

4. **Dynamic Organization Intake Forms (FWZ Innsbruck-Land Pattern):**
   Physical volunteer intake forms (such as *Interesse für Freiwilligentätigkeit* and *Einwilligung zur Datenverarbeitung*) reveal that each organization has specific questionnaire needs. Some want to know volunteer categories (*Bereiche* such as *Soziales*, *Natur*, *Handwerkliches*, *E-Volunteering*, *Lernbetreuung*), target groups (*Personengruppen* like *Geflüchtete*, *Familien*, *Senior:innen*), and time availability (*Zeitaufwand*, *Wochentage*, *Flexibilität*). Every organization must be capable of creating and tailoring these intake questions, marking sections or fields as mandatory (*Pflichtfeld*) or optional (*Freiwillig*), and collecting legally required criminal clearance (*Strafrechtliche Unbescholtenheit*) and GDPR consent declarations (*Einwilligung zur Datenverarbeitung*).

---

## 2. Decision

### 2.1 Multi-Tier Authentication Matrix
We implement a hybrid, modern authentication system:
- **Google Sign-In:** One-tap OAuth 2.0 / OpenID Connect authentication.
- **Email + Password + Confirm Password:** Available for registration across all clients. Upon registration, an activation email with a signed verification token is dispatched. Accounts remain unverified until the link is clicked.
- **ID Austria Integration:** Austria's official digital identity protocol (via eIDAS / OpenID Connect federation). Successfully logging in with ID Austria automatically elevates the user's trust profile (*Trust Level 1/2*), verifying real-name identity without document scans.
- **In-Profile Mobile Phone Verification:** Mobile phone is entered in the profile and verified via SMS OTP with an explicit "Verify Phone Number" button.
- **Session Security:** 90-day refresh tokens for personal devices, rate-limited login endpoints, and silent refresh remain active.

### 2.2 Austrian Administrative Directory
- A comprehensive database of all 9 Austrian Bundesländer, 94 Bezirke, 2,093 Gemeinden, and all PLZ codes is seeded into `austrian_administrative_units`.
- Form inputs provide cascading selection and auto-completion (Bundesland → Bezirk → Gemeinde → PLZ / Ort).
- PostGIS-ready spatial math computes Euclidean/Haversine distance to calculate nearest towns/cities to any coordinate.

### 2.3 Address Verification & Map Geocoding
- A geocoding endpoint queries OpenStreetMap Nominatim / BEV Austrian address register.
- Users can click "Lookup Address" to inspect their location on an interactive map preview and confirm their pin.
- Coordinates (`latitude`, `longitude`, `geocoded_at_utc`) are stored in the database.
- **Privacy Rule:** Exact street addresses and exact lat/lng are strictly obscured in public or neighborhood discovery views. Only approximate town/district and distance radius (e.g. "Within 5 km in Innsbruck-Land") are displayed until an assignment or connection is approved.

### 2.4 Local Proximity Discovery
- **Citizen / Senior View:** Based on their validated location, users can view nearby registered charities/organizations and independent/free volunteers.
- **Volunteer View:** Independent volunteers can discover nearby organizations and open community help requests.

### 2.5 Dynamic Organization Intake & Categorization Engine
- Organizations can define custom intake forms for Volunteers (*Freiwillige*) and Help-Seekers (*Hilfesuchende*).
- Form builder supports sections, custom field definitions, and per-field mandatory/optional toggles.
- Pre-seeded with the **FWZ Innsbruck-Land Standard Template**:
  - **Bereiche:** Soziales, Natur, E-Volunteering, Klima und Nachhaltigkeit, Handwerkliches / Kreatives, Kunst und Kultur, Freiwilligenpool, Lernbetreuung.
  - **Personengruppen:** Geflüchtete / Personen mit Migrationshintergrund, Familien, Senior:innen, Menschen mit Behinderung, Kinder und Jugendliche, Sonstige.
  - **Zeitaufwand:** Einmalig, regelmäßig (pro Woche, pro Monat, pro Jahr), Stundenanzahl, Tageszeit, Wochentag(e), Flexibel, WhatsApp Erreichbarkeit.
  - **Legal Declarations:** Mandatory *Strafrechtliche Unbescholtenheit* (clean criminal record declaration) and GDPR *Einwilligung zur Datenverarbeitung*.

---

## 3. Consequences

### Positive
- **Reduced Onboarding Friction:** Users and volunteers can register instantly with Google or Email+Password instead of being blocked by SMS delivery issues.
- **Institutional Legitimacy with ID Austria:** Official Austrian government eID elevates trust levels automatically without manual paperwork.
- **Accurate Geo-Matching:** Austrian administrative units eliminate typos and enable district-level and kilometer-based matching.
- **Partner Flexibility:** Organizations like FWZ Innsbruck-Land can map their exact real-world intake questionnaires into the platform without code deployments.

### Trade-offs & Mitigations
- **Email Verification Flow:** Users who register via email must verify their address before accessing high-trust features. Clear onboarding banners guide them.
- **ID Austria Integration Overhead:** Requires maintaining eIDAS OIDC endpoints and staging mock providers for local development.
- **Dynamic Form Storage:** Stored as structured JSON with JSON Schema validation against `intake_form_submissions` to guarantee relational integrity and auditing.
