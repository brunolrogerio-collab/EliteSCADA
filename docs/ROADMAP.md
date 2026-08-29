# EliteSCADA Roadmap

**Status date:** 2026-08-29  
**Active direction:** **08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Current coordinator checkpoint: `docs/COORDINATOR-HANDOFF.md`.  
Wave 08 execution contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.  
Wave 08 Development Monitor contract: `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`.  
TAG bit contract: `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.  
Visual expression follow-up: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.  
Wave 09 historical data context: `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`.  
CI policy: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: canonical Engineering entities participate in versioned JSON, validation/Preview/Apply, Working/revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

Official `main` includes all product work through **Wave 08**.

Wave 08 final integration head:

`9ea0eace15aa925133005f40e16403a2c0f3deb1`

Final integration CI:

- #531 / run `33236703599`: **SUCCESS**.

Merged through replacement non-Draft PR **#96** after Draft PR #90 was administratively superseded because the available connector could not remove Draft state.

Main merge:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Post-merge CI:

- #533 / run `33236999366`: **SUCCESS**.

## Completed waves

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.
- **Wave 08 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `bfd17d035d905e9bcae263f68244cfb2b6453aa2`; final integration CI #531 and post-merge CI #533 green.

## Ordered path to v0.1

```text
Wave 03      Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04      Project portability + basic Trends + Administration                        COMPLETE
Wave 05      Canonical Script Engineering                                                COMPLETE
Wave 06      Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07      Visual Runtime Object Model + typed visual Engineering                      COMPLETE
Wave 08      Graphical Editor + Image + Engineering Development Monitor                  COMPLETE
08-FOLLOW-A  TAG Bit Access + Driver Bit-Level Boolean Binding                           ACTIVE
08-FOLLOW-B  Typed Visual Expressions + Boolean Conditions + Analog Fill                 WAITING ON 08-FOLLOW-A
Wave 09      Screens + Popups + Dynamos + navigation + Historical Data Browser          WAITING
Wave 10      Python visual events + animation + preview                                  WAITING
Wave 11      Complete HMI Runtime demo vertical slice                                    WAITING
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 08 — CLOSED

Wave 08 delivered the first practical graphical Engineering foundation plus development monitoring:

- canonical Screen editor;
- Canvas interactions and transient UI-state boundary;
- schema-driven Property Inspector;
- registered Object Palette;
- canonical source binding foundation;
- project image assets and stable `assetRef`;
- `core.text` with explicit typed scalar dynamic display;
- shared Project Reference Tree;
- canonical closed free `core.polygon` with point authoring/editing and persisted structural geometry;
- Engineering Development Monitor with search and exact-reference quick-add;
- heterogeneous read-only monitored rows for available canonical source families;
- typed value, quality/state and timestamp presentation;
- shared batching/subscription architecture with 100-row acceptance;
- Preview/Apply/CAS and save/reopen/export/import fidelity.

The final defect found by CI #529 was a Preview/Apply mismatch for polygon `points`. The final implementation preserves `points` as structural geometry instead of sending them through the scalar Visual Property codec. CI #531 and #533 prove the corrected path.

## 08-FOLLOW-A — TAG Bit Access + Driver Bit-Level Boolean Binding

**ACTIVE — ARCHITECTURE-FIRST.**

Canonical contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

### Product objective

Make bit-oriented PLC/driver data a first-class canonical capability rather than visual-editor syntax or driver-private metadata.

Required semantics:

- friendly logical syntax such as `Word_status.03`, backed by stable TAG identity + bit index;
- Int16 bits `00..15`, Int32 `00..31`, Int64 `00..63`;
- bit 0 = LSB;
- signed values use fixed-width two's-complement representation;
- result is Boolean but inherits source quality/timestamp/context;
- bad/unavailable integer source never silently becomes Boolean false;
- bit references become reusable through existing canonical source/reference seams;
- Development Monitor and Project Reference Tree later consume the same reference contract, never a private `.NN` parser;
- optional authorized logical bit writes preserve all unrelated bits and coordinate concurrent EliteSCADA mutations;
- Boolean TAGs may bind directly to a physical driver word/register bit when driver capability declares support.

### Required Modbus behavior

- Holding/Input Register Boolean bit binding uses bit `0..15` of one 16-bit register;
- Holding Register may be writable when authorized/configured;
- Input Register remains read-only;
- Coil remains native Boolean and does not need a register bit selector;
- DiscreteInput remains read-only;
- bit write changes only the selected bit;
- prefer native Mask Write Register if the supported transport/device contract deliberately enables it;
- otherwise use coordinated read-modify-write against the same source/address;
- simultaneous EliteSCADA bit writes to one word must not lose each other;
- multiple logical bit TAGs on one physical register should share/coalesce physical reads where practical;
- Engineering must distinguish human register notation from zero-based wire offset instead of hiding another Modbus off-by-one trap in a string.

### Persistence and safety gate

Bit references and physical bit bindings must round-trip through canonical JSON, Preview/Apply/CAS, Working, immutable revisions, PostgreSQL and `.escadapkg`.

Authorization/Audit remain normal product boundaries. A bit selector is not a security shortcut.

### Current execution rule

Coordinator first inspects and reconciles the actual current:

- TAG definition/reference DTOs;
- Engineering exchange/validation/persistence contracts;
- Runtime/current-value reference resolution;
- Modbus point/codec/poll/write paths;
- Project Reference Tree and Development Monitor catalog seams;
- Python/reference consumers that must eventually accept the same stable bit identity.

DEV 1/2/3 remain stopped until explicit bounded assignments are published. No worker infers a Follow-A task merely because this stage is active.

## 08-FOLLOW-B — mandatory after Follow-A

Contract:

`docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`

This stage consumes the canonical bit-reference semantics from Follow-A and adds:

- typed side-effect-free visual expressions over canonical TAGs/Client Memory/bit selectors;
- boolean `and`/`or`/`not`, comparisons and parentheses;
- numeric arithmetic and bounded pure helpers;
- universal public `visible` behavior;
- direct Boolean, interval and expression-driven Boolean conditions;
- Analog Fill for supported closed shapes;
- canonical dependency identity and reactive quality-aware evaluation;
- no arbitrary JavaScript/Python evaluation.

Wave 09 remains blocked until Follow-A and Follow-B are green.

## Wave 09 — locked future expansion / NOT ACTIVE

Wave 09 keeps Screens + Popups + Dynamos + navigation and also includes the locked Historical Data Browser context in:

`docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`

Initial protected logical datasets:

- `historian.samples`;
- `alarm.events`.

Required direction includes relative/absolute time ranges, typed server-side filters, bounded sortable tabular results, exact values/quality, parameterized PostgreSQL/TimescaleDB queries and strict separation between current operational Alarm Center commands and read-only historical alarm browsing.

Do not activate Wave 09 or distribute its work until the mandatory 08-FOLLOW stages are closed.

## Remaining v0.1 sequence

- **08-FOLLOW-A:** TAG Bit Access + Driver Bit-Level Boolean Binding.
- **08-FOLLOW-B:** Typed Visual Expressions + Boolean Conditions + Analog Fill.
- **Wave 09:** Screens/Popups/Dynamos/navigation + Historical Data Browser.
- **Wave 10:** Python visual events, renderer-native animation/tween and Engineering visual Preview/Test.
- **Wave 11:** complete owner-testable HMI Runtime demo vertical slice.
- **Wave 12:** hardening.
- **Wave 13:** Windows x64 product package.
- **Wave 14:** product-owner validation.
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
