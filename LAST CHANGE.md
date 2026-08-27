# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26  
**Development state:** **ACTIVE — TAG GATEWAY ENGINEERING / OPC UA + S7 RESEARCH MERGED**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Internal Memory complete product integration is **MERGED / COMPLETE** through PR #49:

`18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`

PR CI #296 and post-merge `main` CI #297 passed Web build, backend build/tests, runtime smoke and Chromium E2E.

The locked functional sequence remains:

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

Current functional state:

- Internal Memory: **MERGED / COMPLETE**;
- TAG Gateway: **ACTIVE LOCKED BLOCK — ENGINEERING/VALIDATION PR #50 OPEN**;
- common multi-driver diagnostics: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY GATEWAY**;
- interface validation preview: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY DIAGNOSTICS**;
- production MQTT/OPC UA/BACnet/S7 and other external protocols remain blocked by the preceding gates.

Non-production protocol research may be merged early when it does not register runtime behavior, add production dependencies, alter central Engineering contracts outside an assignment, or change the implementation order.

## OPC UA RESEARCH — MERGED

DEV 1 completed the documentation-only OPC UA discovery/browse/import research through PR #51:

- research head: `6aac0f3cfe8e89cc7c56cc2bf3668d03a8c94994`;
- CI #299: green;
- merge: `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`;
- document: `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`.

Merged research direction covers the official OPC Foundation .NET stack; layered standard discovery plus bounded fallback scan; endpoint/security/certificate inspection; explicit trust/reconciliation; lazy Browse/BrowseNext; namespace-URI-aware BrowsePath + last NodeId identity; deterministic NodeId refresh/rescan; scalar-safe type mapping; subscription profiles; canonical candidate -> Preview -> Apply import and reference/interoperability testing.

DEV 1 is **MERGED / WAITING**. Production OPC UA remains **SPECIFIED / NOT IMPLEMENTED** and gated.

## SIEMENS S7 ISO CONNECTION RESEARCH — MERGED

DEV 3 completed the documentation-only Siemens S7 ISO Connection architecture, TIA import and interoperability spike through PR #52:

- research branch: `research/s7-iso-connection`;
- research head: `52cc16c9941b5f8c4442a8c135551de6e9f8976b`;
- CI #300 (`33031599951`): Web build, backend build/tests, runtime smoke and Chromium E2E all green;
- merge: `bd825682ae0ccfdbdb938fab638a27f6961510bf`;
- document: `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`.

The merge changes documentation only. The `main` push CI intentionally does not run for `.md`-only changes because `.github/workflows/dotnet-ci.yml` uses code/frontend path filters for `push`.

Merged research direction includes:

- classic Siemens S7 over ISO-on-TCP / RFC1006 TCP/102;
- S7-300/400/1200/1500 connection matrix;
- Rack/Slot and explicit TSAP modes, with profile suggestions rather than hidden constants;
- PUT/GET/protection diagnostics and modern CPU security boundaries;
- fail-honest optimized/non-optimized DB handling, never fabricated absolute offsets;
- typed I/Q/M/DB address model;
- PDU-aware batching, bounded parallelism, reconnect and common Data Source diagnostics integration;
- **S7.NetPlus as the preferred first future laboratory candidate only**, with Sharp7/Snap7 retained for comparison/reference; no production dependency is selected;
- TIA Portal Openness as optional Engineering-workstation tooling plus XLSX/XML/SDF fallback;
- neutral import candidates through canonical validate/preview/apply;
- bounded, explicit, non-destructive TCP/102 network assistance;
- software-only CI plus PLCSIM/PLCSIM Advanced and representative real-hardware acceptance strategy;
- no CPU RUN/STOP, program/block manipulation or firmware operations in the normal SCADA TAG path.

DEV 3 is **MERGED / WAITING**. Production S7 remains **SPECIFIED / NOT IMPLEMENTED** and gated.

## TAG GATEWAY — ACTIVE

DEV 2 owns the public/versioned Gateway Engineering and deterministic validation slice in PR #50.

Current observed PR state at this handoff:

- PR #50: Draft/Open and mergeable;
- current head: `002f87dd126854c9fd972e453930e229e02f7f30`;
- the coordinator-owned Internal Memory schema-version maintenance fix has been reconciled from `main` and is no longer part of the Gateway delta;
- final Gateway diff is reported as 8 Gateway-owned Engineering/test files;
- previous reconciled CI #303 was fully green;
- current-head CI #304 is running and must complete before coordinator merge review.

Worker slice intentionally excludes runtime Gateway execution, central API/DI/UI, runtime authority/Audit and route diagnostics. Those remain coordinator/future integration work after the Engineering contract is official.

## ACTIVE WORKER ASSIGNMENTS

### DEV 1 - EliteSCADA

- PR #51: **MERGED** `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`;
- Status: `MERGED / WAITING`;
- no new task authorized.

### DEV 2 - EliteSCADA

- Task: `Public TAG Gateway Engineering contract and deterministic validation foundation`;
- Branch: `feature/tag-gateway-engineering`;
- PR #50: `DRAFT / OPEN`;
- Status: `IN_PROGRESS / CI_RUNNING` on current head;
- AfterCompletion: `WAIT_FOR_COORDINATOR`.

### DEV 3 - EliteSCADA

- PR #52: **MERGED** `bd825682ae0ccfdbdb938fab638a27f6961510bf`;
- Status: `MERGED / WAITING`;
- no new task authorized;
- production S7 remains blocked.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread mandatory docs from current `main`;
2. inspect PR #50 current head, diff and CI #304;
3. if current-head CI is green, perform final semantic review of Gateway schema v9/validation and merge only if clean;
4. reconcile Gateway state into official docs after merge;
5. assign the protocol-independent Gateway runtime engine only after the Engineering contract is official;
6. keep DEV 1 and DEV 3 waiting unless a new non-overlapping assignment is explicitly recorded;
7. preserve `Gateway -> common diagnostics -> interface preview -> external protocols`.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Merged protocol research is architecture input, not implemented driver functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
