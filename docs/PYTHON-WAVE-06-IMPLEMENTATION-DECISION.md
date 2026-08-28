# PYTHON WAVE 06 — Implementation Decision

Status: **LOCKED WAVE 06 IMPLEMENTATION BOUNDARY**  
Decision date: 2026-08-28

This document converts the merged Client Visual Python research into the implementation boundary for `PYTHON-WAVE-06`. Public Script Engineering contracts remain engine-independent.

## Product outcome

Wave 06 must deliver a practical Engineering Python editor plus a browser Client Visual Python sandbox that can execute a canonical `ClientVisual` Script without gaining direct infrastructure authority.

Wave gate:

`canonical ClientVisual Script -> edit/compile diagnostics -> isolated execution -> permitted TAG read -> Client Memory read/write -> event dispatch -> bounded failure/timeout -> understandable diagnostics`

Visual-object manipulation beyond the already-public safe API foundation is integrated only where current contracts permit it; full visual Runtime object identity/composition remains Wave 07.

## Selected implementation

### Python engine

Use **Pyodide 314.0.6** as the first Wave 06 browser engine adapter.

Rules:

- engine implementation is not Script identity or Engineering authority;
- run inside a dedicated module Web Worker, never the React/main thread;
- one active Worker/runtime compartment per `ScriptRuntimeInstance` for the first implementation;
- no `globalThis` exposure as the Python `js` surface;
- initialize with deliberately minimal JavaScript globals;
- expose only the versioned EliteSCADA Client Visual bridge;
- no `micropip`, dynamic package install, `loadPackagesFromImports()` or remote code loading from user Scripts;
- runtime assets are pinned/self-hosted for the product path; no Runtime CDN dependency;
- arbitrary network access is denied independently of Python source scanning.

Pyodide remains an adapter behind public contracts. Replacing the engine later must not require changing canonical Script identity merely because implementation technology changes.

### Engineering editor

Use **Monaco Editor 0.56.0** for the first Wave 06 editor.

Required first slice:

- Python syntax highlighting;
- line numbers;
- normal indentation/navigation;
- source editing over the canonical Script Workspace;
- model markers from 1-based line/column diagnostics;
- EliteSCADA API completion/help where practical from public descriptors;
- explicit Script scope and entry-point context;
- no browser-private Script source of truth.

Monaco is UI infrastructure, not the compiler or canonical validation authority.

## Execution policy

Wave 06 preserves the already-merged safe defaults:

- handler timeout: **250 ms**;
- hard-stop grace after soft interrupt: initial **50 ms** implementation value;
- maximum queued events: **128**;
- minimum timer interval: **50 ms**;
- consecutive failures before throttle: **5**;
- queue overflow: coalesce by stable event key;
- fault isolation: `ScriptRuntimeInstance`.

The 50 ms hard-stop grace is an engine-adapter value, not a public Script semantic. It may be tuned from measured behavior without weakening the 250 ms execution budget or allowing detached timed-out execution.

Timeout sequence:

1. parent host owns deadline;
2. request Pyodide interrupt through `SharedArrayBuffer`;
3. allow only bounded grace;
4. if still executing, `Worker.terminate()`;
5. discard interpreter state;
6. record timeout/failure diagnostics;
7. recreate lazily only when policy allows future execution.

No timed-out Worker continues in the background.

## Cross-origin isolation

`SharedArrayBuffer` cancellation requires cross-origin isolation.

Wave 06 development/acceptance host must prove:

- `Cross-Origin-Opener-Policy: same-origin`;
- `Cross-Origin-Embedder-Policy: require-corp` or an explicitly reviewed equivalent;
- `crossOriginIsolated === true` in the relevant browser context;
- existing same-origin API/WebSocket/frontend resources remain functional.

The future Windows product host must reproduce the required headers. Vite-only success is not final packaging evidence.

## Trust boundary

Security boundary:

`untrusted canonical Python source -> bounded Worker/Pyodide compartment -> versioned structured-clone RPC -> trusted browser host -> existing EliteSCADA Runtime/backend authorization`

Python receives no direct:

- filesystem;
- operating system or shell/process execution;
- arbitrary network;
- PostgreSQL/database handle;
- industrial driver;
- secret/credential/token;
- browser DOM;
- browser storage;
- Server Memory write authority;
- direct shared/process TAG write authority.

Source preflight remains developer feedback and defense-in-depth. It is not the security boundary.

## Client Visual API v1 boundary

The first bridge version is `1`.

