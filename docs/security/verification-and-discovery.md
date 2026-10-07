# Verification and discovery eligibility (Phase 4)

> Status: implemented 2026-09-25. Related: `location-consent-and-visibility.md`
> (who sees *where*), `block-report-policy.md`, `safeguarding-location-policy.md`,
> ADR-026 (visit address), BR-TRUST-01..07, BR-SAFETY-01..03.

This phase answers one question: may this person **appear** in discovery or
matching? How precisely their location is shown is a separate decision. It stays
with `ILocationVisibilityService`, and nothing here changes it.

## 1. Verification facts

There is no generic "isVerified" flag. Each fact is stored separately in the
existing `verifications` table (BR-TRUST-05: outcomes only, never documents):

| Fact | Where it comes from | Written by |
| --- | --- | --- |
| Email verified | `users.email_verified_at_utc` (email link / login), or a `verifications` row of type `Email` | the system |
| Phone verified | `users.phone_verified_at_utc` (SMS code), or a `verifications` row of type `Phone` | the system |
| Address verified | `verifications` row, type `Address` | organization staff only (§3) |
| Organization verified | `verifications` row, type `Organization`, `verified_by_organization_id` set | organization staff only (§4) |

Each row records `verified_by_user_id`, `verified_by_organization_id`,
`verified_at_utc`, `valid_until_utc` (expiry), `provider`, and, new in Phase 4:

- `method`: how a manual fact was established. `DocumentSighted` means a staff
  member saw, for example, a Meldebestätigung in person; nothing is stored.
  `OrganizationRecord` means the organization already holds a confirmed address.
  `OrganizationMembership` means the organization vouches for an active member.
- `revoked_at_utc`, `revoked_by_user_id`, `revocation_reason`, and status
  `Revoked`. A revoked row stays as history (BR-AUDIT-03).

A fact counts only while it is `Verified`, unexpired and unrevoked.
`VerificationFactsReader` reads everything fresh on every call. There is no
snapshot and no cache, so revocation and expiry apply to the next request.

**No fact implies another.** Organization verification is not an address,
phone, identity (government) or safeguarding check.

### Pre-existing defect fixed

The check constraint `ck_verifications_provider` allowed only lowercase values
(`'manual'`), while EF writes the enum name (`'Manual'`). On PostgreSQL no
verification row could be inserted: phone verification through `/me/phone/verify`
and ID Austria login would have failed with a constraint violation. The Phase 4
migration syncs the constraint and normalises any existing rows. The
`ix_verifications_expiring` filter had the same casing error and is fixed too.
The fix is proven by `Phase4PostgresTests`.

## 2. Verification level

`VerificationFacts.Level` is computed server-side from the ordered
self-verification path:

| Level | Requires |
| --- | --- |
| `NONE` | nothing verified, or phone without email |
| `EMAIL` | email |
| `EMAIL_PHONE` | email and phone |
| `EMAIL_PHONE_ADDRESS` | email, phone and address |

A later step never counts without the earlier ones. Organization verification
is **not** a level; it is the separate `OrganizationVerified` fact (with the
organization ids).

No endpoint accepts a level, an `isVerified` flag or any other verification
value from the client. `T06_NoRequestContractCarriesAClientWritableLevelOrVerifiedFlag`
guards this.

