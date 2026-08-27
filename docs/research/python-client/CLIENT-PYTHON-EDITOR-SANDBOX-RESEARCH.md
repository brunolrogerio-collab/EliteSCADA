# Client Python editor, browser sandbox and execution-engine research

> **Status:** RESEARCH IN BRANCH / NOT IMPLEMENTED  
> **DEV:** DEV 3 - EliteSCADA  
> **Branch:** `research/client-python-editor-sandbox`  
> **Research date:** 2026-08-26  
> **Production dependency changes:** none

## 1. Purpose and non-goals

This document evaluates the technology and security architecture for the future EliteSCADA **Client Visual Python** editor/runtime required by `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

It is deliberately a research/design artifact. It does **not**:

- add Pyodide, MicroPython, RustPython, Monaco, CodeMirror or another production dependency;
- implement a Python runtime or script editor;
- change `EngineeringContracts.cs` or canonical Script package/schema integration;
- change central frontend routing/application shell;
- implement Server Python;
- implement graphical Screen/Popup/Dynamo editing;
- register a browser-private scripting source of truth;
- weaken the existing Script sandbox contracts merely to fit an engine.

The authoritative future implementation must remain compatible with the public Script/visual contracts already merged in EliteSCADA and with the canonical Engineering lifecycle once Script integration is completed.

---

## 2. Executive recommendation

### 2.1 First browser Python laboratory direction

Use **Pyodide in a dedicated module Web Worker** as the preferred first implementation/laboratory candidate, with the following mandatory constraints:

1. Pyodide is an **engine adapter**, never the public Script model.
2. Python never receives `globalThis` as its normal `js` module surface.
3. Initialize Pyodide with a deliberately minimal `jsglobals` object and expose only a versioned EliteSCADA facade through an explicitly registered JavaScript module.
4. Do not package `micropip` or enable dynamic package installation for Client Visual Scripts.
5. Do not call `loadPackagesFromImports()` for user source.
6. Execute in a Web Worker, never on the UI thread.
7. Enable Pyodide interruption through `SharedArrayBuffer` in a cross-origin-isolated deployment.
8. Treat the Pyodide interrupt as the **soft cancellation path**, then terminate and recreate the Worker after a short bounded grace period if execution does not complete.
9. Enforce network denial independently of Python source scanning. The execution compartment must have no route to arbitrary external network or privileged EliteSCADA backend endpoints.
10. Do not expose DOM, browser storage, auth tokens, secrets, database, drivers, filesystem handles or renderer internals to the engine.
11. Self-host a pinned, integrity-checked minimal runtime asset set. No CDN dependency at Runtime.
12. Preserve the existing parent-side bounded event queue, timeout, throttle and diagnostics semantics.

Pyodide is recommended for the **first lab**, not permanently selected by this research. The lab must prove startup/memory/cancellation/security behavior before the dependency is accepted for production.

### 2.2 Editor direction

Use **Monaco Editor** as the preferred first Engineering editor candidate because the initial EliteSCADA Engineering target is desktop/Windows-oriented and Monaco already provides the primitives needed for:

- Python syntax coloring and editor navigation;
- line numbers;
- model markers for line/column diagnostics;
- custom completion providers;
- hover/signature/help providers where useful;
- multiple text models and future diff workflows;
- mature keyboard-oriented desktop editing.

Monaco does not magically provide Python semantic analysis. EliteSCADA must supply engine-backed syntax diagnostics and its own API completion/stub metadata.

**CodeMirror 6** remains a credible lighter/modular alternative if bundle footprint, mobile support or custom composition later outweigh Monaco's desktop IDE ergonomics.

### 2.3 Security principle

A Web Worker is a responsiveness/isolation primitive, **not by itself a sufficient security sandbox**.

Both Pyodide and MicroPython/WASM can bridge to JavaScript/browser APIs when configured normally. Therefore the security boundary is:

`untrusted Script source -> bounded execution compartment -> narrow versioned EliteSCADA RPC/API -> normal parent/backend authorization`

not:

`untrusted Script source -> scan source for scary import names -> trust interpreter`

The existing `PythonPreflightValidator` remains valuable developer feedback, but the actual sandbox must fail closed even if a user bypasses source scanning through Python introspection or dynamic behavior.

---

## 3. Existing EliteSCADA contracts this design must satisfy

The current merged foundation already constrains the technology choice.

### 3.1 Script scopes

Client Visual and Server scripts are explicitly different scopes. This research covers only `ClientVisual`.

The browser runtime must never accidentally expose Server-only capabilities such as Server Memory writes or shared TAG writes merely because the interpreter technically could call JavaScript.

### 3.2 Client Visual capability surface

The merged `ScriptApiSurface.ClientVisual()` allows conceptually:

- read permitted shared TAGs;
- request an authorized backend operation;
- read Client Memory;
- write Client Memory;
- read declared visual properties;
- write runtime-writable visual properties;
- request visual tweens/animations.

The merged denied-boundary list requires denial of:

- filesystem;
- operating system;
- shell/process execution;
- arbitrary network;
- database;
- industrial drivers;
- secrets;
- browser DOM;
- browser storage.

The browser engine adapter must enforce this capability shape rather than expose a generic Python-to-JavaScript bridge.

### 3.3 Execution policy

The current safe default is already defined as:

- handler timeout: **250 ms**;
- maximum queued events: **128**;
- minimum timer interval: **50 ms**;
- consecutive failures before throttle: **5**;
- default queue behavior: coalesce by event key;
- fault isolation scope: Script Runtime Instance.

The selected engine must fit these contracts or expose a documented gap that is resolved before production. The contracts must not be weakened merely because an engine lacks reliable interruption.

### 3.4 Executor requirement

`IPythonScriptHandlerExecutor` explicitly requires the concrete engine adapter to honor cancellation/abort. The coordinator intentionally does not allow an uncooperative handler to continue as a detached background task after its budget expires.

This makes a deterministic hard-stop/recovery path mandatory.

### 3.5 Diagnostics

Engine-backed syntax validation must map to the existing 1-based `PythonSourcePosition` / `PythonSourceSpan` and `PythonValidationDiagnostic` contracts.

Runtime exceptions must remain sanitized before crossing the sandbox boundary.

### 3.6 Visual runtime API

`IClientVisualObjectApi` already exposes safe object/property operations without DOM access. The Python bridge should adapt to this public semantic surface rather than expose React nodes, selectors, SVG elements or renderer objects.

---

## 4. Browser Python engine comparison

### 4.1 Summary matrix

| Criterion | Pyodide | MicroPython/WASM | RustPython/WASM |
| --- | --- | --- | --- |
| Interpreter lineage | CPython compiled to WebAssembly | MicroPython implementation | independent Python implementation in Rust |
| Browser maturity | high | usable port, narrower web focus | browser demo exists; project still warns about production/fault-intolerant use |
| Python compatibility | strongest of candidates | intentionally reduced/subset-oriented | improving, but not CPython and incomplete stdlib |
| JS interop | extensive | explicit `js` integration | browser module / JS bridge available |
| Worker support | documented/recommended | can be hosted in Worker but official browser docs emphasize execution caveats | possible through WASM host, less turnkey for this use case |
| Cooperative interrupt | documented via `SharedArrayBuffer` + interrupt buffer | official WebAssembly README says browser interrupts are not implemented | no equally mature/documented browser interrupt contract found for this use case |
| Hard kill | `Worker.terminate()` fallback | `Worker.terminate()` | `Worker.terminate()` |
| Runtime/package breadth | broad, including many CPython-compatible packages | intentionally lean | smaller ecosystem/compatibility than Pyodide |
| Runtime footprint | largest | potentially much smaller | potentially smaller than full Pyodide; must benchmark |
| License | MPL-2.0 for Pyodide project, plus bundled third-party/package licenses | MIT core, with third-party licenses per bundled components | MIT |
| Current activity observed in research | active; current release line 314.0.x | active; current v1.29.0 | active, but project maturity warning remains relevant |
| First EliteSCADA lab | **recommended** | fallback/size experiment | watchlist/secondary experiment |

### 4.2 Pyodide

#### Strengths

Pyodide is the strongest first candidate because it provides:

- CPython semantics compiled to WebAssembly;
- documented browser and Worker operation;
- robust Python/JavaScript FFI;
- Python syntax/compile behavior close to what engineers expect from normal Python;
- a documented interrupt mechanism suitable for a Worker;
- configurable JavaScript globals (`jsglobals`);
- explicit JavaScript module registration;
- pinned lock-file/package metadata;
- a mature ecosystem and active release cadence.

At research time the current GitHub release is **314.0.6**, published 2026-08-25. The release contains a `pyodide-core-314.0.6.tar.bz2` asset of about **6.76 MB compressed** and a full distribution archive of about **350 MB compressed**. These archive sizes are not browser resident-memory numbers and must not be used as such.

The large full distribution is a reason to ship only a deliberate runtime/package allowlist, not a reason to reject Pyodide before measuring the minimal deployed set.

#### Important security fact: default `js` is too powerful

Pyodide's `PyodideConfig.jsglobals` defaults to `globalThis` and becomes the object exposed through Python's `js` module.

Therefore a default Pyodide setup does **not** meet EliteSCADA's sandbox contract. A Client Visual Script must not receive the browser Worker global containing `fetch`, storage APIs and other platform capabilities.

Recommended initialization direction:

- provide a minimal/frozen `jsglobals` object rather than `globalThis`;
- do not place generic `fetch`, `WebSocket`, IndexedDB, Cache Storage, crypto credential material or browser handles in that object;
- register only a narrow module such as `elite_scada` through `registerJsModule()`;
- the exact public Python module name remains a future versioned API decision.

#### Package loading risk

Pyodide can install/load packages through `micropip`, `loadPackage()` and package-from-import helpers. `micropip` can obtain wheels from PyPI/CDNs/custom URLs.

That behavior is useful in scientific notebooks and unacceptable as an implicit Client Visual Script capability.

Initial EliteSCADA direction:

- package installation by user scripts is disabled;
- `micropip` is not in the Client Visual package allowlist;
- `loadPackagesFromImports()` is not called on user source;
- package set is selected, pinned, licensed and shipped with the application;
- any later package extension is an Engineering/product decision with explicit version/license/security review.

#### Interrupt model

Pyodide documents browser interruption using:

- execution inside a Web Worker;
- a `SharedArrayBuffer` shared with the controlling thread;
- `pyodide.setInterruptBuffer()`;
- writing signal value `2` to request `KeyboardInterrupt`.

This is a good fit for the current EliteSCADA 250 ms handler budget, but it is not an absolute preemption guarantee. Pyodide documents that C code must periodically check Python signals and JavaScript code called from Python must explicitly call `pyodide.checkInterrupt()` to become interruptible.

Therefore production must implement two stages:

1. **soft deadline:** request interrupt at the configured execution deadline;
2. **hard deadline:** after a small bounded grace period, terminate the Worker if the handler has not completed, then rebuild the Script Runtime Instance deterministically.

The first lab should start with a **50 ms soft-interrupt grace** after the existing 250 ms budget. The 50 ms value is a benchmark hypothesis, not a locked product constant. It must be tuned from measured behavior without allowing an unbounded handler.

No timed-out Worker is left running in the background.

#### Cross-origin isolation dependency

`SharedArrayBuffer` requires a secure, cross-origin-isolated browser context. In practice the deployment must validate appropriate COOP/COEP headers and `crossOriginIsolated` in the actual Runtime/Engineering host.

This is a real packaging/deployment prerequisite, not an editor-only detail.

### 4.3 MicroPython/WASM

MicroPython is attractive for size and constrained-runtime characteristics:

- active project;
- MIT core license;
- official WebAssembly port;
- explicit configurable GC heap in supported host scenarios;
- straightforward JavaScript embedding API;
- likely smaller deployment footprint than a CPython/Pyodide environment.

However the official WebAssembly README states that browser code execution can suspend the browser and that **interrupts have not been implemented for the browser**.

Running MicroPython in a Worker avoids freezing the UI, and `Worker.terminate()` can still provide a hard kill, but the missing cooperative browser interruption makes it a poorer fit for the current `ScriptExecutionLease`/250 ms execution policy.

MicroPython also intentionally implements a lean Python environment rather than full CPython compatibility. That is not intrinsically bad, but it would create a product-level Python dialect/compatibility decision.

**Recommendation:** do not use MicroPython/WASM as the first EliteSCADA lab engine. Keep it as a fallback experiment if Pyodide's memory/startup footprint proves unacceptable and the team is willing to explicitly define a supported Python subset.

### 4.4 RustPython/WASM

RustPython is architecturally interesting:

- MIT licensed;
- written in Rust;
- embeddable;
- can run in the browser as WebAssembly;
- can pass controlled serialized values/functions between JavaScript and Python.

It is not the first choice for EliteSCADA because its own project material still describes the interpreter as being in development and not suitable for production/fault-intolerant settings, and it does not currently present as mature a browser interruption/deployment contract for this use case as Pyodide.

**Recommendation:** watchlist/secondary technical experiment only.

### 4.5 Why a custom Python interpreter is not recommended now

Building a custom parser/interpreter merely to obtain a small sandbox would transfer a large, security-sensitive language-maintenance burden into EliteSCADA.

That option should be revisited only if all maintained browser Python engines fail measurable safety/footprint requirements.

---

## 5. Code editor comparison

### 5.1 Monaco Editor

Observed current direction during research:

- MIT license;
- browser editor that powers VS Code;
- current site advertises version **0.55.1**;
- supports modern desktop Edge/Chrome/Firefox/Safari/Opera;
- explicitly does not target mobile browsers/mobile web frameworks;
- supports custom completion providers;
- exposes model markers suitable for diagnostics;
- supports diff/model workflows useful for future Script revision comparison.

This aligns well with EliteSCADA's current Engineering and Windows x64 preview direction.

#### Monaco limitation to keep explicit

Monaco's Python syntax highlighting is not a complete Python language server. It does not by itself provide authoritative Python semantic errors or knowledge of the EliteSCADA API.

EliteSCADA must provide:

- engine-backed compile diagnostics;
- deterministic Engineering/preflight diagnostics;
- custom completions/stubs for the EliteSCADA API;
- optional future semantic analysis only after benchmarking a language-service strategy.

### 5.2 CodeMirror 6

CodeMirror 6 remains a credible alternative because it is modular and has first-class extension points for:

- Python grammar highlighting;
- completions;
- lint/diagnostic decorations;
- custom commands/keymaps;
- a more deliberately assembled editor bundle.

It may win later if a smaller bundle, mobile support or custom UI integration becomes more important.

The cost is more EliteSCADA-owned assembly for an IDE-like experience and the same basic issue that Python semantic knowledge must come from another layer.

### 5.3 Editor recommendation

First lab:

`Monaco + generated EliteSCADA completion provider + engine/preflight diagnostic markers`

Do **not** add Pyright or another heavy Python language service in the first slice by default. First prove that:

- syntax highlighting;
- engine compile diagnostics;
- API completion generated from public contracts;
- signature/help metadata;
- reference lookup/navigation

are sufficient for the initial Engineering workflow.

If richer semantic analysis is needed, evaluate a browser-compatible static analyzer as a separate measured dependency rather than quietly importing a second Python toolchain.

---

## 6. Recommended browser architecture

### 6.1 Logical layers

```text
Engineering / Runtime UI (React)
        |
        | typed messages / request IDs
        v
