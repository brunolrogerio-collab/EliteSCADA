# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-26

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open branch/PR and is not product implementation.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Review and integrate the active TAG Gateway Engineering slice; OPC UA and Siemens S7 research spikes are merged

**Branch:** `main`

**Status:** `IN_PROGRESS`

**Objective:**

Keep the locked functional sequence moving through TAG Gateway while preserving the merged OPC UA and Siemens S7 research as future implementation inputs only. DEV 1 and DEV 3 remain idle until explicitly reassigned.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, workflow maintenance, assignment board, handoff/roadmap documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no production OPC UA or S7 Data Source/runtime before the external-protocol gate opens;
- no new external protocol family in Active Runtime before Gateway, common diagnostics and interface-preview gates;
- no concurrent worker ownership of `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
- no canonical Script schema integration while DEV 2 owns overlapping central Engineering contract files.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`
- `docs/S7-ISO-CONNECTION.md`
- `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`

**ObservedGitHubState:**

- PR #49 complete Internal Memory runtime/product integration: **MERGED** `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`; CI #296 and post-merge CI #297 fully green.
- Internal Memory: **MERGED / COMPLETE**.
- PR #51 OPC UA discovery/browse/import research: **MERGED** `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`; research head `6aac0f3cfe8e89cc7c56cc2bf3668d03a8c94994`; CI #299 green.
- PR #52 Siemens S7 ISO Connection research: **MERGED** `bd825682ae0ccfdbdb938fab638a27f6961510bf`; research head `52cc16c9941b5f8c4442a8c135551de6e9f8976b`; CI #300 fully green. The merge is documentation-only, so the `main` push workflow does not run for that merge by path filter.
- Production OPC UA and Siemens S7 remain **SPECIFIED / NOT IMPLEMENTED** and gated.
- PR #50 TAG Gateway Engineering remains **DRAFT / OPEN**. It has reconciled the coordinator-owned Internal Memory schema-test fix out of its delta. Its current head is `002f87dd126854c9fd972e453930e229e02f7f30`; CI #304 is the current-head validation and must complete before coordinator merge review.
- TAG Gateway remains the active locked functional block.

**Dependencies:**

- DEV 2 owns the current public/versioned TAG Gateway Engineering slice and its narrow Gateway-related central contract exception.
- DEV 1 completed OPC UA research and waits.
- DEV 3 completed Siemens S7 research and waits.
- Production external protocols remain blocked until Gateway, common diagnostics and interface-validation preview are complete.
- Canonical Script package/schema integration remains deferred while DEV 2 owns overlapping central Engineering surfaces.

**NextActions:**

1. monitor/review DEV 2 PR #50 current head and CI #304;
2. merge Gateway Engineering only after current-head CI is fully green and final diff/semantics remain within assignment;
3. after Gateway Engineering is official, assign the protocol-independent runtime Gateway engine;
4. keep DEV 1 and DEV 3 in `MERGED / WAITING`;
5. preserve common diagnostics -> interface preview -> external protocol gate order.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** OPC UA discovery, address-space browse and TAG-import Engineering research spike

**Branch:** `research/opc-ua-discovery-import`

**Status:** `MERGED / WAITING`

**PullRequest:** `#51 MERGED — aa7735fcc15e00aea5bf19a543f53b2735ef48e3`

**MustReadSpecific:**

- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/TAG-GATEWAY.md`

**CompletionCriteria:** **SATISFIED / MERGED** through PR #51. Research covers Elipse comparison, OPC Foundation .NET stack, discovery/scan, endpoint/certificate trust, lazy browse/search, portable node identity, NodeId re-resolution, datatype/access mapping, subscriptions, Preview/Apply import, rescan/diff and interoperability strategy. No production OPC UA code/dependency was introduced.

**NextActions:** no new task. On `siga`, verify PR #51 remains merged and report `MERGED / WAITING`. Do not begin production OPC UA or another task without coordinator reassignment.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public TAG Gateway Engineering contract and deterministic validation foundation

**Branch:** `feature/tag-gateway-engineering`

**Status:** `IN_PROGRESS / CI_RUNNING`

**PullRequest:** `#50 DRAFT / OPEN`

**Objective:** implement the first-class public/versioned Gateway/TAG Bridge Engineering domain and deterministic Preview validation required by `docs/TAG-GATEWAY.md`, without runtime transfer execution, API/DI composition, diagnostics UI or protocol-specific behavior.

**AllowedScope:** new Gateway Engineering files; narrow exclusive Gateway exception for `src/Scada.Engineering/Contracts/EngineeringContracts.cs`; required Gateway integration in Engineering import/export; focused Core/PostgreSQL/package tests; PR evidence.

**ForbiddenScope:** `main`; Program.cs/API/central DI; DriverHost Gateway runtime; TAG event/write execution; frontend Gateway UI; workflows; protocol-specific code; common diagnostics; Script canonical integration; Client Memory Gateway endpoints; silent coercion.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`

**CompletionCriteria:** first-class Gateway routes; stable endpoints/policies; deliberate schema evolution/compatibility; deterministic endpoint/type/cycle/multi-writer/rate validation; Server Memory accepted and Client Memory rejected; fan-out valid; canonical round trips/package persistence; no runtime engine/UI; focused tests and current-head CI green; future runtime `INTEGRATION REQUIRED` recorded.

**NextActions:** remain on PR #50 only. Observe CI #304 for current head `002f87dd126854c9fd972e453930e229e02f7f30`; fix only attributable issues; when green and complete, stop under `WAIT_FOR_COORDINATOR`. Do not merge own PR.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Siemens S7 ISO Connection architecture, TIA import and interoperability research spike

**Branch:** `research/s7-iso-connection`

**Status:** `MERGED / WAITING`

**PullRequest:** `#52 MERGED — bd825682ae0ccfdbdb938fab638a27f6961510bf`

**Objective:** research/specify future Siemens S7 classic communication over ISO-on-TCP / RFC1006 TCP/102, including S7-300/400/1200/1500 connection behavior, Rack/Slot and explicit TSAP, PUT/GET/protection constraints, optimized DB limitations, typed addressing, batching/reconnect, safe network assistance and TIA-assisted import. Production S7 is not authorized yet.

**MustReadSpecific:**

- `docs/S7-ISO-CONNECTION.md`
- `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/TAG-GATEWAY.md`

**CompletionCriteria:** **SATISFIED / MERGED** through PR #52. CI #300 passed Web, backend build/tests, runtime smoke and Chromium E2E. The merged research covers Elipse M-Prot comparison; S7.NetPlus/Sharp7/Snap7 evaluation; CPU/TSAP matrix; PUT/GET/protection; optimized DB boundary; typed addressing; PDU-aware batching/reconnect; TIA Openness/file import; Preview/Apply candidates; bounded TCP/102 assistance; simulator/hardware test strategy and future implementation slices. No production S7 runtime/dependency was introduced.

**NextActions:** no new task. On `siga`, verify PR #52 remains merged and report `MERGED / WAITING`. Do not begin production S7, choose/install a production library or start another task without coordinator reassignment.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
