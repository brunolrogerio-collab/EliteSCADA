# W14-C12 — Server Runtime Automation / Generic Simulation Authoring

**Package:** W14-C12  
**Exact original base:** `2607e03d5445eefe1f434495d0ee81136c6cd220`  
**Findings:** `C11-P2-SCR-01`, `C11-P2-SIM-01`  
**Canonical quality authority:** W14-C13 candidate `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`, accepted by PR #242 and synchronized into C12 by merge commit `c121bb4e7c54f1fc95c56e3e68e9ed7e6e42b194`.

## Objective

Make Server Engineering Scripts executable as part of the persisted Active Runtime lifecycle, using normal Engineering artifacts and the public runtime TAG boundary rather than any DEMO-specific simulator.

The generic automation path is:

`Timer/event -> declared TAG dependencies -> Server Script calculation -> official runtime write -> CurrentTagCache/event bus -> alarms/historian/realtime/bindings`

For deliberately qualified internal/simulation samples, C12 now consumes the accepted C13 server-authoritative contract:

`Server Script -> publish_server_memory_sample -> QualifiedSourceSample -> ServerAuthoritativeSamplePublisher -> CurrentTagCache/TagValueChanged -> alarm communication semantics -> historian/query -> realtime/bindings`

No C12-private quality model is introduced.

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

If runtime activation fails, the prior Server Script generation remains active. If Server Script startup fails after successful runtime activation, the new script generation is cancelled and diagnostics surface the failure rather than silently running Working state or falling back to a DEMO driver.

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
- `write_tag(stable_tag_id, value)` — declared writable dependency, value-only;
- `read_server_memory(stable_tag_id)` — requires explicit `ServerMemoryTag` dependency;
- `write_server_memory(stable_tag_id, value)` — requires explicit `ServerMemoryTag` dependency and remains value-only / `Good`;
- `publish_server_memory_sample(stable_tag_id, value, quality)` — requires explicit `ServerMemoryTag` dependency and publishes a canonical qualified sample.

The child runner does not invent a quality vocabulary. It forwards the requested quality name only for the qualified Server Memory operation. The .NET executor accepts it only when it parses to an actually defined canonical `TagQuality`. Consequently normal authoring can deliberately produce `Good`, `Bad`, `Stale`, `Unavailable` and any other quality that is part of the canonical enum, while unknown names are rejected as script faults.

The .NET host revalidates all requested writes against the current Active revision, active TAG registry, read-only state and `IEngineeringRuntimeCoordinator.IsServerMemoryTag`. The child never receives a Driver, source-provider instance, host-memory object or DI container.

The AST runner enforces a deterministic instruction budget in addition to the coordinator's handler timeout. If timeout/cancellation occurs, the Python process tree is terminated. Errors crossing the sandbox boundary are sanitized.

The Python executable is configurable with `ServerScripts:PythonExecutable`; defaults are `python` on Windows and `python3` elsewhere. `ServerScriptRunner.py` is copied to build/publish output by `Scada.Api.csproj`.

## Canonical qualified Server Memory samples

C12 consumes, rather than duplicates, the C13 contract:

- `QualifiedSourceSample` is the only qualified sample payload used by the host;
- `IQualifiedSourceProvider` remains the capability boundary implemented by the accepted Server Memory provider;
- `ServerAuthoritativeSamplePublisher` remains the authority that checks source ownership and writes the resulting `TagValue` into `CurrentTagCache`.

The Server Script qualified path has layered authority checks:

1. `publish_server_memory_sample(...)` is available only for IDs the child received in `serverMemoryTagIds`;
2. that list is derived only from explicit Engineering dependencies of kind `ServerMemoryTag`;
3. the .NET executor independently checks the write still carries `ServerMemoryOnly` and the declared dependency is still `ServerMemoryTag`;
4. `ServerScriptRuntimeManager` revalidates the exact Active project/revision and `IEngineeringRuntimeCoordinator.IsServerMemoryTag(tagId)`;
5. `EngineeringRuntimeCoordinator` dispatches the write to the active `ServerMemoryRuntimeSource` that owns that TAG;
6. the active Server Memory source passes `QualifiedSourceSample` to its C13 `ServerAuthoritativeSamplePublisher`, whose source-ownership check remains authoritative.

A generic `Tag` dependency is therefore insufficient to originate quality. A physical Driver TAG cannot be given arbitrary script-originated quality because the qualified operation is rejected before the Driver write path. Client Visual scope receives no quality-authoring API, endpoint or source-provider capability.