Client Script Host (TypeScript, trusted parent)
        |
        | bounded Script events + narrow API RPC
        v
Dedicated module Worker for one active Script Runtime Instance
        |
        +-- Pyodide engine adapter
        +-- minimal jsglobals
        +-- registered elite_scada bridge module
        +-- no DOM
        +-- no credentials/secrets
        +-- no unrestricted network
        +-- no host filesystem/browser storage surface
```

The host owns policy; Python owns script execution only.

### 6.2 Isolation granularity

The existing contract says fault isolation is `ScriptRuntimeInstance`.

The safest semantic mapping is therefore:

**one lazily-created engine Worker per active Script Runtime Instance.**

Advantages:

- a hard kill affects only one Script Runtime Instance;
- module globals/runtime state belong unambiguously to that script instance;
- timeout recovery can recreate one instance;
- disposal is deterministic;
- there is no co-tenant script left inside an interpreter that was compromised or wedged by another script.

Cost:

- each Pyodide Worker has meaningful memory/startup overhead.

This overhead is the most important item to benchmark before production.

If per-script Workers are too expensive, EliteSCADA must make an explicit architecture change rather than silently pooling scripts and weakening `ScriptFaultIsolationScope.ScriptRuntimeInstance`.

A later engine could provide multiple safely resettable interpreter compartments inside one Worker, but that must be demonstrated, not assumed.

### 6.3 Lazy lifecycle

Recommended lifecycle:

1. no interpreter is created merely because a Script exists in Engineering;
2. when an enabled Script Runtime Instance becomes active, create its Worker lazily;
3. load the pinned engine asset set;
4. initialize restricted host bridge and compile source;
5. process one handler at a time from the existing bounded event queue;
6. on visual instance/script disposal, cancel current execution, terminate Worker and discard runtime globals;
7. reopening the visual definition creates a fresh runtime instance according to existing visual-runtime semantics.

Hidden/closed visuals do not retain orphan Python Workers or timers unless a future explicit scope requires it.

### 6.4 Why not run Python on the React/main thread

Even a legitimate long-running pure Python calculation would block rendering/event processing. The current product contract explicitly requires a faulty script not to freeze the HMI.

Therefore main-thread execution is forbidden even for Preview.

---

## 7. Execution budget and cancellation design

### 7.1 Parent remains authoritative for deadlines

The JavaScript/TypeScript host creates the execution ID and deadline from `ScriptExecutionPolicy`.

Python code does not choose or extend its own deadline.

### 7.2 Dispatch sequence

For one queued event:

1. dequeue according to existing bounded/coalescing policy;
2. send `DispatchEvent` with execution ID, script runtime identity, handler name, target/event payload and deadline;
3. Worker invokes exactly the declared handler;
4. host records completion/fault/cancel/timeout;
5. next event is not dispatched concurrently for that Script Runtime Instance.

### 7.3 Timeout sequence for Pyodide

At deadline:

1. write SIGINT request into the Pyodide interrupt buffer;
2. mark execution as cancellation-requested;
3. allow only a small configured grace window;
4. if the handler returns, classify according to current cancellation/timeout contract;
5. if it does not return, call `Worker.terminate()`;
6. mark execution `TimedOut`;
7. discard that interpreter state;
8. throttle/fault-count according to current diagnostics policy;
9. recreate lazily only when policy permits further execution.

A killed Worker has no continuation task left behind.

### 7.4 Caller/disposal cancellation

Client navigation/disposal uses the same cancellation channel but is classified as `Cancelled`, not `TimedOut`, matching the existing coordinator semantics.

Hard termination is allowed if a disposal cancellation is not acknowledged quickly.

### 7.5 Frame tick

`FrameTick` should remain **disabled by default in the first production Client Visual scripting slice**.

Normal visual motion belongs to the existing tween/animation scheduler. Python should not be used as a 60-fps animation loop.

If FrameTick is enabled later:

- frequency is explicitly bounded;
- callbacks coalesce when the interpreter is busy;
- missed frames are not queued indefinitely;
- a script cannot request an arbitrary tighter cadence.

---

## 8. Event queue, backpressure and recursion

Keep event admission **outside Python** using the already-defined queue policy.

The Worker should normally see at most one active handler invocation at a time for a Script Runtime Instance.

Required behavior:

- capacity remains bounded;
- coalescing uses stable event keys;
- high-rate TAG changes replace stale pending events where policy allows;
- timer minimum remains enforced before Python;
- property-change recursion is detected/bounded by host rules;
- a script cannot enqueue arbitrary unbounded events by calling internal host primitives;
- outbound API requests from Python have their own bounded pending-request table;
- disposal rejects late messages from the old runtime instance.

Every message includes identity fields so a response from a terminated/replaced Worker cannot mutate a new visual runtime instance.

---

## 9. Worker protocol proposal

Exact names are implementation details, but the protocol should be explicit and versioned.

### 9.1 Parent to Worker

Candidate message kinds:

- `InitializeScript`
- `CompileSource`
- `DispatchEvent`
- `ApiResponse`
- `CancelExecution`
- `DisposeScript`

### 9.2 Worker to parent

Candidate message kinds:

- `Ready`
- `CompileResult`
- `ExecutionResult`
- `ApiRequest`
- `Diagnostic`
- `Disposed`

### 9.3 Required envelope fields

At minimum:

- bridge protocol version;
- Script stable ID;
- Script Runtime Instance ID;
- visual Runtime Instance key where applicable;
- execution/request ID;
- event/handler identity where applicable.

Use structured-clone-compatible data only. Never send live DOM nodes, JS functions from application internals, database objects, JWTs or resolved secrets.

Unknown protocol versions or mismatched runtime IDs fail closed.

---

## 10. EliteSCADA Python API injection boundary

### 10.1 Principle

Expose a semantic module, not browser internals.

Conceptually:

```python
from elite_scada import tags, client_memory, visual, animation, actions
```

The spelling above is illustrative and must be versioned later.

### 10.2 TAG reads

Allowed Client Visual script operation:

- read a permitted shared TAG snapshot/value/quality through the parent runtime surface.

The parent resolves access through the logged-in client's existing authorization context.

Do not send the entire project TAG database into Python globals merely for convenience.

### 10.3 Client Memory

Allowed:

- read/write only the current client's Client Memory through the current client store.

Client Memory cannot grant backend authority and cannot be presented as global server truth.

### 10.4 Visual objects/properties

Map Python operations to `IClientVisualObjectApi` semantics:

- list/find an object in the current visual runtime instance;
- read declared property;
- write only runtime-writable property;
- clear script override;
- request a tween through the renderer scheduler.

No DOM selector, React component, SVG node, Canvas context or renderer-private handle crosses the bridge.

### 10.5 Authorized backend actions

A Client Visual Script may request an operation, but it does not receive credentials or perform `fetch` itself.

Flow:

`Python intent -> trusted parent bridge -> normal backend API -> existing authorization/audit -> result`

Examples include process write, command execution or alarm operation only when those operations are deliberately exposed by the future public Script API.

The backend continues to derive identity from trusted authentication. Python cannot supply a stronger actor/principal.

### 10.6 Async behavior

Host-mediated operations should be designed as asynchronous calls. The bridge must not introduce main-thread blocking waits.

### 10.7 Capabilities are scope-derived

The generated Python API/stubs for `ClientVisual` omit Server-only calls entirely.

A runtime capability check still occurs even if a user manually constructs a name that is not advertised by autocomplete.

---

## 11. Denied capabilities and defense in depth

### 11.1 Runtime privilege denial

The execution compartment must not have useful privilege even if Python source filtering fails.

Required controls:

- restricted `jsglobals`;
- no generic `fetch`/WebSocket bridge;
- no app auth token in Worker memory;
- no resolved secrets;
- no DOM/window/document;
- no File System Access handles;
- no IndexedDB/Cache Storage bridge;
- no driver/database objects;
- no Node/Electron host APIs;
- no shell/process APIs;
- network policy at Worker/host level;
- only structured messages to trusted parent.

### 11.2 Python import/builtin policy

Keep the existing denied imports/calls as developer-facing preflight and add engine-side enforcement for the user execution namespace.

Initial user policy should deny or not package direct access to at least:

- `js` as an unrestricted browser module;
- `pyodide_js` / low-level Pyodide host APIs;
- `micropip`;
- arbitrary HTTP/socket clients;
- OS/process/subprocess/import machinery that violates public policy;
- direct `open`, dynamic `exec`/`eval`/`compile` and `__import__` from user code where the public contract forbids them.

Important: Python import hooks and restricted builtins are **defense in depth**, not the ultimate security boundary. Python introspection makes source-level blacklists insufficient as a host security model.

### 11.3 Emscripten filesystem

Pyodide has an internal Emscripten filesystem. Its existence must not be misrepresented as permission to access the user's machine/project filesystem.

Initial direction:

- no native/local directory mounts;
- no OPFS/IDBFS persistence for Client Visual Scripts;
- no File System Access handles;
- no browser project files exposed by path;
- any interpreter-internal temporary files are disposable sandbox internals.

User-facing `open()` remains denied by the Client Visual public contract.

---

## 12. Browser network/CSP containment

### 12.1 Arbitrary network is forbidden

Because Pyodide can otherwise use browser networking, denial must be enforced below Python source.

Recommended lab architecture:

1. the initial module Worker is controlled by EliteSCADA;
2. Pyodide runtime assets are self-hosted, pinned and served from a dedicated **static-only asset origin** with no application APIs and no credentials;
3. the Worker response uses a strict CSP that permits only the script/WASM/static resources needed by the interpreter and does not permit the EliteSCADA backend origin as a `connect-src` target;
4. cross-origin engine asset requests use explicit CORS/CORP as required by COEP;
5. user code receives no generic networking bridge;
6. security tests verify that Python cannot reach arbitrary Internet hosts, loopback services or EliteSCADA backend endpoints directly.

This design is stronger than `connect-src 'self'` when the application backend shares the main UI origin, because `'self'` would otherwise authorize Python to call application endpoints.

### 12.2 WebAssembly CSP

Browsers can require the CSP source expression `'wasm-unsafe-eval'` to allow WebAssembly compilation while still avoiding general `'unsafe-eval'`.

The implementation spike must verify the exact CSP required by the pinned engine/browser and avoid broadening to `'unsafe-eval'` merely for convenience.

### 12.3 Cross-origin isolation

Pyodide interrupts require `SharedArrayBuffer`, which in turn requires cross-origin isolation.

The packaging spike must verify:

- `Cross-Origin-Opener-Policy: same-origin`;
- `Cross-Origin-Embedder-Policy: require-corp` or an explicitly justified alternative;
- CORP/CORS on static runtime assets;
- `crossOriginIsolated === true` in both relevant window/Worker context;
- compatibility with the existing app's fonts/images/WebSocket/API resources.

Do not discover after editor implementation that deployment headers make interruption impossible.

### 12.4 Separate-origin hardening

A stronger future compartment can host the interpreter inside a separate non-credentialed sandbox origin and communicate through validated `postMessage`/`MessagePort` RPC.

This can isolate cookies/storage from the application origin, but cross-origin embedding plus SharedArrayBuffer/COOP/COEP behavior must be proven in target browsers before it becomes a dependency.

Do not make the current design depend on experimental `credentialless` iframe support. It is a potential hardening path, not the baseline.

---

## 13. Package/module policy

### 13.1 Initial rule

Client Visual Script package set is **closed and versioned**.

User scripts cannot `pip install`, use `micropip`, fetch wheels or import arbitrary remote code.

### 13.2 Allowlist criteria

A future package may be added only when:

- required for a real HMI use case;
- license is compatible and recorded;
- package/WASM build is pinned;
- transitive dependencies are reviewed;
- package does not expose a denied boundary;
- startup/memory impact is measured;
- cancellation behavior is tested;
- offline package is included in release integrity metadata.

For initial visual scripting, the standard language plus the EliteSCADA API should be enough. Engineers should not need NumPy to change a pump symbol color.

### 13.3 Version marker

Script Engineering already carries a language/version marker. Future canonical integration must distinguish:

- public EliteSCADA Script API version;
- Python compatibility/version marker;
- engine implementation/version as runtime compatibility metadata, not Script identity.

Do not bake `pyodide-314.0.6` into the public Script schema as if it were the language.

---

## 14. Source editing, compile validation and diagnostics

### 14.1 Diagnostic layers

Use four distinct layers.

#### Layer A: editor lexical feedback

Monaco supplies syntax coloring, bracket behavior and local editor functions.

This is not authoritative compile validation.

#### Layer B: engine compile diagnostics

On a debounce after source changes, send a **compile-only** request to the sandbox engine.

The privileged engine adapter may internally compile source even though user-level `compile()` is denied.

For Python `SyntaxError`, capture at least:

- message;
- line;
- offset/column;
- end line/end offset where available;
- stable diagnostic code generated by the adapter.

Convert to the existing 1-based `PythonValidationDiagnostic` model and apply Monaco model markers.

#### Layer C: deterministic EliteSCADA preflight/Engineering validation

Run existing checks for:

- denied imports/calls;
- Script scope/event validity;
- entry-point identifier validity;
- Engineering Script references/dependencies.

The server/canonical Preview remains authoritative for Engineering acceptance once canonical Script integration is implemented.

#### Layer D: preview execution diagnostics

A script can compile but still fail at runtime. Preview records:

- handler;
- runtime line/trace location where safely available;
- timeout/cancel/fault status;
- sanitized message;
- duration;
- throttling/failure counters.

### 14.2 Debounce/cancellation

Compile jobs should be debounced and superseded when the source changes again. Never queue dozens of obsolete compiles while the engineer types.

### 14.3 Source filename

Give each compile/execution a stable synthetic filename based on Script identity/path so tracebacks map back to the correct editor model without exposing host filesystem paths.

Example concept:

`elite://script/<stable-id>/<path>.py`

