# ADR-005 — Product composition, reusable libraries, trends and access model

## Status
Accepted as product direction. Not scheduled for immediate implementation.

## Context
EliteSCADA must support applications that can be engineered once, reused across projects, and adapted to different operational organizations without forking core code.

The product therefore adopts a generic class/instance reuse model for both server/data structures and graphical components. Reusable definitions expose explicit public properties, bindings and context, while instances retain stable links to their definitions and application-specific overrides. All reuse semantics remain part of EliteSCADA's own public Engineering model and implementation.

## Decision 1 — Reusable application libraries

EliteSCADA engineering must support reusable, versioned library definitions and instances across applications.

The existing concepts evolve toward two complementary families:

- **Data/equipment definitions**: reusable equipment templates and reusable server-side engineering structures. An instance supplies application-specific paths, properties, bindings and context.
- **Visual definitions**: reusable Dynamos/visual components. An instance supplies equipment references, TAG bindings, properties and context.

A reusable definition may contain nested reusable definitions where dependency validation remains deterministic.

Library instances must retain a link to their definition/version so that a definition can be improved and dependent instances can be assessed or upgraded deliberately. Upgrade behavior must never silently destroy instance overrides.

Reusable libraries must themselves participate in Engineering Import/Export and must be movable between projects without requiring the graphical editor.

## Decision 2 — Cross-project copy/paste and engineering fragments

Copy/paste between projects is a first-class engineering workflow, not merely a browser clipboard feature.

Copying an engineering object or selection must be representable as a portable **Engineering Fragment** using the canonical schema. A fragment may contain screens, popups, visual elements, Dynamos, templates, equipment, TAGs, alarms and other required dependencies.

Paste/import must use the normal validation and preview pipeline and explicitly classify:

- new objects;
- matches with existing stable IDs or logical keys;
- dependency conflicts;
- missing dependencies;
- version differences;
- references that can be rebound.

The editor should be able to offer dependency-closure options such as "copy selected only" and "copy selected with required dependencies".

No object format should exist only inside the browser clipboard.

## Decision 3 — Trend charts

EliteSCADA must include an engineering-configurable trend/chart subsystem.

A Trend definition contains one or more **Pens**. Each Pen may obtain values from:

- historical TAG data;
- live/runtime TAG values;
- later, expressions or calculated series using the same binding model.

A Pen must be able to define at least its source/binding, display label, engineering unit, axis assignment, scale behavior and presentation settings. Historical Pens additionally define/query a time range and may later expose aggregation/downsampling options appropriate to TimescaleDB.

Trends can exist as project engineering objects placed on screens/popups and, where allowed by the application's access policy, users may create ad-hoc or saved runtime trend views without changing protected project engineering.

Historical and runtime data must remain conceptually distinct even when displayed on the same chart.

## Decision 4 — Configurable access hierarchy

EliteSCADA must provide users, roles and configurable application-specific access policies.

Roles are not limited to a hard-coded hierarchy. A project may define roles such as Administrator, Engineer, Supervisor, Operator or Viewer, but the actual roles and capabilities are application configuration.

Capabilities must be granular enough to distinguish, at minimum:

- view an area/screen/object;
- execute an operational command;
- change a setpoint or writable process value;
- acknowledge alarms;
- shelve alarms;
- use or save ad-hoc trends;
- change engineering configuration;
- manage users/roles;
- perform system/administrative functions.

A permission may influence visibility and enabled/disabled state in the UI, including hiding whole screens, menus, objects or controls. However, UI visibility is never the security boundary. The API/backend must enforce the same permission for every protected operation.

The access model must support application-defined scope, for example area, equipment, screen, TAG or command scope, rather than requiring one global role to grant identical access everywhere.

Engineering Import/Export must serialize role/policy definitions without exporting password hashes, authentication secrets or external identity credentials.

## Decision 5 — Application shell and persistent regions

Screens may be hosted inside a configurable application shell with persistent regions such as:

- header;
- footer;
- alarm banner/summary;
- navigation area;
- optional side regions.

These regions are engineering objects, not hard-coded React layout. They may be globally defined and selectively overridden by an application or screen where allowed.

Typical reusable widgets include project/application identification, logged-in user, navigation, alarm summary and date/time displays.

## Decision 6 — Date/time source binding

A date/time visual element must support configurable formatting modes such as:

- date and time;
- date only;
- time only.

Its time source must be bindable. Required source modes are:

- **Server clock**: time supplied by the EliteSCADA server/runtime;
- **TAG binding**: time supplied by a TAG, for example a PLC/RTU clock acquired through a communication driver.

Timezone and display formatting are presentation configuration and must not alter the underlying timestamp semantics.

Displaying a PLC time TAG is separate from actively synchronizing device clocks. Future clock-synchronization commands through drivers must be explicit, auditable, permission-controlled operations and must never happen implicitly because a visual widget is bound to a time source.

## Decision 7 — Engineering model remains authoritative

All features described by this ADR must be represented through public, serializable, versioned engineering contracts before or alongside their graphical editor implementation.

The editor is a client of the engineering model. It must not become the only place where libraries, trends, access rules, shell regions or bindings can be created and understood.

## Implementation ordering

This ADR records the product north only. It does not change the current immediate implementation gate. Persistence and real communication-driver work remain ahead of the full graphical editor/library experience.

When implementation reaches these features, preferred order is:

1. access-policy domain model and server-side enforcement;
2. reusable library/version semantics on top of the existing template/equipment/dynamo model;
3. engineering fragments and dependency-aware cross-project copy/paste;
4. trend engineering model and historian-backed query path;
5. configurable application shell and persistent regions;
6. graphical editor workflows for all of the above.

## Consequences

- Reuse between applications becomes an architectural requirement rather than a convenience feature.
- Project engineering remains portable and testable without the UI.
- Operational permissions can vary significantly by application without branching product code.
- Trends can mix historical and runtime Pens while preserving source semantics.
- Header/footer/alarm regions are reusable engineering, not hard-coded application chrome.
- Device clock synchronization is kept separate from visual time display, reducing accidental control-side effects.
