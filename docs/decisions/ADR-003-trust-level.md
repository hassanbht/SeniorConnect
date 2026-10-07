# ADR-003 — Trust ≠ Role ≠ Capability

* **Status:** Accepted
* **Date:** 2026-08-22

## Context

Different activities have varying risk profiles (e.g., matching a volunteer with a senior for gardening vs. home key access). Simple role-based access control (RBAC) is insufficient to model these requirements safely.

## Decision

We decouple authorization using three distinct, server-side calculated concepts:

1. **Trust Level:** A value from 0 to 5 calculated deterministically from verifications (identity, criminal record check, references).
2. **Role:** A tenant-level membership (e.g., OrganizationAdmin, Coordinator, Volunteer).
3. **Capability:** A specific action permission (e.g. `PerformSafetyLevel3Activity`, `AccessSafeguardingConcern`). 

All parameters are calculated server-side. The client's inputs regarding user capabilities or trust levels are completely ignored.

## Consequences

- Enforces high security standards and prevents client-side authorization bypass.
- Clear separation of concerns between user identity verification and their organizational/platform duties.
