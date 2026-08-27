# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26
**Last coordinator synchronization:** 2026-08-26

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its `MustReadSpecific` assignment. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = exists only in an open worker PR/branch.
- **SPECIFIED / NOT IMPLEMENTED** = documented intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Coordinate TAG Gateway Engineering while DEV 1 performs the isolated OPC UA discovery/import research spike

**Branch:** `main`

**Status:** `IN_PROGRESS`

**PullRequest:** none

**Objective:**

Keep DEV 2 on the active functional TAG Gateway Engineering gate, allow DEV 1 to reduce uncertainty around the later OPC UA protocol through a strictly non-production discovery/browse/import spike, preserve the locked implementation order, and avoid central Engineering/runtime overlap.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, workflow maintenance, assignment board, handoff/roadmap documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no runtime TAG Gateway implementation that bypasses the public/versioned Engineering contract;
- no production OPC UA Data Source/runtime implementation before the external-protocol gate opens;
- no new external protocol family in Active Runtime before Gateway, common diagnostics and interface-preview gates;
- no concurrent worker ownership of `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
- no canonical Script schema integration while DEV 2 owns overlapping central Engineering contract files.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/OPC-UA.md`

**ObservedGitHubState:**

- PR #46 Audit UI is **MERGED** as `5629f55699d68d70d11d7058c26033d54306b570` after CI #244 passed.
- PR #47 Script Engineering foundation is **MERGED** as `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb` after CI #248 passed.
- PR #48 Internal Memory Engineering + durable Server Memory retention is **MERGED** as `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4` after CI #265 and post-merge CI #266 passed.
- PR #49 complete Internal Memory runtime/product integration is **MERGED** as `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f` after final CI #296 and post-merge CI #297 passed.
- Internal Memory complete product integration is **MERGED / COMPLETE** and no longer blocks TAG Gateway.
- Canonical Engineering remains schema v8 until the Gateway Engineering worker deliberately evolves it with compatibility coverage.
- `docs/OPC-UA.md` now locks the future OPC UA discovery, endpoint/security inspection, browse, multi-select import, NodeId re-resolution and rescan experience.
- No production OPC UA implementation is authorized by this research assignment.

**Dependencies:**

- TAG Gateway remains the active locked functional source/protocol block.
- DEV 2 exclusively owns the public/versioned Gateway Engineering contract and central Gateway-related `EngineeringContracts.cs` changes.
- DEV 1 may work only on the isolated non-production OPC UA research/specification slice; it must not touch runtime protocol composition or DEV 2 central Engineering ownership.
- DEV 3 remains `WAIT_FOR_COORDINATOR`.
- Canonical Script package/schema integration remains deferred while DEV 2 owns overlapping central Engineering contract surfaces.

**NextActions:**

1. let DEV 2 create/use `feature/tag-gateway-engineering` and implement only the assigned Engineering/validation slice;
2. let DEV 1 create/use `research/opc-ua-discovery-import` and produce the assigned OPC UA research/specification deliverable without production protocol code;
3. inspect each worker branch/PR/head/diff/CI or documentation evidence on the next coordinator `siga`;
4. merge functional Gateway Engineering only after focused tests and relevant full CI are green;
5. review DEV 1 OPC UA findings for incorporation into the later production protocol assignment, without changing the current runtime gate;
6. after the Gateway Engineering contract is official on `main`, assign the protocol-independent runtime Gateway engine as the next functional slice;
7. keep common multi-driver diagnostics and interface-validation preview blocked until preceding gates are complete;
8. keep DEV 3 idle until a non-conflicting explicit assignment is recorded.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** OPC UA discovery, address-space browse and TAG-import Engineering research spike

**Branch:** `research/opc-ua-discovery-import`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Research and specify the future EliteSCADA OPC UA client Engineering workflow, taking useful conveniences from the Elipse E3 OPC UA experience and improving them for EliteSCADA: network/server discovery, endpoint/security inspection, certificate trust, lazy address-space browsing, search/filter, multi-select/subtree TAG import preview, subscription profiles, NodeId/BrowsePath reconciliation and safe rescan. This is explicitly a non-production spike and must not activate an OPC UA runtime Data Source.

**AllowedScope:**

