# Wave 14 C07 — Built-in Dynamo inventory and migration baseline

Base audited: `e0189165e288452519b1c17c9e1ee72986498d49` (validated W14-C05 candidate, PR #216).

This document records the exact built-in Dynamo library found in `src/Scada.Api/Runtime/BuiltinDynamoLibrary.cs` before the C07 maturity work. It is the migration baseline; C07 must evolve these definitions rather than create a parallel ninth library.

## Current canonical inventory

| # | Stable type key | Friendly name | Category | Library version | Default size | Intended semantic role | Current public interface | Current state behavior | Current command behavior |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `dynamo.pump.standard` | Bomba centrífuga | pump | 1.0.0 | 132 × 92 | Centrifugal/process pump | `equipmentPath` placeholder only | `Running` green lamp; `Fault` red lamp | None |
| 2 | `process.pump.submersible` | Bomba submersível | pump | 1.0.0 | 94 × 132 | Submersible pump | `equipmentPath` placeholder only | `Running` green lamp; `Fault` red lamp | None |
| 3 | `process.motor.standard` | Motor padrão | motor | 1.0.0 | 106 × 92 | Standard motor | `equipmentPath` placeholder only | `Running` green lamp; `Fault` red lamp | None |
| 4 | `process.motor.vfd` | Motor com inversor | motor | 1.0.0 | 138 × 96 | Motor driven by VFD | `equipmentPath` placeholder only | `Running` green lamp; `Fault` red lamp | None |
| 5 | `process.valve.onoff` | Válvula abre/fecha | valve | 1.0.0 | 128 × 92 | Discrete on/off valve | `equipmentPath` placeholder only | `Open` green lamp; `Fault` red lamp | None |
| 6 | `process.valve.control` | Válvula de controle | valve | 1.0.0 | 128 × 108 | Modulating/control valve | `equipmentPath` placeholder only | `Fault` red lamp; static `%` label | None |
| 7 | `process.tank.vertical` | Tanque vertical | tank | 1.0.0 | 108 × 158 | Vertical tank/vessel | `equipmentPath` placeholder only | `High` amber lamp; `Fault` red lamp; static liquid geometry | None |
| 8 | `process.tank.horizontal` | Tanque horizontal | tank | 1.0.0 | 168 × 100 | Horizontal tank/vessel | `equipmentPath` placeholder only | `High` amber lamp; `Fault` red lamp; static liquid geometry | None |

## Identity and representation

The definitions have stable GUID identities generated from sequence `1..8`, using the `43000000-0000-0000-0000-XXXXXXXXXXXX` namespace. Child visual elements likewise have stable GUIDs in the `43100000-0000-0000-0000-XXXXXXXXXXXX` namespace.

Each definition is canonical `DynamoEngineeringDto`, not a renderer-private asset. Internal composition is made from canonical `core.*` primitives. Runtime instances are `core.group` visual elements carrying `dynamoKey` and optional `equipmentPath`; the runtime composer resolves the referenced definition rather than copying its children into the Screen.

The current library metadata is:

- `category`: `pump`, `motor`, `valve`, or `tank`;
- `defaultWidth` / `defaultHeight`;
- `libraryVersion = 1.0.0`;
- context `usage = process-screen`;
- metadata `builtinLibrary = true`;
- metadata `equipmentPathBinding = {equipmentPath}`.

## Current public-interface gap

The Engineering contracts already support typed Dynamo parameters (`Boolean`, `Number`, `String`, `EquipmentPath`, `TagReference`) and instance parameter values. The eight built-ins currently declare no typed `Parameters` collection. Their only effective configuration surface is the instance `equipmentPath`, substituted into child TAG binding targets such as `{equipmentPath}.Running`.

Therefore C07 must not describe the current lamp bindings as a mature public interface. The migration target is to expose equipment-specific typed public properties/inputs while keeping internal child shapes encapsulated.

## Current state-model gap

State visualization is currently implemented mostly as small colored ellipses whose `visible` property is directly TAG-bound. There is no definition-level deterministic state-priority contract. In particular:

- `Fault` does not formally override `Running`/`Open`;
- bad/unavailable quality is not represented;
- local/remote, manual/auto, interlocked/blocked, pending and feedback mismatch are absent;
- critical states rely primarily on color and therefore do not meet the C07 non-color-only requirement;
- tank level and control-valve position are static artwork rather than typed public values driving composition.

C07 should introduce deterministic state semantics without creating a renderer-only state authority.

## Current command boundary

None of the eight built-ins currently exposes a command action. This is preferable to adding an unsafe shortcut. When command-capable Dynamos are introduced, actions must use the authenticated/authorized runtime command path and TAG command authority; definitions must never write to a Driver directly.

## C07 migration constraints

1. Preserve all eight stable type keys.
2. Preserve definition identity unless a documented migration requires otherwise.
3. Version schema/public-interface changes explicitly; do not overload `libraryVersion` as the only compatibility signal.
4. Keep instance-to-definition references canonical.
5. Keep child composition encapsulated from ordinary Screen authoring.
6. Reuse C05 canonical visual property types and capability metadata where applicable.
7. Engineering preview/test inputs must remain simulation-only and must never write TAGs or Drivers.
8. Public properties/events/actions are the contract C08 may browse later; internal child shapes are not.
