# Zigbee2MQTT External MQTT v1 Validation

- DEV: `DEV-HOME-ZIGBEE2MQTT-BRIDGE-V1`
- DriverType: `zigbee2mqtt.bridge`
- Required base: `cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`
- Profile: `DRIVER_PROTOCOL`

This connector talks to a separately installed, user-operated Zigbee2MQTT bridge through the existing MQTT transport. Inventory and state are bounded and sanitized. Stable device identity uses DataSourceId plus IEEE address; a friendly name is display/routing metadata. The curated v1 state set is Boolean and Double. Event/action exposes, commissioning, network mutation, Z2M packaging, radio ownership, rich commands and physical compatibility claims are out of scope.

## Evidence status

| Gate | Status | Evidence / limitation |
| --- | --- | --- |
| L0 parser and mapping | `NOT_RUN/ENVIRONMENT` locally | Focused tests cover bounded inventory, duplicate IEEE, malformed/oversized payloads, mutable friendly-name routing, stable multi-endpoint capability identity, Boolean value mapping, Double range/step, access/type/unit checks and action/config/diagnostic exclusion. The local environment has no `dotnet` executable. |
| L1 fake peer and failures | `NOT_RUN/ENVIRONMENT` locally | Focused tests cover selected-device materialization, fresh PointRead reports, malformed state diagnostics, Standby inertness, Active start, activation-token cancellation, report-confirmed write, ambiguous no-retry, retained/startup uncertainty, availability separate from device freshness, disconnect/reconnect/resubscription and stop/restart cleanup. The fake is same-code test evidence only. |
| L2 independent peer | `SKIP_WITH_REASON` proposed to Main | The existing lab has independent MQTT broker evidence for the shared transport, but no independently implemented Zigbee2MQTT peer/schema fixture is connected to this lane. Fake tests do not prove Z2M interoperability. This skip applies only to Z2M-specific software-peer evidence; it makes no Z2M software or physical compatibility claim. |
| L3 canonical activation | `BLOCKED-CONTRACT` | The driver honors `CanOwnExternalEffects=false` by remaining Stopped and making no external connection. Main's #305 comment `6065900173` records the unresolved staged-readiness/effect-authority cycle affecting canonical candidate activation. This lane has no authorization to add a protocol-specific lifecycle workaround. Resume the canonical Save/Publish/Activate gate after Main resolves the shared #560 contract. |
| #560 direct lifecycle | `NOT_RUN/ENVIRONMENT` locally | The focused tests exercise inert Standby, activation-token cancellation, stop/restart, reconnection and transport cleanup. Exact-head T1 must execute them. |
| T1 exact-head record | Initial run #977 at HEAD `4581cb6537eab6f3fcfd4fa1d4f27e0f57dcf66c` ([37824896494](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/37824896494)) was `RED / WORKFLOW-CI`: the profile router rejected the PR body's Markdown-formatted declaration before any evidence job ran. | The required declaration is now bare `VALIDATION_PROFILE: DRIVER_PROTOCOL`. The current exact-head T1 result is tracked in PR #575 and this lane issue. |
| L4 physical | `DEFERRED` | Human physical validation is scheduled after Wave 16, partner disclosure and a stable EliteSCADA installation. No physical-device compatibility is claimed. |

## Proposed L2 disposition

`SKIP_WITH_REASON: The existing independent Mosquitto/HiveMQ evidence validates the reused MQTT transport, but no independently implemented Zigbee2MQTT peer/schema fixture is available to this lane. The new fake transport is same-code evidence and cannot establish Z2M software interoperability. Skip only the Z2M-specific independent-peer gate for this PR; retain the existing broker evidence as transport-only, and make no Z2M software or physical compatibility claim. Main disposition required.`

## Local environment

- `dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --filter FullyQualifiedName~Zigbee2MqttBridgeTests`: `NOT_RUN/ENVIRONMENT` (`dotnet` is not installed in the isolated workspace).
- No local Mosquitto or Docker executable is available for an independent wire-peer run.
- Exact-head normal PR T1 remains mandatory; test source is not test evidence.