- research official Elipse E3 OPC UA manuals and relevant OPC Foundation specifications/reference client behavior;
- evaluate the official OPC Foundation UA .NET Standard client stack as the primary implementation candidate, including current package/version/licensing/security/interoperability considerations;
- create/update only workstream-specific research documentation under `docs/research/opc-ua/**` or another new isolated OPC-UA research path agreed by this assignment;
- document proposed discovery modes, bounded network scan, endpoint/security/certificate model, address-space browse/search model, Node identity/re-resolution strategy, TAG type/access mapping, subscription profile model, import-preview UX/data contracts, simulator/test-server strategy and known risks;
- create diagrams/tables/examples inside the research documentation when useful;
- PR body may record research findings, sources, decisions proposed for coordinator review and exact future `INTEGRATION REQUIRED` implementation slices.

**ForbiddenScope:**

- `main`;
- production OPC UA networking/client execution inside EliteSCADA runtime;
- adding/registering an OPC UA Data Source or driver type in Active Runtime;
- adding OPC UA NuGet/runtime dependencies to production projects;
- `src/Scada.Api/Program.cs`, central DI/composition, hosted services or API endpoints;
- `src/Scada.DriverHost/**` functional OPC UA runtime work;
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs` or any Gateway/schema file currently owned by DEV 2;
- frontend central routing/shell or production OPC UA UI;
- `.github/workflows/**`;
- changing the locked source/protocol order;
- implementing MQTT/BACnet/S7 or unrelated protocol code;
- insecure certificate auto-trust as a proposed product default;
- claiming the OPC UA driver is implemented merely because the spike is complete.

**MustReadSpecific:**

- `docs/OPC-UA.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/TAG-GATEWAY.md`

**Dependencies:**

- production OPC UA remains after the interface-preview/external-protocol gate;
- this spike is allowed early only because it does not register protocol runtime behavior;
- DEV 2 owns central Engineering contract evolution during the current Gateway slice;
- future OPC UA production implementation must use the common Data Source/Source Provider/TAG/Gateway/diagnostics/security architecture.

**CompletionCriteria:**

1. Provide a sourced comparison of relevant Elipse E3 OPC UA conveniences and what EliteSCADA should adopt, improve or intentionally avoid.
2. Recommend an OPC UA .NET client stack/package/version direction, with license/security/support rationale and no private-protocol reinvention without cause.
3. Define manual endpoint, standard discovery and bounded/cancellable network-scan behavior, including duplicate result reconciliation and industrial-network safety limits.
4. Define endpoint inspection and certificate trust workflow, including fail-closed unexpected certificate/server identity changes.
5. Define lazy address-space browse, continuation/pagination strategy, search/filter behavior and large-server limits/cancellation.
6. Define node identity using NodeId plus namespace-aware portable BrowsePath/namespace URI reconciliation, including Refresh/Re-resolve Node IDs and mismatch handling.
7. Define OPC UA -> EliteSCADA TAG data-type/access mapping, including explicit unsupported/lossy cases.
8. Define subscription/monitored-item Engineering profiles and how imported TAGs choose/default them.
9. Define multi-select/subtree import candidate workflow and canonical Engineering Preview/Apply integration contract without editing the central schema in this spike.
10. Define Rescan/diff behavior for new/missing/changed nodes without silent Engineering deletion.
11. Identify representative OPC UA test servers/simulators and a later CI/interoperability test strategy.
12. Produce a concise recommended implementation breakdown for the future production driver after the gate opens.
13. Do not introduce production OPC UA runtime code or dependencies.

**NextActions:**

1. create/use only `research/opc-ua-discovery-import` from current `main`;
2. reread the required repository docs and verify the branch/assignment before research;
3. study current Elipse E3 OPC UA manuals and official OPC Foundation client/discovery/subscription references;
4. write the isolated research deliverable with concrete EliteSCADA recommendations;
5. open/update a Draft PR to `main` containing documentation/research only and wording `RESEARCH IN PR / NOT IMPLEMENTED`;
6. stop under `WAIT_FOR_COORDINATOR` when the completion criteria are met.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public TAG Gateway Engineering contract and deterministic validation foundation

**Branch:** `feature/tag-gateway-engineering`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Implement the first-class public/versioned Gateway/TAG Bridge Engineering domain and deterministic Preview validation required by `docs/TAG-GATEWAY.md`, without implementing runtime transfer execution, API/DI composition, diagnostics UI or protocol-specific behavior.

**AllowedScope:**

- new isolated Gateway Engineering files under `src/Scada.Engineering/Gateway/**`;
- **narrow explicit exception:** `src/Scada.Engineering/Contracts/EngineeringContracts.cs` only for the canonical Gateway route collection/contracts required by this assignment;
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs` only for Gateway schema/export/parse/preview/apply integration;
- Gateway-specific additions under `src/Scada.Engineering/ImportExport/Handlers/**` and adjacent import/export helpers when necessary;
- focused tests under `tests/Scada.Core.Tests/**`;
- focused persistence/package compatibility tests under `tests/Scada.Persistence.PostgreSql.Tests/**` when required to prove revision/project-package preservation;
- PR body updates describing implementation, tests, CI and exact `INTEGRATION REQUIRED` runtime hooks.

**ForbiddenScope:**

- `main`;
- `src/Scada.Api/Program.cs`, API endpoints, central DI/composition and hosted services;
- `src/Scada.DriverHost/**`, runtime Gateway execution, TAG event subscriptions or destination write routing;
- frontend routing/shell or Gateway UI;
- `.github/workflows/**`;
- protocol-specific driver code;
- common communication-driver diagnostics implementation;
- Python/Script canonical integration;
- Client Memory as a Gateway endpoint;
- silent type coercion, hidden scripts or browser-only Gateway configuration;
- runtime Audit/event flooding behavior.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`

**Dependencies:**

- Internal Memory complete product integration is official on `main` through PR #49 merge `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f` and post-merge CI #297.
- Canonical Engineering baseline is schema v8.
- This worker exclusively owns Gateway-related changes to `EngineeringContracts.cs` for the duration of this assignment.

**CompletionCriteria:**

1. Gateway routes are a first-class collection/entity in canonical `scada.engineering`, not metadata, scripts or browser state.
2. Public route semantics include stable ID, stable key/name, enabled state, source TAG stable ID/path, destination TAG stable ID/path, OnChange/Periodic mode, source-quality policy, deterministic type/conversion policy, bounded rate/deadband controls, startup/initial-transfer policy and description/metadata where appropriate.
3. If the canonical package shape requires a schema evolution, advance from v8 to the next schema version deliberately and preserve deterministic compatibility for v1-v8 inputs; never create a parallel private schema.
4. Preview validation deterministically rejects missing or ID/path-mismatched endpoints, Client Memory endpoints, read-only/unwritable destinations detectable from Engineering, invalid/disabled source ownership where applicable, duplicate route identity, direct or indirect active cycles, multiple active Gateway writers to one destination, pathological intervals/rate settings and unsafe type/conversion combinations.
5. Fan-out from one source to several distinct destinations remains valid.
6. `builtin.memory.server` is accepted as a normal server-owned source or destination; `builtin.memory.client` is rejected.
7. Canonical JSON export/parse/preview/apply round trips preserve Gateway routes and references.
8. Revision/project-package persistence/backup round trips preserve Gateway routes without adding mutable runtime diagnostics/state to Engineering.
9. No runtime route engine, API/DI hook, protocol-specific adapter or UI is introduced in this worker slice.
10. Focused tests cover the validation graph and compatibility rules, and relevant full CI is green.
11. PR body lists exact coordinator/runtime `INTEGRATION REQUIRED` items for the later Gateway engine slice.

**NextActions:**

1. create/use only `feature/tag-gateway-engineering` from current `main`;
2. implement the smallest coherent Gateway Engineering domain satisfying the completion criteria;
3. add focused automated tests before broad integration changes;
4. open/update a Draft PR to `main` with `IMPLEMENTED IN PR / NOT MERGED` wording;
5. run/observe CI, correct attributable failures without weakening tests;
6. when completion criteria are met and CI is green, stop under `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public Script Engineering integration foundation

**Branch:** `feature/script-engineering-integration`

**Status:** `MERGED / WAITING`

**PullRequest:** `#47` — **MERGED**

**MergedCommit:** `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`

**Validation:** CI #248 passed Web build, backend build/tests, runtime smoke and Chromium E2E.

**MergedScope:** isolated Script Engineering contracts, adapters, deterministic validation and focused tests. No concrete Python engine/editor or graphical editor was introduced.

**IntegrationRequired:** first-class Scripts/references still require canonical Engineering schema/package integration. This remains deferred while the active Gateway Engineering slice owns overlapping central contract files.

**NextActions:** none. Do not start Python editor/sandbox, graphical editor or canonical Script schema work until this board gives a new task. On `siga`, report `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`