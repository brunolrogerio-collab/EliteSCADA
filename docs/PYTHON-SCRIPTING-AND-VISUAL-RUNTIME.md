# Python Scripting and Visual Runtime Architecture

Status: **locked product architecture / not yet implemented**.

This document defines the scripting and visual-object runtime foundation that must exist before EliteSCADA develops the full graphical screen/popup/Dynamo editor.

The intent is to support industrial HMI behavior that is not limited to simple one-property TAG bindings. A developer must be able to use Python scripts to implement richer animations and interactions while preserving Engineering authority, runtime safety, authorization and predictable performance.

## Architectural order

The full graphical screen/popup/Dynamo editor must not become the first place where visual behavior is invented.

Before that editor is implemented, EliteSCADA must establish:

1. a versioned visual-object property model;
2. stable object identity and runtime instance identity;
3. a sandboxed Python scripting runtime;
4. a developer script editor and validation workflow;
5. an event/subscription model;
6. a safe client visual-object API exposed to scripts;
7. a clear separation between design-time Engineering values and runtime presentation state.

The graphical editor will then consume these contracts rather than creating a private visual model that scripts later have to reverse-engineer.

## Two scripting scopes

EliteSCADA scripting has two distinct scopes and they must never be confused.

### Client visual scripts

Client visual scripts execute in the Runtime Client context and are the primary scripting mechanism for screens, popups and Dynamos.

They may:

- read permitted shared runtime TAG values;
- read/write that client's `builtin.memory.client` TAGs;
- read the current screen/popup/Dynamo instance context;
- read and change runtime-writable visual properties of objects in the current client instance;
- react to visual events, TAG changes, timers and lifecycle events;
- request permitted backend operations through explicit APIs, subject to the logged-in user's normal authorization.

They must not:

- access the host operating system, arbitrary files, shell/process execution or unrestricted networking;
- access browser DOM internals directly;
- access passwords, tokens, secrets or browser storage as a trusted API;
- call industrial drivers directly;
- bypass TAG/process-write, command, alarm or other backend authorization;
- mutate immutable Engineering revisions merely by changing a runtime visual property.

### Server scripts

Server scripts are a separate later runtime capability for server-owned calculations, automation and sequencing.

They may eventually read/write permitted shared TAGs and `builtin.memory.server` values, react to server runtime events and execute under an explicit trusted script/service security model.

Server scripts never manipulate one browser/client's visual object instances and never choose one client's Client Memory as global truth.

The exact first implementation slice may focus on client visual scripting because it is the prerequisite for the graphical editor, but the public scripting model must keep client/server scope explicit from the start.

## Python language requirement

Python is the locked scripting language for the initial scripting subsystem.

The exact execution engine is deliberately not locked yet. A technical spike may choose an implementation such as a WebAssembly/browser Python runtime for client scripts and a separately sandboxed backend Python host for server scripts, provided the security/performance rules in this document are preserved.

Python source code is part of Engineering configuration and must be versioned, importable/exportable, revisioned and included in project backup/restore. Mutable interpreter/runtime state is not Engineering configuration.

## Script Engineering model

Scripts are first-class Engineering entities rather than anonymous strings hidden inside UI controls.

At minimum a script definition requires:

- stable ID;
- name/path;
- scope: client visual or server;
- language/version marker;
- Python source text;
- enabled state;
- event/entry-point declarations or references;
- dependency/reference metadata;
- optional description and metadata;
- validation status produced by preview/compile tooling.

Screens, popups, Dynamos and visual objects may reference scripts and named entry points for events. The authoritative relationship is through stable references, not translated labels or fragile array positions.

Reusable library/Dynamo definitions may carry reusable script behavior. Instances inherit that behavior through the same class/instance rules as other Dynamo properties, with controlled overrides rather than duplicated hidden code.

## Developer script editor

Before the full graphical screen editor, Engineering must provide a practical Python script-editing environment.

Required direction:

- syntax highlighting;
- line numbers and normal code-editor navigation;
- syntax/compile validation before apply/publish;
- diagnostics with line/column and understandable messages;
- autocomplete/intellisense for the EliteSCADA scripting API where technically practical;
- access to documented TAG, Client Memory, screen/object and animation APIs;
- explicit script scope display;
- event/entry-point association UI;
- a sandboxed test/run or preview workflow against a controlled Engineering/runtime preview context;
- prevention of a script test from silently modifying the Active Engineering revision;
- localization of editor chrome/messages through the common `pt-BR` / `en` / `es` infrastructure while Python identifiers/API names remain stable.

