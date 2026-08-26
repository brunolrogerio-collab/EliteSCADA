# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** PAUSED by explicit user request.

## Latest task

The user added two stable product requirements and asked that they be incorporated into the project memory/roadmap:

1. **BACnet communication-driver support** as an additional future industrial/building-automation protocol target.
2. **Developer-selectable Engineering/development UI language** among Portuguese, English and Spanish across the engineering environment.

No runtime/product code was developed. This task changed only product-goal/architecture/roadmap documentation, and the development pause remains in effect.

## BACnet requirement added

BACnet is now a locked future communication-driver target alongside MQTT and OPC UA.

The intended protocol direction is now:

- Modbus TCP — implemented real-driver baseline;
- MQTT — planned first-class integration;
- OPC UA — planned first-class interoperability integration;
- BACnet — planned driver protocol, especially relevant to building automation/BMS and BACnet-capable controllers/devices;
- Siemens S7 ISO Connection — first intended installable driver-module target;
- Allen-Bradley — later explicit research/module target;
- additional first-party/third-party drivers through the installable Driver Module framework.

The user considers this protocol family broad enough to target more than 90% of practical PLC/controller needs in the intended market. Repository documentation records that as a **planning hypothesis**, not an externally verified market statistic. Validate any numerical market-coverage claim before presenting it publicly.

All future protocols continue to use the common Driver SDK/DriverHost, Data Source, TAG, quality, Engineering, security, audit and persistence boundaries.

## Engineering/development UI localization requirement added

The developer/engineering user must be able to select the EliteSCADA Engineering interface language among:

- Portuguese (Brazil) — `pt-BR`;
- English — `en`;
- Spanish — `es`.

The selection applies consistently across developer-facing Engineering surfaces, including:

- Data Sources and driver configuration;
- TAG engineering;
- database/historian configuration and diagnostics;
- alarm engineering;
- Equipment Templates, Equipment and Dynamos;
- screen and popup creation/editing;
- trends;
- project/revision/save/publish/activate workflows;
- users, roles and security administration;
- driver/module administration and diagnostics;
- menus, dialogs, property editors, validation messages and product-owned engineering help text.

Changing interface language must **not** change stable Engineering IDs, TAG paths, communication addresses, internal enum/storage values, public schema keys, revision identity or runtime semantics. Product-owned UI strings should use localization/resource keys instead of becoming authoritative engineering data.

The language preference should be persistable per user/profile when that subsystem exists.

This requirement is specifically for the Engineering/development interface. Multilingual content inside runtime HMI/process screens is a separate future capability and must not be assumed to exist merely because the editor is localized.

## Files changed in this task

- `PROJECT GOAL.md`
  - added BACnet to the locked industrial communication direction;
  - recorded the broad-market-coverage planning rationale with the >90% figure explicitly treated as unverified until validated;
  - added the complete Portuguese/English/Spanish Engineering UI localization requirement.

- `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`
  - added BACnet as an explicit protocol target;
  - aligned the protocol-coverage rationale with Modbus TCP, MQTT, OPC UA, S7 and future Allen-Bradley support.

- `docs/ADR-008-ENGINEERING-UI-LOCALIZATION.md`
  - new accepted architectural decision defining Portuguese (`pt-BR`), English (`en`) and Spanish (`es`) localization for the Engineering/development interface;
  - keeps localization as presentation/user preference and Engineering contracts language-neutral;
  - distinguishes editor localization from future multilingual runtime-HMI content.

- `docs/ROADMAP.md`
  - added a future BACnet integration slice;
  - added a future Engineering UI localization slice;
  - added locked future-requirement sections for BACnet/protocol coverage and Engineering UI localization.

## Repository state before this LAST CHANGE update

Live `main` HEAD observed immediately before updating this file:

`e3c0f36638fd940a3e38211adaeeb983aba64046` — `Add BACnet and engineering UI localization to roadmap`

This `LAST CHANGE.md` update creates a newer commit. Always fetch live `main` HEAD on the next task.

## Previous driver targets still locked

- First intended installable module: Siemens S7 ISO Connection.
- At implementation time, research Node-RED S7 work and other public/open-source implementations; reuse only after license review and industrial suitability validation.
- Allen-Bradley remains a future research/module target with protocol/library/family scope intentionally undecided until evidence is gathered.

## Previous functional milestone

The latest functional/product milestone before the continuity/documentation work remains:

`fdaa093f8ba735e447cb871beaf515f4417e7559` — `Secure alarm shelving lifecycle`

Alarm shelving is already integrated into `main`.

## Current development pause

The user previously requested development to stop after a ChatGPT/platform error.

**Do not continue product implementation automatically.**

Documentation/goal maintenance is allowed when explicitly requested. New product development resumes only after an explicit instruction to continue.

## Immediate next product slice when development is explicitly resumed

According to the current roadmap, the immediate next technical slice remains:

1. introduce a first-class operational command domain;
2. enforce/audit `CommandExecute` against real command objects;
3. extend authorization to sensitive read/realtime/WebSocket surfaces.

Later roadmap slices include historian retention/downsampling, MQTT, OPC UA, BACnet, installable Driver Modules with Siemens S7 as first target, Engineering UI localization, XLSX Engineering, diagnostics and frontend hardening.

## Resume checklist

Before any EliteSCADA task:

1. Read `PROJECT GOAL.md` completely.
2. Read this `LAST CHANGE.md` completely.
3. Fetch live `main` HEAD/recent commits when repository state matters.
4. Read `docs/ROADMAP.md` when planning implementation.
5. For protocol/module work, read `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`.
6. For Engineering UI localization work, read `docs/ADR-008-ENGINEERING-UI-LOCALIZATION.md`.
7. Do not rely on old chat/branch assumptions.
8. Validate implementation through GitHub CI when .NET cannot be executed locally in the ChatGPT environment.
9. Immediately before the final user-facing response, update this file again.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.

This rule remains in force until the user explicitly changes it.