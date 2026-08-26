# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md` and `docs/PARALLEL-WORK.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — PARALLEL WORK ENABLED**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## MERGED

### PR #35 — Add first-class operational command domain

- merged into `main`;
- merge commit `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- Engineering Schema v7 and first-class Operational Commands are official state.

### PR #36 — Protect runtime read and realtime surfaces

- merged into `main`;
- merge commit `10b0320149c1ef2109e9517539717a8800b200c2`;
- protected TAG/historian/alarm/Engineering/diagnostic reads, JWT-authenticated `/ws/tags`, per-event authorization, JWT-expiration handling and minimal public `/health` are official state.

### PR #37 — Engineering UI foundation and localization

- merged into `main`;
- merge commit `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- `/engineering` is official platform UI while `/` remains Runtime HMI;
- Runtime↔Engineering navigation and `pt-BR`, `en`, `es` localization are on `main`;
- structured TAG, Data Source and Alarm editors are on `main`;
- general secured Apply/Delete/bulk lifecycle is still not implemented.

### PR #38 — Local identity and browser login foundation

- final tested head `bdd2003230570387328f5a4083c9c47b9817af50`;
- CI #167 fully green: Web build, Backend build/test/runtime smoke and Chromium end-to-end;
- merged into `main` as `2a581d279a428cb605429d5939c333ff7ad8d1b4`.

Official merged identity behavior includes:

- local user account model separate from Engineering roles/policies;
- PBKDF2-SHA256 password hashing with random salt;
- in-memory identity store for local/dev scenarios;
- PostgreSQL `elitescada.local_users` persistence when configured;
- JWT issuance through trusted issuer/audience/signing-key boundary;
- HttpOnly cookie support for same-origin browser clients while preserving Bearer tokens and explicit WebSocket token paths;
- bootstrap-first-user support;
- login/logout/profile foundation and login throttling;
- shared browser session used by Runtime and Engineering.

Credentials/password hashes remain outside Engineering Import/Export. User accounts reference role keys; active Engineering security remains authoritative for capabilities/scopes.

### Permanent architecture/documentation consolidated on `main`

Locked specifications include:

- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/TAG-GATEWAY.md`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
- `docs/INTERFACE-VALIDATION-MILESTONE.md`;
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`;
- Source Provider direction and multi-Data-Source isolation;
- mandatory preview gate after common driver diagnostics;
- mandatory Python scripting + visual property foundation before the full graphical screen/popup/Dynamo editor.

Latest merged documentation baseline before this handoff:

- scripting/visual architecture: `e6c273d3f26ba12740a1fdf8b94f497e71e28803`;
- `PROJECT GOAL.md`: `267430b24e77d45428f2f7361cafe198e63ac573`;
- `docs/ROADMAP.md`: `2d96b4911946c0c747d041249785c219e0caa168`;
- prior `main` handoff head: `f30584f893b97ab20abe9088b5010d2fd62f0731`.

## IMPLEMENTED IN PR

### PR #39 — Add protected local user administration

- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/local-user-administration`;
- current head: `68df83c9793ee1c03a2801b237e80ae5f41d06c0`;
- GitHub currently reports the PR as mergeable;
- latest CI on this exact head: **CI #179 = failure**;
- do not merge until the failing job is diagnosed/fixed and a final integrated CI is green.

Implemented in the branch includes:

- protected local-user list/create/update/password-reset endpoints;
- Engineering role catalog for assignment;
- role-key validation;
- `UserRoleAdmin` / `SystemAdmin` administration direction;
- safe DTOs with no password hash/salt exposure;
- last-enabled-local-admin safety protection;
- local JWT security-version claim and store revalidation;
- changes to enabled state/roles/profile/password invalidate older local JWTs;
- realtime revocation direction for already-open WebSockets;
- Engineering user administration UI with `pt-BR`, `en`, `es`;
- Chromium administration flow coverage under development.

This is still branch state, not official product state.

## PARALLEL WORK COORDINATION

Parallel development is now explicitly supported through `docs/PARALLEL-WORK.md`.

Active ownership model:

1. **Coordinator/integration chat** — finishes PR #39, owns shared integration files, cross-PR reconciliation and merge order.
2. **Worker A** — branch `feature/internal-memory-foundation`; isolated Internal Memory / Source Provider foundation and tests.
3. **Worker B** — branch `feature/python-scripting-foundation`; isolated Python scripting contracts + typed visual property/runtime-override foundation and tests.

Worker chats must not merge their own PRs and must avoid coordinator-owned shared files. In particular, `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `.github/workflows/**`, `src/Scada.Api/Program.cs`, central composition/routing files and `src/Scada.Engineering/Contracts/EngineeringContracts.cs` are coordinator-owned during parallel work unless explicitly assigned.

