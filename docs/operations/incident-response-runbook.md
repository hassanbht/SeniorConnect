# Incident Response Runbook — SeniorConnect Production

This runbook defines the incident classification, triage procedures, escalation paths, and remediation protocols for SeniorConnect operational incidents in municipal and field pilot deployments.

---

## 1. Incident Classification & Severity Matrix

| Severity | Description | Response SLA | Escalation Target | Examples |
| :--- | :--- | :--- | :--- | :--- |
| **SEV-1 (Critical)** | Direct threat to physical safety, safeguarding escalation, or critical infrastructure outage | **< 15 minutes** | Lead Safeguarding Officer, Municipal Coordinator, CTO | - Volunteer welfare violation during Level 3+ activity<br>- Total database outage during operational hours<br>- False emergency call loop |
| **SEV-2 (High)** | Data breach, unauthorized tenant access, or core capability failure | **< 1 hour** | Data Protection Officer (DPO), Tech Lead | - Suspected Safeguarding access leak<br>- ID Austria / SMS OTP gateway complete failure<br>- Concurrency corruption in Help Requests |
| **SEV-3 (Medium)** | Non-critical feature degradation, slow SMS delivery, minor bug | **< 4 hours** | On-call Engineer, Coordinator Support | - Delayed push notifications<br>- Excel report generation timeout on large orgs<br>- Map tile rendering latency |
| **SEV-4 (Low)** | UI cosmetic defects, translation typo, non-urgent support query | **< 24 hours** | Development Backlog | - Typo in Persian or German FAQ<br>- Minor padding overflow on rare screen sizes |

---

## 2. SEV-1: Critical Safeguarding & Physical Safety Protocol

### Immediate Actions (< 15 Minutes)
1. **Quarantine & Access Suspension**:
   - Immediately suspend the accused user account via Platform Admin / Safeguarding Officer console:
     ```http
     POST /api/v1/users/{userId}:suspend
     ```
   - Auto-cancel all pending and upcoming accepted assignments associated with the volunteer:
     ```http
     POST /api/v1/coordinator/volunteers/{userId}:emergency-unassign
     ```
2. **Safeguarding Case Isolation**:
   - Verify the case is filed in the isolated schema (`safeguarding.safeguarding_cases`) and NOT accessible to ordinary coordinators or organization admins (`BR-SG-02`).
   - Assign the case immediately to the designated municipal safeguarding lead (`POST /api/v1/safeguarding/cases/{id}:assign`).
3. **Physical Safety Check**:
   - If an immediate physical threat to a vulnerable senior exists, contact local authorities or emergency services (144 / 112 / 133 in Austria) outside the platform.

---

## 3. SEV-2: GDPR Data Breach & Security Incident Protocol

### 72-Hour Notification Workflow (Art. 33 DSGVO)
1. **Breach Assessment & Containment**:
   - Identify affected user IDs and scope of exposed personal data.
   - Revoke all active sessions and refresh tokens across all devices:
     ```http
     POST /api/v1/auth/sessions:revoke-all
     ```
2. **Access Log Forensics**:
   - Extract immutable access logs from `safeguarding_access_logs` and `audit_entries`.
   - Compute hash signatures and preserve database snapshots for forensic analysis.
3. **Authorities Notification**:
   - Notify the Austrian Data Protection Authority (*Österreichische Datenschutzbehörde - DSB*) within **72 hours** of becoming aware of the breach if high risk to rights and freedoms exists.
   - If high risk to affected individuals, dispatch priority notifications directly to users (`BR-GDPR-03`).

---

## 4. SEV-3: SMS OTP Gateway & Communication Failover

### Fallback Procedure
1. If Austrian mobile SMS deliverability drops below 95% or latency exceeds 30 seconds:
   - Automated routing switches to secondary EU SMS carrier.
   - Email Magic Link fallback (`POST /api/v1/auth/request-magic-link`) is highlighted in mobile UI (`BR-AUTH-01`).
   - Check monthly SMS budget consumption via `INotificationBudgetService` (`BR-NOTIFY-02`).

---

## 5. Post-Incident Review (PIR) & Blameless Post-Mortem

Within 5 business days of resolving any SEV-1 or SEV-2 incident:
1. Publish root-cause analysis (RCA) document.
2. Verify immutable audit trails are intact.
3. Update automated regression tests to permanently prevent recurrence.
