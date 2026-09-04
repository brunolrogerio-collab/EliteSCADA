# W14-C12 — Server Runtime Automation / Generic Simulation Authoring

**Package:** W14-C12  
**Exact base:** `2607e03d5445eefe1f434495d0ee81136c6cd220`  
**Findings:** `C11-P2-SCR-01`, `C11-P2-SIM-01`

## Objective

Make Server Engineering Scripts executable as part of the persisted Active Runtime lifecycle, using the existing public runtime TAG boundary and the existing bounded `ScriptRuntimeExecutionCoordinator` rather than any DEMO-specific simulator.

The resulting generic data path is:

`Timer/event -> declared TAG dependencies -> Server Script calculation -> official runtime write -> CurrentTagCache/event bus -> alarms/historian/realtime/bindings`

## Active lifecycle

The production host is entered only by the persisted Active lifecycle paths:

- `PublishedRuntimeActivationService`, using the exact Published revision that is committed as Active;
- `PersistedRuntimeRecoveryService`, using the exact persisted Active revision recovered at startup.

Both paths call `ServerScriptRuntimeManager.ActivateRuntimeAsync(projectKey, revision, package, ...)`. Working/unpublished Engineering state is never used as the script execution source.

The manager owns two independent synchronization boundaries:

- a lifecycle gate serializes Active generation replacement;
- a revision gate serializes runtime activation against every Server Script TAG read/write.

Every script read/write verifies the exact `(projectKey, revision)` reported by `IEngineeringRuntimeCoordinator.Describe()` while holding the revision gate. An execution from revision N therefore cannot finish late and write into revision N+1 merely because the new revision retained the same stable TAG ID.

Activation order is:

1. validate Server Script definitions and Timer minimums;
2. acquire lifecycle + revision boundaries;
3. activate the target Engineering Runtime revision, including the normal persistence commit callback when publishing;
4. only after successful activation, install the new Server Script generation and cancel the previous generation;
5. release the revision boundary;
6. drain/cancel the previous generation;
7. run `Initialize` for the new generation;
8. subscribe `TagChanged` handlers;
9. start periodic Timers.

If runtime activation fails, the prior Server Script generation remains the active generation. If Server Script startup fails after a successful runtime activation, that new script generation is cancelled and diagnostics surface the failure rather than silently running Working state or falling back to a DEMO driver.

## Runtime architecture

`ServerScriptRuntimeManager` creates one `ScriptRuntimeExecutionCoordinator` per enabled `ScriptEngineeringScope.Server` definition.

The existing coordinator remains authoritative for:

- bounded queues;
- event coalescing/overflow policy;
- per-handler serialization;
- handler timeout;
- cancellation;
- failure isolation;
- consecutive-failure throttling;
- execution diagnostics.

The host adds the missing lifecycle/event layer:

- `Initialize` once per Active generation;
- repeating `Timer` entry points through `PeriodicTimer`;
- `TagChanged` from the canonical `IScadaEventBus` / `TagValueChanged` stream;
- `DispatchRuntimeEventAsync` for generic `ServerRuntimeEvent` entry points;
- `Dispose` on orderly host shutdown;
- complete generation cancellation on Active project/revision replacement.

`Initialize` completes before TAG event subscriptions and Timers are started, so initialization writes do not recursively trigger the new generation's configured `TagChanged` handlers before initialization has completed.

## Python execution and sandbox

`IsolatedPythonScriptHandlerExecutor` launches the fixed `ServerScriptRunner.py` in an isolated Python process with shell execution disabled and Python `-I -S` isolation flags. The child receives only:

- script source;
- normalized event metadata;
- current values of explicitly declared `Tag` / `ServerMemoryTag` dependencies;
- the explicit set of dependencies granted `ServerMemoryTag` capability.

The runner parses Python 3 syntax with `ast` and interprets a deterministic subset. It does **not** use `eval`, `exec`, imports, attribute access, filesystem, network, database, Driver objects, DI services or host memory. Top-level statements other than function declarations are rejected.

Supported authoring covers functions, local assignment, arithmetic, comparisons, boolean expressions, `if`, return, basic lists/tuples/dictionaries and approved pure scalar helpers. The Script API is:

- `read_tag(stable_tag_id)` — declared `Tag` or `ServerMemoryTag` dependency;
- `write_tag(stable_tag_id, value)` — declared writable dependency;
- `read_server_memory(stable_tag_id)` — requires explicit `ServerMemoryTag` dependency;
- `write_server_memory(stable_tag_id, value)` — requires explicit `ServerMemoryTag` dependency.