The exact code-editor component is an implementation choice. A Monaco-class editor is an appropriate direction, but the product contract is the capability rather than one JavaScript package.

## Visual object model

Every screen, popup and Dynamo is composed of visual objects with stable Engineering identity and a typed property model.

Each visual object definition requires at minimum:

- stable object ID;
- developer-visible name/key unique within the relevant visual scope;
- object type key;
- parent/container relationship where applicable;
- design-time property values;
- optional TAG/property bindings;
- event-handler/script references;
- metadata.

Object types expose a **public property schema**. The property schema defines for each property:

- stable property key;
- data type;
- default value;
- optional min/max/enum constraints;
- whether it is visible/editable in Engineering;
- whether it is readable at runtime;
- whether it is writable by a client visual script;
- whether it supports TAG/expression binding;
- whether it is animatable;
- optional units/presentation metadata.

The graphical property inspector and the Python runtime API must consume the same property schema. They must not maintain two conflicting lists of properties.

## Common visual properties

Exact type-specific properties will evolve, but the common visual contract must support properties such as:

### Geometry and layout

- `x`;
- `y`;
- `width`;
- `height`;
- `rotation`;
- `scaleX` / `scaleY` where supported;
- anchor/origin/alignment where supported;
- `zIndex` or equivalent stacking order.

### Visibility and appearance

- `visible`;
- `opacity`;
- fill/background color;
- line/stroke color;
- line/stroke width or thickness;
- line style where supported;
- corner radius where supported;
- shadow/effect properties where supported.

### Text-capable objects

- text/value;
- text color;
- font family reference;
- font size;
- font weight/style;
- horizontal/vertical alignment.

### Image and symbol objects

- image/resource reference;
- fit/stretch mode;
- crop/position where supported;
- tint/opacity where supported.

Object-specific types may add additional properties, for example valve position, gauge ranges, path geometry or reusable Dynamo parameters, but all script-visible properties must remain explicitly declared in the property schema.

Colors must use a stable serialized representation rather than translated names.

## Engineering value versus runtime visual state

This distinction is mandatory.

A visual property has a design-time/base value stored in the versioned Engineering model. At runtime, bindings and scripts may produce a **runtime presentation override** for a specific visual-object instance.

Runtime override rules:

- a Python animation may change `x`, `width`, `fillColor`, `strokeWidth`, `opacity`, `rotation`, `visible` and other runtime-writable properties without modifying the saved Engineering definition;
- different Runtime Clients may display different runtime overrides for the same engineered object when their Client Memory, screen context or user interaction differs;
- closing/reopening the screen creates a new instance and re-establishes base/binding/script initialization according to deterministic lifecycle rules;
- runtime visual state must never be mistaken for project configuration or automatically written into a saved revision;
- design-time edits remain explicit Engineering operations subject to preview/apply/revision workflow.

A future explicit developer command such as "capture runtime value as design value" may be considered, but it must be an intentional Engineering action, never an automatic side effect of script execution.

## Object access from Python

Client scripts must receive an object-oriented, stable EliteSCADA API rather than arbitrary renderer/DOM access.

Conceptually the API must allow operations such as:

- obtain the current screen/popup/Dynamo instance;
- find a child visual object by stable developer key or ID;
- read a declared property;
- write a property marked runtime-writable;
- read a TAG or Client Memory value;
- subscribe/react to an event;
- request an animation/tween/transition;
- request an authorized command or process write through the normal backend boundary where appropriate.

The final Python API names are an implementation decision and will be versioned. Scripts must not depend on React component internals, browser DOM selectors or renderer-private object shapes.

## Event model

Client visual scripting must be primarily event driven.

The first practical event family should include:

- screen/popup/Dynamo instance load/initialize;
- unload/dispose;
- object click/tap;
- pointer/interaction events where safe and useful;
- TAG-value change;
- Client Memory change;
- timer/interval;
- object/property change where recursion is controlled;
- optional bounded frame/tick callback for advanced visual behavior.

Subscriptions are automatically disposed with their owning visual instance unless explicitly scoped otherwise. Hidden or closed screens must not leave orphan timers/subscriptions consuming resources.

Handlers must have recursion/reentrancy protection and bounded queues so a fast TAG cannot produce an unbounded backlog of Python executions.

## Animation model

