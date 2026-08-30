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

Later docs-only activation/coordination commits use `[skip ci]`; they do not change the frozen Wave 10 product base.

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

## Parallel Drivers and Interoperability Lab — current snapshot

The authoritative coordination snapshot is now:

`docs/DRIVER-AND-INTEROP-LAB-STATUS.md`

Read it before coordinating Driver convergence or laboratory acceptance. It intentionally separates worker-code maturity, normal CI, independent software interoperability and representative hardware evidence.

Observed worker state at this handoff:

| Driver | Head | Handoff / CI | Coordination state |
| --- | --- | --- | --- |
| D4 BACnet/IP | `2ced848124350a5d83ec563a4fb22312ac224fe1` | Draft #109 / CI #787 green | reviewable, parked; external interoperability remains |
| D5 Allen-Bradley CIP | `18ff6dc989a65c1f8b006f83c08d8394a5510914` | Draft #111 / CI #785 green | reviewable, parked; hardware/conformance remains |
| D6 IEC-104 | `d597ef5ed1885b63dcd0b3568287bc1e34330bee` | Draft #146 / CI #798 green | formal handoff now complete; ready for convergence review |
| D7 DNP3 | `ac0dd6944f53d19447f3353addd404c02da7249c` | Draft #108 / CI #697 green | reviewable, parked; independent peer + commercial licensing remain |
| D8 Siemens S7 | `0c37b922b44f591ebd143470abf3ebaa6b4bffae` | Draft #135 / CI #789 green | strong parked milestone; Siemens external evidence remains |
| D9 OPC UA | `8ba5870d7dbe119a2999d8a73394289e2349f401` | no worker PR / no Actions run on canonical branch | least formalized; not review-ready |
| D10 MQTT | `fd2f3cbba3e8fc701e376cfcbd1685b28e3d98ef` | Draft #128 / CI #791 green | reviewable, parked; product-path live broker evidence remains |

Canonical worker branches remain:

- `driver4/bacnet`
- `driver5/allen-bradley-cip`
- `driver6/iec-60870-5-104`
- `driver7/dnp3`
- `driver8/siemens-s7-iso`
- `driver9/opc-ua`
- `driver10/mqtt`

Old aliases `driver1/siemens-s7-iso`, `driver2/opc-ua` and `driver3/mqtt` are retired.

Shared convergence authority is `docs/DRIVER-CONVERGENCE-COORDINATION-V1.md` plus ADR-007.

Driver branches do **not** merge automatically into `main` or Wave 10. Shared registry/planner/factory, readiness, protected-material resolution, rich Communication TAG binding, module activation and shared command/timestamp policy remain Coordinator concerns.

### Interoperability Lab current evidence

Mainline `interop-lab/` is test infrastructure separate from the product runtime.

The current base lab proves that the lab can build/start and that its MQTT round-trip smoke succeeds. The CIP overlay Compose model is validated, but this is not yet equivalent to a complete Driver 5 product-path acceptance scenario.

OPC UA independent-software work is isolated in PR #148 on `coordination/driver-interop-opcua-v1`, exact observed head:

`ffa810c2a4e6524fdb4d05c7c094a899e80af67b`

On that exact head:

- normal EliteSCADA CI #807: **GREEN**;
- dedicated Interop Lab Smoke #8: **RED**;
- failure step: `Build and start independent OPC UA peer`;
- the later `OPC UA open62541 interoperability smoke` step was skipped.

Therefore PR #148 does **not** yet provide accepted L2 OPC UA independent-software interoperability evidence and must not be merged on the strength of normal CI alone.

The failing lab run still proved the scenario JSON, Node-RED JSON, base/CIP/OPC-UA Compose models, base lab startup and MQTT round-trip smoke before reaching the open62541 peer build/start failure.

This lab work must not block Wave 10 unless it changes a shared canonical product contract.

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

1. Read this file, `docs/WAVE-10-PYTHON-VISUAL-EVENTS-ANIMATION-PREVIEW.md` and `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.
2. Re-read live `main`; do not assume any SHA recorded here is still live.
3. Re-read Wave 10 integration and DEV 1/2/3 branch heads.
4. Read issues #149, #150, #151 and #152 and any new comments/PRs.
5. Treat `bbfd730e404b0dee2c05e0ec0afb979b1b14ea35` as the frozen Wave 10 **product** WaveBaseSHA unless a deliberate rebase decision is recorded.
6. Inspect open Driver PRs/issues and lab checks separately; do not let them steal Wave priority.
7. Distinguish normal CI from independent-software/hardware interoperability evidence.
8. Integrate only narrow reviewed worker heads; reconcile shared files centrally.
9. Require exact final integration CI before `main`, then exact post-main CI before closing Wave 10.

## Required worker handoff

Every worker handoff reports:

1. exact branch and head SHA;
2. delivered scope;
3. exact changed files;
4. tests/results and exact CI evidence;
5. known limitations/risks;
6. shared decisions needing Coordinator action;
7. confirmation that no unassigned shared files/contracts were independently redefined.