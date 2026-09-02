# LAST CHANGE — EliteSCADA

**Date:** 2026-09-02 (BRT)  
**Operational state:** **WAVE 12 #201 — COMPLETE / ACCEPTED / CLOSED; TEMPORARY BROWSER TEST PREVIEW #208 — PLANNED / NEXT; WAVE 13 #205/#207 — PAUSED**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA without superseding that product baseline.

## 1. Accepted product baseline

Wave 12 Hardening is **COMPLETE / ACCEPTED**.

Final accepted Wave 12 product-code `main` baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

This baseline contains the Wave 12 implementation integrated through PR #203 plus the post-merge Modbus CI timing stabilization integrated through PR #204.

Accepted runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; mutable Working never drives HMI Runtime directly.

## 2. Final Wave 12 acceptance evidence

Exact `main` SHA `63bced02426fcb84b26028913f6c68feb3457d80`:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Pre-merge stabilization head `8d9950f56cf4cac8d835f448df8f77dc6a780928` also passed:

- EliteSCADA CI #1095 / `33576006577`: **SUCCESS**;
- L3 Seven-Driver Lab #91 / `33576006594`: **SUCCESS**.

The first post-Wave-12 merge SHA `be710e630da63639af9a0fc63458f9bd92068746` failed EliteSCADA CI #1094 only because two existing Modbus loopback happy-path tests used a runner-sensitive 500 ms request timeout. The failure was diagnosed before any rerun. PR #204 changed only those two healthy-path test timeouts to 2 s; production code and explicit timeout/fault assertions were not weakened.

## 3. Wave 12 accepted findings

All identified findings are **FIXED / REGRESSION / VALIDATED**:

- W12-RT-001 — realtime client isolation and preserved WebSocket 1008 revocation semantics;
- W12-PER-001 — atomic/serialized persistence Save;
- W12-ING-001 — bounded JSON/CSV Engineering ingress;
- W12-PKG-001 — `.escadapkg` export/import resource-limit symmetry;
- W12-PER-002 — Persistence Apply mutation lease + caller-observed workspace CAS;
- W12-AUTH-001 — serialized local-identity logical mutations and last-enabled-administrator invariant;
- W12-AUTH-002 — bounded login-limiter key lifecycle without active lockout eviction;
- W12-API-001 — deterministic request validation and typed/sanitized historical failures;
- W12-AUD-001 — durable pre-mutation audit admission for unsafe `/api` mutations, failing closed on audit-store outage before endpoint execution.

Historical detail remains in `docs/WAVE-12-HARDENING-AUDIT.md` and issue #201.

## 4. Current coordination pivot — Temporary Browser Test Preview

Development Lead direction on 2026-09-02 pauses Wave 13 and inserts a temporary browser-access Test Preview before Wave 13 resumes.

Tracking:

- issue #208 — `Temporary browser Test Preview — Codespaces launch environment`;
- preparation/requirements document — `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`.

State: **PLANNED / NEXT**.

Target operator path:

`Open Codespace / Launch Test Preview -> automatic environment startup -> temporary authenticated Web URL -> use the real EliteSCADA`

Preferred direction is GitHub Codespaces with automated startup of the real .NET backend, React/Pyodide frontend and PostgreSQL/TimescaleDB environment, automatic loading of the validated Wave 11 Demo `.escadapkg` where compatible, normal Save/Publish/Activate transition to Active Runtime, and Web-only temporary exposure.

The environment is development/homologation only. It is not a production host, permanent public service, supported deployment target or durability/SLA claim.

A dedicated administrative Preview account is required with username `EliteSCADA`. The requested password was supplied directly by the Development Lead but must **not** be committed to this public repository or emitted in normal artifacts/logs. Implementation should inject it through protected Codespaces/GitHub secret material such as `ELITESCADA_PREVIEW_ADMIN_PASSWORD` and fail clearly when it is unavailable.

## 5. Wave 13 paused state

Issue #205 remains open. Draft implementation PR #207 on branch `wave13/windows-release-signing` contains the Wave 13 audit/release-engineering work already performed.

Wave 13 state is now deliberately **PAUSED**.

Do not merge, expand or treat PR #207 as current implementation truth while issue #208 is active. Preserve the branch and its evidence. When Wave 13 resumes, first re-read live `main`, issue #205, PR #207, exact current CI and the package/signing audit because Preview work may have advanced product/application startup surfaces.

Wave 13 remains responsible for Windows x64 packaging, Authenticode signing, trusted timestamping, deterministic signed-byte manifest verification and its existing DNP3 commercial-distribution gate. Pausing does not weaken those requirements.

## 6. Exact next action for the next Coordinator

1. read `PROJECT GOAL.md`;
2. read this file;
3. read `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. read `docs/ROADMAP.md`;
5. read `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`;
6. read issue #208;
7. read `docs/CI-VALIDATION-POLICY.md`;
8. verify live `main`, open PRs/issues and exact Actions state;
9. audit the current local-development/startup, database, authentication bootstrap, `.escadapkg` import/activation and Codespaces/devcontainer surfaces;
10. persist Test Preview implementation slices before changing product code;
11. implement the smallest repeatable `Launch Test Preview` path while preserving the accepted backend/runtime/security authority;
12. leave Wave 13 #205/#207 paused until the Development Lead resumes it.

Do not begin Wave 14 owner validation, Wave 15 corrections, Linux packaging or physical L4 work as part of this Test Preview pivot.
