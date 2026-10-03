#!/usr/bin/env python3
"""SeniorConnect — HTTP-level synthetic dry run (deployment-integration phase).

Executes the approved synthetic scenario against a REAL running API over HTTP
using only synthetic data (synthetic users, one synthetic organization,
synthetic coordinates, synthetic email identities, one synthetic help request):

   1. register synthetic users (email + password)
   2. authenticate through the configured provider channel
      (Development: OTP log fallback; a real provider is configured via the
      Email:Provider deployment configuration — no vendor is embedded)
   3. verify the accounts
   4. create the synthetic organization + memberships (admin paths)
   5. create the synthetic volunteer profile + enable discoverability +
      verify a synthetic phone number (trust level L2 via the dev SMS channel)
   6. create the synthetic help request
   7. matching (organization-admin authorized hybrid proposals)
   8. assignment (volunteer accepts)
   9. verify the visit-address gate stays OFF (assignee baseline under
      BR-COMM-04, no grant row, exact address withheld from org staff)
  10. block relationship -> automatic unassignment (B-1)
  11. unsafe-behavior report (group-member target) -> officer conversion (B-2)
  12. impose restriction -> S-2 operational task / S-3 operational hold /
      S-4 platform-level case ownership
  13. verify every state transition went through real API boundaries
  14. cleanup = drop the disposable environment (documented at the end)

Setup performed by THIS script for the dry-run environment (documented
deployment bootstrap, not an API bypass):
  - capability grants (PlatformAdmin / SafeguardingOfficer) are inserted into
    user_capabilities of the DISPOSABLE database — these are deployment
    provisioning steps no API endpoint exposes (by design).

Usage:
  SC_DRYRUN_API_LOG=<api log file> python scripts/http_dry_run.py [BASE_URL]

BASE_URL default: http://localhost:8080. Requires the API running in
Development against a DISPOSABLE PostgreSQL database.
Exit 0 = all steps passed; 1 = any step failed (nothing is faked).
"""

from __future__ import annotations

import base64
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

BASE_URL = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080"
ADMIN_PASSWORD = "S3cureDryRun!2026"
PG_CONTAINER = os.environ.get("SC_DRYRUN_PG_CONTAINER", "sc_test_pg_phase")
PG_DB = os.environ.get("SC_DRYRUN_PG_DB", "sc_dryrun_http")
PG_USER = os.environ.get("SC_DRYRUN_PG_USER", "seniorconnect")
API_LOG = os.environ.get("SC_DRYRUN_API_LOG")
RUN_TAG = "dryrun-" + uuid.uuid4().hex[:8]

PASSED = 0
FAILED = 0


def step(number: int, name: str):
    print(f"\n[{number:02d}] {name}")


def check(label: str, condition: bool, detail: str = ""):
    global PASSED, FAILED
    if condition:
        PASSED += 1
        print(f"     PASS  {label}")
    else:
        FAILED += 1
        print(f"     FAIL  {label} {detail}")