Exact URI syntax is an implementation decision.

---

## 15. Autocomplete and stubs

### 15.1 Do not hand-maintain a second API list

Generate completion metadata from public EliteSCADA Script API descriptors and visual property schemas.

The same source should drive:

- runtime capability dispatch;
- editor completions;
- help/signature metadata;
- generated `.pyi`-style stubs where useful;
- API documentation tests.

### 15.2 Completion content

Initial completions should include:

- EliteSCADA module namespaces/functions;
- current Script scope capabilities;
- handler/event signatures;
- visual object developer keys in the current definition where appropriate;
- declared property names/types from the public visual property schema;
- TAG/Client Memory references filtered to the current Engineering context/permissions where safe;
- tween/easing/options enums.

Do not put secrets, raw browser endpoints or auth headers into completion payloads.

### 15.3 `.pyi` direction

Generate a versioned stub package for editor/static-analysis use. Stubs are an editor artifact derived from the public API, not a second runtime contract.

If a future Pyright-like analyzer is added, it consumes these same generated stubs.

---

## 16. Script event/entry-point authoring UX

The editor should make association explicit outside the source text:

- Script scope shown prominently;
- list of declared entry points;
- event kind;
- target reference where applicable;
- handler function name;
- validation that handler exists/compiles;
- visual definition/object association;
- dependency/reference issues.