Permitted capability families:

- `tag.read` for permitted shared TAG snapshots;
- `clientMemory.read`;
- `clientMemory.write` for the owning Runtime Client only;
- `visualProperty.read` where the existing public visual API allows it;
- `visualProperty.write` only for runtime-writable properties;
- `visualTween.request` through renderer/public animation authority where available;
- `backendOperation.request` only as an intent forwarded to normal protected backend APIs.

Backend/session identity remains authoritative. Python never supplies a stronger actor or credential.

Server Python is explicitly outside Wave 06.

## Worker protocol

Parent -> Worker message families:

- initialize Script;
- compile source;
- dispatch event;
- API response;
- cancel execution;
- dispose Script.

Worker -> Parent message families:

- ready;
- compile result;
- execution result;
- API request;
- diagnostic;
- disposed.

Every message carries bridge version and stable runtime identity. Event execution additionally carries execution/request identity. Responses from stale/terminated runtime instances are rejected.

Structured-clone-compatible values only. No live application objects/functions/DOM nodes/auth material cross the bridge.

## Compile and diagnostics

Diagnostic layers remain separate:

1. Monaco lexical/editor feedback;
2. engine-backed compile-only diagnostics;
3. deterministic EliteSCADA preflight and canonical Engineering Preview validation;
4. runtime execution diagnostics.

Engine diagnostics map to existing 1-based line/column contracts and are sanitized before presentation.

Compile requests are debounced/superseded. Obsolete compiles must not form an unbounded queue.

## Events and lifecycle

First Wave 06 runtime acceptance uses controlled event dispatch through the host. Timers remain bounded by the existing minimum interval.

Rules:

- one handler executes at a time per Script Runtime Instance;
- event admission/backpressure is parent-owned;
- subscriptions/timers are disposed with their runtime instance;
- disposal cancels active execution and terminates the Worker if it does not acknowledge promptly;
- stale Worker messages cannot mutate a replacement instance;
- `FrameTick` remains disabled by default in Wave 06;
- normal smooth animation is not implemented as a high-frequency Python loop.

Full Screen/Popup/Dynamo event authoring belongs to later visual waves, especially Wave 10.

## Engineering authority

Canonical `scada.engineering` v10 Scripts remain the only source of Script definition truth.

The editor never stores a competing authoritative browser Script model. Save/update continues through canonical Engineering Preview/Apply/CAS and later lifecycle Save/Publish/Activate.

Preview/test execution must not silently mutate Active Engineering or bypass backend authorization.

## Dependency and shared-file ownership

Coordinator owns:

- `web/scada-web/package.json` shared dependency integration;
- central Python bridge/protocol contracts;
- Vite/host cross-origin-isolation configuration;
- `EngineeringApp.tsx`, `main.tsx`, central composition;
- any backend/DI bridge required across domains;
- final integration and CI.

Workers receive isolated domains after the central foundation is validated.

## Parallel worker split

### DEV 1 — Python Editor UX

Isolated Engineering editor component using Monaco and canonical Script APIs. No Pyodide host/runtime implementation.

### DEV 2 — Client Visual Python Worker/runtime adapter

Pyodide Worker, restricted globals/module bridge, compile/dispatch/cancel/dispose protocol and trusted-parent capability dispatch. No central Engineering composition.

### DEV 3 — Sandbox safety and acceptance

Adversarial/failure acceptance: infinite loop, timeout/hard kill, stale messages, queue bounds, denied capabilities, diagnostics, isolation and cross-origin-isolation behavior. Production changes require escalation rather than silent scope expansion.

## CI under CONSTRAINED mode

- central dependency/protocol foundation receives one meaningful checkpoint validation before workers are promoted;
- workers batch coherent work and use focused evidence during iteration;
- unchanged-head full reruns are prohibited;
- coordinator integrates accepted slices and spends the full matrix on meaningful integrated/final checkpoints;
- final Wave 06 exact-head Web + backend Release/full tests + Runtime smoke + Chromium remains mandatory before merge;
- inability to run the final matrix means `BLOCKED_BY_CI_BUDGET`, never a weaker merge.

## Explicitly not implemented by this decision

This document does not itself implement:

- Server Python;
- full visual object identity/runtime model;
- graphical Screen/Popup/Dynamo editor;
- unrestricted package ecosystem;
- process writes directly from Python;
- arbitrary network/filesystem/database/driver access;
- final Windows packaging of Pyodide assets.

Those remain controlled later-wave work.
