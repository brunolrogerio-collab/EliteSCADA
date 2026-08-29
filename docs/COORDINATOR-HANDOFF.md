# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. Read this with the mandatory current `main` documents, then verify live GitHub branch/PR/head/CI before acting.

**Handoff date:** 2026-08-29  
**Current stage:** `08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING`  
**Merged product:** **Wave 08 CLOSED / post-merge green**  
**Current status:** **FOLLOW-A ACTIVE — ARCHITECTURE-FIRST / WORKERS STOPPED**  
**CI policy:** `NORMAL`; Actions authorized with conservative usage

## Wave 08 final closure checkpoint

Final integration head:

`9ea0eace15aa925133005f40e16403a2c0f3deb1`

Final integration CI:

- #531 / run `33236703599`: **SUCCESS**.

Administrative PR history:

- Draft PR #90 was closed unmerged because the available connector errored while removing GitHub Draft state;
- non-Draft replacement PR #96 used the exact same integration branch/head and merged successfully.

Main merge:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Post-merge CI:

- #533 / run `33236999366`: **SUCCESS** on exact merge commit;
- Web build, backend build/full PostgreSQL/Timescale tests, Runtime smoke and Chromium E2E all green.

### What Wave 08 delivered

- canonical Screen graphical Engineering workspace;
- Canvas selection/multiselect/move/resize/rotate/zoom/pan/grid/snap/duplicate/delete/z-order;
- schema-driven Property Inspector;
- registered Object Palette and canonical binding authoring;
- first-class project raster assets with stable `assetRef` and revision/PostgreSQL/package fidelity;
- `core.text` as general text box plus explicit typed scalar dynamic-display binding;
- shared Project Reference Tree for canonical source/reference browsing and selection;
- canonical closed free `core.polygon` with point authoring, vertex editing and structural geometry persistence;
- Engineering Development Monitor with search + exact quick-add, heterogeneous read-only rows, exact typed values, quality/state/timestamp and shared batching/subscription behavior;
- 100-row monitor architecture acceptance;
- Preview/Apply/CAS + save/reopen/export/import fidelity;
- transient editor/monitor state kept outside canonical Engineering.

### Last defect fixed before closure

CI #529 found a real Preview/Apply asymmetry for polygon `points`. Preview correctly excluded structural geometry from scalar property validation, while Apply sent the same `points` through the scalar property migration codec and returned HTTP 500.

Final fix keeps `core.polygon.points` structural, normalizes only registered scalar properties and reattaches the cloned geometry. Unknown scalar properties remain fail-closed. A backend regression test directly exercises `VisualEngineeringPropertyMigration.NormalizeScreen`. CI #531 and #533 both passed after the fix.

## Current Follow-A checkpoint

Canonical contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

No Follow-A PR exists yet. No worker is authorized yet.

### Locked product semantics

Logical integer TAG bit selector:

`<canonical/friendly TAG reference>.<bit index>`

Examples:

- `Word_comando.00`;
- `Word_comando.07`;
- `Word_status.15`;
- `Status_geral.31`.

Friendly text is authoring/display syntax. Canonical saved authority must retain stable TAG identity plus an explicit bit selector/index. TAG rename must not silently retarget the reference.

Supported initial logical widths:

- Int16: `00..15`;
- Int32: `00..31`;
- Int64: `00..63`.

Bit 0 is LSB. Signed integers use fixed-width two's-complement bit patterns. Float/Double/String/DateTime/Enum do not gain accidental raw-bit semantics.

Bit read returns Boolean while inheriting source quality/timestamp/context. Unavailable/bad source must not become false.

Bit reference is a view over authoritative TAG truth, not an automatic second historian series.

### Direct driver bit binding

A Boolean TAG may bind to a bit inside a physical word/register when the driver declares bit-selection capability.

For Modbus:

