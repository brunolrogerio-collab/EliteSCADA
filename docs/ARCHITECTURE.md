# SCADA Platform — Architecture Baseline v0.1

## Principle
The SCADA Core must not depend on a specific PLC protocol, database engine, or UI technology.

## Initial boundaries

- `Scada.Core`: TAG model, quality, current-value cache, domain events.
- `Scada.Api`: REST/WebSocket boundary for clients and integrations.
- `Scada.Historian`: persistence of time-series values and retention policies.
- `Scada.Drivers`: communication-driver contracts and open drivers.
- `Scada.DriverHost`: out-of-process host for communication drivers.
- `scada-web`: browser runtime and, later, SVG screen editor.
- `scada-python`: sandboxed scripting/analytics service (later phase).
- `scada-sdk`: public extension SDK (later phase).

## Mandatory data flow

Device/Source -> Driver -> Tag Engine/Current Cache -> Event Bus -> Historian / Alarm Engine / WebSocket / Scripts

No frontend component may access a driver directly. No driver may know about UI screens.

## Tag state
A runtime value is always a tuple of value + timestamp + quality + source.

Initial quality states:
GOOD, UNCERTAIN, BAD, BAD_COMM, BAD_CONFIG, BAD_DEVICE, STALE, DISABLED.

## Technology baseline
- Backend/Core: .NET 10 LTS
- Frontend: React 19.2 + TypeScript
- Configuration DB: PostgreSQL
- Historian: PostgreSQL + TimescaleDB
- Realtime UI: WebSocket
- Public integration: REST API
- Messaging: MQTT
- Scripting/analytics: Python (sandboxed, later)
