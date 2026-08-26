# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## MERGED

### PR #35 — Add first-class operational command domain

- merged into `main`;
- merge commit `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- Engineering Schema v7 and first-class Operational Commands are official state.

### PR #36 — Protect runtime read and realtime surfaces

- merged into `main`;
- merge commit `10b0320149c1ef2109e9517539717a8800b200c2`;
- merged behavior includes protected TAG/historian/alarm/Engineering/diagnostic reads, JWT-authenticated `/ws/tags`, per-event authorization, JWT-expiration handling and minimal public `/health`.

### PR #37 — Engineering UI foundation and localization

- merged into `main`;
- merge commit `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- `/engineering` is now official platform UI while `/` remains Runtime HMI;
- Runtime↔Engineering navigation and shared `pt-BR`, `en`, `es` localization are on `main`;
- structured TAG, Data Source and Alarm editors are on `main`;
- current editor mutation boundary remains intentionally preview-oriented: no general secured Apply/Delete/bulk lifecycle yet.

### PR #38 — Local identity and browser login foundation

- final head: `bdd2003230570387328f5a4083c9c47b9817af50`;
- CI #167 completed fully green:
  - Web build: success;
  - Backend build/test/runtime smoke: success;
  - Chromium end-to-end: success, including real local login flow;
- the earlier Chromium failure was test isolation only: the local-login browser context inherited the global developer Bearer token; the test was fixed by explicitly clearing that inherited Authorization header without weakening product authentication;
- merged into `main` as `2a581d279a428cb605429d5939c333ff7ad8d1b4`.

Merged identity behavior now includes:

- local user account model separate from Engineering roles/policies;
- PBKDF2-SHA256 password hashing with random salt;
- in-memory identity store for local/dev scenarios;
- PostgreSQL `elitescada.local_users` persistence when configured;
- JWT issuance through the existing trusted issuer/audience/signing-key boundary;
- HttpOnly cookie support for same-origin browser clients while preserving normal Bearer tokens and explicit WebSocket token paths;
- bootstrap-first-user support for an empty local identity store;
- login/logout/profile foundation and login throttling;
- shared browser session used by Runtime and Engineering.

Credentials/password hashes remain outside Engineering Import/Export. User accounts reference role keys; the active Engineering revision remains authoritative for capabilities/scopes.

### Permanent architecture/documentation consolidated on `main`

The following locked specifications are official product north even where functionality remains unimplemented:

- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/TAG-GATEWAY.md`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
- `docs/INTERFACE-VALIDATION-MILESTONE.md`;
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`;
- Source Provider direction and multi-Data-Source isolation;
- mandatory preview gate after common driver diagnostics;
- mandatory Python scripting + visual property foundation before the full graphical screen/popup/Dynamo editor.

Documentation commits in the latest task:

- scripting/visual architecture document: `e6c273d3f26ba12740a1fdf8b94f497e71e28803`;
- consolidated `PROJECT GOAL.md`: `267430b24e77d45428f2f7361cafe198e63ac573`;
- refreshed `docs/ROADMAP.md`: `2d96b4911946c0c747d041249785c219e0caa168`.

## IMPLEMENTED IN PR

There is currently **no active feature PR containing unmerged functional implementation** at this handoff point.

The next implementation slice should start from current `main`, not from the old #37/#38 feature branches.

## SPECIFIED / NOT IMPLEMENTED

### Immediate security/Engineering hardening

- full user lifecycle/administration UI/API, including safe role-key assignment and enable/disable/password-management behavior;
- secured Engineering Apply/Delete/bulk-edit lifecycle for the current TAG/Data Source/Alarm editors;
- audit retention/query policy and bounded buffering/outbox for temporary storage failures;
- historian retention/downsampling.

### Locked source/protocol foundation

- `builtin.memory.client` and retentive `builtin.memory.server` with typed initial values and stable-ID retention/migration semantics;
- protocol-independent TAG→TAG Gateway with quality/rate/transform/cycle/multi-writer policy and route diagnostics;
- common isolated per-Data-Source communication diagnostics and Engineering diagnostics UI;
- product-owner **USER INTERFACE VALIDATION PREVIEW** after the diagnostics slice and before additional external protocols.

The mandatory sequence is:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

The preview must be a practical Windows x64 testable package/path with local login, demo project, visible version and startup/test instructions. It is a development preview, not a production-certified release.

### Python scripting and visual runtime

Before the full graphical screen/popup/Dynamo editor, implement the locked sequence:

`Python scripting contract + visual property schema -> script editor/sandbox -> visual runtime object instances/property API -> graphical screen/popup/Dynamo editor -> advanced reusable visual libraries`

Locked scripting decisions:

- Python is the initial scripting language;
- Client Visual Scripts and Server Scripts are distinct scopes;
- Client Visual Scripts operate on their Runtime Client instance, permitted shared TAGs and that client's Client Memory;
- Server Scripts operate on shared server runtime/Server Memory and never manipulate one client's screen-object instances;
- visual objects expose a typed public property schema used by both the property inspector and Python API;
- common script-accessible properties include position, size, width/height, rotation, visibility, opacity, z-order, fill/background color, line/stroke color, line/stroke thickness/width, text/font and image/resource properties where applicable;
- object-specific properties are allowed only when explicitly declared in the type schema;
- Engineering stores base/design-time values;
- script/binding/animation changes create runtime presentation overrides and must never silently mutate saved Engineering revisions;
- Client Visual Scripts are event-driven and may react to load/unload, object interaction, TAG/Client Memory changes and timers;
- smooth animation should use renderer-native tween/animation primitives invoked from Python rather than requiring Python busy loops;
- binding/script/animation property precedence must be deterministic and diagnosable;
- scripts are sandboxed and cannot directly access drivers, database, arbitrary filesystem/OS/shell/network/DOM internals, secrets or stronger authorization than the current trusted boundary;
- the script editor must provide syntax highlighting, diagnostics with line/column, validation, event association, API autocomplete where practical and sandboxed test/preview;
- script execution requires budgets, cancellation, bounded queues and per-script/visual-instance error isolation.

Full specification: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

The interface-validation preview after driver diagnostics may occur before the final graphical editor. The scripting/property foundation is mandatory specifically before graphical screen/Dynamo editor work starts.

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

Start from current `main` after merge `2a581d279a428cb605429d5939c333ff7ad8d1b4` and subsequent documentation commits.

Recommended next implementation order:

1. implement local **user lifecycle/administration** using the existing `UserRoleAdmin` / `SystemAdmin` capability model;
2. expose only safe user profile/state/role-key data, never hash/salt;
3. validate assigned role keys against current Engineering security roles where required;
4. add tests for authorization, PostgreSQL persistence and Chromium administration flow;
5. merge only after full relevant CI is green;
6. then implement secured Engineering Apply/Delete/bulk workflows;
7. then audit durability/retention;
8. then historian retention/downsampling;
9. then internal memory -> Gateway -> multi-driver diagnostics -> interface-validation preview;
10. only then continue the external-protocol wave according to `docs/ROADMAP.md`.

Before beginning graphical screen/popup/Dynamo editor development, stop and execute the Python scripting/visual-property prerequisite chain documented above.

## Permanent continuity rule

- `PROJECT GOAL.md` = official permanent product north and locked architecture.
- `LAST CHANGE.md` = exact operational resume point with explicit MERGED / IMPLEMENTED IN PR / SPECIFIED state.
- `docs/ROADMAP.md` = ordered implementation plan/status.
- Feature branches must never be the sole durable home of permanent architecture decisions.
