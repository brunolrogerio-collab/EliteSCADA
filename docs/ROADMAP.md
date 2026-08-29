# EliteSCADA Roadmap

**Status date:** 2026-08-29  
**Active direction:** **WAVE 08 — GRAPHICAL EDITOR FOUNDATION + ENGINEERING DEVELOPMENT MONITOR / GRAPHICAL GATE GREEN / MONITOR IMPLEMENTATION PENDING**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Current coordinator checkpoint: `docs/COORDINATOR-HANDOFF.md`.  
Wave 08 execution contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.  
Wave 08 Development Monitor contract: `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`.  
Wave 08 asset storage contract: `docs/VISUAL-ASSET-STORAGE-WAVE-08.md`.  
TAG bit access + bit-level driver binding follow-up: `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.  
Visual Expressions + Boolean Conditions + Analog Fill follow-up: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.  
CI policy: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: canonical Engineering domains join versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

Official `main` product state includes the complete Wave 07 foundation and all earlier SCADA layers. Wave 08 remains unmerged.

## Completed waves

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.

## Ordered path to v0.1

```text
Wave 03      Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04      Project portability + basic Trends + Administration                        COMPLETE
Wave 05      Canonical Script Engineering                                                COMPLETE
Wave 06      Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07      Visual Runtime Object Model + typed visual Engineering                      COMPLETE
Wave 08      Graphical Editor + Image + Engineering Development Monitor                  ACTIVE
08-FOLLOW-A  TAG Bit Access + Driver Bit-Level Boolean Binding                           WAITING FOR WAVE 08
08-FOLLOW-B  Typed Visual Expressions + Boolean Conditions + Analog Fill                 WAITING ON 08-FOLLOW-A
Wave 09      Screens + Popups + Dynamos + asset dependencies/navigation                 WAITING
Wave 10      Python visual events + animation + preview                                  WAITING
Wave 11      Complete HMI Runtime demo vertical slice                                    WAITING
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 08 — Graphical Editor + Engineering Development Monitor

**ACTIVE / NOT MERGED.**

Integration: `integration/graphical-editor-wave-08`  
Draft integration PR: **#90**

### Gate A — Graphical Editor/Image

**GREEN.** Exact graphical product head: `a7176a44df3a0af5bc1a271b25101d333da7a161`.  
CI #525 / run `33230239968`: **SUCCESS**.

The integrated graphical path now covers:

- Engineering Schema v13 first-class Visual Assets;
- stable asset identity + package/revision persistence;
- Canvas zoom/pan/grid/snap, selection/multiselect and geometry operations;
- schema-driven Property Inspector;
- registered Object Palette;
- canonical TAG binding authoring with early type compatibility checks;
- project image import/selection by stable `assetRef`;
- canonical Preview/Apply/CAS;
- save/reopen/export/import acceptance;
- transient Canvas UI state excluded from canonical Engineering.

DEV 1/2/3 original graphical worker deliveries are integrated. Their worker PRs #91/#92/#93 are closed without direct merge to main. Workers are stopped until explicitly reassigned.

### Gate B — Engineering Development Monitor

**OWNER-LOCKED / SPECIFIED / NOT IMPLEMENTED.**

Contract: `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`.

Purpose: allow development/commissioning engineers to watch live variables and diagnostics during application development without building temporary HMI objects or code.

Required workflow:

`open Monitor -> search OR type exact canonical reference -> add row -> observe value/type/quality-state/timestamp -> source changes -> row updates -> remove/clear`

Required initial source families:

- TAGs;
- Client Memory;
- Server Memory;
- authoritative System/Runtime variables and diagnostics;
- Data Source / driver diagnostics.

Required table facts:

- canonical name/reference/path;
- source category;
- current value;
- canonical data type;
- quality or authoritative diagnostic state;
- timestamp / last update when defined.

Architecture rules:

- unified provider/catalog seam, not a separate variable model per domain;
- search/browse plus exact-reference quick-add;
- explicit not-found/ambiguous behavior;
- heterogeneous source rows together;
- strict read-only boundary;
- preserve exact typed values including Int64;
- unavailable/bad/stale/disconnected state is explicit and never coerced to normal values;
- reuse realtime/subscription infrastructure where possible;
- bounded/coalesced polling only where needed;
- never one independent backend poll loop per row;
- acceptance proves at least 100 simultaneous monitored entries through shared batching/subscription;
- current monitored values/qualities/timestamps are Runtime/diagnostic state and never canonical Engineering/project package state.

The monitor is explicitly not a force/write table, command console, historian or alarm-control surface.

### Wave 08 final gate

Wave 08 closes only when both Gate A and Gate B are green on the final integrated product and one final full CI is green.

PR #90 stays Draft until then.

## Mandatory TAG-bit follow-up before visual expressions

After complete Wave 08 closure, execute `08-FOLLOW-A` under `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.

When first-class TAG bit selectors are implemented, they must participate through the same Development Monitor provider/catalog seam instead of monitor-private `.NN` parsing.

## Mandatory visual-expression follow-up before Wave 09

After 08-FOLLOW-A is green, execute `08-FOLLOW-B` under `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.

Wave 09 remains blocked until required preceding work is green.

## Remaining v0.1 sequence

- **Wave 08:** finish Engineering Development Monitor and final combined CI/merge.
- **08-FOLLOW-A:** TAG Bit Access + Driver Bit-Level Boolean Binding.
- **08-FOLLOW-B:** Typed Visual Expressions + Boolean Conditions + Analog Fill.
- **Wave 09:** multiple Screens, Popups, reusable Dynamos, navigation and deterministic asset dependencies.
- **Wave 10:** Python visual events, renderer-native animation/tween and Engineering visual Preview/Test.
- **Wave 11:** build `Estação Elevatória EliteSCADA Demo` only through normal product APIs/tools.
- **Wave 12:** hardening.
- **Wave 13:** Windows x64 product package.
- **Wave 14:** owner validation.
- **Wave 15:** feedback/corrections; v0.1 requires P0=0, P1=0 and required validation green.

## v0.1 protocol boundary

Required real industrial protocol: **Modbus TCP**. Simulation, Client Memory, Server Memory and Gateway remain part of product validation.

Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module framework remain post-v0.1 owner validation.

## Development quality

- use Development Waves with frozen logical bases and coordinator integration trains;
- exactly one ACTIVE assignment per worker;
- workers edit only owned scopes and stop after delivery;
- never merge known-failing work;
- fix root causes instead of weakening tests/security/concurrency;
- preserve canonical Engineering/backend authority;
- use Actions to buy evidence, not ceremony;
- require final integrated CI and healthy post-merge `main` for every functional wave;
- keep assignment board/handoff synchronized because `siga` depends on them.