Do not require users to encode event registration into unrestricted Python startup code.

This preserves deterministic subscriptions and enables import/export/reference validation.

---

## 17. Test/Preview semantics

### 17.1 Preview is not Active Engineering

A script test runs against a **disposable Preview Runtime Instance** built from the Working/preview snapshot.

Script writes during Preview may alter only:

- preview visual runtime overrides;
- preview-local Client Memory;
- explicitly simulated/mock API results.

They do not mutate:

- immutable Engineering revisions;
- Published/Active revision;
- production Client Memory belonging to another Runtime Client;
- Server Memory;
- process TAGs/devices by default.

### 17.2 Shared TAG reads

Preview may optionally read current shared TAG values through the parent when the engineer has normal read permission. Those values are snapshots/runtime inputs, not Engineering configuration.

A deterministic mock/snapshot mode must also exist so Preview can be repeated without a live PLC.

### 17.3 Writes/commands during Preview

Default Preview policy:

- process writes: deny/simulate;
- operational commands: deny/simulate;
- alarm mutations: deny/simulate;
- other backend side effects: deny/simulate.

A future explicit **live authorized test** mode, if product-approved, must require deliberate arming/confirmation and use the normal backend authorization/Audit path. It must never be the accidental consequence of clicking Run in the editor.

### 17.4 Preview lifecycle

