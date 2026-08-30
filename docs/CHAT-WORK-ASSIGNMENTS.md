# CHAT WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-30
Stage: **WAVE 10 — ACTIVE**
Integration owner: **Coordinator**
Wave 10 product `WaveBaseSHA`: `bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

## Priority

Wave 10 has absolute priority over parallel Driver work.

GitHub live state is authoritative. Re-read exact branch/PR/CI state before mutations because worker chats advance independently.

CI policy remains NORMAL: no reassurance CI on unchanged product trees; exact final integration/product heads require green evidence before merge/stage closure.

## Wave 09 — CLOSED

Wave 09 is formally complete. Its worker scopes must not be reopened or rebuilt.

Final Wave 09 integration/product head at closure:

`4d081f442b4f21cbb29e0d6cd1e76d251b8610aa`

Delivered scope includes Historical Query v1, alarm/historian providers, Popup/Dynamo/navigation, Historical Data Browser, Reporting core and mounted Report Designer/Preview, plus central Historical Query composition.

## Wave 10 — ACTIVE

Canonical coordination document:

`docs/WAVE-10-PYTHON-VISUAL-EVENTS-ANIMATION-PREVIEW.md`

Integration branch:

`integration/wave-10-python-visual-events-animation-preview`

Wave exit:

`click -> canonical event binding -> Python entry point -> script visual command -> animated public visual property -> deterministic stable final result`

Also require mounted Preview/Test with actionable safe traceback diagnostics.

### DEV 1 — Events editor

- Issue: #149
- Branch: `dev1/wave-10-event-editor`
- Base: `bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Owns:

- mounted Events editor;
- click/object/visual event associations;
- lifecycle associations;
- TAG value-change associations;
- Client Memory change associations using stable definition identity;
- timer associations;
- Engineering round-trip and validation.

Must not create a frontend-private second event model or central runtime dispatcher. If canonical schema is insufficient, report the smallest gap to Coordinator.

### DEV 2 — Runtime animation/tween

- Issue: #150
- Branch: `dev2/wave-10-runtime-animation-tween`
- Base: `bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Owns:

- deterministic animation/tween execution over public visual properties;
- bounded explicit duration/transition semantics;
- start/intermediate/completion behavior;
- replacement/cancellation behavior;
- stable final value and diagnostics;
- focused runtime/E2E coverage.

Must preserve exactly:

`Animation > Script > Binding/Expression > Engineering Base > Default`

Must not create renderer-private persisted truth or a second visual property/state model.

### DEV 3 — Python Preview/Test

- Issue: #151
- Branch: `dev3/wave-10-python-preview-test`
- Base: `bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

Owns:

- mounted Preview/Test in canonical Python authoring;
- execution through accepted sandbox/runtime path;
- structured deterministic output;
- success/error/timeout/cancellation states;
- actionable traceback and failing source line;
- protected-data redaction;
- focused unit/integration/E2E coverage.

Must not create a second Python evaluator/host or private persistence path.

### Coordinator — central Wave 10 work

- Issue: #152
- Integration branch: `integration/wave-10-python-visual-events-animation-preview`

Owns:

- genuine shared event-binding schema gaps;
- central DI/runtime bridge composition;
- shared event dispatcher wiring;
- cross-component animation arbitration changes when required;
- review/reconciliation and integration of DEV 1/2/3;
- final end-to-end acceptance;
- exact integration-head CI and `main` transition;
- exact post-main CI before Wave closure.

Workers do not independently redefine central contracts.

## Parallel Driver branches

Canonical worker branches remain:

- Driver 4 BACnet/IP: `driver4/bacnet`
- Driver 5 Allen-Bradley Logix EtherNet/IP/CIP: `driver5/allen-bradley-cip`
- Driver 6 IEC 60870-5-104: `driver6/iec-60870-5-104`
- Driver 7 DNP3: `driver7/dnp3`
- Driver 8 Siemens S7 ISO-on-TCP: `driver8/siemens-s7-iso`
- Driver 9 OPC UA: `driver9/opc-ua`
- Driver 10 MQTT Industrial: `driver10/mqtt`

Old aliases `driver1/siemens-s7-iso`, `driver2/opc-ua` and `driver3/mqtt` remain retired.

Shared convergence authority remains `docs/DRIVER-CONVERGENCE-COORDINATION-V1.md` and ADR-007.

Drivers may continue bounded protocol-owned work in parallel, but must not edit Coordinator-owned shared contracts, the Wave 10 integration branch or `main` without Coordinator acceptance.

At Wave 10 activation, OPC UA independent-software interoperability work was parked in PR #148 and did not block Wave 10.

## Shared locks

- Engineering Import/Export, Preview/Apply/CAS, revisions and project-package fidelity remain mandatory for canonical public Engineering changes.
- Protected credentials/private keys are never plaintext Engineering/package data.
- TAG-bit identity remains stable `TagId + selector`; `.NN` is authoring/display only.
- ADR-007 byte/word transform remains binding-level; bit selection occurs after physical transform and typed decode.
- No arbitrary SQL, JavaScript `eval`/`Function`, unrestricted Python evaluation or implicit coercion engines.
- Visual precedence remains `Animation > Script > Binding/Expression > Engineering Base > Default`.
- Client Memory references preserve stable definition identity.
- Driver registry dispatch uses stable Driver type and duplicate registrations fail closed.
- Runtime readiness means protocol/Data Source readiness, not every point being `Good`.

## Required worker handoff

Every worker handoff must report:

1. exact branch and head SHA;
2. delivered scope;
3. exact changed-file list;
4. tests/results and exact CI evidence;
5. known limitations/risks;
6. shared decisions requiring Coordinator action;
7. confirmation that no unassigned shared files/contracts were independently redefined.