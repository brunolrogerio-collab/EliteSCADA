# Native Zigbee v1 host setup

This profile supports a TI zStack/CC2652 coordinator and Boolean `genOnOff/onOff` TAGs. The radio, serial path, local state directory and protected network key belong to the host. Engineering packages contain the portable `host-material-slot-v1:configured` marker; the random encrypted-vault reference stays in the host's `integration-material-references` store.

## Host requirements

- Windows x64 or Linux x64.
- Node.js `24.21.0` and npm `11.19.0` on the host. Configure `CommunicationDrivers:ZigbeeNative:NodeExecutable` to the exact executable path if `node` is not on the service PATH.
- Install the sidecar's locked production dependencies in the published sidecar directory on the target OS so the serial-port native module is built for that OS:

```powershell
Set-Location <publish-directory>\sidecars\zigbee-native
npm.cmd ci --omit=dev --no-audit --no-fund
```

```sh
cd <publish-directory>/sidecars/zigbee-native
npm ci --omit=dev --no-audit --no-fund
```

The application publish copies the sidecar source, lock, SBOM and notices. It does not download Node.js or run `npm ci` as a cross-platform build step. Use the official Node distribution for the host OS and the matching license notice in this sidecar's `licenses/` directory.

## Trusted host configuration

The coordinator is configured by the server operator, never by an imported `.escadapkg`. Use the platform's stable serial locator and the physical identity observed for that coordinator.

```json
{
  "CommunicationDrivers": {
    "HostResources": {
      "zigbee-main": {
        "Kind": "zigbee-coordinator",
        "PortName": "COM4",
        "PhysicalIdentity": "usb:ti:cc2652:001",
        "Family": "ti-zStack",
        "Model": "CC2652P2",
        "Firmware": "2026.1",
        "NetworkIdentity": "zigbee-network:home-01"
      }
    },
    "ZigbeeNative": {
      "NodeExecutable": "node",
      "ArtifactDirectory": "<publish-directory>/sidecars/zigbee-native",
      "StateDirectory": "<persistent-data-directory>/sidecars/zigbee-native"
    }
  }
}
```

On Linux, set `PortName` to the host's stable path, for example `/dev/serial/by-id/usb-TI_CC2652`. Grant the service account access to that device and to the configured persistent state directory. Do not use the temporary `/dev/ttyACM*` name when a stable by-id path is available.

## Provision the network key

Create the Zigbee Data Source with its stable GUID, `hostResourceId`, matching `networkIdentity`, PAN ID, extended PAN ID, channel and baud rate. Set `SecretReferences.networkKey` to `host-material-slot-v1:configured`. Provision the key through the authenticated Engineering endpoint using the `zigbee.native.network-key` purpose, `application/octet-stream`, and the current `x-elitescada-workspace-version` header. The body must be either 16 raw key bytes or 32 hexadecimal ASCII characters. The endpoint authorizes `EngineeringModify`, stores encrypted material under the project/Data Source/driver/purpose scope, audits the operation without the key, updates the working Data Source marker, and clears the request buffer.

The secret is not returned by the endpoint, stored in project settings, or written to `.escadapkg`. A package imported on another host retains only the portable marker and remains unable to start until that host provisions its own key in the same project/Data Source scope.

## Current v1 boundary

The runtime validates the coordinator family, model, network identity and exclusive host lease before opening the sidecar. It reads real state into the candidate Runtime, reports device updates, and treats command acknowledgement as distinct from device state. Writes reconcile to a readback/report value. Commissioning, permit-join, network restore, channel changes, group administration, and non-Boolean datapoints are outside v1. Physical device compatibility remains a separate L4 qualification.
