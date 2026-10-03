# Security Policy

## Supported versions

SeniorConnect is a single-line main-branch project. Run the latest release;
container images are rebuilt from source on every deploy.

## Reporting a vulnerability

**Do not open a public issue for an exploitable finding.**

This is an independent open-source project without a vendor or security desk
(ADR-028). Report privately to the maintainers via the project's published
security contact (GitHub Security Advisory / maintainer email — the operator of
your deployment can also forward reports). Include: affected component, impact,
reproduction steps, and any proof-of-concept.

You can expect an initial acknowledgement within 7 days and a fix or mitigation
plan for confirmed issues; coordinated disclosure after a fix is shipped is the
default.

Safeguarding emergencies (risk to a person) are **not** security reports —
contact the safeguarding officer of the responsible organization or local
emergency services first.

## Scope notes

- The `safeguarding` schema and its access policy are documented in
  [docs/security/safeguarding-scoping-policy.md](docs/security/safeguarding-scoping-policy.md).
- Deployment hardening responsibilities (EU hosting, TLS, backups) sit with the
  operator: [docs/operations/deployment-guide.md](docs/operations/deployment-guide.md) §4.
- Security documentation entry point: [docs/security/security-overview.md](docs/security/security-overview.md).
