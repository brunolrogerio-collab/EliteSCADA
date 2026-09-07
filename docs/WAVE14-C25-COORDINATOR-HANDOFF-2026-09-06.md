# Wave 14 — C25 Coordinator Handoff — 2026-09-06

**Status:** ACTIVE / NOT ACCEPTED / NOT INTEGRATED / C25.7 COMPLETE / C25.8 GREEN INFRASTRUCTURE BUT FINAL ACCEPTANCE REMEDIATION REQUIRED / C25.9 AUDIT OPEN-BLOCKED  
**Repository:** `brunolrogerio-collab/EliteSCADA`  
**C25 branch:** `wave14/c25-post-demo`  
**C25 PR:** #283 -> `wave14/corrections-integration`  
**Tracking issue:** #282  
**Integration PR:** #212 -> `main` (OPEN/DRAFT; no merge authorization)  
**Binding contract:** `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`  
**Execution ledger:** `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`

> GitHub live state is the sole project authority. Revalidate all refs, PR state and exact-SHA CI before any decision or mutation. This document is a handoff snapshot, never a substitute for live GitHub.

## 1. Mandatory read order for the next coordinator

1. `docs/WAVE14-C25-COORDINATOR-HANDOFF-2026-09-06.md`
2. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
3. `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`
4. `docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`
5. `docs/WAVE14-C25-REUSABLE-LIBRARIES-IMPLEMENTATION-STATUS-2026-09-06.md`
6. `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`
7. issue #282, especially the most recent comments
8. PR #283

## 2. Live state revalidated immediately before this handoff

PR #283:

- OPEN;
- DRAFT;
- merged=false;
- target `wave14/corrections-integration`;
- product/test HEAD before this handoff documentation commit: `c5cf2dca1090acc6ebb278d31276d508304a6381`.

Exact-SHA validation for `c5cf2dca1090acc6ebb278d31276d508304a6381`:

- Wave 14 C25 Post-Demo #304 / run `34068529435` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #296 / run `34068529398` — **SUCCESS**.

Guard rails:

- #212 — OPEN/DRAFT, merged=false, integration head `c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`; no authorization to merge into `main`.
- #263 — OPEN/DRAFT, C11 preserved head `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #266 — OPEN/DRAFT validation-only against `main`, same C11 head, **MUST NEVER MERGE**.
- issue #205 — OPEN and paused.
- #207 — OPEN/DRAFT, Wave 13 preserved head `fda87ba4445127c174f6ea533a6bcabaabc7bb20`.

## 3. Completed C25 checkpoints

- C25.0 — COMPLETE.
- C25.1 — COMPLETE.
- C25.2 — COMPLETE.
- C25.3 — COMPLETE.
- C25.4 — COMPLETE.
- C25.5 — COMPLETE.
- C25.6 Reusable Resource Libraries — COMPLETE / exact-SHA green.
- C25.7 Distributed Runtime Foundation — COMPLETE / exact-SHA green.

C25.6 exact validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

- C25 #275 / `34052101709` — SUCCESS;
- C03 #280 / `34052101702` — SUCCESS.

C25.7 exact validated product/test SHA:

`233002ded306c971858b356e1d2a50a89921da37`

- C25 #300 / `34062152621` — SUCCESS;
- C03 #294 / `34062152648` — SUCCESS.

C25.7 includes the production-path proof that a privileged identity allowed to command/write under base Authority is denied server-side after voluntarily entering a Viewer Runtime Session Lease. `.escadapkg` remains topology-neutral. No production HA/replication/failover was added.

## 4. C25.8 current truth

The first C25.8 implementation was committed in:

`9ab78d00ea908636e1d2f05f7df716ee15f197f7` — `feat(w14-c25): add contextual multilingual help`

A diagnosed C# translation-record constructor mismatch was fixed in:

`c5cf2dca1090acc6ebb278d31276d508304a6381` — `fix(w14-c25): align contextual help translation contract`

The corrected SHA is exact-SHA green on both required workflows.

Delivered infrastructure at `c5cf2dca...`:

- local/offline contextual Help;
- stable language-neutral Topic IDs;
- pt-BR / en / es;
- canonical product locale persistence;
- same-origin `/api/help` endpoint;
- Runtime/Engineering/Audit/Licensing contextual navigation;
- Driver/source topics generated from the canonical Engineering Data Source catalog;
- actual Server Script API allow-list surfaced by the Help catalog;
- browser tests for contextual topic resolution and language switching;
- dedicated backend `ContextualHelpTests` in C25 CI.

Driver inventory rule is binding:

