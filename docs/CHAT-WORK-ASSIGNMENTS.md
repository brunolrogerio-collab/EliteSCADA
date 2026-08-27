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

**CurrentTask:** Coordinate TAG Gateway Engineering plus isolated OPC UA and S7 ISO Connection research spikes

**Branch:** `main`

**Status:** `IN_PROGRESS`

**Objective:**

Keep DEV 2 on the active functional TAG Gateway Engineering gate while DEV 1 and DEV 3 reduce uncertainty around later external protocols through strictly documentation/research-only spikes. Preserve the locked implementation order and prevent central Engineering/runtime overlap.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, workflow maintenance, assignment board, handoff/roadmap documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no production OPC UA or S7 Data Source/runtime implementation before the external-protocol gate opens;
- no new external protocol family in Active Runtime before Gateway, common diagnostics and interface-preview gates;
- no concurrent worker ownership of `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
- no canonical Script schema integration while DEV 2 owns overlapping central Engineering contract files.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/OPC-UA.md`
- `docs/S7-ISO-CONNECTION.md`

**ObservedGitHubState:**

- PR #46 Audit UI: **MERGED** `5629f55699d68d70d11d7058c26033d54306b570`.
- PR #47 Script Engineering foundation: **MERGED** `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`.
- PR #48 Internal Memory Engineering/retention: **MERGED** `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4`.
- PR #49 complete Internal Memory runtime/product integration: **MERGED** `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`; CI #296 and post-merge CI #297 fully green.
- Internal Memory is **MERGED / COMPLETE**.
- TAG Gateway is the active locked functional block.
- Canonical Engineering remains schema v8 until DEV 2 deliberately evolves it with compatibility coverage.
- `docs/OPC-UA.md` locks the later OPC UA discovery/browse/import experience.
- `docs/S7-ISO-CONNECTION.md` locks the later Siemens S7 classic ISO-on-TCP/RFC1006 direction and TIA-assisted import research.

**Dependencies:**

- DEV 2 exclusively owns the functional public/versioned TAG Gateway Engineering slice and Gateway-related central Engineering contract changes.
- DEV 1 is research-only for OPC UA and must not touch production protocol/runtime or DEV 2 central Engineering ownership.
- DEV 3 is research-only for Siemens S7 ISO Connection and must not touch production protocol/runtime or DEV 2 central Engineering ownership.
- Production external protocols remain blocked until Gateway, common diagnostics and interface-validation preview gates are complete.
- Canonical Script package/schema integration remains deferred while DEV 2 owns overlapping central Engineering surfaces.

**NextActions:**

1. let DEV 2 continue `feature/tag-gateway-engineering` within its functional Engineering/validation assignment;
2. let DEV 1 continue `research/opc-ua-discovery-import` as documentation/research only;
3. let DEV 3 create/use `research/s7-iso-connection` as documentation/research only;
4. inspect all active branches/PRs on the next coordinator `siga`;
5. merge Gateway Engineering only after review and relevant full CI are green;
6. review protocol research PRs as design inputs only, never as claims of implemented drivers;
7. after Gateway Engineering is official, assign the protocol-independent runtime Gateway engine;
8. keep common diagnostics, interface preview and production external protocols blocked until their prerequisites are satisfied.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** OPC UA discovery, address-space browse and TAG-import Engineering research spike

**Branch:** `research/opc-ua-discovery-import`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Research/specify the future OPC UA Engineering experience: server/network discovery, endpoint/security/certificate inspection, lazy address-space browse/search, multi-select/subtree import preview, subscription profiles, NodeId + namespace-aware BrowsePath reconciliation and safe rescan. No production OPC UA runtime is authorized.

**AllowedScope:** official Elipse/OPC Foundation research; isolated docs under `docs/research/opc-ua/**`; library/package/license/security/interoperability evaluation; diagrams/examples; documentation-only PR.

**ForbiddenScope:** production OPC UA networking/runtime; production OPC UA dependency; Data Source registration; `Program.cs`; central DI/API; `src/Scada.DriverHost/**` runtime work; `EngineeringContracts.cs`; Gateway/schema files; production frontend routing/UI; workflows; unrelated protocols.

**MustReadSpecific:**

- `docs/OPC-UA.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/TAG-GATEWAY.md`

**CompletionCriteria:** sourced Elipse comparison; recommended OPC UA .NET stack; discovery/scan limits; endpoint/certificate trust; lazy browse/search; Node identity/re-resolution; datatype/access mapping; subscription profiles; import Preview/Apply proposal; rescan/diff; test-server strategy; future implementation breakdown; no production code/dependency.

**NextActions:** create/use only `research/opc-ua-discovery-import`, produce a Draft documentation PR marked `RESEARCH IN PR / NOT IMPLEMENTED`, then stop.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public TAG Gateway Engineering contract and deterministic validation foundation

**Branch:** `feature/tag-gateway-engineering`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Implement the first-class public/versioned Gateway/TAG Bridge Engineering domain and deterministic Preview validation required by `docs/TAG-GATEWAY.md`, without runtime transfer execution, API/DI composition, diagnostics UI or protocol-specific behavior.

**AllowedScope:** new Gateway Engineering files; narrow exclusive Gateway exception for `src/Scada.Engineering/Contracts/EngineeringContracts.cs`; required Gateway integration in Engineering import/export; focused Core/PostgreSQL/package tests; PR evidence.