Preferred integration order:

1. fix/validate/merge PR #39;
2. reconcile both worker branches onto new `main`;
3. merge the cleaner/smaller worker PR first after green CI;
4. rebase/reconcile the remaining worker PR again;
5. run full relevant CI and integration smoke tests before final merge.

## SPECIFIED / NOT IMPLEMENTED

### Immediate security/Engineering hardening

- secured Engineering Apply/Delete/bulk-edit lifecycle for TAG/Data Source/Alarm editors;
- audit retention/query policy and bounded buffering/outbox for temporary storage failures;
- historian retention/downsampling.

### Locked source/protocol foundation

- `builtin.memory.client` and retentive `builtin.memory.server` with typed initial values and stable-ID retention/migration semantics;
- protocol-independent TAG→TAG Gateway with quality/rate/transform/cycle/multi-writer policy and route diagnostics;
- common isolated per-Data-Source communication diagnostics and Engineering diagnostics UI;
- product-owner **USER INTERFACE VALIDATION PREVIEW** after the diagnostics slice and before additional external protocols.

Mandatory operational sequence remains:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

The preview must be a practical Windows x64 testable package/path with local login, demo project, visible version and startup/test instructions. It is a development preview, not a production-certified release.

### Python scripting and visual runtime

Before the full graphical screen/popup/Dynamo editor, implement:

`Python scripting contract + visual property schema -> script editor/sandbox -> visual runtime object instances/property API -> graphical screen/popup/Dynamo editor -> advanced reusable visual libraries`

Locked scripting decisions:

- Python is the initial scripting language;
- Client Visual Scripts and Server Scripts are distinct scopes;
- Client Visual Scripts operate on their Runtime Client instance, permitted shared TAGs and that client's Client Memory;
- Server Scripts operate on shared server runtime/Server Memory and never manipulate one client's screen-object instances;
- visual objects expose a typed public property schema used by both property inspector and Python API;
- common script-accessible properties include position, size, width/height, rotation, visibility, opacity, z-order, fill/background color, line/stroke color, line/stroke thickness/width, text/font and image/resource properties where applicable;
- object-specific properties are allowed only when declared in the type schema;
- Engineering stores base/design-time values;
- script/binding/animation changes create runtime presentation overrides and never silently mutate saved Engineering revisions;
- Client Visual Scripts are event-driven;
- smooth animation uses renderer-native tween/animation primitives rather than Python busy loops;
- binding/script/animation precedence is deterministic and diagnosable;
- scripts are sandboxed from arbitrary drivers/database/filesystem/OS/shell/network/DOM/secrets access;
- editor requires syntax highlighting, diagnostics, validation, event association, autocomplete where practical and sandboxed preview;
- execution requires budgets, cancellation, bounded queues and per-script/instance error isolation.

Full specification: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

### Later product blocks

- MQTT;
- OPC UA;
- BACnet;
- installable/versioned Driver Module framework;
- Siemens S7 ISO Connection first intended installable module target;
- later Allen-Bradley research;
- graphical screens/popups and reusable Dynamos/components after scripting/property prerequisites;
- Engineering Fragments/cross-project copy-paste;
- multi-Pen trends;
- configurable application shell;
- Engineering XLSX;
- visual asset/resource management;
- advanced reusable Equipment/Template/Dynamo libraries;
- later Server Python scripting expansion.

## Immediate continuation

Coordinator:

1. diagnose CI #179 on PR #39 head `68df83c9793ee1c03a2801b237e80ae5f41d06c0`;
2. fix PR #39 without broad unrelated refactors;
3. run final full CI;
4. merge #39 only when green;
5. reconcile worker PRs one at a time.

Worker A:

- follow `docs/PARALLEL-WORK.md` and `docs/INTERNAL-MEMORY-TAGS.md` on `feature/internal-memory-foundation`.

Worker B:

- follow `docs/PARALLEL-WORK.md` and `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md` on `feature/python-scripting-foundation`.

## Permanent continuity rule

- `PROJECT GOAL.md` = official permanent product north and locked architecture.
- `LAST CHANGE.md` = exact operational resume point with explicit MERGED / IMPLEMENTED IN PR / SPECIFIED state.
- `docs/ROADMAP.md` = ordered implementation plan/status.
- `docs/PARALLEL-WORK.md` = concurrent work ownership, conflict-avoidance and integration rules.
- Feature branches must never be the sole durable home of permanent architecture decisions.