Each Preview run has:

- bounded duration;
- disposable Worker/Script runtime identity;
- bounded events;
- explicit Reset;
- output/diagnostic limits;
- no persistence of interpreter globals after Preview closes unless a future explicit session feature is designed.

---

## 18. Offline packaging and cache/version strategy

### 18.1 No CDN requirement

The Windows x64 validation preview and later Runtime Client must work from a controlled local deployment without Internet access.

Therefore:

- vendor the exact approved Pyodide runtime assets;
- vendor only approved package wheels/files;
- serve from the product's static packaging infrastructure / static-only asset origin;
- pin versions;
- include SHA-256/integrity manifest for shipped assets;
- verify the manifest before release/package assembly;
- never use unversioned `dev` URLs.

### 18.2 Avoid shipping the full Pyodide distribution by default

The current full release archive is hundreds of MB because it contains a broad package catalog. EliteSCADA should package a minimal runtime set plus explicitly approved packages.

The first lab must record the real compressed transfer size, extracted/on-disk size and browser memory after initialization for that minimal set.

### 18.3 Cache invalidation

Use immutable engine asset paths keyed by product/engine version, for example conceptually:

`/python-runtime/pyodide/<pinned-version>/<content-hash>/...`

A new product build points at the new immutable manifest. Do not silently replace the bytes behind the same cache key.

