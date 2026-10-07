# Email / SMTP Provider — Deployment Readiness

> **Status: EXTERNAL ACTION REQUIRED.**
> No SMTP provider is selected, contracted or provisioned by this repository.
> This document describes the **production-safe configuration path** that is
> already implemented in code. It names **no vendor** (hard rule) and makes no
> delivery or compliance claim.

The authentication system's email path (registration verification, email OTP /
magic link, phone-change confirmation) is fully implemented. What is missing
for production is a **real provider contract and credentials** — an external
action, not a code change.

## 1. Implemented provider boundary

| Component | Role |
| --- | --- |
| `IEmailSender` | Provider-agnostic send contract used by the Identity service. |
| `EmailProviderOptions` (`Email` config section) | Manual config binding + environment overrides; `Provider` default `none`. |
| `SmtpEmailSender` | Real SMTP adapter (TLS-capable, timeout, bounded retry, masked logging). |
| `Program.cs` startup | `EmailProviderOptions.Resolve(...).EnsureValidSelection()` fails **loudly** at boot on an incomplete SMTP selection. |
| Deleted stub | The former fake-success sender was removed; nothing pretends delivery. |

## 2. Required configuration keys

All read from configuration **or environment** (never from source):

| Key | Env override | Required when | Notes |
| --- | --- | --- | --- |
| `Email:Provider` | — | always | `none` (default, sends fail) or `smtp`. |
| `Email:Host` | — | `Provider=smtp` | SMTP relay hostname. |
| `Email:Port` | — | `Provider=smtp` | Default `587`. |
| `Email:SenderAddress` | — | `Provider=smtp` | Envelope From / sender identity (must be a domain the operator controls). |
| `Email:SenderName` | — | optional | Display name, default `SeniorConnect`. |
| `Email:Username` | `EMAIL_USERNAME` | provider-dependent | Credential — secret store only. |
| `Email:Password` | `EMAIL_PASSWORD` | provider-dependent | Credential — secret store only. |
| `Email:TimeoutSeconds` | — | optional | Default `10`; drives the SMTP client timeout. |
| `Email:MaxRetries` | — | optional | Default `1`; transient faults only. |

The `Email` section in `appsettings.json` / `appsettings.Production.json`
commits **`Provider: none`, `Host: null`, `SenderAddress: null`** — no
secret, and a secure default that refuses to send until a provider is chosen.

## 3. Secret storage

- `Email:Username` / `Email:Password` **must** come from the environment or
  secret store (`EMAIL_USERNAME` / `EMAIL_PASSWORD`, or the deployment's
  equivalent). They must never appear in a committed file.
- The repository `.env` is gitignored; the deployment supplies its own
  out-of-band values.

## 4. Sender identity

- The operator chooses a `SenderAddress` on a domain it controls; SPF/DKIM/DMARC
  alignment is a **DNS/external requirement** (see
  `docs/operations/production-deployment-checklist.md`), not repository work.
- The address is the envelope From for all OTP / verification / magic-link
  mail.

## 5. TLS

- `SmtpEmailSender` always sets `EnableSsl = true` on the SMTP client.
- The provider must offer STARTTLS/implicit TLS on the configured port; a
  plaintext-only relay must not be used.
- Certificate validation uses the runtime's default trust store.

## 6. Timeout & retry behavior

- Client timeout = `Email:TimeoutSeconds × 1000` ms (default 10 s).
- `Email:MaxRetries` (default 1) retries **transient** faults only
  (`MailboxBusy`, `ServiceNotAvailable`, socket/IO). Permanent failures are
  not retried.
- Cancellation is honored (operation-cancel is re-thrown, never swallowed).

## 7. Failure behavior (fail-closed)

| Situation | Behavior |
| --- | --- |
| `Provider=none` (committed default) and **Development** | Logs the message (developer convenience), returns success. Dev holds no real personal data. |
| `Provider=none` and **non-Development** | Fails with `EMAIL_PROVIDER_NOT_CONFIGURED`; nothing is sent, nothing is logged beyond a masked recipient. |
| `Provider=smtp` but `Host`/`SenderAddress` missing | **Startup fails** (`OptionsValidationException`) — never a silent no-send. |
| SMTP send fails permanently / retries exhausted | Fails with `EMAIL_PROVIDER_SEND_FAILED`; registration/verification reports the failure honestly. |
| Any case | The message body (which carries the OTP / token) is **never logged**; only the masked recipient is. |

A provider failure never weakens authentication: it fails the request instead of
issuing a bypass.

## 8. Test procedure

1. **Code-level (already green):**
   `tests/backend/SeniorConnect.Modules.Identity.Tests/EmailOtpProviderSecurityTests.cs`
   covers unconfigured-prod failure, dev fallback, incomplete/complete SMTP
   selection, env override, no-logging of codes/credentials, and OTP/token
   replay/expiry/exhaustion.
2. **Deployment smoke (external):**
   - set `Email:Provider=smtp` + host/port/sender + credentials in the
     deployment secret store;
   - boot the API — it must start (no `OptionsValidationException`);
   - trigger `POST /api/v1/auth/request-email-link` with a **synthetic**
     mailbox controlled by the operator;
   - confirm delivery and that the log line shows only a masked recipient;
   - confirm a deliberately wrong credential produces
     `EMAIL_PROVIDER_SEND_FAILED` and no delivery.

## 9. Rollback

- **To disable email delivery:** set `Email:Provider=none` and restart. Sends
  then fail honestly with `EMAIL_PROVIDER_NOT_CONFIGURED`; no credential is
  left dangling.
- **To change provider/credentials:** update the secret store and restart;
  no code change or migration is involved.

## 10. External action list

- [ ] Contract a standards-compliant SMTP relay (operator's choice — not chosen here).
- [ ] Publish/align SPF, DKIM and DMARC for the sender domain.
- [ ] Provision credentials into the deployment secret store
      (`EMAIL_USERNAME` / `EMAIL_PASSWORD` or equivalent).
- [ ] Set `Email:Provider=smtp` + `Host` / `Port` / `SenderAddress` for the deployment.
- [ ] Run the §8 deployment smoke test with a synthetic mailbox.
- [ ] Record the sender identity and provider in the deployment runbook.

**Overall status: EXTERNAL ACTION REQUIRED (no provider provisioned).**
The configuration path is production-safe and fails closed.
