# Interface Validation Milestone

Status: **SUPERSEDED AS AN EARLY OWNER-FACING PACKAGE / retained as acceptance history**.

The earlier milestone proposed an owner-facing Windows interface preview immediately after communication diagnostics. On 2026-08-27 the product owner deliberately changed the first true validation gate.

The first build presented as the actual EliteSCADA product validation version is now:

# EliteSCADA v0.1 — Full Product Validation Preview

Authoritative plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

## New gate

The first owner-facing product validation must allow the complete supervisory application path:

```text
Engineering
  -> Data Source/TAG/Alarm/Historian
  -> Screen/Popup/Dynamo
  -> visual objects/bindings
  -> Client Visual Python
  -> Save/Revision
  -> Publish
  -> Activate
  -> graphical Runtime
  -> restart/recovery of Active Revision
```

Therefore the product-owner package is **not** delivered merely because Runtime/Engineering/Audit and current operational surfaces are usable.

Client Visual Python and functional graphical Engineering/Runtime are mandatory before the first build is treated as the true v0.1 validation preview.

## Internal validation before v0.1

Internal CI artifacts, packaging spikes and development builds may be created earlier when useful to prove:

- Windows/runtime startup mechanics;
- PostgreSQL/TimescaleDB service composition;
- browser/backend packaging;
- launcher approaches;
- clean-machine assumptions;
- CI/package smoke behavior.

Those builds are development evidence only and must not be confused with or branded as the owner-facing `EliteSCADA v0.1 — Full Product Validation Preview`.

## Required v0.1 package characteristics

When Wave 13 is reached, the Windows x64 owner-facing package must include or reliably automate/document:

- EliteSCADA backend/runtime;
- Web UI;
- database/services needed for the preview;
- launcher/startup path;
- known bootstrap/login procedure;
- full demo project built through normal product surfaces;
- visible version/build identity;
- logs/diagnostics useful for feedback;
- no production credentials or committed secrets.

The owner must not need to reconstruct a developer environment using `dotnet run`, npm/Vite, Git, solution knowledge or manual schema migrations.

## Acceptance before delivery

The final integrated/package candidate requires:

- backend build/tests green;
- frontend build green;
- runtime smoke green;
- Chromium E2E green;
- package/install/startup smoke green;
- full vertical HMI flow green;
- restart/Active Revision recovery green;
- no known P0/P1 issue preventing meaningful validation.

## Relationship to external protocols

Additional production protocol families remain postponed until after v0.1 owner validation/correction unless the product owner deliberately changes the gate.

Modbus TCP, Simulation, Client/Server Memory and Gateway are sufficient protocol/source coverage for the first product validation.

Merged MQTT/OPC UA/BACnet/S7/Allen-Bradley research and Driver SDK convergence remain architecture inputs, not authorization to start production drivers.

Preferred post-v0.1 protocol progression is documented in `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md` and `docs/ROADMAP.md`.

## Historical note

The original interface-preview milestone was useful in forcing the project to prioritize usability before protocol proliferation. That principle remains. What changed is the **depth of the first owner-facing validation**: the owner wants to evaluate a complete SCADA creation/Runtime loop, including Python and graphical Engineering, rather than a pre-graphical platform preview.