# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-29  
**Merged product state:** **WAVE 08 CLOSED / POST-MERGE GREEN**  
**Active development state:** **08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING / ARCHITECTURE-FIRST**  
**CI mode:** **NORMAL — Actions authorized with conservative usage**

## Mandatory resume reading

Before any action read current `main`:

- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/DEVELOPMENT-WAVES.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `docs/CI-USAGE-POLICY.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/COORDINATOR-HANDOFF.md`
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`
- `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`
- current assignment `MustReadSpecific`.

Then verify live GitHub branch/PR/head/CI. GitHub is operational truth.

## Wave 08 — CLOSED

Final integrated product head before merge:

`9ea0eace15aa925133005f40e16403a2c0f3deb1`

Final integration CI:

- CI **#531** / run `33236703599`: **SUCCESS**;
- Web build: SUCCESS;
- backend Release build + full PostgreSQL/Timescale tests: SUCCESS;
- Runtime smoke: SUCCESS;
- Chromium E2E: SUCCESS.

Administrative PR history:

- Draft PR **#90** was closed unmerged only because the available GitHub connector failed while removing Draft state;
- replacement non-Draft PR **#96** used the exact same branch/head and was merged normally;
- `main` merge commit: **`bfd17d035d905e9bcae263f68244cfb2b6453aa2`**.

Post-merge health:

- CI **#533** / run `33236999366`: **SUCCESS** on exact `main` merge `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- Web build, backend/full tests, Runtime smoke and Chromium all green.

Wave 08 delivered, together:

- canonical graphical Screen editor foundation;
- Canvas interaction, Property Inspector, Object Palette and canonical binding authoring;
- first-class project image assets with stable `assetRef` and revision/PostgreSQL/package fidelity;
- `core.text` general text plus explicit typed scalar dynamic-display binding;
- shared Project Reference Tree for canonical source/reference selection;
- canonical closed free `core.polygon` authoring, vertex editing and persisted structural geometry;
- Engineering Development Monitor with search, exact quick-add, heterogeneous read-only rows, exact typed values, quality/state/timestamp and shared batching/subscription behavior;
- 100-row monitor architecture acceptance;
- canonical Preview/Apply/CAS and save/reopen/export/import fidelity;
- transient editor/monitor state kept outside authored Engineering.

### Final Wave 08 defect fixed

CI #529 exposed a real Preview/Apply asymmetry for `core.polygon`: Preview correctly treated `points` as structural geometry while Apply incorrectly passed it through the scalar Visual Property codec, causing HTTP 500.

The final implementation preserves polygon `points` as structural geometry and normalizes only registered scalar properties. Unknown scalar properties remain fail-closed. A backend regression test covers the normalization path. CI #531 and post-merge #533 both prove the corrected flow.

## Current active work — 08-FOLLOW-A

Canonical contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Status:

**ACTIVE — COORDINATOR ARCHITECTURE/CONTRACT RECONCILIATION. DEV 1/2/3 REMAIN STOPPED UNTIL EXPLICITLY ASSIGNED.**

Required product outcome:

1. integer TAG bit selectors such as `Word_status.03` with stable canonical TAG identity + bit index;
2. Int16/Int32/Int64 fixed-width Boolean projection with LSB=0 and two's-complement semantics;
3. inherited source quality/timestamp, never bad quality coerced to false;
4. reusable canonical bit reference for monitor/binding/alarm/Python/future expression consumers;
5. driver-declared physical bit binding for Boolean TAGs;
6. Modbus Holding/Input Register bit `0..15` read semantics;
7. writable Holding Register bit mutation preserving unrelated bits;
8. concurrency-safe EliteSCADA bit writes and shared/coalesced reads where practical;
9. canonical JSON/Preview/Apply/revision/PostgreSQL/package fidelity;
10. authorization/Audit and existing whole-register/Coil/DiscreteInput regressions preserved.

Before implementation expands, inspect the actual current TAG reference DTOs, TagDefinition/binding schema, Engineering exchange/persistence paths, Modbus point/codec/poll/write paths, Runtime/current-value resolution and shared Project Reference Tree/Development Monitor source contracts. Do not invent a second reference model merely because `.NN` looks convenient.

## Worker state

- DEV 1: **STOPPED / WAIT_FOR_COORDINATOR**.
- DEV 2: **STOPPED / WAIT_FOR_COORDINATOR**.
- DEV 3: **STOPPED / WAIT_FOR_COORDINATOR**.

No worker is authorized for 08-FOLLOW-A until `docs/CHAT-WORK-ASSIGNMENTS.md` explicitly grants one bounded scope.

## Ordered work after 08-FOLLOW-A

1. **08-FOLLOW-B** — Typed Visual Expressions + Boolean Conditions + Analog Fill, consuming the canonical TAG-bit reference semantics;
2. **Wave 09** — remains NOT ACTIVE until both mandatory follow-ups are green;
3. Wave 09 later includes Screens/Popups/Dynamos/navigation plus the locked Historical Data Browser context in `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`.

## Next coordinator execution

1. verify current `main`, Follow-A integration branch and CI state;
2. inspect current canonical TAG/reference/driver seams before choosing DTOs;
3. freeze the minimum public bit-selector + physical-bit-binding implementation contract on the Follow-A integration branch;
4. decide whether any parallel-safe worker slices exist, and authorize them explicitly before workers act;
5. implement focused Core/Engineering/Runtime/Modbus tests before spending a full matrix;
6. run final integrated CI only at a meaningful Follow-A checkpoint;
7. merge only green, verify post-merge `main`, then activate 08-FOLLOW-B.
