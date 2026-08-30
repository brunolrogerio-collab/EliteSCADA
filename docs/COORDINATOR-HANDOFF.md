# COORDINATOR HANDOFF — EliteSCADA

Date: 2026-08-30
Status: **WAVE 10 ACTIVE / WAVE 09 CLOSED / DRIVER WORK PARALLEL-ISOLATED**

## Read this first

GitHub live state is the operational source of truth. Before every mutation or exact status assertion, re-read `main`, the relevant integration branch, worker branch/PR and current CI because multiple chats advance concurrently.

Wave work has absolute priority over parallel Driver work.

CI policy is **NORMAL**:

- do not run reassurance CI on unchanged product trees;
- the exact final integration/product head must have green evidence before merge/stage transition;
- after a Wave moves to `main`, require exact post-main green evidence before closing it.

## Current product baseline and Wave 10 activation

Wave 10 product `WaveBaseSHA`:

`bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

This was the healthy `main` product head used to freeze all Wave 10 branches. Normal CI was green on this product state before activation.

A later docs-only activation commit created `docs/WAVE-10-PYTHON-VISUAL-EVENTS-ANIMATION-PREVIEW.md` with `[skip ci]`; such coordination-only commits do not change the WaveBaseSHA.

Wave 10 integration branch:

`integration/wave-10-python-visual-events-animation-preview`

Worker branches/issues:

- DEV 1: `dev1/wave-10-event-editor` — #149
- DEV 2: `dev2/wave-10-runtime-animation-tween` — #150
- DEV 3: `dev3/wave-10-python-preview-test` — #151
- Coordinator: #152

All three worker branches were created from the same `WaveBaseSHA` above.

## Wave 10 goal

Roadmap slice: Python visual events and animation authoring.

Required exit path:

`click -> canonical event binding -> Python entry point -> script visual command -> animated public visual property -> deterministic stable final result`

Also require mounted Python Preview/Test with actionable traceback/failing-line diagnostics and protected-data redaction.

Canonical coordination document:

`docs/WAVE-10-PYTHON-VISUAL-EVENTS-ANIMATION-PREVIEW.md`

Python/runtime boundary:

`docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`

### DEV 1

Events editor and canonical associations for object/visual events, lifecycle, TAG value-change, Client Memory change and timers.

Do not create a frontend-private second event model. If the canonical schema is insufficient, surface the smallest schema gap to the Coordinator.

### DEV 2

Deterministic runtime animation/tween execution over public canonical visual properties, including bounded duration, completion, replacement/cancellation and stable final value.

Do not create renderer-private persisted truth or a second visual state/property model.

### DEV 3

Mounted Python Preview/Test through the accepted sandbox/runtime path with structured results, timeout/cancellation states, actionable traceback/failing line and secret/protected-context redaction.

Do not create a second evaluator/Python host or private persistence path.

### Coordinator-owned Wave 10 work

- resolve only real shared event-binding schema gaps;
- central DI/runtime bridge composition;
- shared event dispatcher wiring;
- cross-component animation arbitration changes when genuinely required;
- review/integrate worker heads into the Wave 10 integration branch;
- reconcile shared files;
- run/assess the final end-to-end acceptance and exact-head CI;
- transition to `main` only after exact integration-head green evidence.

Permanent visual precedence remains:

`Animation > Script > Binding/Expression > Engineering Base > Default`

## Wave 09 — formally CLOSED

Do not reopen or rebuild Wave 09 worker slices.

Final Wave 09 integration/product head at closure:

`4d081f442b4f21cbb29e0d6cd1e76d251b8610aa`

Wave 09 closing records document both exact integration-head CI and exact post-main CI green before closure.

Delivered scope includes:

- Historical Query v1 (`historian.samples`, `alarm.events`);
- typed/bounded filters/order, opaque cursor and exact Int64 decimal-string wire semantics;
- Timescale historian and append-only PostgreSQL alarm history providers;
- canonical Popup/Dynamo/navigation Engineering and Runtime Web;
- Historical Data Browser;
- Reporting Engineering/execution core and mounted Report Designer/Preview;
- central Historical Query configuration/composition.

## Parallel Drivers — policy and current handoff state

Canonical worker branches remain:

- Driver 4 BACnet/IP: `driver4/bacnet`
- Driver 5 Allen-Bradley Logix EtherNet/IP/CIP: `driver5/allen-bradley-cip`
- Driver 6 IEC 60870-5-104: `driver6/iec-60870-5-104`
- Driver 7 DNP3: `driver7/dnp3`
- Driver 8 Siemens S7 ISO-on-TCP: `driver8/siemens-s7-iso`
- Driver 9 OPC UA: `driver9/opc-ua`
- Driver 10 MQTT Industrial: `driver10/mqtt`

Old aliases `driver1/siemens-s7-iso`, `driver2/opc-ua` and `driver3/mqtt` are retired.

Shared convergence authority is `docs/DRIVER-CONVERGENCE-COORDINATION-V1.md` plus ADR-007.

Driver branches do **not** merge automatically into `main` or Wave 10. Shared registry/planner/factory, readiness, protected-material resolution, rich Communication TAG binding, module activation and shared command/timestamp policy remain Coordinator concerns.

At Wave 10 activation, OPC UA independent-software interoperability work was isolated in PR #148 on `coordination/driver-interop-opcua-v1`. Its normal product CI had passed on its then-current head, while the dedicated OPC UA Interop Lab gate still required correction/green evidence. It must not block Wave 10 unless a shared canonical contract changes. Re-read the PR and exact checks before acting because this state may advance in another chat.

## Permanent contracts that must survive Wave 10

- Engineering Import/Export, Preview/Apply/CAS, revisions and project-package fidelity remain mandatory for public canonical Engineering entities.
- Protected secrets/private keys are never plaintext Engineering/package data.
- Integer TAG-bit identity is stable `TagId + selector`; `.NN` is authoring/display syntax only.
- ADR-007 byte/word transform remains binding-level, symmetric/deterministic and precedes bit selection.
- No arbitrary JavaScript `eval`/`Function`, Python evaluation, unrestricted SQL or implicit truthiness/coercion engines.
- Historical alarm browsing never authorizes alarm commands.
- Client Memory references use stable definition identity; friendly path is UX only.
- Bad/unavailable values fail closed with diagnostics.

## New coordinator startup checklist

1. Read this file and `docs/WAVE-10-PYTHON-VISUAL-EVENTS-ANIMATION-PREVIEW.md`.
2. Re-read live `main`; do not assume the SHA recorded here is still the live docs head.
3. Re-read Wave 10 integration and DEV 1/2/3 branch heads.
4. Read issues #149, #150, #151 and #152 and any new comments/PRs.
5. Treat `bbfd730e404b0dee2c05e0ec0afb979b1b14ea35` as the frozen Wave 10 **product** WaveBaseSHA unless a deliberate rebase decision is recorded.
6. Inspect open Driver PRs/issues separately; do not let them steal Wave priority.
7. Integrate only narrow reviewed worker heads; reconcile shared files centrally.
8. Require exact final integration CI before `main`, then exact post-main CI before closing Wave 10.

## Required worker handoff

Every worker handoff reports:

1. exact branch and head SHA;
2. delivered scope;
3. exact changed files;
4. tests/results and exact CI evidence;
5. known limitations/risks;
6. shared decisions needing Coordinator action;
7. confirmation that no unassigned shared files/contracts were independently redefined.