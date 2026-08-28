# Field Pilot Onboarding Playbook — Municipalities & Organizations

This playbook guides municipal coordinators (*Gemeinde-Koordinatoren*), social workers, and volunteer leaders through the step-by-step onboarding process for a new SeniorConnect pilot.

---

## 1. Pre-Launch Readiness Checklist (T-Minus 14 Days)

- [ ] **Data Processing Agreement (DPA)** signed with the municipality ([dpa-template.md](file:///c:/Users/h.behtooei/Downloads/WorkFlowAI/SeniorConnect/docs/legal/dpa-template.md)).
- [ ] **Safeguarding Lead Appointed**: Designated municipal officer holds the `SafeguardingOfficer` capability claim (`BR-SG-02`).
- [ ] **Volunteer Accident & Transport Insurance Verified**: Confirmation that private-vehicle transport liability is covered by municipal group insurance (`BR-TRANSPORT`, `BR-SAFETY-06`).
- [ ] **Physical Zugangskarten Printed**: Batch generated for seniors without smartphones or independent onboarding capacity (`P6-04`).

---

## 2. Setting Up the Municipal Organization (T-Minus 7 Days)

### 1. Organization Creation
The platform administrator seeds or registers the municipality organization:
```http
POST /api/v1/organizations
Content-Type: application/json

{
  "name": "Marktgemeinde Musterort — Seniorenhilfe",
  "legalName": "Marktgemeinde Musterort",
  "registrationNumber": "AT-GMD-12345",
  "postalCode": "3400",
  "contactEmail": "seniorenhilfe@musterort.gv.at",
  "contactPhone": "+43 2243 12345"
}
```

### 2. Designating Municipal Coordinators
Coordinators receive elevated permissions to manage volunteer rosters, view attention flags, and generate monthly impact reports.

---

## 3. Senior Onboarding Paths (Launch Week)

### Path A: Family-Led Onboarding (`J1` / `P6-03`)
1. Adult child downloads SeniorConnect, creates account.
2. Selects *"Angehörigen einrichten"* and enters senior details.
3. Generates 6-digit Zugangskarte code and printed card.
4. Senior enters code on tablet/phone → Home screen immediately populated with trusted family contacts and emergency tiles.

### Path B: Direct Senior Onboarding with Coordinator (`J2` / `P1-08`)
1. Coordinator assists senior with phone OTP sign-in.
2. App switches automatically to **Senior Mode** (64dp touch targets, high contrast, simplified navigation).
3. First-time tutorial explains picture-card request flow.

---

## 4. Volunteer Roster Verification & Trust Tiering (`P4-01`, `P4-02`)

| Step | Action | Trust Level Result |
| :--- | :--- | :--- |
| 1 | Phone OTP verification completed | **Level 1 (Community)** — eligible for public events & non-home tasks |
| 2 | In-person interview by coordinator | **Level 2 (Basic Verified)** — home dropoff, shopping |
| 3 | Strafregisterbescheinigung Kinder- und Jugendfürsorge verified | **Level 3 (Safeguarded)** — in-home accompaniment, key custody eligible |

---

## 5. Daily Coordinator Operational Routine

1. **Morning Sweep (08:30)**:
   - Check `GET /api/v1/coordinator/attention/expiring-verifications` (renewals required within 30 days).
   - Review `GET /api/v1/coordinator/attention/unconfirmed-hours` (approve volunteer self-logs).
2. **Weekly Engagement**:
   - Check `GET /api/v1/coordinator/attention/silent-volunteers` (>60 days inactive).
   - Send broadcast encouragement or check-in note (`POST /api/v1/coordinator/volunteers/broadcast`).
3. **Monthly Reporting (End of Month)**:
   - Download Excel impact report (`GET /api/v1/reporting/organizations/{orgId}/export.xlsx`).
   - Submit volunteer hours and activity metrics to municipal council for funding reconciliation (`P2-31`).
