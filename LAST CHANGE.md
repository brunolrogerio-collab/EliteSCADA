# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 12 #201 — COMPLETE / ACCEPTED / CLOSED; WAVE 13 #205 — PREPARED / NOT STARTED**

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

## 4. Wave 13 preparation state

Issue #205 exists as the next coordination surface:

`Wave 13 — Signed Windows x64 package + Authenticode release verification`

Preparation document:

`docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`

State is deliberately **PREPARED / NOT STARTED**.

No Wave 13 implementation branch has been created. The next Coordinator must inspect the then-live `main`, open PRs/issues and current workflows before creating any implementation branch. The implementation branch must be created from live `main`, not copied from the product baseline SHA above if `main` has since advanced through documentation-only commits.

## 5. Wave 13 objective and locked boundaries

Wave 13 is release engineering, not feature expansion. It must produce and verify a controlled Windows x64 package with Authenticode signing and trusted timestamping while preserving accepted Wave 11/12 product contracts.

Before implementation, audit the current Windows publish/package surfaces and decide the exact package/launch or installer contract. Do not pick installer technology by habit.

Signing boundary is locked:

- private code-signing keys/certificates do not enter source control, GitHub, normal CI secrets/artifacts, logs or product packages;
- prefer a protected signing service or hardware-backed key;
- release verification must fail closed for missing/invalid Authenticode signatures, missing trusted timestamp, integrity mismatch or unexpected package contents;
- SmartScreen reputation is distinct from cryptographic signature validity and must not be falsely claimed.

Commercial DNP3 inclusion remains gated on an appropriate Step Function I/O `dnp3` 1.6.0 commercial license or an approved/revalidated dependency replacement.

Linux `.deb` remains **SPECIFIED / NOT STARTED** and requires explicit Development Lead authorization.

## 6. Exact next action for the next Coordinator

1. read `PROJECT GOAL.md`;
2. read this file;
3. read `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. read `docs/ROADMAP.md`;
5. read `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`;
6. read issue #205;
7. read `docs/CI-VALIDATION-POLICY.md`;
8. verify live `main`, open PRs/issues and exact Actions state;
9. audit current Windows publishing/package/signing surfaces;
10. only then create a dedicated Wave 13 implementation branch from live `main` and persist the planned slices before coding.

Do not begin Wave 14 owner validation, Wave 15 corrections, Linux packaging or physical L4 work as part of Wave 13 preparation.
