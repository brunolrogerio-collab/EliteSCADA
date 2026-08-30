# EliteSCADA Roadmap

**Status date:** 2026-08-30  
**Active direction:** **WAVE 10 — Python visual events + animation + preview**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Current coordinator checkpoint: `docs/COORDINATOR-HANDOFF.md`.  
TAG bit contract: `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.  
Visual expression contract: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.  
Wave 09 shared query contract: `docs/WAVE-09-HISTORICAL-QUERY-CONTRACT.md`.  
Wave 09 Historical Data Browser context: `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`.  
Wave 09 Reporting contract: `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`.  
CI policy: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: canonical Engineering entities participate in versioned JSON, validation/Preview/Apply, Working/revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current validated foundation

Current observed `main` baseline before Wave 10 product merge:

`d7ef5db6a583fa949059f6b00cb2dfab3549e919`

Wave 09 is closed on `main` and is the product foundation consumed by Wave 10.

## Completed waves and follow-ups

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.
- **Wave 08 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `bfd17d035d905e9bcae263f68244cfb2b6453aa2`; final integration CI #531 and post-merge CI #533 green.
- **08-FOLLOW-A — COMPLETE / MERGED / POST-MERGE GREEN.** PR #105; post-merge CI #543 green.
- **08-FOLLOW-B — COMPLETE / MERGED / POST-MERGE GREEN.** Final product head `dededaca980fdb72b5d4955685ab1161aca441fd`; CI #657 and #658 green.
- **Wave 09 — COMPLETE / MERGED.** Screens, Popups, Dynamos, canonical navigation, Historical Data Browser and Reporting/Report Designer are merged on `main` and form the Wave 10 visual/runtime foundation.

## Ordered path to v0.1

```text
Wave 03      Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04      Project portability + basic Trends + Administration                        COMPLETE
Wave 05      Canonical Script Engineering                                                COMPLETE
Wave 06      Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07      Visual Runtime Object Model + typed visual Engineering                      COMPLETE
Wave 08      Graphical Editor + Image + Engineering Development Monitor                  COMPLETE
08-FOLLOW-A  TAG Bit Access + Driver Bit-Level Boolean Binding                           COMPLETE
08-FOLLOW-B  Typed Visual Expressions + Boolean Conditions + Analog Fill                 COMPLETE
Wave 09      Screens + Popups + Dynamos + navigation + Historical Data + Reporting       COMPLETE
Wave 10      Python visual events + animation + preview                                  ACTIVE / INTEGRATED
Wave 11      Complete HMI Runtime demo vertical slice                                    WAITING
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 10 — ACTIVE / INTEGRATED

Integration train:

`integration/wave-10-python-visual-events-animation-preview`

Frozen product base:

`bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Wave 10 scope includes:

- canonical visual event associations in Engineering, including object interaction, Timer interval and typed TAG value-change identity;
- Client Visual Python event dispatch through the accepted sandbox bridge;
- renderer-native deterministic visual tween/animation over public Runtime Visual properties;
- explicit precedence `Animation > Script > Binding/Expression > Engineering > Default`;
- mounted Python Preview/Test with bounded sample context, actionable traceback/failing-line diagnostics and protected-data redaction;
- mounted browser acceptance of `click -> event reference -> Python -> visualTween.request -> rendered intermediate animation -> deterministic stable final value`.

Worker tracks DEV 1/2/3 and coordinator runtime composition have converged into the integration train. Coordinator PR #170 merged as `ee5d3c3f622765f79b49f32fa92c22760d195ae2`.

The exact functional coordinator product head `8b7871bcd5a14ae17ffb070732f5a92c60462536` passed EliteSCADA CI #872: Backend SUCCESS, Web build SUCCESS and Chromium SUCCESS with 330/330 tests passed.

Wave 10 remains ACTIVE until the exact current integration head is validated against `main`, merged, and exact post-main CI is green.

The complete owner-testable HMI Runtime demo vertical slice is deliberately Wave 11. Wave 10 establishes the canonical runtime behavior and mounted acceptance without inventing a second product Runtime surface.

## Remaining v0.1 sequence

- **Wave 10:** integrated; exact integration/main closure gate pending.
- **Wave 11:** complete owner-testable HMI Runtime demo vertical slice.
- **Wave 12:** hardening.
- **Wave 13:** Windows x64 product package.
- **Wave 14:** product-owner validation.
- **Wave 15:** feedback/corrections; v0.1 requires P0=0, P1=0 and required validation green.

## Protocol boundary and parallel Drivers

Required v0.1 protocol remains Modbus TCP. Simulation, Client Memory, Server Memory and Gateway remain part of product validation.

Additional protocol Drivers may continue on isolated parallel branches, but Wave work has priority and Driver heads do not merge automatically into `main`. Driver integration is a later explicit Coordinator decision.

## Development quality

- use Development Waves with frozen logical bases and coordinator integration trains;
- exactly one ACTIVE assignment per Wave worker;
- workers edit only owned scopes and stop after delivery;
- never merge known-failing work;
- fix root causes instead of weakening tests/security/concurrency;
- preserve canonical Engineering/backend authority;
- use Actions to buy evidence, not ceremony;
- require final integrated CI and healthy post-merge `main` for every functional wave;
- keep assignment board/handoff synchronized because `siga` depends on them.