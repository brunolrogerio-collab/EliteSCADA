# W14-C12 — Server Runtime Automation / Generic Simulation Authoring

**Package:** W14-C12  
**Base:** `2607e03d5445eefe1f434495d0ee81136c6cd220`  
**Findings:** `C11-P2-SCR-01`, `C11-P2-SIM-01`

## Objective

Make Server Engineering Scripts executable as part of the persisted Active Runtime lifecycle, using the existing public runtime TAG boundary and the existing bounded `ScriptRuntimeExecutionCoordinator` rather than any DEMO-specific simulator.

## Active lifecycle

The host is entered only by the two persisted Active lifecycle paths:

- `PublishedRuntimeActivationService`, after a published revision has been atomically activated and recorded as Active;
- `PersistedRuntimeRecoveryService`, after the persisted Active revision is recovered at startup.

The host receives the exact `(projectKey, revision, EngineeringPackage.Scripts)` that was activated. It rejects activation when `IEngineeringRuntimeCoordinator.Describe()` does not report the same project/revision. Working/unpublished Engineering state is therefore not a script execution source.

When a new Active revision is loaded, the previous script generation is cancelled and disposed. Every dispatch also verifies the expected Active runtime identity; a generation that no longer matches the runtime cannot enqueue or drain work.

## Runtime architecture

`ServerScriptRuntimeManager` creates one `ScriptRuntimeExecutionCoordinator` per enabled `ScriptEngineeringScope.Server` definition.

The existing coordinator remains authoritative for:

- bounded queues;
- event coalescing/overflow policy;
- per-handler serialization;
- timeout;
- cancellation;
- failure isolation;
- consecutive-failure throttling;
- execution diagnostics.

The host adds the missing lifecycle/event layer:

- `Initialize` once per Active generation;
- repeating `Timer` entry points through `PeriodicTimer`;
- `TagChanged` from the canonical `IScadaEventBus` / `TagValueChanged` stream;
- a public `DispatchRuntimeEventAsync` boundary for `ServerRuntimeEvent` entry points;
- `Dispose` on orderly host shutdown;
- generation cancellation on Active revision/project replacement.

## Python execution and sandbox

`IsolatedPythonScriptHandlerExecutor` launches a fixed `ServerScriptRunner.py` with shell execution disabled and supplies only:

- script source;
- normalized event metadata;
- current values of explicitly declared `Tag` / `ServerMemoryTag` dependencies.

The runner parses Python 3 syntax with `ast` and interprets a deterministic subset. It does **not** use `eval`, `exec`, imports, attribute access, filesystem, network, database, Driver objects, DI services or host memory.

Supported authoring covers functions, local assignment, arithmetic, comparisons, boolean expressions, `if`, return, basic lists/tuples/dictionaries and approved scalar helpers. The public runtime API available to a Server Script is:

- `read_tag(stable_tag_id)`;
- `write_tag(stable_tag_id, value)`;
- `read_server_memory(stable_tag_id)`;
- `write_server_memory(stable_tag_id, value)`.

All requested writes are validated again in the .NET host and replayed only through `IEngineeringRuntimeCoordinator.WriteAsync`. `write_server_memory` additionally requires `IEngineeringRuntimeCoordinator.IsServerMemoryTag`. Read-only TAGs and undeclared dependencies fail closed.

The Python executable is configurable with `ServerScripts:PythonExecutable`; defaults are `python` on Windows and `python3` elsewhere. The runner is copied into build/publish output.

## Generic simulation model

A normal project can now implement:

`Timer -> read Server Memory/shared TAG -> calculate -> write official TAG boundary -> CurrentTagCache/event bus -> alarm/historian/realtime/bindings`

No second simulation data plane is introduced.

The integration test uses a generic integer process state. `Initialize` writes the initial state and a repeating Timer reads Server Memory and increments it. The same runtime update is observed by the current TAG state, historian and a configured High alarm.

## Diagnostics

`ServerScriptRuntimeManager.Snapshot()` returns the Active project/revision and the existing `ScriptRuntimeDiagnosticsSnapshot` for every hosted Server Script, including queue/execution/failure/throttle data. Script failures are sanitized by `ScriptRuntimeExecutionCoordinator`; the sandbox runner never returns host stack traces.

## C13 dependency

C12 intentionally does not define or privately inject TAG quality. Normal script writes use the existing official `WriteAsync` semantics and therefore remain `Good` for Server Memory.

After W14-C13 is integrated, Server automation that needs deliberate `Bad/Stale/Unavailable` samples should consume C13's public server-authoritative qualified Source contract. No duplicate quality API exists in C12.

## Limitations

- The initial sandbox intentionally supports a deterministic Python subset rather than arbitrary Python modules/libraries.
- Python 3 must be present on the runtime host, or `ServerScripts:PythonExecutable` must identify a compatible executable.
- `ServerRuntimeEvent` has a public dispatch boundary, but domain-specific event producers remain responsible for invoking it; C12 does not invent new domain events.
- C12 does not originate explicit quality; that is W14-C13.

## Explicit exclusions

No EEE/pump/well physics, `EeeSimulatorService`, DEMO page, `.escadapkg` editing shortcut, `SimulationDriver`/`DemoRuntimeServices` reuse as the canonical automation engine, private Driver/host memory access, authorization bypass or licensing bypass is introduced.
