# Controlled Early Access — Stop Conditions

> **Date:** 2026-10-02 · **Phase:** Production Activation & Controlled Early
> Access Readiness
>
> Stop conditions are based on **actual repository policies and operational
> rules** documented in this repository. **No arbitrary numeric KPIs are
> invented.** A stop condition is either:
>
> - **Technical** — a verifiable defect in repository-proven behavior, or
> - **Operational** — a loss of a required owner/appointment, or
> - **Owner decision** — the operating owner chooses to stop.
>
> Stop is **reversible only** when the underlying condition is demonstrably
> fixed and re-verified (tests re-run; gate re-audited where applicable).

---

## Stop Conditions

### Technical Stop Conditions

These are verifiable defects in implemented, tested behavior. Any of the
following requires **immediate stop** of the affected community's Early Access
participation until the condition is fixed and re-verified:

| # | Condition | Repository Policy / Test Evidence |
|---|---|---|
| T1 | **Cross-tenant or safeguarding isolation failure** — a user accesses data belonging to another organization, or a non-officer accesses safeguarding case text | BR-TENANT-03; BR-SG-02; architecture tests (59/59); HTTP dry run cross-tenant 404 checks |
| T2 | **Withholding protections fail** — a block, restriction, cancellation or revocation does not withhold BR-COMM-04 / BR-GEO data (contact, location, or address disclosed to a blocked or restricted party) | BR-COMM-04 withholding; BR-DISC-06; HTTP dry run "exact address withheld" 4/4 checks |
| T3 | **`ADR-026` gate found enabled without the written activation record** — `VisitAddress:RequireGrant` is `true` in any committed configuration or any deployment configuration without a signed authorization record | `docs/security/visit-address-activation-gate.md` §4; architecture guard `VisitAddressGrant_FeatureGate_StartsOffInEveryCommittedConfiguration` fails the build on any committed activation |
| T4 | **Authentication bypass** — a request succeeds that should be denied (rate-limit bypass, expired token accepted, replayed token accepted, invalid token accepted) | `EmailOtpProviderSecurityTests.cs` (14 tests); `JwtSettings.EnsureSafeFor()`; rate limiting in `Program.cs` |
| T5 | **Credential compromise** — a real SeniorConnect secret is committed to the working tree or any secret is exposed in logs at runtime | §24 secret scan; `docs/security/credential-exposure-remediation.md`; hard rule §15 |
| T6 | **Safeguarding control failure** — S-2/S-3/S-4/B-1/B-2 behavior deviates from the verified implementation (e.g., org officer can access org-less cases, block does not release assignment, officer conversion produces no audit log) | `docs/launch/PRODUCTION-DEPLOYMENT-VALIDATION-REPORT.md` §SAFEGUARDING; HTTP dry run steps 9–13 |
| T7 | **Unauthorized exact-location exposure** — precise coordinates or exact visit address reach a party not entitled to them outside the BR-DISC-06 policy | BR-GEO-05; BR-COMM-04; HTTP dry run "no exact private coordinates" |
| T8 | **Audit integrity failure** — access logs or status history can be altered or deleted (append-only violation) | BR-AUDIT-01; BR-SG-06; architecture audit-interceptor tests |

### Operational Stop Conditions

These require the **loss** of a capability or appointment that must be real and
accountable before and during Early Access:

| # | Condition | Required Replacement |
|---|---|---|
| O1 | **Loss of Safeguarding Lead** — the appointed Safeguarding Lead for the participating community becomes unreachable or steps down | Re-appointment via `docs/operations/operational-ownership-checklist.md` |
| O2 | **Loss of Platform Safeguarding Officer** — the S-4 platform officer is removed and not replaced (org-less cases stop being worked — fail-closed) | Re-appointment via `POST /api/v1/safeguarding/platform-officers/{userId}` |
| O3 | **Loss of Operational Coordinator** — the day-to-day coordinator for the community becomes unreachable | Re-appointment |
| O4 | **Loss of Deployment/Backup capability** — no one can deploy, restore, or back up the system; the deployment owner is unreachable with no deputy | Re-appointment of Deployment Owner / Backup Owner (with deputy) |
| O5 | **Loss of Data Protection / Legal sign-off** — the qualified legal review that enabled processing for the community is withdrawn or no longer covers the deployment | Re-establish and document legal basis before any real personal data is processed |
| O6 | **Unreachable Support Contact** — the named support contact for participants is unreachable with no deputy for the duration of the review period | Re-appointment with a reachable deputy |

### Owner Decision Stop Condition

| # | Condition |
|---|---|
| R1 | The operating owner decides to stop Early Access for the community (keep / change / stop review, per `docs/product/early-access-validation-plan.md` §6) |

---

## Stop vs. Reversible

- **All technical stops (T1–T8):** reversible only after the defect is fixed,
  the full relevant test subset is re-run with zero failures, and the gate is
  re-audited against actual configuration (not a claim).
- **All operational stops (O1–O6):** reversible only after the named person is
  re-appointed, their contact and backup are recorded, and the capability is
  re-verified (e.g., a test assignment through the appointed officer succeeds).
- **Owner decision (R1):** effective immediately upon the owner's decision;
  can be reversed by a fresh owner re-authorization after remediation.

## Distinction Summary

| Type | Trigger | Who decides | Reversal |
|---|---|---|---|
| Technical | Verifiable defect in tested behavior | Automated detection + owner confirmation | Fix + re-test + re-audit |
| Operational | Loss of required appointment | Owner observes | Re-appointment + re-verify |
| Owner decision | Owner judgment | Owner | Fresh owner re-authorization |

---

## What Is NOT a Stop Condition

The following are **not** stop conditions and must not be treated as such:

- A non-critical bug that does not affect isolation, safeguarding,
  authentication, location privacy, or audit integrity
- A performance metric that dips below a target (targets are calibration
  instruments per `early-access-validation-plan.md` §3, not gate criteria)
- A single false-positive safeguarding flag that is investigated and resolved
  without a control failure
- A community's individual feedback score or sentiment that does not rise to a
  technical or operational failure above
