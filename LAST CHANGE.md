# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** **PAUSED**

Only documentation/continuity maintenance is authorized while paused. Do not perform new functional implementation until the user explicitly resumes development.

This checkpoint separates repository truth into three explicit states: **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## MERGED

### PR #35 — Add first-class operational command domain

Status verified from GitHub:

- merged into `main`;
- PR CI run **#144** completed successfully on head `4254c698d5a99c5fb2849d1ab1558ad61a0d4361`;
- Web build, Backend build/test/runtime smoke and Chromium E2E passed in that successful PR run;
- merge commit: `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- a subsequent `main` push CI also completed successfully after the merge.

Merged functional baseline includes:

- first-class operational command definitions and registries;
- Engineering Schema **v7** command serialization/import/export;
- runtime command compilation/execution through the target TAG's owning driver;
- scoped `CommandExecute` authorization;
- succeeded/denied/failed command audit without persisting commanded values as Engineering configuration;
- demo commands and automated Core/Engineering/Driver/Security coverage.

Commands are no longer a future/next-step item in the roadmap.

### Documentation/architecture consolidation performed on `main`

This paused-state maintenance task moved permanent architecture out of the PR #37 branch and into the official `main` documentation without changing functional code.

Added to `main`:

- `docs/INTERNAL-MEMORY-TAGS.md` — commit `697cd9e6bdd14de595bc6b38587a5aee6f120d27`;
- `docs/TAG-GATEWAY.md` — commit `59ddef56dd79da72a0cfacfc54d4dfe5ab7299e2`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md` — commit `7322d921ef4bce0cce70bebfb7802c516ebb07a8`.

Updated on `main`:

- `PROJECT GOAL.md` — architecture consolidation commit `e7e771842ab976027a607cdc1f5539083630733d`;
- `docs/ROADMAP.md` — status/order consolidation commit `d8e5597d1dc0522e6bc5b7d64f18d183d3099432`;
- this `LAST CHANGE.md` update creates the next `main` head after those commits.

`PROJECT GOAL.md` now records the permanent rule that architectural decisions must not live only in feature branches. Permanent decisions go to the official `main` product north even before implementation.

## IMPLEMENTED IN PR

### PR #36 — Protect runtime read and realtime surfaces

Current verified GitHub state:

- open;
- not merged;
- not Draft;
- base ref is `main`;
- head branch `feature/runtime-read-authorization`;
- head SHA `1df64077b235321f0c3318b994f7b89632261cee`.

Implemented in the PR:

- `TagRead` enforcement for TAG collections, individual reads and historian queries;
- alarm-read filtering by readable TAG and `View` area scope;
- JWT-authenticated `/ws/tags` browser WebSockets;
- per-event realtime authorization and JWT-expiration handling;
- fail-closed behavior if active runtime changes during authorization;
- protected driver/diagnostic, Engineering workspace/export/preview/persistence and project-package reads;
- minimal public `/health` with detailed technical diagnostics behind authorization;
- expanded Chromium security coverage;
- CI concurrency behavior.

**Validation status:** no GitHub Actions workflow run was found for the current head SHA during this checkpoint. Therefore PR #36 is not considered independently validated after retargeting and must not be merged until current-head Web, Backend/test/smoke and Chromium CI are green.

### PR #37 — Add Engineering UI foundation and localization

Current verified GitHub state:

- open;
- **Draft**;
- not merged;
- head branch `feature/engineering-ui-foundation`;
- head SHA `74307b51df65a71ce0a5179deb957ffea958a440`;
- CI run **#143** on that head completed successfully.

Implemented in the PR:

- `/engineering` developer-facing workspace while `/` remains Runtime HMI;
- Runtime <-> Engineering navigation;
- shared Engineering UI localization in `pt-BR`, `en` and `es`;
- structured TAG editor;
- structured Data Source editor;
- structured Alarm editor;
- existing-entity editing through browser-local draft + canonical backend preview;
- creation of new TAG/Data Source/Alarm through preview-only ID-less drafts;
- no Workspace mutation during preview;
- preservation of metadata and non-exposed fields;
- stale preview invalidation when drafts change;
- unsaved-draft confirmation when switching entities plus `beforeunload` protection;
- changing an Alarm `tagPath` clears the old `tagId` so preview validates against the intended TAG;
- Chromium coverage for navigation, localization, validation, creation previews and proof that preview does not mutate Workspace/export state.

**Important boundary:** PR #37 still has **no Apply, Delete or bulk edit**. Existing edits and creates are preview-only. Do not quietly convert this into a mutable Engineering editor before the security/integration boundary is ready.

PR #37 was created from an older `main` base and must be reconciled with the command merge, PR #36 integration state and the documentation now consolidated directly on `main` before any merge decision.

## SPECIFIED / NOT IMPLEMENTED

The following are permanent product/architecture requirements now preserved on `main`, but they do **not** yet represent functional implementation.

### Source Provider and multi-Data-Source architecture