Python must support richer animation than simple boolean/color bindings, but continuous animation should not require developers to manually implement a wasteful busy loop.

The visual runtime should expose animation primitives to scripts, for example:

- animate a numeric/color/opacity/rotation property from current value to target;
- duration;
- easing function;
- repeat/ping-pong behavior;
- cancel/replace policy;
- completion callback/event.

A bounded frame/tick callback may exist for advanced cases, but normal motion and transitions should use a renderer-native animation scheduler invoked from Python. This keeps smooth 30/60-fps rendering out of the Python interpreter's hot path whenever possible.

Bindings, animations and script writes need deterministic precedence. The implementation slice must define a property-resolution order and prevent two independent writers from silently fighting forever. At minimum the runtime must expose/diagnose the active source of a property value.

## TAG bindings and script interaction

Simple HMI behavior should remain possible without Python.

The graphical editor must eventually support direct TAG/expression bindings for common properties such as visibility, color, text, value and position. Python complements bindings for behavior that is genuinely procedural or multi-property.

A script may read the effective property value and may override a script-writable property according to the defined precedence model. The runtime must diagnose conflicting binding/script writers rather than making behavior depend on timing accidents.

## Images and resources

Image resources used by screens/Dynamos are Engineering assets with stable IDs/references and participate in project package/export semantics.

Python receives resource/object references through the EliteSCADA API. It does not receive unrestricted file-system paths.

## Security boundary

Client Python is untrusted application logic running under a user session, not an authorization bypass.

Therefore:

- reading shared TAGs follows the user's readable runtime surface;
- process-value writes use the normal protected write API;
- operational commands use the normal `CommandExecute` boundary;
- alarm operations use their normal authorization boundary;
- scripts cannot manufacture a stronger principal;
- Client Memory remains client-local and is not trusted server process state;
- secrets are never exposed to script globals;
- direct driver, database and filesystem APIs are absent.

Script definition/edit/apply/publish is an Engineering modification and is permission controlled/auditable like other Engineering changes.

## Sandboxing and reliability

A bad visual script must not freeze the entire HMI indefinitely.

The runtime requires:

- execution time budgets;
- cancellation/abort of over-budget handlers;
- memory/resource limits appropriate to the selected Python engine;
- bounded timer/event frequency;
- bounded event queue/backpressure/coalescing;
- error isolation per script/visual instance;
- diagnostics including script ID, handler, location and sanitized exception;
- optional automatic temporary disablement/throttling after repeated failures;
- no silent catch-and-ignore behavior for persistent script faults.

One script failure must not terminate the backend, driver host or unrelated Runtime Client sessions.

## Diagnostics

Engineering/runtime diagnostics should eventually expose:

- script enabled/disabled/faulted state;
- last execution time/duration;
- execution count;
- error count;
- timeout/budget-abort count;
- last sanitized error with source line/handler;
- active subscriptions/timers where useful;
- visual-property conflict/source diagnostics where a property has binding/script/animation writers.

## Import/export and revisioning

Scripts, script references, visual property schemas/values, object IDs, bindings and event associations are part of the public versioned Engineering model.

They participate in:

- canonical JSON Engineering export/import;
- validation/preview/apply;
- immutable project revisions;
- `.escadapkg` backup/restore;
- Engineering Fragments/cross-project copy-paste with dependency analysis;
- reusable Dynamo/library dependency validation.

Python source code may be exported as part of canonical Engineering/package data, but secrets and mutable runtime state may not.

## Required implementation sequencing around the graphical editor

The locked local sequence is:

`Python scripting contract + visual property schema -> script editor/sandbox -> visual runtime object instances/property API -> graphical screen/popup/Dynamo editor -> advanced reusable visual libraries`

The interface-validation preview that occurs after multi-driver diagnostics may precede this graphical-editor sequence. The preview does not need the final screen editor. However, once graphical screen/Dynamo development starts, the scripting/property foundation in this document is a mandatory prerequisite.

## Deferred implementation choices

The following are intentionally deferred until technical implementation/research:

- exact client Python engine/WASM implementation;
- exact server Python host/isolation mechanism;
- exact public Python API spelling/package name;
- exact renderer technology for visual objects;
- exact animation library/scheduler;
- whether optional static typing/stubs are generated for autocomplete;
- exact property precedence rules beyond the requirement that they be deterministic and diagnosable.

These implementation choices may evolve without weakening the locked product behaviors above.
