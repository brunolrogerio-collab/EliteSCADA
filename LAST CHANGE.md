# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — PARALLEL WORK ENABLED**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## NEW PERMANENT CHAT COORDINATION MECHANISM

A repository-owned assignment board now exists at:

- `docs/CHAT-WORK-ASSIGNMENTS.md`

Its only purpose is to record **who is doing what now and what each fixed EliteSCADA chat must do when the user says only `continue`**.

Permanent behavior is also established in `docs/PARALLEL-WORK.md`:

1. every EliteSCADA chat reads `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and its task-specific documents before working;
2. the chat identifies itself by fixed name such as `COORDENADOR - EliteSCADA`, `DEV 1 - EliteSCADA`, `DEV 2 - EliteSCADA` or `DEV 3 - EliteSCADA`;
3. `continue` resumes only the assignment explicitly recorded for that chat;
4. real GitHub branch/PR/head/CI state remains operational truth;
5. `WAIT_FOR_COORDINATOR` forbids a worker from selecting new work, creating another branch, resuming an older delivered task as new work, modifying `main` or touching another branch;
6. only the coordinator may change worker assignments in the assignment board;
7. workers may record implementation/CI/integration requirements in their own PR bodies but may not self-assign another mission.

The coordination board was introduced on `main` by documentation commit `1da96d1738f34cf982204c8ab7fd7458c5d2c251`; permanent parallel/`continue` rules were updated by `111b888c166413f325a13f11da6f708b61971ae2`.

This mechanism does **not** replace the existing document responsibilities:

- `PROJECT GOAL.md` = stable product/architecture north;
- `docs/ROADMAP.md` = macro implementation sequence;
- `docs/PARALLEL-WORK.md` = permanent concurrency/integration rules;
- `docs/CHAT-WORK-ASSIGNMENTS.md` = live chat assignment board;
- `LAST CHANGE.md` = technical operational handoff.

## MERGED

### PR #35 — First-class operational command domain

- merge commit `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- Engineering Schema v7 and first-class Operational Commands are official state.

### PR #36 — Protected runtime read and realtime surfaces

- merge commit `10b0320149c1ef2109e9517539717a8800b200c2`;
- protected TAG/historian/alarm/Engineering/diagnostic reads and authenticated `/ws/tags` are official state.

### PR #37 — Engineering UI foundation and localization

- merge commit `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- `/engineering`, Runtime↔Engineering navigation, localization and structured TAG/Data Source/Alarm editors are official state.

### PR #38 — Local identity and browser login foundation

- merge commit `2a581d279a428cb605429d5939c333ff7ad8d1b4`;
- trusted local identity/browser login foundation is official state.

### PR #39 — Protected local user administration

- merge commit `6de8f06a443ad829ccc95c6dfcd9511e906adeff`;
- protected local-user administration, safe DTOs, role assignment, last-admin protection, security-version invalidation and realtime session revocation are official state.

### Permanent architecture/documentation on `main`

Official locked specifications include:

- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/TAG-GATEWAY.md`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
- `docs/INTERFACE-VALIDATION-MILESTONE.md`;
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`;
- `docs/PARALLEL-WORK.md`;
- `docs/CHAT-WORK-ASSIGNMENTS.md`.

Locked operational sequence remains:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

Locked visual sequence remains:

`Python scripting/property contracts -> script editor/sandbox -> visual runtime object/property API -> graphical Screens/Popups/Dynamos editor`.

## IMPLEMENTED IN PR

### PR #40 — Internal Memory / Source Provider Foundation

- owner history: previous DEV 2 assignment;
- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/internal-memory-foundation`;
- head: `77990fd161580f2e70de941632e5398dfac5c6bd`;
- CI #184: **SUCCESS**.

Implemented foundation includes protocol-neutral Source Provider contracts, `builtin.memory.server` / `builtin.memory.client`, typed memory defaults, stable-ID retention identity, deterministic in-memory server retention, incompatible-type fail-closed behavior, deleted-TAG non-resurrection, normal `Good` quality and per-client Client Memory isolation.

This is a delivered previous task, not DEV 2's current authorized branch. Coordinator integration remains required for public Engineering schema/import-export, central runtime composition, durable production retention, historian/alarm semantics and authorization/audit hooks.

### PR #41 — Python Scripting + Visual Property Foundation — DEV 3

- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/python-scripting-foundation`;
- head: `77d9eb49acd56629aaae96764a48c25784ceb328`;
- CI #210: **SUCCESS**;
- assignment status: `READY_FOR_COORDINATOR_REVIEW`;
- `AfterCompletion: WAIT_FOR_COORDINATOR`.

Implemented foundation includes typed visual-property schemas, Engineering base vs Runtime presentation overrides, deterministic precedence, tween/animation contracts, Client Visual vs Server script scope separation, sandbox capability boundaries, visual runtime instances/object API, bounded event queues/execution coordination/diagnostics and Python validation/editor diagnostic contracts.

Central Engineering schema/import-export/revision/package integration, concrete Python engine, renderer/runtime composition and final graphical editor remain coordinator/later integration work.

### PR #42 — Secured Engineering Apply/Delete/Bulk — COORDENADOR

- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/engineering-secured-apply`;
- head: `4fcc5ab5de03e5c7d9b194554aef25e97daed98d`;
- CI #227: **FAILED**;
- Backend build/tests/runtime smoke: success;
- Web build: success;
- Chromium E2E: failure.

