# EliteSCADA Roadmap

**Status date:** 2026-08-30  
**Next direction:** **WAVE 11 — Complete HMI Runtime demo vertical slice — READY / NOT STARTED**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Current coordinator checkpoint: `LAST CHANGE.md`.  
TAG bit contract: `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.  
Visual expression contract: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.  
CI policy: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: canonical Engineering entities participate in versioned JSON, validation/Preview/Apply, Working/revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current validated foundation

Wave 10 product merge on `main`:

`15daff2cc076f46f9433812babbd5cbb4b8d9554`

Validation evidence:

- exact integration head `adb0153dff36e172d0553463cc961a11bd7c7e1e` — CI #873 SUCCESS;
- exact post-main product head `15daff2cc076f46f9433812babbd5cbb4b8d9554` — CI #874 SUCCESS.

## Completed waves and follow-ups

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.
- **Wave 08 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `bfd17d035d905e9bcae263f68244cfb2b6453aa2`; final integration CI #531 and post-merge CI #533 green.
- **08-FOLLOW-A — COMPLETE / MERGED / POST-MERGE GREEN.** PR #105; post-merge CI #543 green.
- **08-FOLLOW-B — COMPLETE / MERGED / POST-MERGE GREEN.** Final product head `dededaca980fdb72b5d4955685ab1161aca441fd`; CI #657 and #658 green.
- **Wave 09 — COMPLETE / MERGED.** Screens, Popups, Dynamos, canonical navigation, Historical Data Browser and Reporting/Report Designer.
- **Wave 10 — COMPLETE / MERGED / POST-MAIN GREEN.** Final PR #172; integration CI #873 green; product merge `15daff2cc076f46f9433812babbd5cbb4b8d9554`; post-main CI #874 green.

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
Wave 10      Python visual events + animation + preview                                  COMPLETE
Wave 11      Complete HMI Runtime demo vertical slice                                    READY / NOT STARTED
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 10 — CLOSED

Wave 10 established one canonical path for visual Python behavior rather than a renderer-private side channel:

`click -> canonical event association -> Client Visual Python sandbox -> public Runtime Visual command -> Animation layer -> stable Script final value -> canonical renderer`

Delivered scope includes:

- persisted canonical visual event associations and Engineering Events editor;
- object interaction, Timer interval and typed TAG value-change identities;
- deterministic visual animation/tween with cancellation/replacement/repeat/easing behavior;
- Python `visualTween.request` through the accepted capability bridge;
- mounted Python Preview/Test with bounded sample context and actionable sanitized diagnostics;
- transient renderer composition preserving `Animation > Script > Binding/Expression > Engineering > Default`;
- exact browser acceptance on mounted Screen/Popup visual runtime composition.

## Wave 11 — NEXT / READY

Wave 11 owns the complete owner-testable HMI Runtime demo vertical slice. It should compose the already accepted Screen/Popup/Dynamo, realtime TAG, Client Memory, Python visual events, animation, alarms/trends/history/reporting and operational navigation into the real Runtime product surface rather than creating alternate models.

Wave 11 is **not started by this Wave 10 closure**. Its branch/worker assignments and frozen base should be established explicitly when execution begins.

## Protocol boundary and parallel Drivers

Required v0.1 protocol remains Modbus TCP. Simulation, Client Memory, Server Memory and Gateway remain part of product validation.

Additional protocol Drivers may continue on isolated parallel branches, but product Wave work has priority and Driver heads do not merge automatically into `main`.

## Development quality

- use Development Waves with frozen logical bases and coordinator integration trains;
- exactly one ACTIVE assignment per Wave worker;
- workers edit only owned scopes and stop after delivery;
- never merge known-failing work;
- fix root causes instead of weakening tests/security/concurrency;
- preserve canonical Engineering/backend authority;
- use Actions to buy evidence, not ceremony;
- require final integrated CI and healthy post-merge `main` for every functional wave;
- keep coordination checkpoints synchronized because `siga` depends on them.