# LAST CHANGE — EliteSCADA

> Operational handoff. A new coordinator must be able to resume from GitHub without depending on chat history.

**Handoff date:** 2026-08-27  
**Development state:** **WAVE 00 CLOSED / v0.1 ROADMAP + DEVELOPMENT WAVE MODEL LOCKED / WAVE 03 PREPARATION ACTIVE**

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
8. current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth.

## Wave 00 / second interface wave — MERGED

Worker slices #65, #66 and #67 are merged.

Coordinator PR #69 `Integrate second-wave Runtime operations surfaces` is **MERGED**:

- final head: `c493709a221614a093717b6e6a16bf8821226e91`;
- exact-head EliteSCADA CI #403: **SUCCESS**;
- Web build PASS;
- backend Release build + tests PASS;
- runtime smoke PASS;
- Chromium E2E PASS;
- merge SHA: `ee65ab51a39cd74ef6f14395d27b0ee16b8c6970`.

PR #69 mounts the Runtime Alarm Center in the actual Runtime composition, removes the legacy duplicate alarm polling/list/ACK path and removes the legacy client-supplied `demo-operator` ACK identity. Protected backend identity/authorization remains authoritative.

During integration, Chromium correctly exposed and caused repair of duplicate Runtime operational composition and an invalid WebSocket fallback (`ws//` instead of `ws://`). Final CI #403 proves the repaired head.

## New permanent coordination model — MERGED DOCUMENTATION

`docs/DEVELOPMENT-WAVES.md` is now the permanent Development Wave / Integration Train protocol.

Core rule: workers prove isolated slices against a frozen logical `WaveBaseSHA`; the coordinator composes accepted slices in an integration branch and performs final reconciliation/CI once on the integrated product. Unrelated `main` movement should be avoided during an active wave, except critical/security/CI-blocking/indispensable dependency work.

Worker next-task queue states are `QUEUED -> READY -> ACTIVE`; queue presence is not authorization to start.

Event-driven reviews replace percentage checkpoints:

- Early Contract Review;
- Integration Review;
- Delivery Review.

Preferred specialization is documented but is not rigid ownership.

`docs/PARALLEL-WORK.md` has been reconciled to this model.

## First owner validation product gate — LOCKED

The first true product-owner validation build is now:

# EliteSCADA v0.1 — Full Product Validation Preview

Authoritative scope/order: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

The older pre-graphical owner-facing Interface Validation Preview is superseded. Internal package/build spikes may happen earlier, but the first build treated as the real EliteSCADA validation product must include the complete vertical path:

`Engineering -> TAG/Alarm/History -> Screen/Popup/Dynamo -> bindings -> Client Visual Python -> Save/Revision -> Publish -> Activate -> graphical Runtime -> restart/Active recovery`.

Modbus TCP is sufficient as the real industrial protocol for v0.1. Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module expansion stay after v0.1 owner validation unless the product owner deliberately changes the gate.

`docs/INTERFACE-VALIDATION-MILESTONE.md` now records this supersession.

## Ordered v0.1 waves

- Wave 03: Lifecycle + Runtime TAG Inspector + acceptance foundation;
- Wave 04: Project portability + basic Trends + Administration;
- Wave 05: canonical Script Engineering (architecture-first);
- Wave 06: Python Editor + Client Visual sandbox;
- Wave 07: Visual Runtime Object Model (architecture-first);
- Wave 08: Graphical Editor Foundation;
- Wave 09: Screens + Popups + Dynamos;
- Wave 10: Python visual events + animation + preview;
- Wave 11: complete Estação Elevatória HMI demo vertical slice;
- Wave 12: hardening;
- Wave 13: Windows x64 owner package;
- Wave 14: owner validation;
- Wave 15: feedback/corrections;
- FINAL: EliteSCADA v0.1 Full Product Validation Preview.

Detailed gates are in the v0.1 plan; do not reconstruct them from conversation memory.

## Current worker state

All three workers are deliberately `WAIT_FOR_COORDINATOR`.

Wave 03 tasks are only **QUEUED**, not ACTIVE:

- DEV 1: Engineering Lifecycle Workspace;
- DEV 2: Runtime TAG Inspector + Recent History;
- DEV 3: Interface Validation Readiness Harness.

Do not instruct workers to start until the coordinator records the exact Wave 03 `BaseSHA`, integration target and promotes assignments after Definition of Ready.

## Immediate technical blocker before Wave 03 base freeze

A CI execution during PR #69 integration exposed a real PostgreSQL initialization race. Concurrent tests/stores can both execute `CREATE SCHEMA IF NOT EXISTS elitescada`; PostgreSQL may still raise SQLSTATE `23505` on system catalog uniqueness under concurrent schema creation.

Existing code uses `pg_advisory_xact_lock`, but the observed design executes initialization statements under autocommit, so a transaction-scoped advisory lock may be released before subsequent DDL is protected.

A retry of the unchanged head passed, confirming concurrency sensitivity rather than a deterministic UI regression. This defect must be fixed or deliberately isolated with evidence before freezing the next wave base. Do not merely weaken/retry CI and forget it.

## Coordinator resume point

On next coordinator `siga`:

1. reread mandatory docs and real GitHub state;
2. inspect PostgreSQL store initialization/advisory-lock code and affected tests;
3. implement a dedicated root-cause concurrency hardening change, preferably isolating it from Wave 03 product work;
4. add/strengthen concurrent initialization regression coverage;
5. require relevant exact-head CI green;
6. merge the maintenance change;
7. confirm healthy `main` and freeze exact `WaveBaseSHA`;
8. create `integration/interface-wave-03`;
9. promote DEV 1/2/3 queued Wave 03 assignments to ACTIVE with complete Wave/BaseSHA/ReservedFiles/IntegrationTarget/ValidationMatrix fields;
10. keep unrelated product/research work out of `main` while the wave runs.

## Permanent continuity rules

- No permanent product/coordination decision may exist only in chat history.
- Workers never choose their own next task or merge their own PR.
- Final wave quality is proven on the integrated composition, not inferred from three isolated worker PRs.
- Documentation-only coordination commits do not invalidate the logical WaveBaseSHA.
- Research merge is not production implementation.
- Python/visual dependency order remains canonical Script -> Python editor/sandbox -> visual Runtime model -> graphical editor.
- The editor/renderer never becomes private project truth; canonical Engineering remains authoritative.
- Known failing work is never merged.