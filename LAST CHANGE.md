# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — TAG GATEWAY ENGINEERING + S7 RESEARCH / OPC UA RESEARCH MERGED**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Internal Memory complete product integration remains **MERGED / COMPLETE** through coordinator PR #49, merge:

`18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`

PR CI #296 and post-merge `main` CI #297 passed Web build, backend build/tests, runtime smoke and Chromium E2E.

The locked functional sequence remains:

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

Current functional state:

- Internal Memory: **MERGED / COMPLETE**;
- TAG Gateway: **ACTIVE LOCKED BLOCK — ENGINEERING/VALIDATION SLICE ASSIGNED TO DEV 2**;
- common multi-driver diagnostics: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY GATEWAY**;
- interface validation preview: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY DIAGNOSTICS**;
- production MQTT/OPC UA/BACnet/S7 and other external protocols remain blocked by the preceding gates.

Non-production documentation/research spikes are allowed early when they do not register runtime protocol behavior, alter central Engineering contracts or change the implementation order.

## OPC UA RESEARCH DIRECTION

`docs/OPC-UA.md` locks a future Engineering experience with server/network discovery, endpoint/security/certificate inspection, lazy address-space browsing, search/filter, multi-select/subtree import preview, subscription profiles, NodeId + namespace-aware BrowsePath reconciliation and safe rescan/diff.

DEV 1 completed the documentation-only OPC UA spike through PR #51. The research is **MERGED** as `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`; research head `6aac0f3cfe8e89cc7c56cc2bf3668d03a8c94994`; CI #299 passed. The merged research document is `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`.

Key merged research direction includes the official OPC Foundation .NET stack, layered standard discovery with bounded fallback scan, explicit certificate trust, lazy Browse/BrowseNext, namespace-URI-aware BrowsePath plus last NodeId identity, deterministic NodeId refresh/rescan, scalar-safe type mapping, subscription profiles, canonical candidate -> Preview -> Apply import and reference/interoperability test-server strategy.

DEV 1 is now **MERGED / WAITING**. Production OPC UA remains **SPECIFIED / NOT IMPLEMENTED** and blocked by Gateway, common diagnostics and interface-preview gates.

## SIEMENS S7 ISO CONNECTION DIRECTION

The product owner confirmed Siemens S7 through **ISO Connection** as a desired later protocol path.

`docs/S7-ISO-CONNECTION.md` now defines this as classic Siemens S7 communication over **ISO-on-TCP / RFC1006, TCP port 102**, not generic PROFINET I/O, OPC UA or an implicit S7commPlus implementation.

Locked/research direction includes:

- S7-300/400/1200/1500 connection matrix;
- Rack/Slot and explicit TSAP connection modes;
- CPU family/profile-aware defaults while keeping all critical parameters explicit;
- PUT/GET and access-control constraints for modern S7-1200/1500;
- explicit treatment of optimized vs non-optimized DB access limitations;
- typed I/Q/M/DB address model rather than Elipse-style opaque N/B parameters;
- PDU-aware batching, scan classes, reconnect and bounded parallelism;
- optional bounded TCP/102 network assistance that performs no destructive operation;
- TIA Portal Openness and Siemens-supported file exports as Engineering-side TAG import sources;
- canonical `candidates -> validate -> preview -> apply` import workflow;
- no dependency on TIA Portal in the production Runtime service;
- no CPU RUN/STOP, program/block manipulation or firmware operation through the normal SCADA TAG path.

The Elipse M-Prot ISO/TCP driver is a useful workflow reference: it exposes Rack/Slot or TSAP, connection type, additional connections/max simultaneous requests and a TIA Portal importer/Tag Browser. EliteSCADA will adopt useful concepts while keeping its own public Engineering contracts and stronger diagnostics/safety boundaries.

S7.NetPlus and Sharp7 are initial .NET library candidates to evaluate; no production dependency has been selected or added.

## ACTIVE WORKER ASSIGNMENTS

### DEV 1 - EliteSCADA

- Task: `OPC UA discovery, address-space browse and TAG-import Engineering research spike`;
- Branch: `research/opc-ua-discovery-import`;
- PR #51: **MERGED** `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`;
- Status: `MERGED / WAITING`;
- research is merged; production OPC UA remains blocked;
- AfterCompletion: `WAIT_FOR_COORDINATOR`.

### DEV 2 - EliteSCADA

- Task: `Public TAG Gateway Engineering contract and deterministic validation foundation`;
- Branch: `feature/tag-gateway-engineering`;
- PR #50: Draft/Open;
- Status: `ASSIGNED`;
- owns the only active functional central Engineering contract slice;
- coordinator fixed the legacy Internal Memory schema assertion on `main`; DEV 2 must reconcile rather than own unrelated maintenance;
- runtime Gateway/API/UI remain out of this worker slice;
- AfterCompletion: `WAIT_FOR_COORDINATOR`.

### DEV 3 - EliteSCADA

- Task: `Siemens S7 ISO Connection architecture, TIA import and interoperability research spike`;
- Branch: `research/s7-iso-connection`;
- Status: `ASSIGNED`;
- research/documentation only;
- must compare Siemens/Elipse guidance and S7.NetPlus/Sharp7 candidates;
- must define Rack/Slot/TSAP, PUT/GET/protection, optimized DB boundaries, typed addressing, batching/reconnect, TIA import and test strategy;
- no production S7 runtime, dependency, central Engineering schema, DI/API, DriverHost or frontend work;
- AfterCompletion: `WAIT_FOR_COORDINATOR`.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread mandatory docs from current `main`;
2. inspect DEV 2 `feature/tag-gateway-engineering` / PR #50 head, reconciliation with current `main`, diff and CI;
3. confirm DEV 1 remains `MERGED / WAITING` after PR #51;
4. inspect DEV 3 `research/s7-iso-connection` documentation PR/state;
5. enforce that DEV 3 remains research-only and that DEV 1 does not self-start production OPC UA;
6. merge Gateway Engineering only after the public/versioned contract and validation/compatibility coverage are reviewed and relevant CI is green;
7. review S7 research as future implementation input, not implemented product state;
8. after Gateway Engineering is official on `main`, assign the protocol-independent Gateway runtime engine;
9. preserve the common diagnostics and interface-preview gates before production external drivers.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Open protocol research PRs are **RESEARCH IN PR**, not implemented drivers.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
