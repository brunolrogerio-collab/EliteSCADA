# EliteSCADA — Visual Component Library Baseline

## Status
Planned product baseline. This document defines the first reusable visual/dynamo catalog and its engineering contract. It does not authorize immediate implementation of the full SVG editor.

## Goal
EliteSCADA should let an engineer assemble a real application from reusable, versioned objects instead of redrawing pumps, valves, trends, headers and faceplates for every project.

The library must be usable in at least four contexts:

1. process screens;
2. equipment popups/faceplates;
3. reusable project libraries;
4. copy/paste and Engineering Import/Export between applications.

Visual definitions must use the public Engineering model. The graphical editor must never own a private representation that cannot be exported.

## Visual design principles

- Prefer a high-performance HMI style: neutral background and restrained color use.
- Reserve strong colors primarily for abnormal conditions, alarms, interlocks and actionable states.
- Never encode important state by color alone; combine color with shape, icon, text or pattern.
- Keep equipment symbols visually simple enough to remain legible at small sizes.
- Use consistent state semantics across all objects.
- Separate process representation from decorative artwork.
- All components must support scaling without losing readability.
- State transitions must be testable with deterministic sample data.
- Runtime interaction must respect backend authorization; hiding a button is presentation, not security.

## Common dynamo contract

Every reusable visual component should eventually expose a common envelope containing:

- `key`: stable library key;
- `version`: semantic component/library version;
- `name` and description;
- category and tags;
- default dimensions and supported orientations;
- required and optional bindings;
- properties with defaults;
- context parameters;
- supported runtime states;
- supported commands;
- authorization requirements per action;
- optional popup/faceplate association;
- alarm/status summary behavior;
- metadata for Engineering Import/Export;
- migration information between component versions.

A component instance must preserve project-specific overrides when its library definition is updated whenever that can be done safely.

## Standard equipment state model

Where applicable, process dynamos should understand a common subset of these states:

- `Unknown`
- `Stopped`
- `Running`
- `Starting`
- `Stopping`
- `Manual`
- `Automatic`
- `Local`
- `Remote`
- `Interlocked`
- `Warning`
- `Alarm`
- `Fault`
- `BadCommunication`
- `Stale`
- `Disabled`

Specific equipment may extend this state set, but should not redefine the meaning of common states.

# Initial library catalog

## A. Process equipment

### `process.pump.centrifugal`
Primary reusable centrifugal pump symbol.

Typical bindings:
- running;
- fault;
- local/remote;
- manual/automatic;
- permissive/interlock summary;
- current;
- frequency/speed;
- command availability.

Properties:
- orientation;
- display label;
- flow direction;
- show motor;
- show state text;
- popup key.

Expected interaction:
- click/tap opens equipment faceplate;
- optional direct command only when explicitly enabled by engineering and authorization.

### `process.motor.standard`
General motor symbol independent of driven equipment.

Typical bindings:
- running;
- fault;
- current;
- speed/frequency;
- mode;
- thermal/overload state.

### `process.valve.onoff`
Two-position valve.

Typical bindings:
- open feedback;
- closed feedback;
- command open;
- command close;
- fault;
- local/remote;
- interlock.

Properties:
- valve body type;
- actuator visible/hidden;
- orientation;
- normal position.

### `process.valve.control`
Analog/modulating valve.

Typical bindings:
- position feedback;
- position setpoint;
- mode;
- fault;
- command availability.

### `process.tank.vertical`
Vertical tank/reservoir with optional animated level.

Typical bindings:
- level value;
- low/high state;
- quality;
- optional temperature/pressure summary.

Properties:
- minimum/maximum engineering scale;
- unit;
- show numeric value;
- show fill;
- show alarm markers.

### `process.blower.standard`
Blower/fan/compressor family baseline.

### `process.mixer.standard`
Agitator/mixer baseline.

### `process.pipe.standard`
Process line segment.

Properties:
- orientation/path;
- line class;
- thickness;
- process medium;
- normal-flow direction.

Optional bindings:
- flow active;
- line availability;
- alarm/abnormal state.

### `process.flow-arrow`
Flow-direction indicator that may animate only when process state warrants it.

## B. Instrumentation and values

### `instrument.numeric`
Canonical numeric process indicator.

Bindings:
- value;
- quality;
- optional alarm state.

Properties:
- engineering unit;
- decimals;
- prefix/suffix;
- min/max for abnormal highlighting;
- timestamp visibility.

### `instrument.bargraph`
Linear vertical/horizontal process bar.

### `instrument.level`
Level-specific value visualization for use outside a tank symbol.

### `instrument.gauge`
Use only where a gauge improves operational comprehension. It should not become the default merely because semicircles look industrious.

### `instrument.status`
Boolean/multi-state status indication with icon + text + state semantics.

### `instrument.text-binding`
Text whose content can come from a TAG, expression or static engineering property.

### `instrument.datetime`
Configurable date/time widget.

Supported modes:
- date and time;
- date only;
- time only.

Supported sources:
- EliteSCADA server clock;
- runtime TAG supplied by a communication driver;
- future expression/time-service source.

