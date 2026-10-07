# ADR-016 — Passwordless authentication by default

Status: Accepted
Date: 2026-08

## Context

Discovery F3: both reported prior-software failures were authentication failures,
not feature failures.

> "A heavy web portal the older volunteers couldn't operate, and the login kept
> breaking. After two months we went back to Excel and WhatsApp."
>
> "An app that needed a complex password every time I opened it. I deleted it."

Neither complaint mentions a missing capability. The original Phase 1 spec made
email + password the primary credential, which reproduces the exact failure mode
that killed the incumbent tools.

The population also matters: many volunteers in this sector are themselves over
60, and the seniors are the least password-capable users imaginable. A forgotten
password for a 78-year-old is not a support ticket, it is permanent churn.

## Decision

1. **Phone number + one-time SMS code is the primary credential** for seniors,
   volunteers and family members. Email magic link is the alternative where a
   phone number is unavailable.
2. **Email + password remains available for organization staff and platform
   admins only**, where a shared workstation and a browser make it appropriate.
   Staff accounts additionally support TOTP.
3. **Sessions last 90 days** on a device the user marks as personal, refreshed
   silently. Biometric or device-PIN re-auth is used for sensitive screens rather
   than a password prompt.
4. **The app never shows a login screen to an authenticated user with a valid
   refresh token.** A volunteer who opens the app once a week must land on
   content, not on a form.
5. Re-authentication is required only for: changing the phone number, changing
   family permissions, and any safeguarding screen.

## Consequences

- SMS becomes a hard dependency and a real cost line. Budget for it, choose an
  EU provider, and rate-limit aggressively (OTP request throttling per number and
  per IP).
- Account recovery is simpler for the user and needs care for us: a lost phone
  number is now a support process. Trusted Contacts (Phase 6) can serve as a
  recovery path once they exist; until then it is a manual, audited staff action.
- SIM-swap is a real attack surface. Mitigation: any phone-number change
  invalidates all sessions, notifies the old number and email, and is audited.
- Removing passwords from the senior and volunteer paths removes an entire class
  of support burden — the one the coordinator explicitly named.