### 18.4 Browser caching

Normal HTTP/browser cache can warm immutable assets. A Service Worker should not be introduced merely to cache Python unless a later packaging requirement justifies the additional security/lifecycle surface.

### 18.5 Local preview headers

The Windows launcher/static server must be able to emit the required:

- WASM MIME type;
- CSP;
- COOP/COEP/CORP/CORS headers;
- immutable cache headers.

This must be smoke-tested through the packaged path, not only Vite dev mode.

---

## 19. Memory/resource limits

### 19.1 Important limitation

A normal browser Web Worker does not provide EliteSCADA a portable, precise hard per-Worker memory quota comparable to an OS process/job/container limit.

The product must not claim a hard 32/64/128 MB script memory cap unless the chosen engine/build/runtime can actually enforce it.

### 19.2 Initial controls

Until a hard engine memory ceiling is demonstrated:

- bound Script source size;
- bound event payload size;
- bound bridge result/argument serialization size;
- bound queued event count;
- bound outstanding API RPC count;
- do not load arbitrary packages;
- destroy PyProxy/bridge objects deterministically;
- terminate Worker after timeout/fatal fault;
- terminate Worker on visual/script disposal;
- collect benchmark memory telemetry where browser APIs permit;
- detect gross WASM memory growth through engine adapter telemetry where stable.

### 19.3 If a hard memory cap is required

Evaluate, in order:

1. a Pyodide/Emscripten build with deliberately bounded WebAssembly memory growth, if maintainable and compatible with required packages;
2. a different browser interpreter with enforceable heap limits;
3. a stronger external process sandbox for the affected client architecture.

Do not invent a fake quota in UI while the browser can exceed it.

---

## 20. Logging/output limits

`print()`/stdout/stderr are developer conveniences and potential denial-of-service channels.

Initial direction:

- redirect stdout/stderr to the sandbox host;
- cap bytes/lines per execution and per Script Runtime Instance;
- truncate with an explicit diagnostic marker;
- never forward output directly to browser console as the only diagnostic store;
- never allow output to contain unsanitized host exception internals or secrets;
- do not persist unbounded Script logs in browser storage.

---

## 21. Security threat scenarios and required tests

The implementation spike must intentionally attempt to break the sandbox.

### 21.1 Host/browser escape tests

User code attempts:

- `import js` then access `fetch`, `WebSocket`, `indexedDB`, `caches`, `document`, `window`;
- import low-level Pyodide JS modules;
- access `micropip`/package loading;
- mount native filesystem handles;
- inspect Python object graphs/builtins to recover denied bridge objects;
- access browser storage;
- open arbitrary URL/network connection;
- call EliteSCADA `/api` directly;
- call loopback/localhost services directly;
- access a secret/JWT/cookie value.