Displaying a PLC clock and synchronizing a PLC clock are separate capabilities. Clock synchronization must be an explicit command with authorization and audit.

## C. Commands and control widgets

### `control.command-button`
Generic command action.

Properties/bindings must distinguish:
- visual availability;
- runtime permissive state;
- command target;
- confirmation requirement;
- required authorization action.

### `control.mode-selector`
Manual/Automatic, Local/Remote or other enumerated mode selector.

### `control.setpoint-editor`
Numeric setpoint entry with bounds, engineering unit, confirmation and separate authorization from ordinary equipment commands.

The system must be able to grant a role permission to start/stop equipment while denying setpoint changes.

### `control.toggle`
Boolean command control for engineering cases where a toggle is operationally appropriate.

## D. Faceplates / popups

### `faceplate.pump.standard`
Initial pump faceplate.

Recommended contents:
- equipment identification;
- operational state;
- mode;
- running/fault/local/interlock indications;
- current;
- frequency/speed;
- start/stop controls;
- setpoint control where applicable;
- active alarms for the equipment;
- short trend section;
- link to detailed trend/alarm history.

### `faceplate.valve.onoff`
Open/close control, feedback disagreement and interlock status.

### `faceplate.valve.control`
Position feedback/setpoint, mode and trend.

### `faceplate.motor.standard`
Motor measurements, status, controls and alarms.

### `faceplate.instrument.standard`
Process value, quality, alarm state, limits and historical trend.

## E. Trends

### `trend.standard`
Reusable trend object with multiple pens.

Each pen should eventually support:
- key and label;
- TAG/expression target;
- historical or runtime mode;
- unit;
- axis association;
- scale policy;
- visible/hidden state;
- interpolation/display policy;
- sampling/history query behavior;
- optional alarm/event markers.

A trend configuration may be:
- engineered and fixed in a screen;
- opened from a faceplate;
- assembled ad hoc by an authorized runtime user;
- optionally saved as a user/project view according to authorization.

The same trend engine should serve current/runtime data and historical data instead of maintaining unrelated widgets.

## F. Persistent application shell

### `shell.header.standard`
Configurable application header.

Candidate contents:
- application/site title;
- current screen title;
- logged-in user;
- system/communication health;
- date/time widget;
- navigation controls.

### `shell.footer.alarms`
Persistent alarm strip/footer.

Candidate contents:
- most recent/highest-priority alarms;
- unacknowledged count;
- alarm-navigation action;
- optional driver/system health indication.

### `shell.navigation.standard`
Configurable navigation region, including visibility rules by role/area/application.

### `shell.content-region`
Placeholder defining where selected process screens are mounted while persistent header/footer/navigation remain in place.

Persistent regions must be configurable engineering entities, not hard-coded React markup.

## G. Layout and utility components

- `layout.panel`
- `layout.group`
- `layout.tabs`
- `layout.grid`
- `layout.separator`
- `utility.label`
- `utility.icon`
- `utility.image`
- `utility.link`

These objects should be intentionally boring. Boring layout primitives are vastly more useful than fifty bespoke rectangles named after whatever project first needed them.

# First implementation priority

When visual-library implementation becomes the active development phase, build and validate this minimum set first:

1. `process.pump.centrifugal`
2. `process.valve.onoff`
3. `process.valve.control`
4. `process.tank.vertical`
5. `instrument.numeric`
6. `instrument.status`
7. `control.command-button`
8. `control.setpoint-editor`
9. `faceplate.pump.standard`
10. `trend.standard`
11. `shell.header.standard`
12. `shell.footer.alarms`

This set is sufficient to build a credible pumping/process demonstration application while exercising reusable bindings, commands, authorization, history, alarms and persistent layout.

# Reference-research phase

Before finalizing the visual language, perform a dedicated reference study of real industrial HMI/SCADA screens and process-flow diagrams.

Research should compare:
- overview screens;
- process detail screens;
- pump/valve faceplates;
- alarm presentation;
- trend interaction;
- navigation structures;
- persistent header/footer conventions;
- high-performance HMI patterns;
- common failure modes such as excessive color, gradients, decorative 3D equipment and poor information hierarchy.

References are for functional and ergonomic study. EliteSCADA should create its own original SVG/component artwork rather than copying vendor graphics.

# Cross-project reuse and clipboard

Copy/paste must operate on Engineering Fragments rather than browser-only visual state.

A copied component or group may include:
- visual elements;
- dynamo/library references;
- equipment references;
- TAG bindings;
- optional dependent templates;
- optional alarms/trends/popups;
- metadata necessary for safe re-import.

Paste into another project must support preview of missing dependencies, collisions and remapping before apply.

# Testing expectations

Every baseline dynamo should eventually have:
- unit tests for engineering validation;
- serialization round-trip tests;
- deterministic state fixtures;
- browser interaction tests;
- visual regression snapshots for key states;
- authorization tests for commands and setpoint actions;
- backward-compatible component-version migration tests.

The component library is part of the engineering model, not a collection of loose SVG files.