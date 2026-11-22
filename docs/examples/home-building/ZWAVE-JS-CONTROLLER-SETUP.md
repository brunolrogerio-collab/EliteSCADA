# Z-Wave JS Controller v1 setup

The v1 driver is a host-managed `zwave.js.controller` Data Source for already-commissioned Z-Wave networks. It exposes only persistent, non-secret, typed process values through the exact-locked `zwave-js-server` and `zwave-js` sidecar. The physical serial controller and its network identity are trusted Host Resource configuration, not project-package data.

## Host requirements and sidecar installation

- Windows x64 or Linux x64 with Node.js `24.21.0` and npm `11.19.0`.
- A service-account-readable serial controller path and a persistent, access-controlled sidecar state directory.
- Install production dependencies from the shipped lock in the published directory for the target OS. The API publish includes source, lock, SBOM, notices and license texts; it does not install Node.js or run npm as a cross-platform build step.

```powershell
Set-Location <publish-directory>\sidecars\zwave-js
npm.cmd ci --omit=dev --no-audit --no-fund
npm.cmd run verify:dependencies
```

```sh
cd <publish-directory>/sidecars/zwave-js
npm ci --omit=dev --no-audit --no-fund
npm run verify:dependencies
```

Keep the deployed sidecar directory read-only to the service account after installation. The service needs write access only to the separate persistent `StateDirectory`. It does not install packages or require internet access during Runtime operation.

## Trusted controller configuration

Configure the stable serial locator and physical identity on the host. `NvmIdentity` is the expected `homeid:0x########`; the sidecar refuses startup if the controller reports a different Home ID. Select the correct RF region for the physical installation.

```json
{
  "CommunicationDrivers": {
    "HostResources": {
      "zwave-main": {
        "Kind": "zwave-controller",
        "PortName": "COM7",
        "PhysicalIdentity": "usb:zwave:controller:001",
        "Family": "Silicon Labs",
        "Model": "UZB-7",
        "Firmware": "7.21.0",
        "RfRegion": "US",
        "NvmIdentity": "homeid:0x12345678"
      }
    },
    "ZWaveJs": {
      "NodeExecutable": "node",
      "ArtifactDirectory": "<publish-directory>/sidecars/zwave-js",
      "StateDirectory": "<persistent-data-directory>/sidecars/zwave-js"
    }
  }
}
```

On Linux, use a stable device path such as `/dev/serial/by-id/...`, not a transient `/dev/ttyACM*` name. Grant the EliteSCADA service account access to that device and the persistent state directory. Never point two live processes at the same controller or state directory; the shared host-resource lease fences duplicate access on this host.

## Data Source and TAG binding

Create a Data Source with driver `zwave.js.controller` and the stable `hostResourceId` `zwave-main`. A TAG uses CommunicationBinding schema `elitescada.driver.zwave.js.controller` version 1. Its portable address is:

```text
node/<1..232>/endpoint/<0..127>/cc/<1..255>/property/<typed-value>/key/<typed-value-or-none/->
```

For example, a Boolean Switch Binary current value is:

```text
node/5/endpoint/0/cc/37/property/string/currentValue/key/none/-
```

String property/key components use canonical `encodeURIComponent` percent encoding; numeric components are safe integers. Each TAG must match the upstream value metadata and the declared EliteSCADA data type. Values must be explicitly readable, explicitly non-secret, and persistent (`stateful: true`). Configuration Command Class (112) writes are excluded.

## Network keys

If the commissioned network needs security keys, provision each 16-byte binary key separately through the authenticated protected-material API. The supported purposes are `zwave.network-key.s0-legacy`, `zwave.network-key.s2-access-control`, `zwave.network-key.s2-authenticated`, and `zwave.network-key.s2-unauthenticated`. The Engineering package carries only `host-material-slot-v1:configured`; keys are resolved through the host Protected Material authority, sent to the child only in its bounded startup pipe, and cleared from temporary buffers. Keys are never placed in project settings, command-line arguments, ordinary environment variables, logs, SBOMs, or notices. Imported projects require the receiving host to provision its own keys.

## Runtime and HA behavior

The managed child exposes only an authenticated ephemeral loopback RPC. It checks exact sidecar/driver/schema versions, the controller Home ID, and each TAG's current value metadata before becoming ready. Read results and device reports are accepted only after type/range validation. Writes require a fresh value poll or a post-acknowledgement device report matching the requested value. A timeout or lost response after dispatch is outcome-unknown: the last known TAG value is retained with `Uncertain` quality and the write is never replayed automatically.

The driver acquires the controller only when the Runtime is allowed to acquire inputs; writes additionally require effective Active effect authority. A standby that lacks authority does not start the sidecar, open the serial controller, poll, subscribe, or write. A live transition still depends on the shared HA coordinator and host-resource lease; this driver adds no independent failover authority.

## Explicit v1 exclusions

This is not a commissioning or controller-administration tool. SmartStart/DSK enrollment, inclusion/exclusion, transient Central Scene/events, access-control/user-code/door-lock values, lock management, Configuration CC writes, Long Range, NVM backup/restore, and firmware operations are out of scope. Z-Wave JS upstream NVM backup/restore work is tracked separately (#8833); do not add it to this driver while that upstream blocker remains open. Software CI covers Windows and Linux x64 only. Physical controller/device compatibility remains deferred to the separately scoped L4 qualification and is not claimed by T1.
