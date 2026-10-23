# Scripting, Alarms, and History

## Server Scripts

A Server Script is automation that runs on the server. Think of it as a small function called by a product event: it reads declared dependencies, calculates something, and uses the permitted APIs. Although its syntax resembles Python, the runtime executes a deterministic subset — it is not general-purpose Python or a place to install libraries.

Enabled Server-scope Scripts are hosted by the **Active** revision. When another revision is activated, the previous generation is cancelled; reads, writes, and event emission from the old generation must not continue acting on the new revision.

### Before you write: concepts that prevent errors

- **Handler** is the function called by an entry point. The name configured in Engineering must exactly match the function name.
- **Entry point/trigger** determines when the handler is called: initialization, TAG change, Timer interval, or Runtime event.
- **Dependency** declares which TAG or command the Script may use. Putting a path in code does not declare access.
- **Readable binding** is a path such as `Plant.Pump.DischargePressure` linked to a stable identity. When this binding is configured, code may use the path; the product still validates the identity. A stable ID is also accepted.
- **Server Memory** is retentive server memory. Its TAGs must be declared as `ServerMemoryTag` dependencies; do not confuse it with Client Memory, which belongs to the client Runtime.
- **Active** is the revision the Runtime is executing. Saving or editing code does not, by itself, make the new version Active.

### Writing a handler

At the file's top level, declare functions. Do not place free-standing reads, writes, or calculations outside a function. A common handler may take no arguments. `ServerRuntimeEvent` provides an event dictionary, in which case the handler receives one argument:

```python
def on_start():
    pressure = read_tag("Plant.Pump.DischargePressure")

def on_device_event(event):
    action = event["canonicalEvent"]["payload"]["action"]
```

The second example shows the canonical event shape used by the host; only use fields present in the real event configured for your project. Accessing a missing key fails the handler.

### Available triggers

| Trigger | When to use it | Note |
| --- | --- | --- |
| `Initialize` | Initialize instance state when it starts | Do not assume the code runs once forever; a new generation may initialize again. |
| `Dispose` | Close or clean up instance state | The generation may already be cancelling; keep the handler short. |
| `TagChanged` | Recalculate a rule when its associated TAG changes | Declare every TAG read or written as a dependency. |
| `Timer` | Run a periodic check independently of an open screen | Configure an interval of at least 50 ms or the product's stated minimum. Do not create a loop or wait inside the Script. The Timer is not synchronized with the Driver acquisition cycle. |
| `ServerRuntimeEvent` | React to a canonical Transient Event occurrence | Associate the real event in the Active graph; generic targets do not invoke the handler. |

### Syntax: what works and what does not

The current subset accepts:

- `def` without decorators, default values, positional-only, or keyword-only parameters;
- local assignment and `+=`, `-=`, `*=`;
- `if`/`else`, `return`, and `pass`;
- lists, tuples, dictionaries, and index access, for example `event["payload"]["action"]`;
- `+`, `-`, `*`, `/`, `//`, `%`, `**`; comparisons `==`, `!=`, `<`, `<=`, `>`, `>=`, `is`, `is not`;
- `not`, `and`, `or`, positional calls to functions in the Script itself, and `min`, `max`, `abs`, `round`, `int`, `float`, `bool`, `str`, `len`.

It does not accept `import`, top-level code/variables, `for`, `while`, comprehensions, `class`, `try/except`, `lambda`, f-strings, attribute access, decorators, named arguments, or external Python libraries. Even syntactically accepted code can fail because of an incompatible type, division by zero, or a missing key/index.

**Important difference:** in this interpreter, `and` and `or` evaluate every operand; they do not short-circuit like normal Python. Do not use them to guard an operation that may fail. Use nested `if` statements:

```python
def evaluate_pressure():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    if pressure is not None:
        if high_limit is not None:
            above_limit = pressure >= high_limit
```

### TAG, Server Memory, and Operational Event API

These are the six documented public functions for TAGs, memory, and events. Every reference must be a declared dependency; for a readable path, its binding must match the stable identity. If a reference fails, correct the dependency/binding — do not try to bypass it through the Driver.

