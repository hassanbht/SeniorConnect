# Credential Exposure Remediation

> **Status:** 2026-10-02 (launch-security & deployment-closure phase).
> This document records a **security finding and its repository-side
> remediation**. It is an evidence record, not a legal or compliance
> statement. **No credential was rotated by this phase.** Git history was
> **not** rewritten.

## 1. Finding

During the previous phase's tracked-secret scan, the file `opencode.json`
(committed at the repository root) was found to contain **two plaintext
`sk-…` API keys** for local developer LLM-relay tooling.

| Property | Value |
| --- | --- |
| Affected file | `opencode.json` (repository root, **tracked by Git**) |
| Provider 1 | `omniroute` — base URL `http://localhost:20128/v1` |
| Provider 2 | `krouter` — base URL `http://localhost:20228/v1` |
| Masked identifier 1 | `sk-4c0a…7101` |
| Masked identifier 2 | `sk-d36e…a424` |
| Secret type | Bearer API keys for **localhost-only** LLM relay endpoints |
| Referenced by SeniorConnect runtime | **No** — no application code, config, test or script references these keys (verified with `git grep` across the working tree and every commit). |
| Network reachability | `localhost` only — the endpoints are not routable from outside the developer machine. |
| Value still in working tree | **No** — removed by this phase (§3). |
| Value still in Git history | **Yes** — present in historical commits (§4). |

The keys are **syntactically real** credentials for the developer's local
LLM-relay tools. They are not SeniorConnect platform credentials, but they
**were committed as plaintext** and are therefore treated as exposed.

## 2. Current-state remediation (applied)

1. **Removed the secret values from the tracked working tree.** Both
   `apiKey` values were replaced with configuration references:

   ```json
   "apiKey": "{env:OMNIROUTE_API_KEY}"
   "apiKey": "{env:KROUTER_API_KEY}"
   ```

   (OpenCode's documented `{env:VARIABLE_NAME}` substitution; the committed
   file now contains **placeholders only**.)

2. **The file remains usable without embedded credentials.** Developers set
   the two environment variables locally (or use an untracked override, §3).
   If a local variable is unset, the tool fails to authenticate — the honest
   failure mode — rather than falling back to a committed secret.

3. **`.gitignore` hardened** with the local-secret/config variants:
   `opencode.local.json`, `.env.local`, `.env.*.local`, `*.local.env`.
   A developer who prefers a local override file can keep real keys in
   `opencode.local.json` (untracked) while `opencode.json` stays
   placeholder-only.

4. **Example configuration with placeholders only:** the committed
   `opencode.json` now serves this role; it contains only the two
   `{env:…}` references plus non-secret `localhost` base URLs.

> Remediation does **not** undo historical exposure. Rotating the two keys is
> an **external owner action** (§5).

## 3. What was removed

| File | Before | After |
| --- | --- | --- |
| `opencode.json` | `"apiKey": "sk-4c0a…7101"` | `"apiKey": "{env:OMNIROUTE_API_KEY}"` |
| `opencode.json` | `"apiKey": "sk-d36e…a424"` | `"apiKey": "{env:KROUTER_API_KEY}"` |
| `.gitignore` | *(no local-override rule)* | added `opencode.local.json`, `.env.local`, `.env.*.local`, `*.local.env` |

No other tracked file contains these keys. No SeniorConnect secret was found
in any tracked production/tooling configuration.

## 4. Git history secret audit

Determined from `git log --diff-filter=A -- opencode.json` and
`git log -p -- opencode.json` (values never printed in full; masked here):

| Event | Commit | Date | Content |
| --- | --- | --- | --- |
| File first added | `4a09731` (`20260911`) | 2026-09-11 | a **different** single key, masked `sk-f7b4…fb96` |
| Keys replaced | `504a78f` (`change IdentityService`) | 2026-09-14 | the **two current** keys `sk-4c0a…7101`, `sk-d36e…a424` |
| Current tree | `6f9c5ec` (`P9-07 , P9-10`) | 2026-09-29 | working tree — keys present until this phase |

- **First known commit containing the current keys:** `504a78f`, 2026-09-14.
  A **prior, different** key (`sk-f7b4…fb96`) existed since the file was first
  added (`4a09731`, 2026-09-11).
- **Present in current history:** yes. Every commit from `504a78f` to `HEAD`
  still contains the two current keys inside `opencode.json`; the earlier
  `sk-f7b4…fb96` remains in the earliest commits.
- **Recall that `504a78f` is an ancestor of `HEAD`** — so a normal `git log`
  of the branch shows them.
- **Was the file public/shared?** The repository has a configured remote
  (`origin → https://github.com/hassanbht/SeniorConnect.git`). Whether that
  remote is public cannot be determined from this working copy; the values
  must be treated as potentially shared regardless.
- **Total commits in history:** 54 (all local history retained; no rewrite).

**History rewrite was not performed.**

## 5. External credential rotation requirement

> **EXTERNAL CREDENTIAL ROTATION REQUIRED**

The two keys (`sk-4c0a…7101`, `sk-d36e…a424`) and the historical key
(`sk-f7b4…fb96`) must be treated as compromised:

- **Owner action (external, human):** revoke/rotate all three keys in the
  local LLM-relay tooling and issue fresh values.
- The new values must live **only** in the environment / secret store (or an
  untracked `opencode.local.json`) — never in a committed file.
- This phase **did not** rotate them (hard rule: no automatic external
  credential rotation).

## 6. History-rewrite recommendation

- **This phase:** do **not** rewrite history (hard rule). The working tree is
  now clean; history still carries the old values.
- **Deployment owner decision (recommended, not performed here):** because the
  keys are now being rotated, the residual risk in history is reduced to
  already-revoked values. If the owner wants history purged anyway, that is a
  deliberate, separately-approved operation (e.g. `git filter-repo` +
  coordinated force-push) — **not** an automatic step and **not** part of this
  phase.
- Until rotation is complete, treat the repository history as containing live
  secrets.

## 7. Owner action checklist

- [ ] Revoke/rotate `sk-4c0a…7101` (OmniRoute local relay).
- [ ] Revoke/rotate `sk-d36e…a424` (kRouter local relay).
- [ ] Revoke `sk-f7b4…fb96` (historical, first-commit key).
- [ ] Store replacement values in the environment / untracked local override.
- [ ] Decide (with the deployment owner) whether a history rewrite is wanted;
      if yes, schedule it as a separate approved operation.
- [ ] Re-run the tracked-secret scan after rotation to confirm no plaintext
      key remains in the working tree.

**Overall status: repository remediation DONE; external rotation PENDING —
EXTERNAL ACTION REQUIRED.** "History rewrite was not performed."