> **Not the trust level.** The existing `TrustLevelCalculator` (0–5, snapshots,
> used by matching's safety gate) is unchanged. It deviates from BR-TRUST-02:
> code L1 = email *or* phone and L2 = both, while BR-TRUST-02 has L1 = email
> *and* phone and L2 = identity. That is decision **V-3** below; changing it
> changes matching outcomes and is out of scope here.

## 3. Address verification (manual, authorized)

Geocoding is **not** verification. A geocoded home only proves an address could
be turned into coordinates; `Geocoding_IsNotAddressVerification` guards this.

The production path is a staff workflow, because the platform cannot yet
automate proof of residence (no postal-letter or eID flow; ID Austria is out of
scope):

```
POST /api/v1/organizations/{organizationId}/volunteers/{volunteerUserId}/verifications
{ "type": "ADDRESS", "method": "DOCUMENT_SIGHTED" | "ORGANIZATION_RECORD", "validUntilUtc": null }
```

- The caller must be an active **Coordinator or Admin** of that organization.
  Plain `Staff`, volunteers and other tenants are refused (403 or 404).
- The target must be an **active volunteer on that organization's roster**
  (404 otherwise). Staff cannot verify strangers or other tenants' people.
- Nobody can verify themselves (`RecordManual` refuses it).
- Recording the same fact twice is idempotent.
- **Moving home invalidates it.** When the person changes the postal code or
  coordinates of their profile, `ProfileService` revokes the current address
  fact (`AddressVerificationInvalidator`).

Revocation, allowed only for staff of the organization that recorded the fact:

```
POST /api/v1/organizations/{organizationId}/verifications/{verificationId}:revoke
{ "reason": "..." }
```

## 4. Organization verification

The same endpoint with `{ "type": "ORGANIZATION", "method": "ORGANIZATION_MEMBERSHIP" }`.
The row keeps the organization id, verifier, timestamp, method and status.

- It **lapses automatically** when the person's volunteer membership in that
  organization is no longer active (checked on every read).
- It may make a person eligible for open discovery (§6). It never grants precise
  location, cross-tenant access or any other fact (tests T04, T16, T17).

## 5. Discoverable setting

`discovery_preferences` holds one row per person. **No row means not
discoverable.** The migration creates no rows, so nobody became findable on
deployment (T09).

```
GET /api/v1/me/discoverability        -> { isDiscoverable, updatedAtUtc }
PUT /api/v1/me/discoverability        { isDiscoverable: true|false }
```

Being discoverable shares **no** location. Being findable and "who can see
where I live" are independent settings (T20).

## 6. Discovery eligibility

`IDiscoveryEligibilityService.FilterEligibleAsync(counterpart, candidates, context)`
(Profiles module) is the only place these rules live. Every purpose applies:

- the candidate is not the counterpart;
- no block in either direction (`block-report-policy.md`);
- no safeguarding **discovery** restriction on the candidate, towards everyone
  or towards this counterpart (`safeguarding-location-policy.md`).

Then per purpose:

| Purpose | Used by | Additional rules |
| --- | --- | --- |
| `OpenDiscovery` | `GET /discovery/nearby-volunteers` | active account **and** discoverable **and** (level ≥ `EMAIL_PHONE` **or** organization-verified) |
| `Matching` | `MatchingService.FindCandidatesAsync` (coordinator candidates, hybrid proposals, automatic tiers) | no safeguarding **assignment** restriction; active account; and either on the **request organization's own active roster**, or discoverable and verified as for open discovery |
| `OwnedContent` | open help-request list, request detail for non-parties, `nearby-requests`, volunteer feed | blocks and discovery restrictions only; publishing a request is the owner's own choice to be seen |

Existing gates stay on top: `IsAcceptingRequests`, the trust level against
`RequiredTrustLevel`, the buddy rule, and distance bands.

**Closes hotfix residual 2.** Coordinators used to see candidates from every
accepting volunteer on the platform. Now they see only their own roster plus
people who chose to be discoverable and are verified. Org A's roster is never a
candidate pool for org B (T15).

### Required verification by activity (matrix)

| Situation | Minimum | Source |
| --- | --- | --- |
| Appearing in open discovery | discoverable + `EMAIL_PHONE`, or organization-verified | decision **V-1** |
| Appearing as a matching candidate of an organization request | own roster, or as open discovery | decision **V-2** (organization rule) |
| Accepting / being assigned | trust level ≥ `RequiredTrustLevel` from `IActivitySafetyPolicy` | BR-SAFETY-01..03 (unchanged) |
| Safety level 3+ | + buddy rule | BR-SAFETY-05 (unchanged) |
| Safeguarding-restricted person | excluded as configured by the officer | `safeguarding-location-policy.md` |

No new risk categories were invented. Higher-risk categories keep using the
existing safety-level to trust-level mapping.

## 7. Verification status API

```
GET /api/v1/me/verification
{ "level": "EMAIL_PHONE",
  "badges": [ { "fact": "ORGANIZATION_VERIFIED", "verifiedAtUtc": "...",
                "verifiedByOrganizationId": "...", "organizationName": "...",
                "method": "ORGANIZATION_MEMBERSHIP", "validUntilUtc": null } ],
  "nextStep": "VERIFY_ADDRESS" }
```

Badges name the fact, who confirmed it and when (BR-TRUST-04). There is no
"safe" or "trusted person" wording anywhere (T07). The existing `GET /me/trust`
is unchanged.

## 8. Flutter

Profile → **"Can people find me?"** (`DiscoverySettingsScreen`) shows:

- the discoverability switch;
- a plain sentence saying that being findable shares no location;
- each fact with who confirmed it and when;
- the next step.

Only server-confirmed values are displayed. A failed change keeps the last
confirmed value, and a network error is an error state, never a logout. The
screen is available in de/en/fa.

## 9. Decisions required

- **V-1** Minimum verification for open discovery is `EMAIL_PHONE`
  (BR-TRUST-02 "Contact Verified") or organization-verified. Address
  verification is deliberately not required just to be listed. Product should
  confirm this.
- **V-2** Organization roster members are matching candidates for their own
  organization without the public discoverable switch: the membership is the
  organization rule. Product should confirm, or require an explicit
  per-organization opt-in.
- **V-3** `TrustLevelCalculator` differs from BR-TRUST-02 (see §2). An ADR is
  needed before changing it, because matching outcomes change.
- **V-4** Who may record an address fact: currently Coordinator/Admin of the
  person's organization. Independent (non-organization) volunteers cannot reach
  `EMAIL_PHONE_ADDRESS` yet; a platform-level process (postal code letter, eID)
  is a later phase.
