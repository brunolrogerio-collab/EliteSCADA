# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-01 BRT  
**Status:** **WAVE 12 #201 IN PROGRESS — PR #202 — SLICE A + W12-PER-002 VALIDATED — W12-AUTH-001 NEXT**

> GitHub/main/CI is implementation truth. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the mutable resume point. Never resume from chat alone.

## 1. Mandatory resume protocol

Read in this order before changing code:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this handoff;
4. `docs/ROADMAP.md`;
5. `docs/WAVE-12-HARDENING-PREPARATION.md`;
6. issue #201;
7. `docs/CI-VALIDATION-POLICY.md`;
8. live `main`, PR #202, open issues and exact Actions state;
9. `docs/WAVE-12-HARDENING-AUDIT.md`.

If repository state differs from copied prose, GitHub/main/CI wins.

## 2. Accepted foundation

Wave 11 issue #194 is **CLOSED / COMPLETED**.

Accepted product-code baseline:

`4ccc29cb4bb334dc473d8265f48a9c8601993413`

Accepted lifecycle authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

The Runtime uses persisted Active Engineering only; Working edits remain isolated until Save/Publish/Activate. Screens, Popups, Dynamos, Active assets, fail-closed behavior, Runtime View separation, protected TAG writes and Simulation fallback rules are accepted and must not be reopened without a concrete defect.

Owner-test package:

`EliteSCADA-Wave11-Demo.escadapkg`  
SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`

## 3. Wave 12 live state

Issue #201: **OPEN / IN PROGRESS**.  
Branch: `coordination/wave12-hardening`.  
Draft PR: #202.  
Branch base: `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`.  
Ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.

Latest validated Wave 12 product-code SHA:

`012d15554d96af8600953a793cd58f0a5fc11c4d`

Exact evidence:

- EliteSCADA CI #1075 / `33565105224`: **SUCCESS** including Chromium E2E;
- L3 Seven-Driver Lab #71 / `33565105291`: **SUCCESS**;
- Preview Licensing CI #124 / `33565105254`: **SUCCESS**;
- Wave 11 Active HMI Runtime #22 / `33565105207`: **SUCCESS**.

A documentation-only `[skip ci]` commit may place the branch head after this SHA. Do not confuse that with a new validated product-code baseline.

## 4. Completed Wave 12 findings at this checkpoint

- W12-RT-001 — realtime WebSocket client isolation;
- W12-PER-001 — consistent serialized persistence Save;
- W12-ING-001 — bounded JSON/CSV Engineering ingress;
- W12-PKG-001 — `.escadapkg` export/import resource symmetry;
- W12-PER-002 — Persistence Apply lease/CAS parity.

Important validation history:

- realtime hardening initially caused session revocation to close as 1006 instead of required 1008;
- root cause was premature cancellation of the shared WebSocket connection lifetime;
- correction preserved the policy-close assertion and was validated at `25444267e20b668a22191a662d6eeb4bef4b88d5`;
- PER-002's first exact-head CI (#1074 at `329083a...`) then found one E2E caller missing the newly required workspace-version header;
- commit `012d155...` corrected that caller by reading post-checkout `changeVersion`; #1075 passed completely.

Do not rerun either historical failure as a supposed solution. Their causes are known and fixed.

## 5. Exact next implementation

**W12-AUTH-001 — local-identity concurrency / last-administrator invariant.**

Confirmed race:

- API mutation flows perform account read, invariant validation and update as separate store operations;
- concurrent update/password-reset requests can overwrite changes from the same starting account state;
- concurrent administrator removals/disables can each pass the last-admin check against stale state.

Chosen implementation direction:

1. add a mutation lease to `ILocalIdentityStore`;
2. implement an async local lease for `InMemoryLocalIdentityStore`;
3. implement a PostgreSQL session advisory-lock lease for `PostgreSqlLocalIdentityStore`;
4. hold it across the full read/validate/write transaction in administration mutations and bootstrap where applicable;
5. add focused serialization and last-administrator regression tests;
6. validate the exact new product-code SHA with EliteSCADA CI before moving on.

Then continue W12-AUTH-002, W12-API-001 and W12-AUD-001 according to the audit ledger.

## 6. CI/merge rules

- EliteSCADA CI is the universal Coordinator gate for PRs to `main`;
- specialized workflows run according to actual impact;
- diagnose failures before rerun;
- do not weaken assertions, authorization or architecture to obtain green;
- PR #202 remains draft/unmerged until Wave 12 acceptance is satisfied;
- validate post-merge `main` when Wave 12 is eventually integrated.

## 7. Explicit exclusions and gates

Do not start during Wave 12:

- Wave 13 Authenticode/trusted-timestamp release signing;
- Linux `.deb` implementation;
- new Drivers/protocols;
- owner validation/feedback Waves 14/15;
- physical L4 claims.

Licensing remains host-owned. Private license-signing material never enters repository/CI/product.

Commercial DNP3 inclusion is gated: Step Function I/O `dnp3` 1.6.0 requires an appropriate commercial license or an approved/revalidated replacement before commercial distribution.
