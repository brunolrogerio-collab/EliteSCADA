# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-27  
**Development state:** **SCRIPT-WAVE-05 ACTIVE — PARALLEL WORKER PHASE**

Repository truth remains separated into `MERGED`, `IMPLEMENTED IN PR`, `MERGED_TO_INTEGRATION`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` and `SPECIFIED / NOT IMPLEMENTED`.

## Mandatory resume reading

Read current `main` before action:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
8. current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth.

## Wave 04 — MERGED

Coordinator PR #78 closed Wave 04.

- WaveBaseSHA: `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`;
- final integration head: `f0762d12814496a223abe740c57eb995ca472e97`;
- final CI #446: Web, backend/full tests, Runtime smoke and Chromium SUCCESS;
- main merge: `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`.

Merged product includes Project Management/Portability, Basic Trend Viewer and Administration Workspace integrated with the existing Wave 03 product composition. This merge is the logical WaveBaseSHA for Wave 05.

## SCRIPT-WAVE-05 — CENTRAL CONTRACT STABILIZED / WORKERS ACTIVE

**Logical WaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**IntegrationBranch:** `integration/interface-wave-05`  
**Integration PR:** #79 `Canonicalize Script Engineering for Wave 05` — Draft integration train  
**Frozen central ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**Contract CI:** #458 fully green: Web, backend Release/full tests including PostgreSQL, Runtime smoke and Chromium.

### Central Script contract — IMPLEMENTED IN PR #79 / NOT MERGED TO MAIN

The coordinator architecture-first slice now provides:

- canonical Engineering schema v10 with first-class `Scripts` and `ScriptVisualEventReferences`;
- backward compatibility for v9 packages with absent Script collections normalized safely;
- stable Script ID/path ownership through a Workspace-owned registry;
- Script mutations participate in normal Workspace dirty/changeVersion semantics;
- canonical JSON Export/Import and Preview/Apply use the existing Script Engineering validator rather than a competing model;
- stable dependency boundaries for Script, TAG, Client Memory, Server Memory and visual definitions;
- protected read endpoints for Scripts and visual Script references;
- canonical Script delete through the normal Engineering mutation CAS/security/Audit path;
- deletion refuses a Script still referenced by another Script dependency and removes the target Script's owned visual-event associations;
- `.escadapkg` preserves canonical Script content through `engineering.json`;
- PostgreSQL immutable revision tests preserve Script identity/path/source/scope/language/enabled state between revisions;
- existing Gateway schema tests were corrected to assert `CurrentSchemaVersion` instead of hard-coding v9.

No production Python interpreter/editor/sandbox, graphical editor, new protocol or direct driver/database/filesystem/network Script authority has been introduced.

## Active worker assignments

All worker execution branches were created **from the frozen ContractSHA**, because their tasks depend on the central architecture. The logical WaveBase remains the Wave 04 merge for lineage.

### DEV 1 — ACTIVE

Branch: `feature/script-wave-05-engineering-workspace`  
Task: Script Engineering Workspace foundation.

Build isolated frontend list/select/create/edit/delete UX for canonical Scripts. Plain multiline source editing only; no Monaco/Python execution. Create/update must use canonical Preview before Apply with Workspace CAS. Delete uses `/api/engineering/scripts/{id}`. Coordinator owns final `EngineeringApp.tsx` placement.

### DEV 2 — ACTIVE

Branch: `feature/script-wave-05-reference-validation`  
Task: Script Reference Runtime / validation adapter.

Build isolated reusable stable-reference/catalog/diagnostic helpers over canonical TAG, Client Memory, Server Memory and visual-definition identities. No direct driver/runtime authority and no central DI/handler edits; report integration hooks for coordinator.

### DEV 3 — ACTIVE

Branch: `test/script-wave-05-compatibility`  
Task: Script compatibility validation.

Independently attack v9→v10 compatibility, JSON/package/revision fidelity, stable identities, dependency cycles/missing references/invalid scope-event-language-source, delete safety and deterministic validation. Production source remains reserved.

Full AllowedScope, ReservedFiles, ValidationMatrix, CompletionCriteria and AfterCompletion rules are in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## Coordinator next sequence

1. user sends `SIGA` in DEV 1, DEV 2 and DEV 3 chats;
2. workers reread current `main`, verify their branches start at ContractSHA and open Draft PRs early;
3. coordinator performs Early Contract Reviews as PRs appear;
4. accepted slices are integrated into `integration/interface-wave-05` / PR #79, not directly into `main`;
5. coordinator owns central `EngineeringApp.tsx` placement/composition and any central handler wiring explicitly reported as integration-required;
6. run final full Wave 05 exact-head CI after all accepted slices are composed;
7. only then mark PR #79 Ready and merge to `main`;
8. synchronize docs and prepare architecture/implementation readiness for Wave 06.

## Wave 05 final gate

Exporting/importing and revisioning a project containing Scripts must preserve source, scope, events, dependencies/references and enabled state deterministically. The product must also expose a practical Script Engineering workspace without bypassing canonical Engineering, Preview/Apply/CAS or backend authority.

## Wave 06 — QUEUED ONLY

Python Editor + Client Visual sandbox remains queued. Production Python execution must not start merely because Script schema v10 exists in PR #79. Wave 06 begins only after Wave 05 is fully integrated/merged and its sandbox implementation decision/Definition of Ready is pinned.

## First owner validation gate — LOCKED

The first true owner-facing build remains **EliteSCADA v0.1 — Full Product Validation Preview** after functional Client Visual Python + graphical Engineering/Runtime. Modbus TCP is sufficient as the real industrial protocol. MQTT/OPC UA/BACnet/S7/Allen-Bradley production work remains post-v0.1 unless the product owner deliberately changes the gate.

## Permanent continuity rules

- workers never choose their own next task, modify `main`, merge their own PR or broaden scope;
- workers depending on an architecture-first contract start from its frozen green ContractSHA;
- final wave quality is proven on the integrated composition, not worker CI alone;
- documentation-only `main` movement does not invalidate logical WaveBaseSHA;
- known failing work is never merged;
- tests/security/CAS/runtime guards are never weakened merely for green CI;
- research does not equal production implementation;
- canonical Engineering remains authority.