Implemented in PR includes preview-gated backend-authoritative Apply, Workspace mutation serialization/version CAS, explicit dependency-aware Delete, safe scoped Bulk Preview/Apply, authorization/audit wiring, UI mutation panels and focused E2E/security coverage.

Current CI failure is a Chromium regression in existing `engineering.spec.ts`: strict `getByText('Demo.P01.Frequency', { exact: true })` now matches three visible/DOM elements after the new mutation panel introduced another representation of the same stable Engineering identifier. The coordinator must fix the locator/assertion without weakening the test and rerun full CI.

### PR #43 — Historian Retention + Downsampling Foundation — DEV 2

- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/historian-retention-downsampling`;
- head: `98e75948bac3ebe68f424c3a45ebbaefdf9a9331`;
- CI #215: **SUCCESS**;
- assignment status: `READY_FOR_COORDINATOR_REVIEW`;
- `AfterCompletion: WAIT_FOR_COORDINATOR`.

Implemented foundation includes typed raw-retention/downsampling policy, safety approval for potentially destructive retention changes, 1m/5m/15m/1h aggregation semantics, quality-aware numeric aggregation, data-type-aware raw storage, Timescale continuous aggregates, serialized concurrent DDL initialization, retention/downsampling store abstraction and focused Timescale tests.

Coordinator integration remains required for public/versioned Engineering policy representation, canonical validation/import-export/schema migration, central Historian configuration/DI and later raw-vs-aggregate trend/history selection.

### PR #44 — Audit Durability + Retention + Query Foundation — DEV 1

- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/audit-durability-retention-query`;
- head: `8429b1bed28bd998ed25cf1b4a47caf364aef887`;
- CI #229: **SUCCESS**;
- assignment status: `READY_FOR_COORDINATOR_REVIEW`;
- `AfterCompletion: WAIT_FOR_COORDINATOR`.

Implemented foundation includes bounded keyset Audit query/pagination, combined filters, retention execution, health snapshots, optional structural context, PostgreSQL schema/index evolution, controlled batched retention, bounded asynchronous outage buffering/retry, overflow rejection and storage-boundary sensitive-metadata sanitization.

Coordinator integration remains required for API/DI configuration, protected `/api/audit` query evolution, periodic retention hosted-service composition and reconciliation of shared Engineering Delete/Bulk Audit action keys with PR #42.

## CURRENT CHAT ASSIGNMENTS

The exact live assignment details are authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

Current snapshot:

1. **COORDENADOR - EliteSCADA** — PR #42 / `feature/engineering-secured-apply` — `CI_FAILED` — fix Chromium regression, revalidate, then coordinate worker PR review/integration — `AfterCompletion: CONTINUE_COORDINATION`.
2. **DEV 1 - EliteSCADA** — PR #44 / `feature/audit-durability-retention-query` — `READY_FOR_COORDINATOR_REVIEW` — `AfterCompletion: WAIT_FOR_COORDINATOR`.
3. **DEV 2 - EliteSCADA** — PR #43 / `feature/historian-retention-downsampling` — `READY_FOR_COORDINATOR_REVIEW` — `AfterCompletion: WAIT_FOR_COORDINATOR`. Previous PR #40 is delivered but not current work.
4. **DEV 3 - EliteSCADA** — PR #41 / `feature/python-scripting-foundation` — `READY_FOR_COORDINATOR_REVIEW` — `AfterCompletion: WAIT_FOR_COORDINATOR`.

Workers must not self-merge or select new tasks while waiting.

## SPECIFIED / NOT IMPLEMENTED

Important next product blocks still not official implementation include:

- full product integration of Internal Memory;
- protocol-independent TAG Gateway;
- common isolated per-Data-Source diagnostics and Engineering diagnostics UI;
- product-owner USER INTERFACE VALIDATION PREVIEW;
- concrete Python script editor/sandbox and browser runtime engine;
- graphical Screens/Popups/Dynamos editor;
- Trend UI and automatic raw/aggregate resolution selection;
- MQTT, OPC UA, BACnet and later installable driver-module work;
- Engineering Fragments/cross-project copy-paste;
- advanced reusable visual/equipment libraries.

See `PROJECT GOAL.md` and `docs/ROADMAP.md` for ordering and architectural constraints.

## IMMEDIATE CONTINUATION

### COORDENADOR - EliteSCADA

On `continue`:

1. reread all permanent coordination documents including `docs/CHAT-WORK-ASSIGNMENTS.md`;
2. verify current `main`, PR #42 head and latest CI;
3. fix the known Chromium locator regression on PR #42 if still present;
4. obtain full green CI for the final PR #42 head;
5. review/reconcile PRs #40, #41, #43 and #44 against then-current `main` and their `INTEGRATION REQUIRED` sections;
6. choose integration order from real conflict/dependency state;
7. update the assignment board whenever a DEV receives new authorized work.

### DEV 1 / DEV 2 / DEV 3

On `continue`, while their assignment remains `READY_FOR_COORDINATOR_REVIEW` with `AfterCompletion: WAIT_FOR_COORDINATOR`, they must report that the current task is delivered and wait. They must not create another branch or choose another roadmap task.

## Permanent continuity rule

- Feature branches must never be the sole durable home of permanent architecture decisions.
- Open PRs remain **IMPLEMENTED IN PR**, never **MERGED**.
- GitHub branch/PR/head/CI state is operational truth.
- Worker assignment authority comes only from `docs/CHAT-WORK-ASSIGNMENTS.md` as maintained by the coordinator.
