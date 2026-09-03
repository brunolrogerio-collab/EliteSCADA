# Wave 14 C07 — Built-in Dynamo inventory and migration baseline

Base audited: `e0189165e288452519b1c17c9e1ee72986498d49` (validated W14-C05 candidate, PR #216).

This document records the exact built-in Dynamo library found in `src/Scada.Api/Runtime/BuiltinDynamoLibrary.cs` before the C07 maturity work and the public contract introduced by C07. The baseline matters because C07 evolves these definitions rather than creating a parallel ninth library.

## Canonical inventory and C07 public interface

All eight definitions retain `libraryVersion = 1.0.0`, their stable type keys, GUID identity and existing geometry. C07 adds explicit `publicInterfaceVersion = 1` and `stateModelVersion = 1` metadata.

| # | Stable type key | Friendly name | Category | Default size | C07 typed public interface |
|---|---|---|---|---|---|
| 1 | `dynamo.pump.standard` | Bomba centrífuga | pump | 132 × 92 | `equipmentPath: EquipmentPath`; `running: TagReference`; `fault: TagReference`; `startCommandKey: String`; `stopCommandKey: String` |
| 2 | `process.pump.submersible` | Bomba submersível | pump | 94 × 132 | `equipmentPath: EquipmentPath`; `running: TagReference`; `fault: TagReference`; `startCommandKey: String`; `stopCommandKey: String` |
| 3 | `process.motor.standard` | Motor padrão | motor | 106 × 92 | `equipmentPath: EquipmentPath`; `running: TagReference`; `fault: TagReference`; `startCommandKey: String`; `stopCommandKey: String` |
| 4 | `process.motor.vfd` | Motor com inversor | motor | 138 × 96 | `equipmentPath: EquipmentPath`; `running: TagReference`; `fault: TagReference`; `processValue: TagReference`; `setpoint: TagReference`; `feedback: TagReference`; `startCommandKey: String`; `stopCommandKey: String` |
| 5 | `process.valve.onoff` | Válvula abre/fecha | valve | 128 × 92 | `equipmentPath: EquipmentPath`; `open: TagReference`; `closed: TagReference`; `fault: TagReference`; `openCommandKey: String`; `closeCommandKey: String` |
| 6 | `process.valve.control` | Válvula de controle | valve | 128 × 108 | `equipmentPath: EquipmentPath`; `processValue: TagReference`; `setpoint: TagReference`; `feedback: TagReference`; `fault: TagReference`; `commandKey: String` |
| 7 | `process.tank.vertical` | Tanque vertical | tank | 108 × 158 | `equipmentPath: EquipmentPath`; `processValue: TagReference`; `high: TagReference`; `fault: TagReference` |
| 8 | `process.tank.horizontal` | Tanque horizontal | tank | 168 × 100 | `equipmentPath: EquipmentPath`; `processValue: TagReference`; `high: TagReference`; `fault: TagReference` |

The command-key parameters define the authoring surface only. They do not create a direct Driver write path. Runtime command execution remains subject to the existing authenticated/authorized command boundary.

## Identity and representation

The definitions have stable GUID identities generated from sequence `1..8`, using the `43000000-0000-0000-0000-XXXXXXXXXXXX` namespace. Child visual elements likewise have stable GUIDs in the `43100000-0000-0000-0000-XXXXXXXXXXXX` namespace.

Each definition is canonical `DynamoEngineeringDto`, not a renderer-private asset. Internal composition is made from canonical `core.*` primitives. Runtime instances are `core.group` visual elements carrying `dynamoKey`, optional legacy `equipmentPath`, and versioned `dynamoParameters`; the runtime composer resolves the referenced definition rather than copying its children into the Screen.

The library metadata is:

- `category`: `pump`, `motor`, `valve`, or `tank`;
- `defaultWidth` / `defaultHeight`;
- `libraryVersion = 1.0.0`;
- context `usage = process-screen`;
- metadata `builtinLibrary = true`;
- metadata `equipmentPathBinding = {equipmentPath}`;
- metadata `publicInterfaceVersion = 1`;
- metadata `stateModelVersion = 1`.

## Legacy baseline and compatibility

At the audited base, the eight built-ins declared no typed `Parameters` collection. Their only effective configuration surface was the instance `equipmentPath`, substituted into child TAG binding targets such as `{equipmentPath}.Running`.

C07 keeps that behavior as a compatibility fallback. The public `equipmentPath: EquipmentPath` parameter is projected to and synchronized with the legacy instance field during authoring. A typed value takes precedence in runtime projection; if absent, the legacy field remains valid.

State-lamp bindings now opt in to their public TAG parameter through binding metadata `dynamoParameter=<key>`. A supplied public `TagReference` can therefore override that specific internal binding without exposing or rewriting the shared Dynamo children. If an optional `TagReference` is absent, the existing `{equipmentPath}.<member>` target remains the fallback.

## C07 deterministic state precedence

The C07 Engineering state model resolves one visual state using this explicit precedence, highest first:

1. bad, stale, or unknown quality;
2. fault;
3. alarm;
4. uncertain quality;
5. operator command intent;
6. transitioning;
7. active;
8. inactive;
9. unknown.

Safety and diagnostic information therefore cannot be hidden by an optimistic command or normal process indication. This is the semantic authority for later visual treatment; color alone is not sufficient to represent critical state.

The current built-in artwork still has simple lamps/static geometry. C07 must continue evolving presentation for bad quality, feedback mismatch, alarm/command indication and analog PV/SP/feedback where applicable without creating renderer-only state authority.

## Current command boundary

The eight built-ins still do not issue commands directly. Public command-key parameters are deliberately inert until connected to the existing Runtime command APIs, authorization and override policy. Definitions must never write to a Driver directly.

## Engineering authoring boundary

The ordinary Screen/Popup inspector may edit only the typed parameter definitions exposed by a Dynamo. Internal child shapes, bindings and stable child IDs remain definition-private. C07 authoring helpers fail closed for unknown parameters and kind mismatches.

Engineering preview/test inputs remain simulation-only. They may drive the deterministic state model in the editor, but must never write TAGs or Drivers.

## Runtime integration status

The C07 branch contains an isolated runtime binding projection model that:

- prefers typed `equipmentPath` over the legacy field;
- substitutes the effective equipment path in legacy targets;
- applies a supplied public `TagReference` only to a binding whose `dynamoParameter` metadata matches;
- preserves bit selectors;
- leaves optional unsupplied parameters on the legacy binding path;
- clones the projected composition rather than mutating the shared definition.

The remaining integration step is to make `composeDynamoRuntime` and the renderer live-value collector consume this projected per-instance composition together. They must change as one cut so rendered state and sampled TAG identity cannot diverge.

## C07 migration constraints

1. Preserve all eight stable type keys.
2. Preserve definition identity unless a documented migration requires otherwise.
3. Version schema/public-interface changes explicitly; do not overload `libraryVersion` as the only compatibility signal.
4. Keep instance-to-definition references canonical.
5. Keep child composition encapsulated from ordinary Screen authoring.
6. Reuse C05 canonical visual property types and capability metadata where applicable.
7. Engineering preview/test inputs must remain simulation-only and must never write TAGs or Drivers.
8. Public properties/events/actions are the contract C08 may browse later; internal child shapes are not.