- Treat the value-source concept more generally as a **Source Provider** where appropriate; not every source is physical communication.
- Driver type = protocol/implementation type.
- Data Source = concrete configured instance of a driver/source type.
- TAG = owned by one Data Source/source provider plus its address/binding where applicable.
- Multiple Data Sources of the same Driver type and multiple different Driver types must run simultaneously.
- Failure of one Data Source must remain isolated and must not contaminate another Data Source's TAG quality or diagnostics.

### Built-in memory providers

#### `builtin.memory.client`

- one local value store per opened Runtime Client/session;
- not shared between clients;
- no server retention initially;
- typed initial/default value;
- intended for popup/navigation/selection/filter/screen-transition state, local demos and future client scripts;
- never valid as backend security, interlock, authoritative process truth, command permissive or audit identity;
- not a global historian/alarm source;
- not a server Gateway endpoint.

#### `builtin.memory.server`

- one server-authoritative shared value per TAG;
- retentive by design;
- typed initial/default value;
- retained mutable value stored separately from immutable Engineering revisions/packages;
- stable TAG ID is the primary retention key so path rename does not lose value;
- incompatible type changes require explicit reset/migration and must never silently coerce retained state;
- can participate in current cache, Event Bus, realtime, security, future server scripts, alarms and historian when configured;
- can participate as source or destination in the server Gateway.

### Protocol-independent TAG Gateway

Authoritative route:

`Source TAG -> Gateway route -> Destination TAG`

The runtime resolves the owning Data Source/source provider on each endpoint. Concrete drivers never call each other directly.

Locked first-version behavior:

- first-class serializable/versioned Engineering routes;
- stable route identity and TAG references;
- OnChange and Periodic transfer modes;
- optional deadband;
- minimum write interval;
- coalescing;
- source quality `Good` required by default;
- no stale-value push by default when source quality becomes bad;
- explicit type compatibility;
- simple linear transformation `destination = source × gain + offset`;
- unidirectional initial implementation;
- reject direct/indirect loops;
- reject multiple active Gateway writers to one destination unless a future explicit arbitration policy exists;
- allow one source to fan out to multiple destinations;
- `builtin.memory.client` excluded from the server Gateway;
- `builtin.memory.server` allowed;
- independent route diagnostics for state, successes/failures, quality skips, throttling/coalescing and sanitized errors.

### Common communication diagnostics

Per active external communication Data Source/driver instance, expose a common protected diagnostic model including, where meaningful:

- healthy/running, degraded, reconnecting and failed/faulted state;
- last good communication/sample;
- last failure and sanitized error;
- request/cycle counts;
- successes and failures;
- consecutive failures;
- timeout count;
- reconnect/disconnect count;
- recent failure/error rate;
- response/round-trip latency;
- configured interval and observed data age;
- associated TAG count and TAG quality aggregation such as Good and BadCommunication.

Internal memory providers must not fabricate network timeout/reconnect/latency diagnostics.

### Locked implementation order before additional external protocols

**internal memory -> TAG-to-TAG Gateway -> common multi-driver diagnostics -> new external drivers/protocols**

After those foundations, preserve the previously locked protocol/module direction including MQTT, OPC UA, BACnet, installable/versioned Driver Modules, Siemens S7 ISO Connection as the first intended installable-module target, and later Allen-Bradley research.

### Other locked remaining product areas

Still preserve in the roadmap/product north:

- identity/login/token issuance/user lifecycle and administration;
- audit durability/buffering/retention/query policy;
- historian retention/downsampling;
- complete Engineering UI and secured Apply lifecycle;
- graphical screens and popups;
- reusable Equipment/Templates/Dynamos and visual component libraries;
- Engineering Fragments/cross-project copy-paste;
- multi-Pen trends, engineered/ad-hoc/saved trend workflows;
- configurable persistent application shell;
- Engineering XLSX import/export;
- full `pt-BR` / `en` / `es` Engineering-interface coverage as new surfaces are added;
- installable driver modules/public Driver SDK;
- later scripting/public SDK expansion where applicable.

## Immediate continuation rule

While development is paused:

- do not implement memory, Gateway, diagnostics, PR #36 fixes, PR #37 Apply, new protocols or other functional slices;
- documentation/continuity corrections are allowed;
- do not merge PR #36 without a green CI for its current integrated head;
- do not merge PR #37 merely because #143 was green against its older base; reconcile it with current `main` and the security/command state first.

When the user explicitly resumes development, begin by reading `PROJECT GOAL.md`, this file and `docs/ROADMAP.md`, then fetch live GitHub state. Continue from repository truth rather than remembered chat state.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent official product north and permanent architecture, including requirements not implemented yet.
- `LAST CHANGE.md` = exact stopping point with explicit **MERGED / IMPLEMENTED IN PR / SPECIFIED / NOT IMPLEMENTED** status.
- `docs/ROADMAP.md` = ordered implementation plan/status.
- Feature branches may contain implementation detail, but must not be the sole durable home of permanent architecture decisions.
