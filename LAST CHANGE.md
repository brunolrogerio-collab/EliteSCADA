# LAST CHANGE — EliteSCADA

> Operational handoff. A new coordinator must be able to resume from GitHub without depending on chat history.

**Handoff date:** 2026-08-27  
**Development state:** **INTERFACE-WAVE-03 ACTIVE**

Repository truth remains separated into `MERGED`, `IMPLEMENTED IN PR`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` and `SPECIFIED / NOT IMPLEMENTED`.

## Mandatory resume reading

Before any EliteSCADA action read current `main`:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
8. `docs/VISUAL-ASSETS-AND-IMAGES.md`;
9. current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth.

## Wave 00 / second interface wave — MERGED

Worker PRs #65, #66 and #67 are merged. Coordinator PR #69 `Integrate second-wave Runtime operations surfaces` is merged:

- final head `c493709a221614a093717b6e6a16bf8821226e91`;
- exact-head CI #403 SUCCESS: Web, backend/tests, runtime smoke and Chromium PASS;
- merge `ee65ab51a39cd74ef6f14395d27b0ee16b8c6970`.

PR #69 mounts Runtime Alarm Center in the actual Runtime composition, removes the duplicate legacy alarm polling/list/ACK path and removes the legacy client-supplied ACK identity path. Backend identity/authorization remains authoritative.

## PostgreSQL schema initialization race — FIXED / MERGED

The concurrency blocker discovered during PR #69 integration is resolved through PR #70 `Harden PostgreSQL schema initialization concurrency`.

Root cause confirmed:

- Engineering, Audit and Local Identity initialization used `pg_advisory_xact_lock(4993446713136202561)` but executed the initialization batch without an explicit transaction protecting the full DDL sequence;
- Server Memory used an explicit transaction but a different lock key even though it also creates the shared `elitescada` schema.

PR #70:

- Engineering/Audit/LocalIdentity now open explicit connection + transaction, execute the lock and entire DDL batch inside it, then commit;
- Server Memory now uses the same shared schema advisory-lock key;
- `PostgreSqlConcurrentInitializationTests` runs all four stores repeatedly/concurrently to regress the catalog-collision failure;
- no schema redesign, frontend change or CI retry weakening.

Evidence:

- final PR head `25b01e2d792d98d4013fa43c97ad27f06e68a8a2`;
- exact-head CI #405 SUCCESS;
- Web PASS;
- backend Release build + full tests PASS;
- runtime smoke PASS;
- Chromium PASS;
- merge SHA **`0aae8317aff5b0640eb713c1ce404224ccbcbbc2`**.

This merge SHA is the logical product-code base for Interface Wave 03.

## INTERFACE-WAVE-03 — ACTIVE

**WaveBaseSHA:** `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`  
**IntegrationBranch:** `integration/interface-wave-03`

All four branches were created from the exact same WaveBaseSHA:

- coordinator: `integration/interface-wave-03`;
- DEV 1: `feature/interface-wave-03-lifecycle-workspace`;
- DEV 2: `feature/interface-wave-03-runtime-tag-inspector`;
- DEV 3: `test/interface-wave-03-acceptance-harness`.

The WaveBaseSHA remains immutable for the wave. Later documentation/coordination commits on `main` do not invalidate it or require workers to reconcile. Keep unrelated product-code changes out of `main` until the wave closes.

### Definition of Ready evidence

DEV 1 does not require a new backend lifecycle contract. Existing protected persistence API already exposes:

- persistence status;
- lifecycle;
- revisions;
- Save;
- Checkout;
- Publish;
- Activate Published;
- live Runtime vs durable Active consistency.

`EngineeringPersistenceSecurityFilter` requires `EngineeringModify` and replaces caller-supplied SavedBy/PublishedBy/ActivatedBy with the authenticated principal for protected lifecycle operations. Frontend identity is therefore not authority.

DEV 2 does not require a new backend Runtime contract. Existing protected surfaces already expose:

- `/api/tags`;
- `/api/tags/current`;
- `/api/tags/by-path/{*path}`;
- `/api/history/{tagId}`;
- `/ws/tags` realtime.

The worker slice is explicitly read-only and must not call the existing TAG write endpoint.

DEV 3 uses the existing Playwright/Chromium product acceptance infrastructure and owns test-only cross-product readiness coverage rather than product fixes.

## Active worker assignments

The authoritative details are in `docs/CHAT-WORK-ASSIGNMENTS.md`.

### DEV 1 — ACTIVE

`Engineering Lifecycle Workspace`

Goal: make Working -> Save Revision -> Publish -> Activate -> Active understandable and operable, showing dirty/base/revisions/published/active/runtime consistency and protected critical actions. Central `EngineeringApp.tsx` integration remains coordinator-owned.

### DEV 2 — ACTIVE

`Runtime TAG Inspector + Recent History`

Goal: read-only search/filter/master-detail over TAG metadata/current values/quality/timestamps with realtime/refresh and recent historian data. Central Runtime mounting remains coordinator-owned. No process writes/setpoints.

### DEV 3 — ACTIVE

`Interface Validation Readiness Harness`

Goal: Chromium acceptance across login/session, Runtime/Operations/Alarm Center, Engineering Data Sources/TAGs/Alarms/Memory/Gateway/diagnostics/lifecycle, Audit, administration authorization, navigation and major `pt-BR`/`en`/`es` states. Product issues are classified `BLOCKER`, `MAJOR UX`, `MINOR UX`, `TEST GAP`; DEV 3 does not silently fix cross-domain defects.

Each worker opens a Draft PR early and finishes with exact-head green evidence plus PR body sections `IMPLEMENTED IN PR`, `INTEGRATION REQUIRED`, `SPECIFIED / NOT IMPLEMENTED`, then `WAIT_FOR_COORDINATOR`.

## Coordinator Wave 03 responsibility

When workers deliver:

1. inspect each real PR/head/diff/CI;
2. perform Early Contract / Integration / Delivery reviews based on actual state;
3. integrate accepted slices into `integration/interface-wave-03` without unnecessary worker reconciliation for unrelated main movement;
4. add coordinator-owned `EngineeringApp.tsx`, `main.tsx`, routing/shell/localization hooks;
5. remove/avoid duplicate old/new UI paths;
6. run final integrated Web + backend/full tests + runtime smoke + Chromium CI;
7. merge Wave 03 only if the integrated gate is green;
8. update roadmap/handoff and decide/open Wave 04.

## First owner validation product gate — LOCKED

The first true product-owner validation build remains:

# EliteSCADA v0.1 — Full Product Validation Preview

Authoritative scope/order: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

It must include the complete vertical path:

`Engineering -> TAG/Alarm/History -> Screen/Popup/Dynamo -> assets/bindings -> Client Visual Python -> Save/Revision -> Publish -> Activate -> graphical Runtime -> restart/Active recovery`.

Modbus TCP is sufficient as the real industrial protocol for v0.1. Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module expansion stay after v0.1 owner validation unless the product owner deliberately changes the gate.

## Visual image/assets requirement — LOCKED FOR v0.1

`docs/VISUAL-ASSETS-AND-IMAGES.md` is normative.

Required raster import includes JPG/JPEG, BMP and PNG; PNG alpha transparency must survive import, Engineering, package/revision/publish/activate and Runtime rendering. Imported images are stable project assets/resources, not loose absolute filesystem paths. Original workstation files may be moved/deleted after successful import without breaking the project.

Implementation order: Wave 07 asset contract -> Wave 08 asset import/Image object -> Wave 09 Screen/Popup/Dynamo dependencies -> Wave 11 demo -> Wave 12 hardening -> Wave 13 package.

## Permanent continuity rules

- No permanent product/coordination decision may exist only in chat history.
- Workers never choose their own next task or merge their own PR.
- Final wave quality is proven on integrated composition, not inferred from isolated worker PRs.
- Documentation-only coordination commits do not invalidate the logical WaveBaseSHA.
- Research merge is not production implementation.
- Python/visual dependency order remains canonical Script -> Python editor/sandbox -> visual Runtime property/asset model -> graphical editor.
- The editor/renderer never becomes private project truth; canonical Engineering remains authoritative.
- Imported visual assets are project-authoritative resources, not external developer file paths.
- Known failing work is never merged.