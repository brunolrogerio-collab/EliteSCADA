# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md` and `docs/PARALLEL-WORK.md`.

**Last coordinator synchronization:** 2026-08-27

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, current `MustReadSpecific` and `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

`NextQueuedTask` is planning only. Workers never modify `main`, merge their own PR, work another DEV branch or broaden their mission.

## Current product gate

`SCRIPT-WAVE-05` is **ACTIVE — PARALLEL WORKER PHASE**.

**Logical WaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**Central ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**IntegrationBranch:** `integration/interface-wave-05`  
**Integration PR:** #79 `Canonicalize Script Engineering for Wave 05` — Draft, integration train  
**Contract validation:** CI #458 fully green: Web, backend Release/full tests including PostgreSQL, Runtime smoke and Chromium.  
**Product objective:** make Scripts first-class canonical Engineering entities whose source, scope, enabled state, entry points, dependencies and stable references survive canonical JSON, Preview/Apply, revisions/PostgreSQL and `.escadapkg` before production Python editor/runtime work begins.

Wave 04 is **MERGED** through PR #78. Final integration head `f0762d12814496a223abe740c57eb995ca472e97`; CI #446 fully green; main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`.

Documentation-only coordination commits after WaveBaseSHA do not invalidate the logical base. Worker execution branches for Wave 05 start from the frozen ContractSHA because their tasks depend on the stabilized central contract.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `SCRIPT-WAVE-05`  
**CurrentTask:** coordinate worker slices and integrate them into the canonical Script Engineering train  
**Branch:** `integration/interface-wave-05` plus `main` for coordination docs  
**Status:** `ACTIVE — INTEGRATION_COORDINATION`  
**LogicalBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**StartCondition:** `TRUE`

**DependsOn:** Wave 04 merged/green; merged isolated Script/VisualScripting foundations; canonical Engineering v9 and project lifecycle/package infrastructure.

**Central contract IMPLEMENTED IN PR #79:** canonical Engineering schema v10; first-class `Scripts` and `ScriptVisualEventReferences`; stable Script registry; Preview/Apply through canonical exchange; Workspace dirty/changeVersion ownership; Script read endpoints; dependency-safe Script delete with CAS/security/Audit; v9 compatibility; canonical JSON and `.escadapkg` fidelity; PostgreSQL immutable revision fidelity; exact-head full CI green.

