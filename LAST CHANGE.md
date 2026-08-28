# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **PYTHON-WAVE-06 ACTIVE — PARALLEL WORKER PHASE**  
**CI budget mode:** **CONSTRAINED until the GitHub Actions allowance resets on 2026-09-01**

Repository truth remains separated into `MERGED`, `IMPLEMENTED IN PR`, `MERGED_TO_INTEGRATION`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` and `SPECIFIED / NOT IMPLEMENTED`.

## Mandatory resume reading

Read current `main` before action:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/CI-USAGE-POLICY.md`;
8. `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
9. `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md` when Wave 06 is relevant;
10. current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth.

## CI BUDGET CONSTRAINT — ACTIVE

GitHub Actions usage remains near the included monthly allowance until reset on 2026-09-01. Use focused validation during iteration, reuse exact-head evidence, never rerun unchanged heads for reassurance, and reserve full matrices for meaningful integration/final gates. Final quality requirements are unchanged. If a mandatory final matrix cannot run, use `BLOCKED_BY_CI_BUDGET`; never merge with weaker evidence.

## SCRIPT-WAVE-05 — MERGED

**Logical WaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**Frozen central ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**Final integration head:** `13d3f8283275dc957d9d6168fc7fb165df992d7e`  
**Final CI:** #466 / run `33139334379` — Web SUCCESS, backend Release/full tests including PostgreSQL SUCCESS, Runtime smoke SUCCESS, Chromium SUCCESS  
**Coordinator PR:** #79 — MERGED  
**Main merge:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`

Merged Wave 05 provides canonical `scada.engineering` v10 Scripts, stable references/dependencies, lifecycle/package/revision fidelity, protected mutations and the practical Script Engineering Workspace. Production Python execution was intentionally not part of Wave 05.

## PYTHON-WAVE-06 — ACTIVE

