# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 12 #201 — REMEDIATION COMPLETE / PR #202 READY FOR MERGE / POST-MAIN CI PENDING**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance a branch beyond the latest validated product-code SHA.

## 1. Accepted foundation

Wave 11 is **COMPLETE / ACCEPTED / CLOSED** under issue #194.

Final accepted Wave 11 product-code `main` baseline:

`4ccc29cb4bb334dc473d8265f48a9c8601993413`

Accepted Runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; mutable Working never drives HMI Runtime directly.

Owner-test package remains:

`EliteSCADA-Wave11-Demo.escadapkg`  
SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`

## 2. Wave 12 integration state

Issue #201 remains **OPEN** until PR #202 is merged and the required post-merge `main` validation succeeds.

- branch: `coordination/wave12-hardening`;
- branch base / `main` at Wave 12 start: `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`;
- PR #202: `Wave 12: harden realtime, persistence, and project ingress`;
- ledger: `docs/WAVE-12-HARDENING-AUDIT.md`;
- all identified Wave 12 findings are fixed with focused regression evidence;
- PR must not be merged if live `main` or PR head differs from the state verified immediately before merge.

## 3. Final pre-merge validated product-code checkpoint

`29141feab168fa6e33d98b0f36cdd6e79f3811d8`

Exact-SHA evidence:

- EliteSCADA CI #1093 / `33574192584`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #89 / `33574192610`: **SUCCESS**;
- Preview Licensing CI #142 / `33574192572`: **SUCCESS**;
- Wave 11 Active HMI Runtime #40 / `33574192580`: **SUCCESS**.

The preceding AUD-001 attempt `0905ce4313122dc266444a047abeb92c8a122572` failed EliteSCADA CI #1092 at compile time only because the new regression test omitted `using Scada.Api.Runtime;`. Product code itself compiled. Commit `29141fe...` corrected only that test namespace; #1093 then passed completely.

## 4. Wave 12 finding ledger

All findings are now **FIXED / REGRESSION / VALIDATED**:

- **W12-RT-001 — High — Realtime:** bounded per-client outbound queues, isolated stalled clients and deterministic eviction; revocation retains WebSocket `1008 Policy Violation` semantics.
- **W12-PER-001 — High — Persistence Save:** one canonical Engineering snapshot held under workspace mutation serialization through persist/AcceptSave.
- **W12-ING-001 — High — Engineering ingress:** bounded JSON/CSV bodies, Content-Length fast rejection, streaming enforcement, strict UTF-8 and sanitized client errors.
- **W12-PKG-001 — High — `.escadapkg`:** export limits match importer limits, preventing self-generated packages the product would reject.
- **W12-PER-002 — High — Persistence Apply:** Working mutation lease plus caller-observed `x-elitescada-workspace-version` CAS; stale state returns conflict.
- **W12-AUTH-001 — High — Local identities:** store-level mutation lease covers read/invariant/write, including PostgreSQL cross-process advisory locking and last-enabled-administrator preservation.
- **W12-AUTH-002 — Medium — Login limiting:** expired remote-key state is reclaimed without evicting active lockout windows.
- **W12-API-001 — Medium — Requests/diagnostics:** persistence request bounds and positive revisions are validated at HTTP boundary; Historical Query distinguishes typed public failures from provider/internal failures and sanitizes public diagnostics.
- **W12-AUD-001 — High — Audit durability:** unsafe `/api` mutations require a durable append-only audit admission before endpoint execution. Store outage returns sanitized 503 and the endpoint is not invoked. Detailed post-action audit remains non-failing to avoid unsafe retries after physical effects; a prior durable admission preserves explicit ambiguity evidence if the detailed outcome cannot be buffered.

## 5. Important failure history

Do not rerun old failures as a substitute for diagnosis:

- realtime hardening initially changed revocation from 1008 to 1006; fixed by preserving the close-frame path before cancellation;
- Persistence Apply CAS initially broke one E2E caller that omitted the new version header; caller was corrected, contract was not relaxed;
- AUTH-001 validation exposed a separate Modbus recovery test with a 100 ms healthy-operation timing margin; test timing was hardened while preserving recovery/write assertions;
- API-001 validation exposed stale failure typing and runner-sensitive Gateway/realtime test watchdogs; assertions were preserved and made deterministic;
- AUD-001 #1092 failed only from a missing test namespace and was corrected at `29141fe...`.

## 6. Exact next action

1. synchronize issue #201 and PR #202 to this checkpoint;
2. verify live `main`, PR head and mergeability;
3. move PR #202 out of draft and merge only with the exact expected head;
4. validate the resulting `main` merge SHA with EliteSCADA CI;
5. only after successful post-merge `main` CI, close issue #201 and record Wave 12 **COMPLETE / ACCEPTED / CLOSED**;
6. then hand off the stable `main` baseline to Wave 13 without starting Wave 13 work in this branch.

## 7. Locked exclusions / gates

Wave 13 Authenticode/trusted-timestamp signing, Linux `.deb`, new Drivers/protocols, owner-validation Waves 14/15 and physical L4 remain outside Wave 12.

Commercial distribution remains gated by Step Function I/O `dnp3` 1.6.0 licensing: obtain an appropriate commercial license or replace and revalidate the dependency before commercial inclusion of that Driver.
