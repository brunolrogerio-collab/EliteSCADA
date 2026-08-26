# EliteSCADA — Architecture Baseline v0.1

## Principle
The SCADA Core must not depend on a specific PLC protocol, database engine, or UI technology.

The engineering model is authoritative. Runtime, editor, import/export, reusable libraries and administrative tools consume the same public, versioned model rather than maintaining private representations of project configuration.

## Initial boundaries

- `Scada.Core`: TAG model, quality, current-value cache, domain events and core access semantics.
- `Scada.Api`: REST/WebSocket boundary for clients and integrations; future server-side authorization enforcement belongs at or below this boundary.
- `Scada.Historian`: persistence of time-series values and retention policies.
- `Scada.Drivers`: communication-driver contracts and open drivers.
- `Scada.DriverHost`: out-of-process host for communication drivers.
- `Scada.Engineering`: public versioned engineering contracts, import/export, validation, reusable assets and project composition.
- `scada-web`: browser runtime and, later, SVG screen/editor and engineering UX.
- `scada-python`: sandboxed scripting/analytics service (later phase).
- `scada-sdk`: public extension SDK (later phase).

## Mandatory data flow

Device/Source -> Driver -> Tag Engine/Current Cache -> Event Bus -> Historian / Alarm Engine / WebSocket / Scripts

No frontend component may access a driver directly. No driver may know about UI screens.

## Tag state
A runtime value is always a tuple of value + timestamp + quality + source.

Initial quality states:
GOOD, UNCERTAIN, BAD, BAD_COMM, BAD_CONFIG, BAD_DEVICE, STALE, DISABLED.

## Reusable engineering and application composition

EliteSCADA is designed around reusable project structures rather than isolated screen copies.

- Equipment templates and equipment instances represent reusable data/process structures.
- Dynamos and visual definitions represent reusable graphical structures.
- Reusable definitions are versioned and portable through Engineering Import/Export.
- Cross-project copy/paste is implemented conceptually as a validated Engineering Fragment with dependency handling, never as browser-only opaque clipboard state.
- Screens may be composed inside a configurable application shell with persistent header, footer, alarm and navigation regions.

The reuse model is conceptually inspired by established SCADA class/instance approaches such as Elipse E3 XObject/XControl, while EliteSCADA keeps its own engineering contracts and implementation.

## Trends

Trend charts are first-class engineering/runtime objects. A trend contains Pens whose sources may be historical TAG data or live/runtime bindings. Historical query semantics remain separate from realtime subscriptions even when both are rendered in one chart.

TimescaleDB is the intended historical backend, but trend engineering definitions must not depend directly on TimescaleDB-specific storage details.

## Access model

Security is role/capability based and configurable by application. Roles are not restricted to one hard-coded hierarchy.

The model must distinguish capabilities such as view, command execution, setpoint/process-value write, alarm acknowledgement, shelving, trend use, engineering changes and administrative actions.

UI visibility may reflect access rules, including hiding screens or controls, but the backend/API remains the actual security boundary and must independently enforce protected operations.

## Time semantics

Visual date/time elements may use the EliteSCADA server clock or a TAG-provided time source such as a PLC clock acquired through a driver. Display formatting and timezone conversion are presentation concerns.

Active synchronization of PLC/RTU clocks is a separate future driver operation that must be explicit, permission controlled and auditable.

## Technology baseline
- Backend/Core: .NET 10 LTS
- Frontend: React 19.2 + TypeScript
- Configuration DB: PostgreSQL
- Historian: PostgreSQL + TimescaleDB
- Realtime UI: WebSocket
- Public integration: REST API
- Messaging: MQTT
- Scripting/analytics: Python (sandboxed, later)