**Logical WaveBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**Central ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**IntegrationBranch:** `integration/python-wave-06`  
**Integration PR:** #83 `Establish Wave 06 Client Visual Python foundation` — Draft integration train  
**Contract CI:** #468 / run `33140329634` — Web SUCCESS, backend Release/full tests including PostgreSQL SUCCESS, Runtime smoke SUCCESS, Chromium SUCCESS  
**Implementation authority:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`

Documentation-only commits after the Wave 05 functional merge do not invalidate the logical WaveBase. Worker branches start from the green ContractSHA because they depend on the architecture-first central foundation.

### Central foundation — IMPLEMENTED IN PR #83 / NOT MERGED TO MAIN

- `monaco-editor` pinned to `0.56.0` for the first Engineering Python editor;
- `pyodide` pinned to `314.0.6` for the first Client Visual browser engine adapter;
- versioned Client Visual Python bridge v1 contracts;
- safe policy preserved: 250 ms handler timeout, 128 queued events, minimum 50 ms timer, five failures before throttle, event-key coalescing and Script Runtime Instance isolation;
- initial 50 ms hard-stop grace after Pyodide soft interrupt, then Worker termination and interpreter discard;
- permitted Client Visual capability families and explicit denied infrastructure boundaries;
- versioned Worker initialize/compile/dispatch/API/cancel/dispose messages and stale-runtime identity checks;
- Vite development/acceptance COOP/COEP headers for SharedArrayBuffer interruption;
- canonical Engineering v10 remains Script definition authority. Monaco/Pyodide do not become project truth.

Security boundary remains:

`untrusted canonical Python source -> bounded Web Worker/Pyodide compartment -> versioned structured-clone RPC -> trusted browser host -> normal EliteSCADA Runtime/backend authorization`

Client Visual Python receives no direct driver, database, filesystem, shell/process, arbitrary-network, credential/token, DOM/storage, Server Memory write or direct shared/process TAG write authority.

### DEV 1 — ACTIVE

Branch: `feature/python-wave-06-editor`  
Task: `Python Editor UX`.

Implement Monaco-backed editing over the canonical Script Workspace: syntax highlighting, line numbers, normal code editing, 1-based diagnostic markers, visible scope/entry-point context and practical EliteSCADA API completion/help where useful. Preserve existing canonical Preview/Apply/CAS and Script metadata/dependencies. No Pyodide runtime authority.

### DEV 2 — ACTIVE

Branch: `feature/python-wave-06-client-runtime`  
Task: `Client Visual Python Worker / runtime adapter`.

Implement the restricted Pyodide module Worker behind bridge v1: compile diagnostics, event dispatch, injected trusted capability providers, timeout/interrupt/hard kill, bounded event admission, stale-response rejection, sanitized diagnostics and deterministic disposal/recreation. No direct infrastructure access and no central TAG/Client Memory/backend wiring; coordinator owns that integration.

### DEV 3 — ACTIVE

Branch: `test/python-wave-06-sandbox-safety`  
Task: `Sandbox execution safety / acceptance`.

Independently attack the sandbox claims: cross-origin isolation, syntax diagnostics, infinite-loop timeout/hard kill, cancellation/disposal, stale responses, queue flood, failure throttle, denied capabilities, permitted TAG/Client Memory behavior after integration, sanitized faults and isolation from unrelated product/backend state. Findings are classified rather than silently repaired across domains.

Full AllowedScope, ReservedFiles, ValidationMatrix, CompletionCriteria and AfterCompletion are authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## Coordinator next sequence

1. DEV 1/2/3 execute only their current ACTIVE Wave 06 assignments from ContractSHA `01d5b3092cf9c33ffa41c12b79133157b24cd148`;
2. workers open Draft PRs against `integration/python-wave-06` where practical and use focused evidence under constrained CI;
3. coordinator performs Early Contract / Integration / Delivery reviews as slices mature;
4. no worker alters shared `package.json`, Vite isolation config, bridge policy, `EngineeringApp.tsx`, `main.tsx`, Program.cs or canonical schema;
5. coordinator integrates accepted slices into PR #83 and wires trusted capability-provider interfaces to actual authorized TAG, Client Memory and backend surfaces;
6. coordinator owns final Engineering/Runtime composition and removal of obsolete/duplicate paths;
7. final exact-head Wave 06 matrix must include Web, backend Release/full tests, Runtime smoke, Chromium and Wave-specific sandbox acceptance;
8. only after that gate can PR #83 be marked Ready and merged to `main`;
9. synchronize docs and begin Wave 07 architecture-first readiness.

## Wave 06 final gate

The integrated product must prove:

`canonical ClientVisual Script -> edit/compile diagnostics -> isolated execution -> permitted TAG read -> owning-client Client Memory read/write -> controlled event -> bounded timeout/failure -> understandable diagnostics`

A faulty Script must not destabilize unrelated Runtime/backend state.

## Wave 07 — QUEUED ONLY

Visual Runtime Object Model remains queued. Its architecture-first work begins only after Wave 06 is merged and its final gate is green. Locked property precedence remains:

`Animation > Script > Binding/Expression > Engineering Base`.

## First owner validation gate — LOCKED

The first true owner-facing build remains **EliteSCADA v0.1 — Full Product Validation Preview** after functional Client Visual Python + graphical Engineering/Runtime. Modbus TCP is sufficient as the real industrial protocol. MQTT/OPC UA/BACnet/S7/Allen-Bradley production work remains post-v0.1 unless deliberately changed by the product owner.

## Permanent continuity rules

- workers never choose their own next task, modify `main`, merge their own PR or broaden scope;
- final wave quality is proven on integrated composition, not worker CI alone;
- documentation-only `main` movement does not invalidate logical WaveBaseSHA;
- known failing work is never merged;
- tests/security/CAS/runtime guards are never weakened for green CI or budget savings;
- research does not equal production implementation;
- canonical Engineering remains authority;
- when CI mode is `CONSTRAINED`, optimize frequency, never the required final evidence.
