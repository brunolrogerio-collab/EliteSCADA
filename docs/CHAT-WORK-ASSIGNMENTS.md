# CHAT WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-30
Stage: **DRIVER CONVERGENCE v1 — ACTIVE**
Integration owner: **Coordinator**
Product baseline: `main` after Wave 09 closure

## Wave 09 — CLOSED

Final product head before this docs-only transition:

`4d081f442b4f21cbb29e0d6cd1e76d251b8610aa`

Validation evidence:

- final pre-main CI #776 / run `33293198798`: Web, backend build/tests/runtime smoke and Chromium E2E all SUCCESS;
- post-merge main CI #782 / run `33293473589`: Web, backend build/tests/runtime smoke and Chromium E2E all SUCCESS;
- `main` and `integration/wave-09-historical-navigation-foundation` were aligned to the exact validated product SHA.

Delivered Wave 09 scope includes:

- Historical Query v1 with `historian.samples` and `alarm.events`;
- opaque keyset cursor, bounded typed filters/orders, deterministic relative-time admission and exact Int64 decimal-string wire semantics;
- Timescale historian query provider and append-only PostgreSQL alarm history provider;
- canonical Popup/Dynamo/navigation Engineering and Runtime Web composition;
- Reporting Engineering/execution core and mounted Report Designer/Preview;
- mounted Runtime Historical Data Browser at `/runtime/history` using the canonical Historical Query contract;
- central Historical Query configuration/composition with external cursor HMAC secret and fail-closed activation.

DEV 1, DEV 2 and DEV 3 Wave 09 assignments are complete. No new DEV 1/2/3 assignment is authorized by this file yet.

## Current active stage — Driver Convergence v1

Authority: `docs/DRIVER-CONVERGENCE-COORDINATION-V1.md` and ADR-007.

Wave priority no longer blocks the parked Driver convergence work. The Coordinator now owns reconciliation of the validated common Driver seams against current `main` before any protocol branch is authorized for mainline integration.

Coordinator priorities:

1. rebase-by-porting the validated common Driver convergence contracts onto current `main`, preserving all Wave 09 Engineering additions;
2. resolve the Engineering schema collision explicitly: Wave 09 owns schema v14, so rich communication TAG binding becomes the next schema revision rather than redefining v14;
3. preserve canonical TAG-bit identity as stable `TagId + TagValueSelector`; `.NN` remains authoring/display only;
4. retain ADR-007 byte/word transform semantics, with bit selection after physical transform and typed decode;
5. converge registry/planner/factory, protected-material resolution and runtime readiness without protocol-specific central switches;
6. review current Driver 4–10 exact heads and handoffs before protocol integration;
7. keep protocol branches isolated from `main` until Coordinator acceptance and exact integration-head CI.

## Parallel Driver branches

Canonical worker branches remain:

- Driver 4 BACnet/IP: `driver4/bacnet`
- Driver 5 Allen-Bradley Logix EtherNet/IP/CIP: `driver5/allen-bradley-cip`
- Driver 6 IEC 60870-5-104: `driver6/iec-60870-5-104`
- Driver 7 DNP3: `driver7/dnp3`
- Driver 8 Siemens S7 ISO-on-TCP: `driver8/siemens-s7-iso`
- Driver 9 OPC UA: `driver9/opc-ua`
- Driver 10 MQTT Industrial: `driver10/mqtt`

Old aliases `driver1/siemens-s7-iso`, `driver2/opc-ua` and `driver3/mqtt` remain retired and are not worker authorities.

Workers may continue protocol-owned bounded milestones under the convergence document, but must not edit shared Coordinator-owned contracts, `main`, or a central convergence branch.

## Shared locks

- Engineering Import/Export, Preview/Apply/CAS, revisions and project-package fidelity remain mandatory for canonical Engineering changes.
- Protected credentials/private keys are never plaintext Engineering/package data.
- No arbitrary SQL, JavaScript `eval`/`Function`, Python evaluation or implicit coercion engines.
- Visual precedence remains `Animation > Script > Binding/Expression > Engineering Base > Default`.
- Driver registry dispatch is by stable Driver type and duplicate registrations fail closed.
- Runtime readiness is Data Source protocol readiness, not a requirement that every point be `Good`.
- CI policy is NORMAL: no reassurance CI on unchanged product trees; exact final integration/product heads require green evidence before merge/stage closure.

## Required worker handoff

Each worker handoff must report:

1. exact branch and head SHA;
2. concise delivered scope;
3. exact changed-file list;
4. tests executed and results;
5. known limitations/risks;
6. confirmation that no unassigned files were changed;
7. module/dependency/license/hardware evidence required by the convergence contract;
8. any shared contract decision requiring Coordinator reconciliation.