**ReservedFiles:** `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, coordination docs, `.github/workflows/**`, `src/Scada.Api/Program.cs`, `src/Scada.Api/Runtime/EngineeringWorkspace.cs`, `src/Scada.Api/Runtime/EngineeringMutationEndpoints.cs`, canonical Engineering schema/contracts, `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`, `src/Scada.Engineering/ImportExport/Handlers/ScriptEngineeringHandler.cs`, central DI/composition, `EngineeringApp.tsx`, `main.tsx`.

**ForbiddenScope:** production Python interpreter/editor/sandbox; graphical editor; new protocol production work; weakening canonical Engineering, Preview/Apply/CAS, security or lifecycle authority.

**ValidationMatrix:** event-based worker reviews; integration of accepted slices into PR #79; final Wave 05 exact-head Web + backend Release/full tests + Runtime smoke + Chromium before Ready/merge.

**CompletionCriteria:** all three worker slices accepted/integrated; central UI hooks reconciled; canonical Script gate demonstrated end-to-end; PR #79 final exact-head CI green; merge to main; documentation synchronized; Wave 06 decision made.

**AfterCompletion:** prepare Wave 06 Python editor + Client Visual sandbox Definition of Ready.

**MustReadSpecific:** `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, `src/Scada.Engineering/Scripts/**`, canonical Import/Export/Persistence/ProjectPackages contracts/tests.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `SCRIPT-WAVE-05`  
**Status:** `ACTIVE`  
**CurrentTask:** `Script Engineering Workspace foundation`  
**Branch:** `feature/script-wave-05-engineering-workspace`  
**LogicalWaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**BaseSHA / ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**StartCondition:** `TRUE`

**Objective:** build a practical Script Engineering workspace over the frozen canonical contract. List/select/create/edit/delete Scripts; edit source as plain multiline text plus name/path, scope, enabled, description, entry points and dependency information. This is the foundation workspace, not the Wave 06 Python editor.

**AllowedScope:** isolated frontend implementation under `web/scada-web/src/engineering/scripts/**`; isolated tests under `web/scada-web/tests-e2e/*script*` when needed. Consume existing protected `GET /api/engineering/scripts`, `GET /api/engineering/workspace`, canonical JSON Preview/Apply endpoints and `DELETE /api/engineering/scripts/{id}`. For create/update, construct only a minimal canonical Script package and use Preview before Apply with the current Workspace version. Preserve backend identity authority and existing auth handling.

**ReservedFiles:** `web/scada-web/src/engineering/EngineeringApp.tsx`, `main.tsx`, central navigation/shell, `Program.cs`, `EngineeringWorkspace.cs`, `EngineeringMutationEndpoints.cs`, canonical C# schema/registry/exchange/handler files, coordination docs.

**ForbiddenScope:** Monaco or sophisticated code-editor framework; Python execution/sandbox; changing schema v10; new backend authority; editing other Engineering entity models; graphical editor; direct drivers/database/filesystem/network.

**DependsOn:** ContractSHA `b08b45201bf25a6d4d403b07c511cc34444177db`.

**ParallelSafeWith:** DEV 2 and DEV 3 provided ReservedFiles are respected.

**IntegrationRequired:** `YES`. Coordinator owns final placement in `EngineeringApp.tsx` and composition in PR #79.

**IntegrationTarget:** `integration/interface-wave-05` / PR #79.

**ValidationMatrix:** frontend build; focused Script workspace tests; Preview-before-Apply behavior; CAS conflict handling; 401/403 behavior; delete dependency conflict handling; no Python execution; PR exact-head green evidence where workflow permits.

**CompletionCriteria:** workspace can list/select/create/edit/delete canonical Scripts without bypassing Preview/Apply/CAS; metadata/source/entry-point/dependency fields round-trip through backend; focused tests green; Draft PR body separates `IMPLEMENTED IN PR`, `INTEGRATION REQUIRED`, `SPECIFIED / NOT IMPLEMENTED`.

**NextQueuedTask:** `Wave 06 Python Editor` — `QUEUED`, not executable.

**NextStartCondition:** Wave 05 merged and Wave 06 board promotion.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**MustReadSpecific:** `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md` Wave 05/06 sections, `src/Scada.Engineering/Scripts/ScriptEngineeringContracts.cs`, `web/scada-web/src/engineering/projectPortabilityApi.ts`, existing Engineering workspace/API patterns.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `SCRIPT-WAVE-05`  
**Status:** `ACTIVE`  
**CurrentTask:** `Script Reference Runtime / validation adapter`  
**Branch:** `feature/script-wave-05-reference-validation`  
**LogicalWaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**BaseSHA / ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**StartCondition:** `TRUE`

**Objective:** strengthen reusable stable Script dependency/reference resolution over the canonical v10 model for TAGs, Client Memory, Server Memory and visual-definition boundaries, without giving Scripts direct runtime infrastructure authority.

**AllowedScope:** isolated additions under `src/Scada.Engineering/Scripts/**` excluding coordinator-modified `ScriptEngineeringRegistry.cs` unless explicitly required by an identified contract defect; focused tests primarily in `tests/Scada.Persistence.PostgreSql.Tests/Script*` or a new isolated Script reference test file. Build reusable reference/catalog/diagnostic helpers that operate on public canonical identities and existing runtime adapters. Do not wire central DI/handlers yourself; report `INTEGRATION REQUIRED` if coordinator wiring is needed.

**ReservedFiles:** canonical Engineering schema, `EngineeringExchangeService.cs`, `ScriptEngineeringHandler.cs`, `Program.cs`, `EngineeringWorkspace.cs`, `EngineeringMutationEndpoints.cs`, `ScriptEngineeringRegistry.cs`, frontend shell/composition, coordination docs.

**ForbiddenScope:** direct driver access; protocol implementation; PostgreSQL schema change; Python interpreter/sandbox; arbitrary filesystem/network/database access; graphical editor; changing visual object identity contract prematurely.

**DependsOn:** ContractSHA `b08b45201bf25a6d4d403b07c511cc34444177db`.

**ParallelSafeWith:** DEV 1 and DEV 3.

**IntegrationRequired:** `YES` only for any central handler wiring; reusable isolated contract/test slice itself should remain mergeable without central-file edits.

**IntegrationTarget:** `integration/interface-wave-05` / PR #79.

**ValidationMatrix:** focused .NET tests for stable reference construction/resolution, TAG versus Client/Server Memory classification, missing references, deterministic output and scope isolation; full backend build evidence on exact branch head.

**CompletionCriteria:** reusable canonical reference/validation helpers are deterministic, public-contract based and tested; no direct driver/runtime authority; PR body separates implementation, integration needs and deferred work.

**NextQueuedTask:** `Wave 06 Browser Client Visual Python Runtime` — `QUEUED`, not executable.

**NextStartCondition:** Wave 05 merged, pinned Wave 06 sandbox decision and board promotion.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**MustReadSpecific:** `src/Scada.Engineering/Scripts/**`, `src/Scada.Engineering/VisualScripting/**`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, python-client research, Internal Memory contracts.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `SCRIPT-WAVE-05`  
**Status:** `ACTIVE`  
**CurrentTask:** `Script compatibility validation`  
**Branch:** `test/script-wave-05-compatibility`  
**LogicalWaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**BaseSHA / ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**StartCondition:** `TRUE`

**Objective:** independently attack the Wave 05 canonical contract with compatibility and acceptance tests. Validate what the implementation claims instead of duplicating its code.

**AllowedScope:** isolated new test files under `tests/Scada.Core.Tests/*Script*`, `tests/Scada.Persistence.PostgreSql.Tests/*Script*` and `web/scada-web/tests-e2e/*script*` only when API/browser acceptance is useful. Test v9→v10 compatibility, JSON round-trip, revision/package fidelity, stable identities, dependency cycles, missing references, invalid scopes/events/language/source, Script delete dependency safety, deterministic validation and restart/persistence expectations available through current contracts.

**ReservedFiles:** all production source files; existing coordinator-owned canonical Script tests may be read but not rewritten unless a demonstrable defect requires escalation; central E2E shell helpers; coordination docs.

**ForbiddenScope:** production implementation or architecture changes; weakening assertions; broad unrelated regression cleanup; Python interpreter/editor/sandbox; graphical editor; protocol work.

**DependsOn:** ContractSHA `b08b45201bf25a6d4d403b07c511cc34444177db`.

**ParallelSafeWith:** DEV 1 and DEV 2.

**IntegrationRequired:** `YES`, coordinator selects non-duplicative acceptance coverage into PR #79.

**IntegrationTarget:** `integration/interface-wave-05` / PR #79.

**ValidationMatrix:** focused .NET/Web tests plus exact-head CI evidence. Findings must be classified `BLOCKER`, `MAJOR UX`, `MINOR UX`, or `TEST GAP`; do not silently expand production scope to fix them.

**CompletionCriteria:** independent coverage demonstrates canonical Script fidelity/migrations/reference safety and reports any real defect with reproduction; Draft PR body clearly separates tests/findings/deferred work.

**NextQueuedTask:** `Wave 06 Sandbox execution safety` — `QUEUED`, not executable.

**NextStartCondition:** Wave 05 merged and Wave 06 board promotion.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**MustReadSpecific:** `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, canonical Script contracts/validation, Engineering Import/Export/Persistence/ProjectPackage tests, PR #79 contract behavior.

---

## Coordinator note for future chats

Current action boundary: **DEV 1, DEV 2 and DEV 3 are ACTIVE on the exact assignments above.** Each worker starts from ContractSHA `b08b45201bf25a6d4d403b07c511cc34444177db`, opens a Draft PR early, remains inside AllowedScope/ReservedFiles, and returns to `WAIT_FOR_COORDINATOR` after its CompletionCriteria. Coordinator alone integrates into PR #79 and merges Wave 05 to `main`.