Expected: no useful capability is obtained.

### 21.2 Authorization tests

A script running for a user without a backend capability attempts an authorized-action RPC.

Expected: parent/backend rejects it exactly as a normal UI/API request; Python cannot override actor identity.

### 21.3 Timeout tests

Scripts:

- `while True: pass`;
- deep computation;
- long C/WASM operation if an approved package introduces one;
- JavaScript bridge operation that intentionally fails to return.

Expected:

- soft interrupt requested at deadline;
- hard Worker kill if required;
- no detached execution survives;
- timeout count increments;
- unrelated Runtime Clients continue;
- affected Script Runtime Instance can be deterministically recreated or throttled.

### 21.4 Event flood tests

Generate far more than 128 rapid TAG/property events.

Expected:

- queue remains bounded;
- configured coalescing/rejection counters are correct;
- latest coalescible value wins where policy says so;
- UI remains responsive;
- no memory-linear backlog.

### 21.5 Disposal races

Close a Popup/Screen while a handler and API request are active.

Expected:

- cancellation/termination;
- late worker/RPC responses ignored by runtime identity;
- no visual property write reaches a replacement instance;
- subscriptions/timers/Worker disappear.

### 21.6 Preview safety tests

Run a Preview script that requests:

- process write;
- command;
- Client Memory write;
- visual property write.

Expected default behavior:

- process/command side effects simulated or denied;
- only Preview Client Memory/visual state changes;
- Active Engineering/revision unchanged.

---

## 22. Performance/compatibility benchmark plan

Do not choose the engine from a single `print('hello')` demo.

### 22.1 Reference environments

At minimum test:

- primary Windows x64 packaged Chromium target;
- current Chrome/Edge release used by CI/product preview;
- current Firefox where the Runtime Client is expected to be supported;
- Safari only if/when it is in product support scope.

### 22.2 Engine startup measurements

For each candidate:

- cold asset bytes transferred;
- cold initialization time;
- warm-cache initialization time;
- WebAssembly/interpreter memory after idle initialization;
- incremental memory per additional active Script Runtime Instance;
- teardown/reclamation behavior after Worker termination.

### 22.3 Compile/editor measurements

Use representative source sizes such as:

- 1 KB simple handler;
- 10 KB realistic Screen/Dynamo behavior;
- 100 KB stress source.

Measure:

- debounce-to-diagnostic latency;
- exact line/column accuracy;
- cancellation of obsolete compile request;
- editor responsiveness while validation runs.

### 22.4 Runtime measurements

Measure:

- no-op handler dispatch latency;
- TAG read bridge roundtrip;
- Client Memory read/write bridge roundtrip;
- visual property read/write bridge roundtrip;
- tween request latency;
- authorized backend request mediation latency separately from Python execution;
- repeated handler throughput with queue coalescing.

### 22.5 Fault/timeout measurements

Measure:

- time from 250 ms deadline to soft interrupt handling;
- time from deadline to hard Worker termination when uncooperative;
- time to recreate a failed Script Runtime Instance;
- collateral impact on UI frame/event responsiveness;
- memory before/after repeated kill/recreate cycles.

### 22.6 Concurrency measurements

Benchmark realistic combinations such as:

- 1 active script;
- 5 active scripts;
- 10 active scripts;
- 25 active scripts;
- 50 active scripts if memory permits.

This is specifically needed to validate the per-Script Runtime Instance Worker recommendation.

### 22.7 Initial acceptance principles

The lab passes only if:

- Python never executes on the UI thread;
- UI stays interactive during infinite-loop/timeout tests;
- uncooperative execution is hard-terminated in bounded time;
- no forbidden network/storage/DOM/backend access succeeds;
- event backlog remains bounded;
- disposal leaves no orphan Worker/timer/subscription;
- line/column diagnostics are deterministic;
- packaged/offline startup works without Internet/CDN;
- measured memory/startup cost is acceptable for the expected active-script count.

Do not lock arbitrary performance numbers until reference hardware measurements exist.

---

## 23. Suggested implementation/lab slices after canonical Script integration

Production work remains blocked until the coordinator completes canonical Script package/schema integration.

When that prerequisite clears, use small slices.

### Slice A - engine packaging and interruption spike

- no editor UI yet;
- pinned minimal Pyodide assets;
- module Worker;
- cross-origin isolation headers;
- compile/run simple source;
- SharedArrayBuffer interrupt;
- hard Worker termination fallback;
- collect cold/warm/memory benchmark.

**Exit:** prove 250 ms policy can be enforced without blocking UI.

### Slice B - hardened sandbox host

- restricted `jsglobals`;
- no direct generic JS/global bridge;
- strict Worker CSP/network policy;
- package loading disabled;
- bounded stdout/stderr;
- security escape tests.

**Exit:** forbidden capability test suite passes.

### Slice C - typed EliteSCADA client API bridge

- generated/versioned bridge manifest;
- shared TAG read;
- Client Memory read/write;
- visual object/property API;
- tween requests;
- parent-mediated authorized operation request abstraction;
- runtime IDs/request IDs/cancellation.

**Exit:** no direct DOM/backend/driver path exists.

### Slice D - engine validator + diagnostics adapter

- engine compile-only validation;
- `SyntaxError` line/end-column mapping;
- merge with deterministic preflight diagnostics;
- sanitized runtime traceback adapter.

**Exit:** `IPythonEngineValidator` semantics are fulfilled.

### Slice E - Monaco Engineering editor shell

- Script list/editor UI;
- source model;
- syntax highlighting/line numbers;
- markers;
- scope/event association;
- custom completion provider;
- generated stubs/help.

**Exit:** practical code editing without direct execution side effects.

### Slice F - controlled Preview

- disposable Preview Runtime Instance;
- mock/snapshot TAG context;
- Preview Client Memory and visual state;
- dry-run backend actions;
- timeout/throttle/output diagnostics;
- Reset/Dispose.

**Exit:** Preview cannot mutate Active Engineering/process by default.