| Function | Inputs and return value | Main rules |
| --- | --- | --- |
| `read_tag(tag_reference)` | Bound path or stable ID; returns the current value. | Does not return source quality or timestamp. Reads only a declared dependency. |
| `read_server_memory(tag_reference)` | Bound path or stable ID; returns current Server Memory value. | Requires a declared `ServerMemoryTag` dependency. |
| `write_tag(tag_reference, value)` | Declared TAG and a value compatible with its type; returns `None`. | Requires a writable TAG and authorization in the corresponding scope. Does not directly change a visual object. |
| `write_server_memory(tag_reference, value)` | Declared ServerMemoryTag and a serializable value; returns `None`. | Writes server memory, not a process TAG. |
| `publish_server_memory_sample(tag_reference, value, quality)` | ServerMemoryTag, serializable value, and canonical `TagQuality` name; returns `None`. | Quality must be exactly a supported value, for example `Good`, `Uncertain`, `Bad`, `BadCommunication`, `BadConfiguration`, `BadDevice`, `Stale`, `Disabled`, or `Unavailable`. The Script supplies quality; the function does not discover it at the source. |
| `emit_operational_event(definition_id, message=None, context=None)` | Stable definition ID; optional text; optional dictionary with text values; returns `None`. | The definition must exist. Inputs have count/size limits. An Operational Event does not replace Audit or an Alarm. |

### Example 1: derive a visual state

Use this pattern to evaluate a value and write the result to a writable TAG already bound to a visual property. Declare pressure, limit, and output as dependencies; configure the output with a compatible type (Boolean in this example).

```python
def update_pressure_indicator():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    if pressure is not None:
        if high_limit is not None:
            write_tag("Plant.Screen.PumpPressureHigh", pressure >= high_limit)
```

If either input is unavailable, the handler does not overwrite the output with `False`; this avoids turning missing data into an indication of a normal condition. A Server Script does not directly change the screen object: the output TAG must be bound to the visual property.

### Example 2: hysteresis and an Operational Event without continuous repetition

Use retentive state to record whether the condition has already been signalled. Configure `TagChanged` for the pressure TAG and declare both limits, the state `ServerMemoryTag`, and the event definition as applicable dependencies. The return limit must be lower than the trip limit.

```python
def on_pressure_change():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    reset_limit = read_tag("Limits.Pump.ResetPressure")
    event_sent = read_server_memory("State.Pump.HighPressure")

    if pressure is not None:
        if high_limit is not None:
            if reset_limit is not None:
                if event_sent is not True:
                    if pressure >= high_limit:
                        write_server_memory("State.Pump.HighPressure", True)
                        emit_operational_event(
                            "<stable-event-definition-id>",
                            "Pump discharge pressure crossed the high limit",
                            {"pressure": str(pressure)}
                        )
                else:
                    if pressure <= reset_limit:
                        write_server_memory("State.Pump.HighPressure", False)
```

Replace the placeholder with the real stable ID. The state prevents emitting the same event on every change while pressure remains above the limit. This is application logic; it does not replace Alarm configuration when the requirement is an alarm.

### Example 3: calculated sample and explicit quality

`read_tag` does not expose whether the source is `Good`, `Stale`, or its source timestamp. Therefore, checking only `value is not None` does **not** prove that the sample is good. If the application cannot confirm quality and freshness, publish an appropriate conservative quality or do not publish:

```python
def publish_unverified_sample():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        publish_server_memory_sample(
            "State.Pump.LastCurrent",
            current,
            "Uncertain"
        )
```

Use `"Good"` only when a validated project rule establishes the value's reliability and does not depend on quality/timestamp information unavailable to the Script. Never replace a missing value with zero to force a sample.

### Example 4: Timer without a loop

Configure the `Timer` entry point in Engineering. The host invokes the handler; the code does not run continuously:

```python
def on_timer():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        write_server_memory("State.Pump.LastCurrent", current)
```

Declare both TAGs and confirm that the destination is a `ServerMemoryTag`. If you need to detect changes, consider `TagChanged`; do not reduce the Timer interval without need, since fast timers create additional server work.

### Rich Command: an explicitly authorized operation

In addition to the six APIs above, the host integration provides `invoke_rich_command` for a declared Rich Command dependency. This is **not** direct Driver access. Configure the real command binding, associate a `ServerRuntimeEvent` with a real Transient Event, and grant `CommandExecute` in the exact scope. Parameters must be scalar values compatible with the command schema.

The result distinguishes `Rejected`, `Accepted`, `Completed`, `Failed`, `TimedOut`, and `Unknown`. `Accepted` means the request was accepted, not that the equipment completed the action. `Unknown` is ambiguous: do not automatically repeat the command, since it may have executed even though the response was lost. Reconcile using independent process feedback before deciding on another action.