- **8 communication Drivers total**, counting Modbus;
- modern runtime composition contributes MQTT, IEC 60870-5-104, Allen-Bradley Logix / EtherNet/IP, OPC UA, DNP3, Siemens S7 ISO and BACnet;
- Modbus completes the eight;
- Simulation is not to be presented as a ninth production communication Driver merely because it appears in the Engineering source catalog;
- Internal Memory and TAG Gateway are separate product concepts and must not be folded into the Driver count.

## 5. Critical acceptance gap discovered by C25.9 audit

Do **not** treat `c5cf2dca...` as the final accepted C25.8 candidate.

Issue #282 records that the earlier `C25.8 COMPLETE` statement was superseded for final acceptance purposes after the integrated C25.9 audit compared the implementation against the higher-priority consolidated contract.

The current Help infrastructure proves localization, routing, Driver/source derivation, Script API accuracy and offline delivery, but installed-manual topic depth is incomplete.

The binding contract still requires coverage for at least:

- Getting Started / first startup / authentication;
- Runtime operator/session behavior;
- Working -> Save -> Revision -> Publish -> Activate;
- `.escadapkg` Import/Export/Inspect/Preview/Apply;
- Data Sources;
- TAGs, quality, timestamps, writeability, addressing, scaling vs presentation formatting;
- all production Drivers with real schema-derived configuration/addressing detail and limitations;
- Internal Memory and TAG Gateway;
- Scripts including real API, triggers/lifecycle, failure/safety semantics and validated examples;
- Alarms;
- Operational Events;
- Audit as a separate concept;
- Historian and Trends;
- Reports;
- Screens, Popups, Dynamos, bindings, commands and visual Runtime behavior;
- users/roles/capabilities/security;
- Licensing;
- Backup/System Recovery;
- diagnostics/troubleshooting.

C25.6 semantics must also be explained by the installed manual:

- `.escadalib` creation/export;
- association makes resources available but does **not** import/mutate Working;
- selective `Usar` incorporates selected resources + validated dependency closure;
- disassociation removes catalog availability only;
- incorporated resources become project-owned content;
- final `.escadapkg` is self-contained and Runtime never depends on `.escadalib`.

## 6. Exact next execution sequence

The next coordinator must not jump to C25.10.

1. Revalidate live #283 HEAD and exact-SHA C25/C03 results.
2. Read the latest #282 comments before changing anything.
3. Treat `c5cf2dca...` as the green intermediate Help infrastructure baseline, not final C25.8 acceptance.
4. Expand the installed manual to satisfy the complete mandatory topic matrix in the consolidated contract.
5. Enrich every production Driver topic from the canonical descriptor/configuration schema. Do not write unsupported protocol claims by hand.
6. Keep the 8-Driver count separate from Internal Memory and Gateway.
7. Add regression gates that fail when required manual topics disappear or when reusable-library semantics are omitted.
8. Preserve stable language-neutral topic IDs and pt-BR/en/es semantic parity.
9. Keep Script examples constrained to APIs actually shipped by the build.
10. Run C25 + C03 on one exact replacement SHA. Diagnose any red before rerun; never weaken tests.
11. Only after the replacement SHA is green and the coverage audit is satisfied may C25.8 be considered final for acceptance.
12. Resume/complete C25.9 integrated regression/audit on that replacement SHA.
13. C25.10 final candidate matrix follows only after C25.9 closes.

## 7. Permanent governance

- GitHub live state is the only authority.
- #283 remains OPEN/DRAFT and targets only `wave14/corrections-integration`.
- #212 remains OPEN/DRAFT and MUST NOT merge to `main` without later explicit Product Owner authorization.
- Never alter `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- No blind CI reruns.
- Never weaken tests, security, identity, Authority, Engineering Lock, licensing, lifecycle, package or Runtime authority for green.
- Backend Active Revision remains canonical Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- C11 remains frozen until C25 is explicitly accepted, integrated only into integration and post-merge exact-SHA validated.
- #266 MUST NEVER MERGE.
- Wave 13 #205/#207 remains paused until the approved post-C25/main sequence reaches it.

## 8. Post-C25 sequence remains unchanged

After explicit Product Owner acceptance of one exact C25 candidate:

1. merge accepted C25 only into `wave14/corrections-integration`;
2. exact-SHA post-merge validate integration;
3. only then synchronize/adapt C11 canonical EEE Demo;
4. revalidate generic Save -> Publish -> Activate and package portability;
5. freeze `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance;
6. launch/keep Preview Codespace active through visual homologation and approved main transition;
7. only explicit Product Owner authorization permits #212 -> `main`;
8. validate the resulting new `main`;
9. only then resume Wave 13 release/signing from that new mainline authority.