**ForbiddenScope:** `main`; Program.cs/API/central DI; DriverHost Gateway runtime; TAG event/write execution; frontend Gateway UI; workflows; protocol-specific code; common diagnostics; Script canonical integration; Client Memory Gateway endpoints; silent coercion.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`

**CompletionCriteria:** first-class Gateway routes; stable endpoints and policies; deliberate schema evolution/compatibility if required; deterministic endpoint/type/cycle/multi-writer/rate validation; Server Memory accepted and Client Memory rejected; fan-out valid; canonical round trips/package persistence; no runtime engine/UI; focused tests and relevant CI green; exact future runtime `INTEGRATION REQUIRED` recorded.

**NextActions:** create/use only `feature/tag-gateway-engineering`; implement/tests; open/update Draft PR with `IMPLEMENTED IN PR / NOT MERGED`; correct attributable CI failures; stop when green and complete.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Siemens S7 ISO Connection architecture, TIA import and interoperability research spike

**Branch:** `research/s7-iso-connection`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Research and specify the future EliteSCADA Siemens S7 driver using classic **S7 communication over ISO-on-TCP / RFC1006 (TCP/102)**. Define connection behavior for S7-300/400/1200/1500, Rack/Slot and TSAP modes, PUT/GET/protection constraints, absolute/optimized DB limitations, typed address mapping, efficient batching/reconnect, safe optional network assistance and TIA Portal-assisted TAG import. This is explicitly a non-production spike.

**AllowedScope:**

- research official Siemens documentation, current Elipse M-Prot ISO/TCP behavior and relevant open-source S7 libraries;
- compare S7.NetPlus, Sharp7 and other justified candidates for licensing, maintenance, correctness, async/cancellation, reconnect, PDU/batching, write semantics and testability;
- create/update only isolated research documentation under `docs/research/s7/**` or another new S7 research path;
- document S7-300/400/1200/1500 connection matrix, Rack/Slot/TSAP rules, CPU protection/PUT-GET behavior, optimized DB limitations and diagnostics;
- define typed S7 area/address/data-type mapping without copying Elipse N/B parameters;
- investigate TIA Portal Openness and Siemens-supported file export as Engineering-side TAG import sources;
- design `TIA project/export -> candidates -> validate -> preview -> apply` workflow without editing central Engineering contracts;
- investigate a bounded, opt-in TCP/102 discovery/connection-test aid that never writes or changes CPU state;
- define later lab/CI strategy using representative hardware/simulators and note PLCSIM limitations;
- documentation-only Draft PR may record findings and future implementation slices.

**ForbiddenScope:**

- `main`;
- production S7 networking/client runtime inside EliteSCADA;
- registering a Siemens S7 Data Source/driver in Active Runtime;
- adding S7.NetPlus, Sharp7, Snap7 or another S7 production dependency to EliteSCADA projects;
- implementing S7commPlus or proprietary Siemens engineering/programming operations;
- CPU RUN/STOP, program/block upload/download/delete or firmware operations;
- `src/Scada.Api/Program.cs`, central DI/API/hosted services;
- `src/Scada.DriverHost/**` functional S7 runtime work;
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs` or Gateway/schema files owned by DEV 2;
- frontend central routing/shell or production S7 UI;
- `.github/workflows/**`;
- changing the locked protocol order;
- claiming the S7 driver is implemented because research is complete.

**MustReadSpecific:**

- `docs/S7-ISO-CONNECTION.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/TAG-GATEWAY.md`

**Dependencies:**

- production S7 ISO Connection remains after the interface-preview/external-protocol gate;
- this spike is allowed early only because it is documentation/research and does not activate protocol runtime behavior;
- DEV 2 owns central Engineering contract evolution during the current Gateway slice;
- future S7 runtime must use the common Data Source/Driver/TAG/Gateway/diagnostics/security architecture.

**CompletionCriteria:**

1. Provide sourced comparison of relevant Elipse M-Prot ISO/TCP conveniences and what EliteSCADA should adopt/improve/avoid.
2. Recommend a .NET S7 stack/library direction with license, support, security, maintenance and testability rationale.
3. Produce S7-300/400/1200/1500 connection matrix covering Rack/Slot, TSAP, port, CPU-side prerequisites and known differences.
4. Document PUT/GET/access-control behavior and distinguish permission/protection failures from transport failures where feasible.
5. Define optimized/non-optimized DB support boundaries and never silently claim classic absolute access for unsupported optimized symbolic data.
6. Define typed address/data-type mapping for I/Q/M/DB and applicable legacy areas, including unsupported/lossy cases.
7. Define PDU-aware grouping/batching, scan classes, reconnect and bounded parallelism direction.
8. Define TIA Portal Openness and file-export TAG import strategy, including hierarchy, addresses, data types, comments, writability and unsupported candidates.
9. Define canonical import candidate/Preview/Apply contract without touching central schema in this spike.
10. Define bounded optional network scan/connection-test behavior with no destructive operations.
11. Define representative hardware/simulator and later CI/interoperability strategy.
12. Produce a concise later production implementation breakdown.
13. Do not introduce production S7 runtime code or dependencies.

**NextActions:**

1. create/use only `research/s7-iso-connection` from current `main`;
2. reread required docs and verify exact branch/assignment;
3. study Siemens, Elipse M-Prot and candidate library documentation;
4. write the isolated research deliverable with concrete EliteSCADA recommendations;
5. open/update a Draft PR containing documentation/research only and wording `RESEARCH IN PR / NOT IMPLEMENTED`;
6. stop under `WAIT_FOR_COORDINATOR` when complete.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
