# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Date:** 2026-09-06 BRT  
**Status:** **ACTIVE / NOT ACCEPTED / NOT INTEGRATED / C25.0-C25.9 COMPLETE / C25.10 NEXT**  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Implementation branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state is the sole project authority. Revalidate live refs, PR state and exact-SHA CI before every decision or mutation. Historical detailed ledger revisions remain preserved in Git history; this file is the current resumable authority.

## 1. Permanent governance

- #283 remains OPEN/DRAFT and may target only `wave14/corrections-integration`.
- #212 remains OPEN/DRAFT and MUST NOT merge to `main` without later explicit Product Owner authorization.
- Never alter `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every CI red before rerun; no blind reruns.
- Never weaken tests, validation, authentication, authorization, Authority, Engineering Lock, licensing, lifecycle, package or Runtime authority for green CI.
- Backend Active Revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for a generic product gap.
- C11 remains frozen at `41d24d89c3b9d2b881215255e44023fabde262f3` until C25 is explicitly accepted, integrated only into integration and post-merge exact-SHA revalidated.
- #266 remains validation-only and MUST NEVER MERGE.
- Wave 13 issue #205 and PR #207 remain paused until the approved post-C25/main sequence reaches them.

## 2. C25 base and current exact product/test authority

C25 branch base / current integration head:

`c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

Accepted C24 product authority beneath C25:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Current exact C25 product/test authority after C25.8 remediation and C25.9 integrated audit:

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd`

Exact-SHA evidence:

- Wave 14 C25 Post-Demo #314 / run `34072225644` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #301 / run `34072225635` — **SUCCESS**.

The coordination-only documentation commit above this product SHA, if present, is not a replacement product/test candidate. Product/test evidence remains anchored to `5193b812...` until another code-bearing exact SHA is explicitly validated.

## 3. Checkpoint matrix

- **C25.0 — Bootstrap + full architecture/code audit:** COMPLETE.
- **C25.1 — Engineering Lock domain/package/security:** COMPLETE.
- **C25.2 — Engineering Lock backend enforcement:** COMPLETE.
- **C25.3 — Engineering Lock UI/lifecycle/package:** COMPLETE.
- **C25.4 — Restore-first / System Recovery:** COMPLETE.
- **C25.5 — Runtime session UX:** COMPLETE.
- **C25.6 — Reusable Resource Libraries:** COMPLETE / exact-SHA green.
- **C25.7 — Distributed Runtime Foundation:** COMPLETE / exact-SHA green.
- **C25.8 — Contextual multilingual Help/manual:** COMPLETE / exact-SHA green after acceptance remediation.
- **C25.9 — Integrated regression/audit:** COMPLETE.
- **C25.10 — Exact final candidate matrix / explicit Product Owner acceptance:** NEXT / NOT ACCEPTED.

### C25.6 retained checkpoint

Exact validated SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

- C25 #275 / `34052101709` — SUCCESS;
- C03 #280 / `34052101702` — SUCCESS.

C25.6 established `.escadalib` association without Working mutation, selective dependency-aware `Usar`, project-owned incorporated content, safe disassociation, self-contained `.escadapkg` and no Runtime dependency on `.escadalib`.

### C25.7 retained checkpoint

Exact validated SHA:

`233002ded306c971858b356e1d2a50a89921da37`

- C25 #300 / `34062152621` — SUCCESS;
- C03 #294 / `34062152648` — SUCCESS.

C25.7 established the canonical Server Runtime boundary, Runtime Session Lease, Viewer/Interactive capability downscope, voluntary View Only server-side enforcement and topology-neutral `.escadapkg`. Production HA/replication/failover remains future-Wave work.

## 4. C25.8 — final Help/manual acceptance

The earlier green Help infrastructure SHA `c5cf2dca1090acc6ebb278d31276d508304a6381` remains historical evidence only. C25.9 correctly discovered that it did not satisfy the complete binding installed-manual contract.

The replacement implementation was completed and exact-SHA validated at:

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd`

Acceptance coverage now includes:

