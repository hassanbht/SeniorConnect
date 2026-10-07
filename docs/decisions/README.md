# Decisions — reconstructed index

> Reconstructed 2026-10-07 during Post-Wave-3 Failure Closure.
> The historical `docs/decisions/` tree (including the 17-decision ADR index
> referenced by `backend/README.md`) is **not present in this snapshot** and
> no Git history is available to restore it from. Nothing below pretends to
> be a historical file. Only records verifiable from current in-tree evidence
> are transcribed here, under their existing in-code numbers.

## Verifiable in-tree decision records

| ADR | Title | Status | Canonical record |
|-----|-------|--------|------------------|
| ADR-029 | Staff TOTP Management Authorization | Accepted | [ADR-029](ADR-029-staff-totp-management-authorization.md) — transcribed from `Organizations/Contracts/StaffSecurityContracts.cs`, `IdentityService.CanManageStaffSecurityAsync`, `OrganizationStaffSecurityAuthorizer` |
| ADR-030 | Effective Trust Recalculation | Accepted | [ADR-030](ADR-030-effective-trust-recalculation.md) — transcribed from `Identity/Infrastructure/TrustLevelReader.cs`, `TrustLevelCalculator`, `TrustLevelReaderRecalculationTests` |

## Numbers referenced in code (documents absent from snapshot)

ADR-007 (nullable org scope) · ADR-014 (manual approval Safety 3+) ·
ADR-015 (roadmap recut) · ADR-016 (passwordless auth) · ADR-017 (funder
scope) · ADR-020 · ADR-026 (VisitAddress grant gate, Option C, OFF) ·
ADR-027 (trust ladder, Option A). Their documents are **not restored** —
only code comments and tests attest them. Do not renumber ADR-029/030:
those numbers are established by consistent in-code references and tests.