### Slice G - runtime Client integration

- load active canonical Script definitions;
- instantiate Workers with visual runtime lifecycle;
- route real permitted TAG/Client Memory/events;
- expose runtime Script diagnostics;
- E2E disposal/fault isolation.

**Exit:** Client Visual scripting is an actual product capability.

Only after these prerequisites are stable should graphical Screen/Popup/Dynamo editing consume Script associations as a production feature.

---

## 24. INTEGRATION REQUIRED

Research intentionally leaves these coordinator/future integration hooks unresolved:

1. Complete first-class canonical Script Engineering integration before production editor/runtime implementation.
2. Add canonical Script API/version compatibility metadata without encoding one interpreter implementation as the language.
3. Implement a frontend/client Script Host adapter that consumes merged `ScriptExecutionPolicy`, queue/diagnostic and visual runtime contracts.
4. Map the browser engine adapter to `IPythonScriptHandlerExecutor`/`IPythonEngineValidator` semantics.
5. Generate autocomplete/stub descriptors from canonical Script API and public visual property schema.
6. Provide TAG/Client Memory/reference discovery through authoritative Engineering/runtime surfaces.
7. Add the deployment headers/static-asset packaging needed for WASM and SharedArrayBuffer.
8. Decide exact CSP/static asset origin after a real target-browser lab.
9. Add protected runtime diagnostics/UI integration for Script state and faults.
10. Coordinate Script Preview authorization/Audit behavior with existing secured mutation/process-operation boundaries.
11. Benchmark per-Script Worker memory and approve an isolation/concurrency model before production.
12. Revisit exact engine/editor versions during implementation; research versions are evidence, not permanent locks.

---

## 25. Decisions explicitly deferred

This research does not lock:

- final Pyodide production version;
- exact public Python module/API names;
- exact Monaco package/version;
- exact hard memory quota;
- exact number of simultaneous active Script Workers;
- exact soft-interrupt grace duration;
- whether a stronger separate-origin interpreter frame is adopted later;
- whether a static analyzer such as Pyright is later justified;
- optional third-party Python package set;
- Server Python technology.

Those require implementation measurements or separate server-security design.

---

## 26. Research conclusions against CompletionCriteria

1. **Browser engine:** Pyodide is preferred for first lab; MicroPython/WASM is size-oriented fallback; RustPython remains watchlist.
2. **Editor:** Monaco preferred for first desktop Engineering lab; custom EliteSCADA completions/markers required; CodeMirror 6 is fallback.
3. **Isolation:** dedicated module Worker per active Script Runtime Instance is the semantic baseline; lazy lifecycle; bounded queue; soft interrupt + hard kill.
4. **API/security:** narrow generated `elite_scada`-style bridge, no generic JS/DOM/network/storage/driver/database/secrets, normal parent/backend authorization.
5. **Validation:** editor + engine compile + existing preflight/Engineering validation + Preview runtime diagnostics, all mapped to 1-based line/column contracts.
6. **Preview:** disposable preview instance, mock/snapshot context, side effects denied/simulated by default, never silently mutates Active Engineering.
7. **Offline:** pinned self-hosted minimal engine assets, integrity manifest, immutable cache keys, required WASM/COOP/COEP/CSP headers in packaged Windows path.
8. **Benchmarks/tests:** concrete startup, memory, diagnostics, dispatch, timeout, concurrency, security escape, event flood, disposal and offline-package matrix defined above.
9. **Future slices:** staged implementation path defined after canonical Script integration.
10. **Production changes:** none in this research branch beyond this documentation file.

---

## 27. Sources consulted

### EliteSCADA repository contracts

- `PROJECT GOAL.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `src/Scada.Engineering/VisualScripting/PythonScriptingContracts.cs`
- `src/Scada.Engineering/VisualScripting/PythonValidation.cs`
- `src/Scada.Engineering/VisualScripting/ScriptRuntimeExecutionCoordinator.cs`
- `src/Scada.Engineering/VisualScripting/VisualRuntimeFoundation.cs`
- `src/Scada.Engineering/Scripts/ScriptEngineeringContracts.cs`

### Pyodide

- Official project: https://pyodide.org/en/stable/
- Current GitHub project/release metadata: https://github.com/pyodide/pyodide
- Web Worker guidance: https://pyodide.org/en/stable/usage/webworker.html
- Interrupt guidance: https://pyodide.org/en/stable/usage/keyboard-interrupts.html
- JavaScript API / `jsglobals` / lockfile / module APIs: https://pyodide.org/en/stable/usage/api/js-api.html
- Package loading: https://pyodide.org/en/stable/usage/loading-packages.html
- Downloading/self-hosting: https://pyodide.org/en/stable/usage/downloading-and-deploying.html

### MicroPython

- Project: https://github.com/micropython/micropython
- WebAssembly port: https://github.com/micropython/micropython/blob/master/ports/webassembly/README.md
- Release/license documentation: https://docs.micropython.org/en/v1.29.0/license.html

### RustPython

- Project: https://github.com/RustPython/RustPython
- Project site / WebAssembly direction: https://rustpython.github.io/

### Editors

- Monaco: https://microsoft.github.io/monaco-editor/
- Monaco API: https://microsoft.github.io/monaco-editor/typedoc/
- CodeMirror: https://codemirror.net/

### Browser isolation/security

- Worker constructor/security: https://developer.mozilla.org/en-US/docs/Web/API/Worker/Worker
- SharedArrayBuffer: https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/SharedArrayBuffer
- Cross-Origin-Opener-Policy: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Cross-Origin-Opener-Policy
- Cross-Origin-Embedder-Policy: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Cross-Origin-Embedder-Policy
- Content-Security-Policy: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy

---

## 28. Product status

**MERGED:** no change from this research.

**RESEARCH IN BRANCH:** browser Python/editor/sandbox technology direction described here.

**SPECIFIED / NOT IMPLEMENTED:** canonical Script integration, production Python engine, Script editor, sandbox Preview, client Script Runtime integration, graphical Screen/Popup/Dynamo editor and Server Python runtime.
