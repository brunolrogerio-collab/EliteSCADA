# EliteSCADA Roadmap

**Status date:** 2026-08-29  
**Active direction:** **WAVE 09 — Screens/Popups/Dynamos + Historical Data + Reporting**

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

Current validated product baseline on `main`:

`dededaca980fdb72b5d4955685ab1161aca441fd`

Wave 08 FOLLOW-B final-head CI #657: **SUCCESS**.  
Post-merge/push CI #658 on exact `main` head: **SUCCESS**.

## Completed waves and follow-ups

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.
- **Wave 08 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `bfd17d035d905e9bcae263f68244cfb2b6453aa2`; final integration CI #531 and post-merge CI #533 green.
- **08-FOLLOW-A — COMPLETE / MERGED / POST-MERGE GREEN.** PR #105; post-merge CI #543 green.
- **08-FOLLOW-B — COMPLETE / MERGED / POST-MERGE GREEN.** Final product head `dededaca980fdb72b5d4955685ab1161aca441fd`; CI #657 and #658 green.

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
Wave 09      Screens + Popups + Dynamos + navigation + Historical Data + Reporting       ACTIVE
Wave 10      Python visual events + animation + preview                                  WAITING
Wave 11      Complete HMI Runtime demo vertical slice                                    WAITING
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 08 follow-up closure

FOLLOW-A established first-class canonical integer TAG bit access and physical bit-level binding semantics. Stable identity is `TagId + selector`; `.NN` is authoring/display syntax only.

FOLLOW-B established typed side-effect-free visual expressions, Boolean Conditions, universal dynamic `visible`, quality-aware evaluation and Analog Fill while preserving canonical Engineering and existing visual precedence.

These contracts are downstream foundations and must not be reimplemented privately by Wave 09 work.

## Wave 09 — ACTIVE

Wave 09 includes:

- Screens + Popups + Dynamos + navigation;
- Historical Data Browser;
- Reporting / Report Designer.

### 09-A — shared historical/navigation foundation

Initial integration train:

`integration/wave-09-historical-navigation-foundation`

The first substage establishes a single protected/versioned Historical Query model before Browser, Trends and Reporting can diverge.

Initial logical datasets:

- `historian.samples`;
- `alarm.events`.

Required shared semantics:

- relative and absolute time ranges;
- one server-resolved anchor per relative query;
- typed allowlisted filters and ordering;
- bounded opaque-cursor paging;
- exact typed values, quality and timestamps;
- exact Int64 transport without JavaScript precision loss;
- authorization, cancellation and parameterized database access;
- no arbitrary SQL or unrestricted scripting;
- historical alarm browsing remains read-only and separate from current Alarm Center commands.

In parallel, Popup/Dynamo/navigation work extends canonical Screen/visual Engineering without introducing a second renderer/property model.

Historical Data Browser consumes the shared query contract rather than defining its own provider/filter/time DTO.

### Reporting sequencing inside Wave 09

Reporting/Report Designer remains mandatory Wave 09 scope. Implementation starts after the shared Historical Query contract is accepted in the integration train so reports reuse exactly the same provider/query/time/filter/result semantics.

Reporting direction includes first-class versioned Report Engineering, graphical designer, parameters, groups/aggregates, page setup/preview/printing, and mandatory PDF/XLSX output while preserving JSON/Preview/Apply/revision/PostgreSQL/`.escadapkg` fidelity.

## Remaining v0.1 sequence

- **Wave 09:** active as above.
- **Wave 10:** Python visual events, renderer-native animation/tween and Engineering visual Preview/Test.
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