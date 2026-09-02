# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-01 BRT  
**Status:** **WAVE 12 #201 — REMEDIATION COMPLETE / PR #202 READY FOR INTEGRATION / POST-MAIN CI PENDING**

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

Runtime uses persisted Active Engineering only; Working edits remain isolated until Save/Publish/Activate. Accepted Wave 11 product boundaries must not be reopened without a demonstrated defect.

Owner-test package:

`EliteSCADA-Wave11-Demo.escadapkg`  
SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`

## 3. Wave 12 current state

Issue #201: **OPEN pending integration acceptance**.  
Branch: `coordination/wave12-hardening`.  
PR: #202.  
Branch base / `main` at formal start: `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`.  
Ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.

Latest validated Wave 12 product-code SHA:

`29141feab168fa6e33d98b0f36cdd6e79f3811d8`

Exact evidence:

- EliteSCADA CI #1093 / `33574192584`: **SUCCESS**, including Chromium E2E;
- L3 Seven-Driver Lab #89 / `33574192610`: **SUCCESS**;
- Preview Licensing CI #142 / `33574192572`: **SUCCESS**;
- Wave 11 Active HMI Runtime #40 / `33574192580`: **SUCCESS**.

A documentation-only `[skip ci]` synchronization commit may place the branch head after this SHA. It does not supersede the validated product-code checkpoint.

## 4. Closed Wave 12 findings

All identified findings are **FIXED / REGRESSION / VALIDATED**:

- W12-RT-001 — realtime WebSocket client isolation and preserved 1008 revocation close semantics;
- W12-PER-001 — consistent serialized persistence Save;
- W12-ING-001 — bounded JSON/CSV Engineering ingress;
- W12-PKG-001 — `.escadapkg` export/import resource symmetry;
- W12-PER-002 — Persistence Apply mutation lease + caller-observed CAS;
- W12-AUTH-001 — local-identity logical mutation serialization and last-enabled-administrator invariant;
- W12-AUTH-002 — expired login limiter state reclamation without active lockout eviction;
- W12-API-001 — deterministic persistence request validation and typed/sanitized Historical Query failure classification;
- W12-AUD-001 — durable pre-mutation append-only audit admission for unsafe `/api` requests; audit-store outage fails closed before the endpoint executes.

AUD-001 deliberately does not convert a detailed post-action audit failure into a process-command failure. A physical or runtime mutation may already have occurred; returning an artificial failure could cause a client to repeat it. The direct durable admission record exists before the mutation and therefore preserves evidence if a detailed outcome later cannot be buffered.

## 5. Important validation history

- Realtime hardening initially produced browser close 1006 instead of required 1008. Root cause was premature cancellation; fixed without weakening policy-close assertions.
- PER-002 first exact-head CI exposed one E2E caller missing the new workspace-version header; the caller was corrected rather than relaxing CAS.
- AUTH-001 validation exposed an unrelated Modbus recovery-test timing margin; normal-operation margin was hardened while preserving failure/recovery assertions.
- API-001 validation exposed stale typed expectations and runner-sensitive timing/watchdog tests; semantics/assertions were retained and tests made deterministic.
- AUD-001 first run #1092 at `0905ce4313122dc266444a047abeb92c8a122572` failed build because the new regression test omitted the `Scada.Api.Runtime` namespace. Product code compiled; `29141fe...` corrected only the test and #1093 passed fully.

Never use blind reruns as a substitute for these diagnosed root causes.

## 6. Exact next action

1. synchronize PR #202 and issue #201 to the validated checkpoint;
2. verify live `main`, exact PR head and mergeability immediately before integration;
3. mark PR #202 ready and merge using the exact expected head only if the base state is still coherent;
4. validate the resulting `main` SHA with the universal EliteSCADA CI;
5. only after post-merge `main` success, close issue #201 and record Wave 12 as **COMPLETE / ACCEPTED / CLOSED**;
6. hand Wave 13 the resulting stable `main` baseline; do not implement signing in the Wave 12 branch.

## 7. CI / merge rules

- EliteSCADA CI is the universal Coordinator gate for PRs to `main`;
- specialized workflows run according to actual impact and never substitute for universal CI;
- diagnose failures before rerun;
- do not weaken assertions, authorization or architecture to obtain green;
- integration must use expected-head protection;
- validate post-merge `main` before declaring Wave 12 complete.

## 8. Explicit exclusions and gates

Do not start during Wave 12:

- Wave 13 Authenticode/trusted-timestamp release signing;
- Linux `.deb` implementation;
- new Drivers/protocols;
- owner validation/feedback Waves 14/15;
- physical L4 claims.

Licensing remains host-owned. Private license-signing material never enters repository/CI/product.

Commercial DNP3 inclusion is gated: Step Function I/O `dnp3` 1.6.0 requires an appropriate commercial license or an approved/revalidated replacement before commercial distribution.
