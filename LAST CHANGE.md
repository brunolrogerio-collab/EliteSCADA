# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 12 #201 IN PROGRESS — PR #202 / SLICE A + W12-PER-002 VALIDATED / W12-AUTH-001 NEXT**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance the branch beyond the latest validated product-code SHA.

## 1. Accepted foundation

Wave 11 is **COMPLETE / ACCEPTED / CLOSED** under issue #194.

Final accepted Wave 11 product-code `main` baseline:

`4ccc29cb4bb334dc473d8265f48a9c8601993413`

Accepted Runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

The owner-test application remains `EliteSCADA-Wave11-Demo.escadapkg`, SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`.

## 2. Wave 12 live execution state

Issue #201 is **OPEN / IN PROGRESS**.

- branch base / live `main` at formal start: `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`;
- branch: `coordination/wave12-hardening`;
- draft PR: #202 — `Wave 12: harden realtime, persistence, and project ingress`;
- active ledger: `docs/WAVE-12-HARDENING-AUDIT.md`;
- preparation: `docs/WAVE-12-HARDENING-PREPARATION.md`.

Wave 12 remains a hardening wave. Wave 13 signing, Linux `.deb`, new Drivers/protocols, owner validation and physical L4 are outside this branch.

## 3. Validated Wave 12 checkpoint

Latest validated Wave 12 **product-code** SHA:

`012d15554d96af8600953a793cd58f0a5fc11c4d`

Exact-SHA evidence:

- EliteSCADA CI #1075 / `33565105224`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #71 / `33565105291`: **SUCCESS**;
- Preview Licensing CI #124 / `33565105254`: **SUCCESS**;
- Wave 11 Active HMI Runtime #22 / `33565105207`: **SUCCESS**.

The immediately preceding EliteSCADA CI #1074 at `329083a9f3273907306f4a17a99f527b382a303a` failed deterministically in one E2E because `security.spec.ts` still invoked Persistence Apply without the new required workspace-version header. The backend contract was not weakened. Commit `012d15554d96af8600953a793cd58f0a5fc11c4d` updated the caller to read `changeVersion` after checkout and send `x-elitescada-workspace-version`; #1075 then passed completely.

## 4. Hardening closed at this checkpoint

The initial remediation slice has regression coverage and is validated on the checkpoint above:

- **W12-RT-001** — bounded per-client realtime delivery and stalled/overflowing WebSocket isolation;
- **W12-PER-001** — one canonical Engineering snapshot and serialized Save/AcceptSave boundary;
- **W12-ING-001** — bounded JSON/CSV ingress with deterministic rejection and sanitized errors;
- **W12-PKG-001** — `.escadapkg` export/import resource-limit symmetry;
- **W12-PER-002** — Persistence Apply now uses the canonical Working mutation lease and caller-observed CAS version.

A regression introduced during W12-RT-001 initially converted revocation close semantics from WebSocket 1008 to 1006. Root cause was premature cancellation of the shared connection lifetime. It was fixed without weakening the policy-close assertion and validated on `25444267e20b668a22191a662d6eeb4bef4b88d5` by EliteSCADA CI #1071 plus the specialized gates.

## 5. Active next finding

**W12-AUTH-001 — High — Local identities** is the next implementation target.

Confirmed defect class:

- user update and password-reset flows perform `read -> validate -> write` as separate store operations;
- individual store calls are thread-safe, but the logical mutation is not serialized;
- concurrent requests can read the same prior account and overwrite each other;
- concurrent administrator-removal/disable operations can each observe another enabled administrator and both pass the last-administrator guard.

Selected remediation direction:

- add a local-identity mutation lease at the store boundary;
- InMemory uses an async process-local lease;
- PostgreSQL uses a dedicated session advisory lock so cooperating EliteSCADA processes sharing the database serialize identity mutations;
- hold the lease across read, invariant validation and write;
- cover lost-update serialization and last-administrator invariants with focused regressions;
- do not weaken authentication/authorization or move identity authority to the client.

## 6. Remaining Wave 12 findings

After W12-AUTH-001:

- W12-AUTH-002 — bounded login-attempt key lifecycle;
- W12-API-001 — deterministic request validation and sanitized diagnostics;
- W12-AUD-001 — explicit product-safe audit-outage contract; silent protected-action audit loss is not acceptable.

## 7. Exact next action

1. implement W12-AUTH-001 on `coordination/wave12-hardening`;
2. add focused concurrency/invariant regression evidence;
3. run EliteSCADA CI on the exact new product-code SHA and diagnose before any rerun;
4. run specialized CI according to actual impact;
5. continue the remaining ledger findings before considering PR #202 merge;
6. keep issue #201 and PR #202 open until all findings are fixed or explicitly dispositioned and final exact-SHA evidence is green.

Commercial distribution remains gated by the Step Function I/O `dnp3` 1.6.0 license: obtain an appropriate commercial license or replace/revalidate the dependency before commercial inclusion of that Driver.