Ordinary `write_tag` and `write_server_memory` remain value-only operations. In particular `write_server_memory` still reaches `ServerMemorySourceProvider.WriteAsync`, whose accepted C13 semantics produce `TagQuality.Good`.

Quality itself is deliberately transient source state: C13 Server Memory retention retains the typed process value, not a synthetic Bad/Stale/Unavailable state across activation.

## Events

Supported server-side event kinds in C12 are:

- `Initialize`;
- repeating `Timer`;
- `TagChanged` using canonical stable `TagValueReference.TagId`;
- generic `ServerRuntimeEvent` dispatch, optionally filtered by target reference;
- `Dispose` during orderly host shutdown.

Client Visual events and Client Memory are not promoted into Server scope.

## Generic stateful acceptance process

The integration coverage authors normal projects with generic Server Memory state. It contains no EEE, pump, well, level or DEMO physics.

The stateful automation flow proves:

`Initialize/Timer -> read Server Memory -> calculate next state -> write Server Memory through runtime -> CurrentTagCache/TagValueChanged -> historian -> High alarm`

The same test also disposes the Server Script host and verifies that periodic state stops changing.

Additional lifecycle coverage proves:

- revision N cannot write after revision N+1 becomes Active even when the stable TAG ID is identical;
- the currently Active revision can still write through the same official capability boundary;
- `TagChanged` and `ServerRuntimeEvent` operate over the same Active shared state;
- handler timeout results in `TimedOut`;
- handler faults remain isolated and produce sanitized `Faulted` diagnostics.

## Qualified-quality acceptance

`ServerScriptQualifiedQualityIntegrationTests` authors a real Server Script plus a real Server Memory TAG, historian policy and Communication alarm and executes the normal Active runtime host.

For each of `Bad`, `Stale` and `Unavailable` it proves:

1. the Server Script calls `publish_server_memory_sample`;
2. the process value and canonical quality reach the current runtime cache;
3. the same `TagValueChanged` event carries the qualified value, which is the realtime publication source;
4. Communication alarm semantics become Active for the non-Good quality;
5. the historian observes and query returns the same value and quality.

Separate tests prove:

- normal `write_server_memory` remains `Good`;
- a generic `Tag` dependency cannot use the qualified Server Memory operation;
- an unknown quality name is rejected because it is not a canonical `TagQuality` member.

`wave-14-c12-server-script-quality-propagation.spec.ts` then verifies the web realtime parser and visual binding path for `Bad`, `Stale` and `Unavailable`: the server-originated quality survives wire parsing and the visual dynamic binding applies its canonical non-Good fallback/diagnostic behavior. The browser is a consumer only; it gains no quality-authoring authority.

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

## Validation scope

C12 automated coverage includes:

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
- failure isolation and sanitized diagnostics;
- canonical qualified Server Memory publication for `Bad`, `Stale` and `Unavailable`;
- ordinary Server Memory write remains `Good`;
- rejection without declared `ServerMemoryTag` authority;
- rejection of quality outside canonical `TagQuality`;
- realtime parsing and visual binding behavior for all three required non-Good qualities.

Repository validation required before coordinator handoff:

- `EliteSCADA CI`;
- Chromium E2E / web build for the quality consumer path;
- `Wave 11 Active HMI Runtime` because `src/Scada.Api/**`, DriverHost and web runtime behavior are affected.

Exact workflow run IDs are recorded in the coordinator handoff after exact-candidate GitHub Actions validation; a failed run must be diagnosed before any rerun.

## Limitations

- The sandbox intentionally supports a deterministic Python subset rather than arbitrary Python modules/libraries.
- Python 3 must be present on the runtime host, or `ServerScripts:PythonExecutable` must identify a compatible executable.
- `ServerRuntimeEvent` provides a generic host dispatch boundary; C12 does not invent application-specific domain events or an unauthenticated event-injection endpoint.
- C12 does not change the historical DEMO fallback itself. Persisted Active Server Scripts use the generic Active Runtime path and never use that fallback as their canonical automation engine.

## Explicit exclusions

C12 introduces no EEE/pump/well/level physics, `EeeSimulatorService`, hard-coded DEMO page, hidden `.escadapkg` mutation, `SimulationDriver`/`DemoRuntimeServices` reuse as the canonical automation engine, private Driver/host memory access, authorization bypass, licensing bypass, Client Visual quality authority, physical Driver quality spoofing or private quality contract.
