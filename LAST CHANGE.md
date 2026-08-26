# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** PAUSED by explicit user request.

## Latest task

The user added two future industrial-driver targets and asked that they be preserved for implementation at the appropriate time.

No runtime/product code was developed. This task changed project documentation/architecture only, and the development pause remains in effect.

### New locked driver direction

1. **First installable driver module target: Siemens S7 ISO Connection**
   - The first intended installable communication-driver module is for Siemens PLC communication compatible with S7 ISO Connection.
   - This is a future implementation target, not active development now.
   - When its implementation slice begins, research existing public/open-source S7 work, including relevant Node-RED nodes/libraries and other reusable implementations.
   - Existing work may be reused only after license/attribution/distribution obligations are checked and the technical behavior is validated for an industrial SCADA runtime.
   - Research must determine actual Siemens PLC-family coverage, address/data-type support, connection modes, reads/writes, reconnect behavior, diagnostics and failure semantics before production scope is locked.

2. **Future Allen-Bradley driver target**
   - A future installable communication module for Allen-Bradley PLCs is part of the product direction.
   - No protocol/library/family scope is locked yet.
   - At the appropriate time, research public protocol documentation, open-source implementations, available libraries, licensing, representative equipment/simulators and legally reusable approaches.
   - Manufacturer documentation/cooperation should be used when available, but the architectural goal must not depend entirely on obtaining direct manufacturer support.
   - Implementation scope is decided only after that research produces enough evidence for reliable support.

### Files updated

- `PROJECT GOAL.md` now records Siemens S7 ISO Connection as the first intended installable driver module target and Allen-Bradley as a later research/module target.
- `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md` now records the same protocol/module priority and research constraints.

### Repository state before this LAST CHANGE update

Live `main` HEAD observed immediately before updating this file:

`4f42a83716590a0cc6c8a083ee9ab8d80a03f05f` — `Record first installable driver targets`

This `LAST CHANGE.md` update creates a newer commit. Always fetch live `main` HEAD on the next task.

## Current industrial communication north

- Modbus TCP: implemented real-driver baseline.
- MQTT: planned first-class integration.
- OPC UA: planned first-class integration.
- Installable/versioned Driver Module framework: locked product requirement.
- First intended installable module: Siemens S7 ISO Connection.
- Later research target: Allen-Bradley PLC communication module.
- All drivers/modules must remain behind the common Driver SDK/DriverHost, Data Source, TAG, Engineering, security, audit and runtime-quality boundaries.
- Plugin-owned configuration participates in public/versioned Engineering Import/Export and must remain preservable even when its module is missing/incompatible.

## Previous functional milestone

The latest functional/product milestone before the continuity/documentation work remains:

`fdaa093f8ba735e447cb871beaf515f4417e7559` — `Secure alarm shelving lifecycle`

Alarm shelving is already integrated into `main`.

## Current development pause

The user previously requested development to stop after a ChatGPT/platform error.

**Do not continue product implementation automatically.**

Documentation/goal maintenance is allowed when explicitly requested. New product development resumes only after an explicit instruction to continue.

## Next product slice when development is explicitly resumed

According to the current roadmap, the immediate next technical slice remains:

1. introduce a first-class operational command domain;
2. enforce/audit `CommandExecute` against real command objects;
3. extend authorization to sensitive read/realtime/WebSocket surfaces.

Later roadmap/goal slices include historian retention/downsampling, MQTT, OPC UA, installable Driver Modules, Siemens S7 as the first intended module, future Allen-Bradley research, XLSX Engineering, diagnostics and frontend hardening.

## Resume checklist

Before any EliteSCADA task:

1. Read `PROJECT GOAL.md` completely.
2. Read this `LAST CHANGE.md` completely.
3. Fetch live `main` HEAD/recent commits when repository state matters.
4. Read `docs/ROADMAP.md` when planning implementation.
5. For protocol/module work, read `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md` before designing or coding.
6. Do not rely on old chat/branch assumptions.
7. Validate implementation through GitHub CI when .NET cannot be executed locally in the ChatGPT environment.
8. Immediately before the final user-facing response, update this file again.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.

This rule remains in force until the user explicitly changes it.