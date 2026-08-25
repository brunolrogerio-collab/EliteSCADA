# ADR-001 — Core boundaries and dependency direction

Status: Accepted
Date: 2026-08-25

## Decision
The SCADA Core is the owner of runtime TAG semantics and domain events. It must not reference protocol drivers, database providers, or web UI projects.

Allowed direction:
- API -> Core
- Historian -> Core
- Drivers -> Core
- DriverHost -> Drivers + Core
- Web -> API only

Forbidden direction:
- Core -> Drivers
- Core -> Historian provider
- Core -> Web
- Driver -> Web

## Rationale
This keeps a TAG independent of its source and permits future proprietary communication modules, alternative historians, and external database connectors without redesigning the Core.

## Consequences
Integration happens through public contracts/events. Some features require additional adapters, but failures and licensing boundaries remain isolated.