- local/offline Help with same-origin `/api/help`;
- stable language-neutral Topic IDs;
- pt-BR / en / es semantic and structural parity;
- all 30 mandatory manual topic IDs;
- lifecycle and `.escadapkg` flow;
- Data Sources and TAG semantics including quality, timestamps, writeability, addressing and scaling versus presentation formatting;
- Alarms, Operational Events and Audit as separate concepts;
- Historian, Trends, Reports, Screens, Popups, Dynamos, bindings and commands;
- users/roles/capabilities/security, Licensing, Backup/System Recovery and diagnostics/troubleshooting;
- reusable-library creation/export/Inspect plus C25.6 association/use/disassociation/self-contained Runtime semantics;
- exactly 8 production communication Drivers including Modbus;
- Simulation excluded from production Driver presentation by its canonical TypeKey `builtin.simulation`;
- Internal Memory and TAG Gateway documented as separate concepts;
- Driver topics derived from canonical Engineering descriptors/configuration schemas, including configuration, TAG binding/addressing, datatype/mapping cues, bit/endianness cues where declared, read/write restrictions, polling/subscription, reconnect/timeouts, protected material, examples, diagnostics and interoperability limits;
- Server Script documentation restricted to the shipped six-function API and actual Runtime triggers/lifecycle/failure/sandbox contracts.

Diagnosed remediation trail is preserved in Git history. Red CI was diagnosed before each correction; no blind rerun and no test weakening occurred.

## 5. C25.9 — integrated regression/audit result

**COMPLETE / NO REMAINING PRODUCT-CODE BLOCKER FOUND**

The audit revalidated the replacement C25.8 product SHA against cross-checkpoint contracts and the current integration base.

### Exact-SHA integrated evidence

C25 #314 / `34072225644` — SUCCESS:

- Core and API/Driver restore/build;
- Engineering Lock package/crypto and backend/lifecycle gates;
- reusable-library package/catalog/incorporation/backend lifecycle;
- Authority backup crypto/validation;
- System Recovery;
- atomic Authority-store replacement;
- C25.7 Distributed Runtime foundation;
- C25.8 contextual multilingual Help contract;
- React/Vite build;
- Chromium integrated flows for Engineering Lock, Restore-first, Runtime session, reusable libraries and Help.

C03 #301 / `34072225635` — SUCCESS:

- managed OpenDNP3 adapter build/tests;
- native OpenDNP3 Linux host;
- native OpenDNP3 Windows x64 host and dependency inspection;
- real OpenDNP3 ↔ dnp3py L3 interoperability;
- Windows commercial publish dependency gate proving the required OpenDNP3 helper is packaged and restricted Step Function / `dnp3` 1.6.0 bytes and dependency graph are absent.

### Integration and governance audit

At the audited product SHA:

- C25 is 191 commits ahead and 0 behind `wave14/corrections-integration`; merge base is the current integration head `c2fc96e...`;
- no integration-base drift blocker was found;
- #283 remains OPEN/DRAFT and not merged;
- #212 remains OPEN/DRAFT and not merged;
- C11 #263 remains OPEN/DRAFT at preserved head `41d24d89...`;
- #266 remains OPEN/DRAFT validation-only and MUST NEVER MERGE;
- Wave 13 #205 remains OPEN/paused;
- Wave 13 #207 remains OPEN/DRAFT at preserved head `fda87ba...`.

### Documentation/coordination drift found and repaired

C25.9 found stale coordination prose in the execution ledger, `CURRENT-COORDINATOR-HANDOFF.md`, `LAST CHANGE.md`, PR #283 and PR #212. The repository documents are repaired by the coordination-only closeout commit following `5193b812...`; PR metadata/issue record are synchronized separately without changing product code.

No product mutation was required to close the C25.9 audit after the exact green replacement candidate.

## 6. Immediate next checkpoint — C25.10

C25.10 may now prepare the exact final candidate matrix using `5193b81220f499cb2039dc1146c6dd1a7f7b3dbd` as the current product/test authority.

C25.10 must not claim acceptance automatically. Required boundary:

1. assemble the exact candidate/evidence matrix;
2. revalidate live refs and any relevant exact-SHA evidence;
3. record all remaining non-C25 blockers separately rather than smuggling them into acceptance;
4. obtain explicit Product Owner acceptance of one exact C25 product candidate;
5. only after that explicit acceptance may C25 merge into `wave14/corrections-integration`;
6. post-merge exact-SHA validate integration;
7. only then resume/synchronize C11 according to the established sequence.

There is still **no authorization to merge #212 into `main`**.