def request(method: str, path: str, body: dict | None = None, token: str | None = None):
    """Returns (status, parsed_json_or_none). Never raises on HTTP errors."""
    data = json.dumps(body).encode("utf-8") if body is not None else None
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(BASE_URL + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            raw = resp.read().decode("utf-8", "replace")
            try:
                return resp.status, (json.loads(raw) if raw else None)
            except json.JSONDecodeError:
                return resp.status, {"raw": raw[:400]}
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        try:
            return e.code, json.loads(raw)
        except json.JSONDecodeError:
            return e.code, {"raw": raw}
    except urllib.error.URLError as e:
        return 0, {"raw": f"connection error: {e}"}


def bearer(auth_response: dict | None) -> str | None:
    return auth_response.get("accessToken") if isinstance(auth_response, dict) else None


def jwt_payload(token: str) -> dict:
    payload = token.split(".")[1]
    payload += "=" * (-len(payload) % 4)
    return json.loads(base64.urlsafe_b64decode(payload))


def read_otp_from_provider_channel() -> str | None:
    """Development delivery channel: the API's own log output.

    The API (Development host, Email:Provider 'none') logs the code to its
    log as '[Email Adapter][Development] ...'. That is the documented
    Development delivery path. Production uses a configured provider instead;
    nothing here bypasses that rule.
    """
    if not API_LOG or not os.path.exists(API_LOG):
        return None
    try:
        with open(API_LOG, "r", encoding="utf-8", errors="replace") as fh:
            tail = fh.read()[-20000:]
        matches = re.findall(r"verification code is: (\d{6})", tail)
        return matches[-1] if matches else None
    except OSError:
        return None


def psql(sql: str) -> str:
    out = subprocess.run(
        ["docker", "exec", PG_CONTAINER, "psql", "-U", PG_USER, "-d", PG_DB, "-t", "-A", "-c", sql],
        capture_output=True, text=True, timeout=30)
    return (out.stdout or "").strip()


def wait_for_api(timeout_seconds: int = 30) -> bool:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        status, _ = request("GET", "/healthz/ready")
        if status == 200:
            return True
        time.sleep(3)
    return False


def main() -> int:
    print(f"=== SeniorConnect HTTP synthetic dry run — tag {RUN_TAG} ===")
    print(f"Base URL: {BASE_URL}")

    step(0, "Environment health")
    ready = wait_for_api()
    check("API ready probe responds 200", ready)
    if not ready:
        print("\nRESULT: HTTP DRY RUN = BLOCKED — API not reachable / not ready.")
        return 1

    senior_email = f"senior-{RUN_TAG}@synthetic.invalid"
    volunteer_email = f"volunteer-{RUN_TAG}@synthetic.invalid"
    officer_email = f"officer-{RUN_TAG}@synthetic.invalid"
    admin_email = f"admin-{RUN_TAG}@synthetic.invalid"

    # --- 1. register synthetic users --------------------------------------
    step(1, "Register synthetic users (email + password)")
    for label, email in (("senior", senior_email), ("volunteer", volunteer_email),
                         ("officer", officer_email), ("admin", admin_email)):
        status, body = request("POST", "/api/v1/auth/register", {
            "email": email, "password": ADMIN_PASSWORD,
            "confirmPassword": ADMIN_PASSWORD, "preferredLocale": "en",
        })
        check(f"{label} registration accepted", status in (200, 201), f"got {status}: {body}")

    # --- 2+3. authenticate via the configured provider channel + verify ---
    step(2, "Authenticate through the configured provider channel")
    step(3, "Verify the accounts")
    tokens: dict[str, str] = {}
    ids: dict[str, str] = {}

    for label, email in (("senior", senior_email), ("volunteer", volunteer_email),
                         ("officer", officer_email), ("admin", admin_email)):
        status, body = request("POST", "/api/v1/auth/request-email-link",
                               {"email": email, "preferredLocale": "en"})
        check(f"{label} OTP requested through provider", status == 200, f"got {status}: {body}")
        code = read_otp_from_provider_channel()
        check(f"{label} OTP delivered via provider channel", code is not None)
        if code is None:
            continue
        vstatus, vbody = request("POST", "/api/v1/auth/verify-email", {
            "email": email, "code": code, "deviceLabel": "dry-run", "isPersonalDevice": True,
        })
        check(f"{label} verified + tokens issued", vstatus == 200 and bearer(vbody),
              f"got {vstatus}: {vbody}")
        if bearer(vbody):
            tokens[label] = bearer(vbody)
            # The identity is the token's 'sub' claim, not a /me body field.
            ids[label] = jwt_payload(tokens[label]).get("sub")

    check("all four identities resolved", len(ids) == 4, f"resolved: {sorted(ids)}")

    # Deployment bootstrap (disposable env): capability grants for the
    # platform admin and the safeguarding officer. These provisioning rows
    # have no API surface by design; they are inserted into the DISPOSABLE
    # database only, with a recorded reason.
    if len(ids) == 4:
        psql(f"INSERT INTO user_capabilities (id, user_id, capability, source, reason, granted_at_utc) "
             f"SELECT gen_random_uuid(), '{ids['admin']}', 'PlatformAdmin', 'Granted', "
             f"'dry-run bootstrap {RUN_TAG}', now() WHERE NOT EXISTS "
             f"(SELECT 1 FROM user_capabilities WHERE user_id = '{ids['admin']}' AND capability = 'PlatformAdmin');")
        psql(f"INSERT INTO user_capabilities (id, user_id, capability, source, reason, granted_at_utc) "
             f"SELECT gen_random_uuid(), '{ids['officer']}', 'SafeguardingOfficer', 'Granted', "
             f"'dry-run bootstrap {RUN_TAG}', now() WHERE NOT EXISTS "
             f"(SELECT 1 FROM user_capabilities WHERE user_id = '{ids['officer']}' AND capability = 'SafeguardingOfficer');")
        # The platform-operator surrogate needs the officer capability claim
        # too: the S-4 case endpoint gates on it before the service-side
        # platform-officer membership decides org-less case visibility.
        psql(f"INSERT INTO user_capabilities (id, user_id, capability, source, reason, granted_at_utc) "
             f"SELECT gen_random_uuid(), '{ids['admin']}', 'SafeguardingOfficer', 'Granted', "
             f"'dry-run bootstrap {RUN_TAG}', now() WHERE NOT EXISTS "
             f"(SELECT 1 FROM user_capabilities WHERE user_id = '{ids['admin']}' AND capability = 'SafeguardingOfficer');")
        # Re-login so the new claims are minted into fresh access tokens.
        for label in ("admin", "officer"):
            email = {"admin": admin_email, "officer": officer_email}[label]
            request("POST", "/api/v1/auth/request-email-link", {"email": email, "preferredLocale": "en"})
            code = read_otp_from_provider_channel()
            if code:
                vs, vb = request("POST", "/api/v1/auth/verify-email", {
                    "email": email, "code": code, "deviceLabel": "dry-run", "isPersonalDevice": True})
                if bearer(vb):
                    tokens[label] = bearer(vb)

    # --- 4. synthetic organization + memberships --------------------------
    step(4, "Synthetic organization setup")
    org_id = None
    if "admin" in tokens:
        s, org = request("POST", "/api/v1/organizations", {
            "name": f"Synthetic Org {RUN_TAG}", "type": 0,
            "legalName": f"Synthetic Org {RUN_TAG} (synthetic)",
            "supportEmail": f"support-{RUN_TAG}@synthetic.invalid",
        }, token=tokens["admin"])
        org_id = org.get("id") if isinstance(org, dict) else None
        check("synthetic organization created (PlatformAdmin path)", s == 201 and org_id, f"got {s}: {org}")
        if org_id:
            # Admin = org staff (matching + coordinator surfaces), Volunteer =
            # plain volunteer (staff visibility must not mask the B-1
            # released-volunteer check), SafeguardingOfficer = B-2 conversion.
            for label, role_int in (("admin", 2), ("volunteer", 5), ("officer", 3)):
                mstatus, mbody = request("POST", f"/api/v1/organizations/{org_id}/members", {
                    "userId": ids[label], "role": role_int,
                }, token=tokens["admin"])
                check(f"{label} membership added", mstatus == 200, f"got {mstatus}: {mbody}")
            # No token refresh here: memberships are never minted into JWTs
            # (JwtTokenService.GenerateAccessToken) — every membership check
            # reads the database; the admin/officer capability claims were
            # already minted by the bootstrap re-login above.
    else:
        check("platform admin token available", False, "deployment bootstrap failed")

    # --- 5. volunteer profile + discoverability + phone (trust L2) --------
    step(5, "Volunteer profile + discoverability + phone verification")
    vs, _ = request("PUT", "/api/v1/me/volunteer-profile", {
        "maxTravelDistanceKm": 20, "maxHoursPerWeek": 10, "hasCar": False,
        "hasDrivingLicense": False, "isCurrentlyAvailable": True,
        "latitude": 48.2082, "longitude": 16.3738,
        "addressCity": "Wien", "addressPostalCode": "1010",
    }, token=tokens.get("volunteer"))
    check("volunteer profile upserted (synthetic coordinates)", vs == 200, f"got {vs}")
    ds, _ = request("PUT", "/api/v1/me/discoverability", {"isDiscoverable": True},
                    token=tokens.get("volunteer"))
    check("volunteer discoverability enabled", ds == 200, f"got {ds}")

    # Trust level L2 (email + phone): the synthetic PublicPlace help request
    # requires L2 (BR-SAFETY-02/04). Development delivers the SMS OTP to the
    # API's own log — the documented dev channel; no vendor is embedded.
    phone = "+43660" + f"{int(RUN_TAG.split('-')[-1], 16) % 10_000_000:07d}"
    prs, prb = request("POST", "/api/v1/me/phone/request-verification",
                       {"phone": phone, "preferredLocale": "en"}, token=tokens.get("volunteer"))
    check("phone OTP requested through provider", prs == 200, f"got {prs}: {prb}")
    sms_code = read_otp_from_provider_channel()
    check("phone OTP delivered via provider channel", sms_code is not None)
    if sms_code:
        pvs, pvb = request("POST", "/api/v1/me/phone/verify",
                           {"phone": phone, "code": sms_code}, token=tokens.get("volunteer"))
        check("phone verified for the synthetic volunteer", pvs in (200, 204), f"got {pvs}: {pvb}")
        # TrustLevelSnapshots are minted on login: one fresh session so the
        # L2 snapshot exists before matching and accept read it.
        lstatus, lbody = request("POST", "/api/v1/auth/login", {
            "email": volunteer_email, "password": ADMIN_PASSWORD,
            "deviceLabel": "dry-run", "isPersonalDevice": True})
        if bearer(lbody):
            tokens["volunteer"] = bearer(lbody)
        check("volunteer re-login mints the L2 trust snapshot",
              lstatus == 200 and bearer(lbody), f"got {lstatus}: {lbody}")

    # --- 6. synthetic help request ----------------------------------------
    step(6, "Create synthetic help request")
    cs, cats = request("GET", "/api/v1/activities/categories", token=tokens.get("senior"))
    category_id = None
    if cs == 200 and isinstance(cats, list):
        for c in cats:
            if isinstance(c, dict) and c.get("code") == "shopping":
                category_id = c.get("id")
                break
    check("activity category resolved (shopping)", bool(category_id), f"got {cs}")
    start = time.time() + 86400
    hs, help_req = request("POST", "/api/v1/help-requests", {
        "organizationId": org_id,
        "categoryId": category_id,
        "scheduledStartUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(start)),
        "scheduledEndUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(start + 3600)),
        "durationMinutes": 60,
        "locationType": 1,
        "notes": f"Synthetic dry-run request {RUN_TAG} — grocery shopping.",
        "address": "Synthetic-Strasse 1",
        "postalCode": "1010",
        "city": "Wien",
        "latitude": 48.2082, "longitude": 16.3738,
        "transportMode": 0,
        "insuranceContext": 0,
    }, token=tokens.get("senior"))
    request_id = help_req.get("id") if isinstance(help_req, dict) else None
    row_version = help_req.get("rowVersion", 1) if isinstance(help_req, dict) else 1
    check("synthetic help request created", hs == 201 and request_id, f"got {hs}: {help_req}")

    # --- 7. matching (organization-admin authorized) -----------------------
    step(7, "Matching")
    ms, proposals = request("POST", "/api/v1/matching/proposals:hybrid", {
        "helpRequestId": request_id, "enableCompletionPrediction": False,
    }, token=tokens.get("admin"))
    check("matching proposals returned for org staff (admin)", ms == 200 and isinstance(proposals, list),
          f"got {ms}: {proposals}")
    if isinstance(proposals, list) and proposals:
        ids_in = [p.get("candidateVolunteerUserId") for p in proposals if isinstance(p, dict)]
        check("synthetic volunteer present among candidates", ids.get("volunteer") in ids_in,
              f"candidates: {ids_in}")
    else:
        check("synthetic volunteer present among candidates", False, "empty candidate list")

    # --- 8. assignment ------------------------------------------------------
    step(8, "Assignment (volunteer accepts)")
    acc_status, acc_body = request("POST", f"/api/v1/help-requests/{request_id}:accept",
                                   {"expectedRowVersion": row_version}, token=tokens.get("volunteer"))
    check("assignment created", acc_status == 200, f"got {acc_status}: {acc_body}")

    # --- 9. visit-address gate stays OFF + address withheld ----------------
    step(9, "Visit-address gate OFF + address withholding (ADR-026)")
    s, vdetail = request("GET", f"/api/v1/help-requests/{request_id}", token=tokens.get("volunteer"))
    check("live assignee can read the request", s == 200, f"got {s}")
    assignee_address = vdetail.get("locationAddress") if isinstance(vdetail, dict) else None
    # Gate-OFF signature (test VisitAddress_ADR026Grant_IsGatedOffByDefault_
    # SoLiveAssigneeStillSeesTheAddress): with VisitAddress:RequireGrant=false
    # the live assignee sees the address under the unchanged BR-COMM-04
    # baseline; with the gate ON this would be null until a grant exists.
    check("gate OFF: live assignee sees the address under BR-COMM-04 (no grant required)",
          bool(assignee_address), f"address was: {assignee_address!r}")
    grants = psql(f"SELECT count(*) FROM visit_address_grants WHERE help_request_id = '{request_id}';")
    check("no visit-address grant exists (ADR-026 grant flow never ran)",
          grants == "0", f"count={grants}")
    # Holds in BOTH gate states: organization staff are not entitled to the
    # exact visit address (ADR-026 open question (d) stays pending).
    s, admdetail = request("GET", f"/api/v1/help-requests/{request_id}", token=tokens.get("admin"))
    staff_address = admdetail.get("locationAddress") if isinstance(admdetail, dict) else None
    check("exact address withheld from organization staff (admin)",
          s == 200 and not staff_address, f"got {s}, address was: {staff_address!r}")

    # --- 10. block -> automatic unassignment (B-1) -------------------------
    step(10, "Block relationship + automatic unassignment (B-1)")
    bs, _ = request("POST", "/api/v1/trust-safety/blocks",
                    {"blockedUserId": ids.get("volunteer"), "reason": None},
                    token=tokens.get("senior"))
    check("block created", bs == 200, f"got {bs}")
    s, rdetail = request("GET", f"/api/v1/help-requests/{request_id}", token=tokens.get("senior"))
    released = isinstance(rdetail, dict) and rdetail.get("status") in ("Open", 1) \
        and rdetail.get("assignedVolunteerUserId") is None
    check("assignment auto-released to Open after block (B-1)", released,
          f"status={rdetail.get('status') if isinstance(rdetail, dict) else rdetail}")
    s, _ = request("GET", f"/api/v1/help-requests/{request_id}", token=tokens.get("volunteer"))
    check("released volunteer no longer sees the request", s == 404, f"got {s}")

    # --- 11. unsafe-behavior report + officer conversion (B-2) -------------
    step(11, "Unsafe-behavior report + officer conversion (B-2)")
    # P5-09: a Member report requires the target to belong to a community
    # group. The org staff create a synthetic public group (staff-verified
    # organization path) and the volunteer joins it.
    # Letters-only tag for the group name: the rule-based moderation screen
    # pattern-matches digit runs as a possible street address and would hold a
    # randomly-digit-heavy name in Draft (not accepting members).
    group_tag = re.sub(r"[^A-Za-z]", "", RUN_TAG)
    gs, gbody = request("POST", "/api/v1/community/groups", {
        "name": f"Synthetic Dry-Run Group {group_tag}",
        "description": "Synthetic community group for the deployment dry run.",
        "type": 0, "visibility": 0, "organizationId": org_id,
        "city": "Wien", "postalCode": "1010",
    }, token=tokens.get("admin"))
    group_id = gbody.get("id") if isinstance(gbody, dict) else None
    check("synthetic community group created (org staff path)", gs == 201 and group_id,
          f"got {gs}: status={gbody.get('status') if isinstance(gbody, dict) else None}, {gbody}")
    if group_id:
        jv, jb = request("POST", f"/api/v1/community/groups/{group_id}/join", None,
                         token=tokens.get("volunteer"))
        check("volunteer joined the community group (report target is a member)",
              jv == 200, f"got {jv}: {jb}")
    rs, report = request("POST", "/api/v1/community/reports", {
        "targetType": 1, "targetId": ids.get("volunteer"), "category": 5,
        "description": f"Synthetic unsafe-behavior report {RUN_TAG}",
    }, token=tokens.get("senior"))
    report_id = report.get("id") if isinstance(report, dict) else None
    check("unsafe-behavior report created", rs == 201 and report_id, f"got {rs}: {report}")
    case_id = None
    if report_id:
        cvs, converted = request("POST", f"/api/v1/community/reports/{report_id}:convert-to-case", {
            "summary": f"Synthetic case {RUN_TAG}", "severity": "Medium", "organizationId": org_id,
        }, token=tokens.get("officer"))
        case_id = converted.get("caseId") if isinstance(converted, dict) else None
        check("officer converts report into case (B-2)", cvs == 201 and case_id,
              f"got {cvs}: {converted}")

    # --- 12. restriction + S-2 / S-3 ----------------------------------------
    step(12, "Restriction (S-2 operational task / S-3 neutral indicator)")
    if case_id:
        ims, imposed = request("POST", f"/api/v1/safeguarding/cases/{case_id}/restrictions", {
            "counterpartUserId": None, "blocksDiscovery": True, "blocksLocation": True,
            "blocksContact": True, "blocksAssignment": True,
        }, token=tokens.get("officer"))
        check("officer imposes restriction", ims == 200, f"got {ims}: {imposed}")
        ots, otasks = request("GET",
                                      f"/api/v1/coordinator/operational-tasks?organizationId={org_id}",
                                      token=tokens.get("admin"))
        content_free = True
        if isinstance(otasks, list):
            serialized = json.dumps(otasks).lower()
            content_free = ("unsafebehavior" not in serialized
                            and "harassment" not in serialized
                            and "case" not in serialized)
        check("coordinator operational-task surface answers and stays content-free (S-2/S-3)",
              ots == 200 and content_free, f"got {ots}: {otasks}")

        # S-3: the volunteer roster marks the subject with a content-free
        # operational hold — no restriction kind, reason, case reference or
        # officer identity may appear on it (BR-SG-05).
        rsv, roster = request("GET", f"/api/v1/coordinator/volunteers?organizationId={org_id}",
                              token=tokens.get("admin"))
        hold_row = next((r for r in roster
                         if isinstance(r, dict) and r.get("userId") == ids.get("volunteer")), None) \
            if isinstance(roster, list) else None
        check("S-3: roster marks the subject with the operational hold",
              rsv == 200 and hold_row is not None and hold_row.get("operationalHold") is True,
              f"got {rsv}, row={hold_row}")
        check("S-3: hold row stays content-free (no case/restriction details)",
              hold_row is not None and not any(w in json.dumps(hold_row).lower()
                                               for w in ("unsafe", "harassment", "safeguarding",
                                                         "restriction", "case")),
              f"row={hold_row}")

        # S-4: a second report converted WITHOUT an organization becomes an
        # organization-less case. Visible only to the appointed platform
        # safeguarding officer; ordinary org officers are denied fail-closed.
        s2, report2 = request("POST", "/api/v1/community/reports", {
            "targetType": 1, "targetId": ids.get("volunteer"), "category": 5,
            "description": f"Synthetic org-less unsafe-behavior report {RUN_TAG}",
        }, token=tokens.get("officer"))
        report2_id = report2.get("id") if isinstance(report2, dict) else None
        check("S-4: second unsafe-behavior report created (officer as reporter)",
              s2 == 201 and report2_id, f"got {s2}: {report2}")
        orgless_case = None
        if report2_id:
            c2, conv2 = request("POST", f"/api/v1/community/reports/{report2_id}:convert-to-case", {
                "summary": f"Synthetic organization-less case {RUN_TAG}", "severity": "Medium",
            }, token=tokens.get("officer"))
            orgless_case = conv2.get("caseId") if isinstance(conv2, dict) else None
            check("S-4: report converted into an organization-less case",
                  c2 == 201 and orgless_case, f"got {c2}: {conv2}")
        if orgless_case:
            pa, pab = request("POST", f"/api/v1/safeguarding/platform-officers/{ids['admin']}",
                              None, token=tokens.get("admin"))
            check("S-4: platform safeguarding officer appointed via the audited API",
                  pa in (200, 201, 204), f"got {pa}: {pab}")
            pv_admin, _ = request("GET", f"/api/v1/safeguarding/cases/{orgless_case}",
                                  token=tokens.get("admin"))
            check("S-4: appointed platform officer sees the organization-less case",
                  pv_admin == 200, f"got {pv_admin}")
            pv_off, _ = request("GET", f"/api/v1/safeguarding/cases/{orgless_case}",
                                token=tokens.get("officer"))
            check("S-4: organization officer is denied the organization-less case (fail-closed)",
                  pv_off == 404, f"got {pv_off}")

    # --- 13. audit trail ----------------------------------------------------
    step(13, "Audit trail")
    audits = psql("SELECT count(*) FROM audit_entries;") if org_id else "0"
    check("audit entries recorded in the disposable database", audits.isdigit() and int(audits) > 0,
          f"count={audits if org_id else 'n/a'}")
    sglogs = psql("SELECT count(*) FROM safeguarding.access_logs;") if case_id else "0"
    check("safeguarding access logs recorded (BR-SG-06)", sglogs.isdigit() and int(sglogs) > 0,
          f"count={sglogs}")

    print("\n=== DRY RUN SUMMARY ===")
    print(f"PASS: {PASSED}  FAIL: {FAILED}")
    print("\nCleanup (synthetic data only):")
    print(f"  docker rm -f {PG_CONTAINER}")
    print("\nRESULT: HTTP DRY RUN = " + ("PASS" if FAILED == 0 else "BLOCKED"))
    return 0 if FAILED == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