```python
def on_device_event(event):
    action = event["canonicalEvent"]["payload"]["action"]
    if action == "single":
        result = invoke_rich_command(
            "<stable-rich-command-guid>",
            {"onTime": "00:00:00.2500000"}
        )
        if result["outcome"] == "Completed":
            write_server_memory("State.Command.LastOutcome", 1)
        else:
            write_server_memory("State.Command.LastOutcome", -1)
```

This example records success only for `Completed`; every other result is marked as not completed/pending diagnosis. Do not treat `-1` as proof of physical failure: to distinguish outcomes, store or publish state according to the application's data model. Replace the command, parameter, and event fields with real project objects. The operation still passes through the product's normal validation and authorization.

### Recommended Engineering workflow

1. Define the goal and permitted effect: calculate only, write memory, write a process TAG, or request a Rich Command.
2. Create/configure the Script and select `Server` scope.
3. Declare every required TAG, ServerMemoryTag, event, or command. Check type, binding, stable identity, write permission, and Authority scope.
4. Create a handler and bind its exact name to the trigger. For `ServerRuntimeEvent`, check the canonical event and available fields.
5. Start with reads and missing-data cases; then add the calculation; add writes/emission last.
6. Validate syntax and dependencies in the editor. Preflight is feedback; runtime still enforces authorization and sandbox rules.
7. Test normal values, limits, missing values, unexpected types, and command failure/timeout. Confirm each write and event.
8. Save, create the revision, and follow the normal publish/activate flow. Verify that the expected revision became `Active`.

### Execution, failures, and sandbox

The documented default policy uses a 250 ms handler timeout, a queue limited to 128 events, a 50 ms minimum Timer interval, and cooldown after 5 consecutive failures. After the configurable cooldown, an event may be accepted as a recovery probe. Keep each handler short; the deterministic budget also limits the number of steps.

Server Scripts do not provide arbitrary access to the filesystem, operating system, shell/processes, network, database, Drivers, secrets, DOM, or browser storage. Preflight helps detect some errors early, but it is not the security boundary: enforcement belongs to the runtime.

### Common problems

| Symptom | Check |
| --- | --- |
| TAG is “undeclared” or a path is rejected | Is the dependency present? Is the readable binding configured and linked to the same stable identity? Is the ID correct? |
| Handler is not called | Does the function name exactly match the entry point? Is the Script enabled and in Server scope? Is the expected revision Active? Is the trigger linked to the correct object/event? |
| Server Memory read/write fails | Is the dependency really a `ServerMemoryTag`? Do the write and value match the contract? |
| Visual output does not change | Is the destination TAG writable and authorized? Is it bound to the visual property? A Server Script does not directly change a visual object. |
| Script validates but its handler fails | Check types, `None`, event keys/indexes, division by zero, and limits; remember that `and/or` do not short-circuit. |
| Command returns `Accepted`, `TimedOut`, or `Unknown` | Do not assume physical completion or retry automatically. Check independent feedback, equipment state, and audit records. |
| Script enters cooldown | Correct the cause, inspect diagnostics, and test with representative data before re-enabling it. |

**Related Help:** `/help?topic=scripts.server`, `/help?topic=sources.internal-memory`, `/help?topic=operational-events.overview`, `/help?topic=alarms.overview`.

## Client Visual Scripts

A Client Visual Script is Python executed in the client Runtime for interaction with an open Screen/Popup and for local presentation. Its scope differs from Server Script: it does not replace backend automation and does not keep working when the client is not running. Server Scripts, the Active revision, backend triggers, and their six APIs are described above.

### Create and bind an interaction

1. In Script Engineering, create a Script and select `Client Visual` scope and Python language.
2. Declare an `objectInteraction` entry point with a handler name, such as `on_click`.
3. In the Screen/Popup object's Events panel, select `Click`, the Script, and the handler.
4. Declare each TAG read or written by the Script as a dependency. The readable path used in code must be bound to the stable TAG identity.
5. Validate, save to Working, and follow the normal revision/publish/activate flow before checking it in Runtime.

Accepted interaction events include `click`, `pointerenter`, `pointermove`, and `pointerleave`. The handler receives an event dictionary. For a visual interaction, it includes keys such as `eventKey`, `visualDefinitionId`, `visualObjectId`, and `pointer`.

### Available API