The .NET host revalidates all requested writes against the current Active revision, active TAG registry, read-only state and `IEngineeringRuntimeCoordinator.IsServerMemoryTag`, then writes only through `IEngineeringRuntimeCoordinator.WriteAsync`. The child never receives a Driver, source-provider instance, host-memory object or DI container.

The AST runner enforces a deterministic instruction budget in addition to the coordinator's handler timeout. If timeout/cancellation occurs, the Python process tree is terminated. Errors crossing the sandbox boundary are sanitized.

The Python executable is configurable with `ServerScripts:PythonExecutable`; defaults are `python` on Windows and `python3` elsewhere. `ServerScriptRunner.py` is copied to build/publish output by `Scada.Api.csproj`.

## Events

Supported server-side event kinds in C12 are:

- `Initialize`;
- repeating `Timer`;
- `TagChanged` using canonical stable `TagValueReference.TagId`;
- generic `ServerRuntimeEvent` dispatch, optionally filtered by target reference;
- `Dispose` during orderly host shutdown.

Client Visual events and Client Memory are not promoted into the Server scope.

## Generic stateful acceptance process

The integration coverage authors a normal project with a generic integer Server Memory state. It does not contain EEE, pump, well or DEMO physics.

The acceptance flow proves:

`Initialize/Timer -> read Server Memory -> calculate next state -> write Server Memory through runtime -> CurrentTagCache/TagValueChanged -> historian -> High alarm`

The same test also disposes the Server Script host and verifies that the periodic state stops changing.

Additional coverage proves:

- revision N cannot write after revision N+1 becomes Active even when the stable TAG ID is identical;
- the currently Active revision can still write through the same official capability boundary;
- `TagChanged` and `ServerRuntimeEvent` operate over the same Active shared state;
- handler timeout results in `TimedOut`;
- handler faults remain isolated and produce sanitized `Faulted` diagnostics.

## Diagnostics

`ServerScriptRuntimeManager.Snapshot()` returns the Active project/revision and the existing `ScriptRuntimeDiagnosticsSnapshot` for every hosted Server Script, including queue/execution/failure/throttle data.

`ScadaRuntimeDescriptor.ServerScripts` projects that snapshot through the existing `/api/diagnostics/runtime` response when an Engineering Runtime is active. The endpoint keeps its existing runtime-engineering read authorization; C12 does not introduce a public unauthenticated diagnostic route.

## Configuration

Optional host settings:

- `ServerScripts:PythonExecutable`;
- `ServerScripts:HandlerTimeoutMs`;
- `ServerScripts:MinimumTimerIntervalMs`;
- `ServerScripts:MaxQueuedEvents`;
- `ServerScripts:MaxConsecutiveFailuresBeforeThrottle`.

Unsafe zero/unbounded values are not accepted; runtime defaults remain bounded.

## C13 dependency

C12 intentionally does not define or privately inject TAG quality. Normal Server Memory script writes continue through the existing official `WriteAsync` semantics.

After W14-C13 is integrated, automation that deliberately needs `Bad/Stale/Unavailable` samples can consume C13's public server-authoritative quality contract. No duplicate/private quality API exists in C12.

## Validation scope

C12 includes automated coverage for:

- Active Runtime activation with Server Scripts;
- `Initialize`;
- repeating Timer;
- generic stateful read/calculate/write;
- Current TAG state;
- historian observation;
- alarm reaction to the same values;
- host shutdown/cancellation;
- Active revision replacement and stale-generation write rejection;
- `TagChanged`;
- `ServerRuntimeEvent`;
- timeout;
- failure isolation and sanitized diagnostics.

Repository validation required before coordinator handoff:

- `EliteSCADA CI`;
- `Wave 11 Active HMI Runtime` because `src/Scada.Api/**` is affected.

Exact workflow run IDs are recorded in the coordinator handoff after GitHub Actions validation; a failed run must be diagnosed before any rerun.

## Limitations

- The sandbox intentionally supports a deterministic Python subset rather than arbitrary Python modules/libraries.
- Python 3 must be present on the runtime host, or `ServerScripts:PythonExecutable` must identify a compatible executable.
- `ServerRuntimeEvent` provides a generic host dispatch boundary; C12 does not invent application-specific domain events or an unauthenticated event-injection endpoint.
- C12 does not originate explicit quality; that remains W14-C13.
- C12 does not change the historical DEMO fallback itself. It only ensures persisted Active Server Scripts use the generic Active Runtime path and never use that fallback as their canonical automation engine.

## Explicit exclusions

C12 introduces no EEE/pump/well physics, `EeeSimulatorService`, hard-coded DEMO page, hidden `.escadapkg` mutation, `SimulationDriver`/`DemoRuntimeServices` reuse as the canonical automation engine, private Driver/host memory access, authorization bypass, licensing bypass or private quality contract.