- HoldingRegister/InputRegister use bit `0..15` of one 16-bit register;
- InputRegister remains read-only;
- HoldingRegister may be writable if Engineering/security/driver capability allow it;
- Coil remains native Boolean;
- DiscreteInput remains read-only;
- physical address/base convention and bit index must be structured/versioned, not only a free-form `400001.7` string.

Holding Register bit write must preserve every unrelated bit.

Preferred strategies:

1. deliberate native Mask Write Register support when available;
2. otherwise coordinated read-modify-write under a lock keyed to the same authoritative source/register.

Two simultaneous EliteSCADA bit writes to the same word must not lose each other. External PLC/device writers may still race; diagnostics must not promise stronger atomicity than the protocol/device provides.

Multiple bit TAGs sharing one physical register should share/coalesce reads where practical.

## Architecture inspection required before coding

Do not start by inventing DTO names. First inspect the merged implementation and reuse its existing identity/persistence seams.

Priority inspection list:

1. Core TAG identity/data-type/current-value contracts;
2. Engineering TAG DTOs, validation, exchange, revision and persistence paths;
3. any existing generic TAG/source reference DTO used by alarms, visuals or scripting;
4. Runtime TAG read/write/current-value resolution;
5. Modbus `ModbusPoint`, address/value-type model, register codec and poll-block construction;
6. Modbus transport/write path, especially current HoldingRegister writes;
7. concurrency/locking boundaries around process writes;
8. Project Reference Tree descriptor/reference model merged in Wave 08;
9. Engineering Development Monitor provider/catalog model;
10. Client Visual Python TAG access surfaces that will later need the same canonical bit identity.

### Design traps to avoid

- do not create a second TAG namespace for bits;
- do not persist only `.NN` strings as identity;
- do not encode register-bit Boolean true/false as whole register `1`/`0`;
- do not add a visual-only bit parser;
- do not put protocol-private library types into Core;
- do not use stale cached word values for unsafe RMW writes without source/address coordination;
- do not create one physical read per logical bit TAG when one register read can feed several projections;
- do not collapse bad quality into Boolean false;
- do not activate expression syntax before this contract stabilizes.

## Current worker state

- DEV 1: STOPPED / WAIT_FOR_COORDINATOR.
- DEV 2: STOPPED / WAIT_FOR_COORDINATOR.
- DEV 3: STOPPED / WAIT_FOR_COORDINATOR.

Before assigning any worker, update `docs/CHAT-WORK-ASSIGNMENTS.md` with exact branch, AllowedScope, ForbiddenScope, dependencies, completion criteria and AfterCompletion behavior.

## CI discipline for Follow-A

- prefer focused Core/Engineering/driver tests while DTO/runtime shape is moving;
- use static inspection before Actions where practical;
- do not rerun unchanged failing heads;
- diagnose before another expensive run;
- reserve a full matrix for a coherent integrated checkpoint;
- final Follow-A Definition of Done still requires exact-head full CI and healthy post-merge `main`.

## Follow-up order

1. **08-FOLLOW-A** — ACTIVE now.
2. **08-FOLLOW-B** — Typed Visual Expressions + Boolean Conditions + Analog Fill; waits on Follow-A canonical bit semantics.
3. **Wave 09** — remains NOT ACTIVE until both mandatory Follow-A/B gates are green.

Wave 09 historical-data product context remains locked in `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md` and must not leak backward into Follow-A.

## Resume procedure

On coordinator `siga`:

1. reread current-main mandatory docs;
2. verify `main`, Follow-A branch, open PRs and live CI;
3. inspect current TAG/reference/Modbus/runtime/catalog code before changing contracts;
4. write the minimum coherent Follow-A implementation plan against actual types;
5. update worker assignments only if genuinely parallel-safe slices exist;
6. implement/test focused seams on `integration/tag-bit-access-wave-08-follow-a`;
7. keep 08-FOLLOW-B and Wave 09 blocked;
8. run full CI only at a meaningful integrated checkpoint;
9. merge only green, verify post-merge main, synchronize docs, then activate Follow-B.