Import functions provided by the `elite_scada` module:

| Function | Use |
| --- | --- |
| `tag_read(reference)` | Reads an allowed TAG and returns data such as `value`, `quality`, `timestamp`, and `source`. |
| `tag_write(reference, value)` | Requests a Boolean, finite number, or text write. Requires a writable TAG, dependency, and Runtime authorization. |
| `client_memory_read(reference)` / `client_memory_write(reference, value)` | Reads or changes non-retentive client memory. It is not Historian and is not synchronized across clients. |
| `visual_property_read(target, property)` / `visual_property_write(target, property, value)` | Reads or requests an override of a property registered on the visual object in the current context. |
| `visual_property_clear(target, property)` | Removes the temporary Script override for that property. |
| `visual_tween_request(arguments)` | Requests animation through the permitted visual API, subject to Runtime validation. |

Bridge calls are asynchronous. Use `async def` and `await`. The API does not provide direct access to Drivers, databases, files, the operating system, credentials, DOM, or arbitrary network access. TAG writes follow the session's permissions and the normal Runtime service; having a button or Script does not grant authorization.

### Example: read a TAG and respect quality

Bind `on_click` to the Click event and declare `Plant.Pump.DischargePressure` as a dependency. Use the reading only when quality is `Good`; missing data or a different quality is not zero and does not prove normal operation.

```client-visual-python
from elite_scada import tag_read

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample["quality"] == "Good":
        pressure = sample["value"]
```

### Example: store a value in Client Memory

Configure the `UI.LastPressure` reference in the project's Client Memory provider. This value is local to the client Runtime and may disappear when the session/client ends.

```client-visual-python
from elite_scada import tag_read, client_memory_write

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample["quality"] == "Good":
        await client_memory_write("UI.LastPressure", sample["value"])
```

### Example: identify the interaction

Declare `on_pointer_enter` as an `objectInteraction` handler and bind it to `Pointer enters object` in the Events panel.

```client-visual-python
async def on_pointer_enter(event):
    object_id = event["visualObjectId"]
    event_name = event["eventKey"]
```

Use pointer handlers for brief responses. `pointermove` may fire repeatedly as the pointer moves; do not perform lengthy work or actively wait in each call. Each execution has a time limit, and failures are isolated from the rest of Runtime.

### Common problems

| Symptom | Check |
| --- | --- |
| `ImportError` | Use `elite_scada` and check the exact function name exposed to Client Visual. General-purpose libraries and arbitrary modules are not part of this API. |
| TAG is undeclared or the path is rejected | Add the stable dependency to the Script; check the readable binding and TAG identity. |
| A reading is available but the screen should not update | Check `quality` and `timestamp`; do not treat an unavailable reading as zero/a normal state. |
| Write is rejected | Check whether the TAG allows writes, the session capability, compatible value, and Runtime feedback. |
| Handler is not called | Check `Client Visual` scope, `objectInteraction` entry point, exact name, and Script/event/object binding. |
| Interaction makes the UI slow | Reduce work in `pointermove`, remove loops/busy waiting, and keep the handler short. |

**Related Help:** `/help?topic=scripts.client-visual`, `/help?topic=scripts.engineering`, `/help?topic=scripts.python-validation`, `/help?topic=visual.events`.

## Client Memory

Client Memory is non-retentive memory owned by the client Runtime. Server Memory is retentive and owned by the server. Both are internal source providers, not communication Drivers.

**Related Help:** `/help?topic=sources.internal-memory`.

## Alarms, Operational Events, and Audit

These are three distinct domains:

- **Alarm**: a configured operational alarm condition.
- **Operational Event**: an explicit operational occurrence defined by the product/application.
- **Audit**: traceability for security, Engineering, and operational actions.

Do not use an Alarm as a generic log or an Operational Event as a substitute for Audit.

**Related Help:** `/help?topic=alarms.overview`, `/help?topic=operational-events.overview`, `/help?topic=audit.overview`.

## Historian and Trends

Historian receives value, quality, and timestamp through the product pipeline. Trends present authorized current or historical data; they do not redefine quality, timestamp, scaling, or persistence.

**Related Help:** `/help?topic=historian.overview`, `/help?topic=trends.overview`.

## Historical Time Range and Playback

The structure of these chapters is reserved, but final procedures are blocked until #383 and the subsequent Playback workstream are accepted. Do not use this manual as an indication that the final UX for these features is frozen.
