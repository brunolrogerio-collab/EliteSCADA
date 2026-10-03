# Packaging / Lifecycle / Scale

Research date: **2026-10-02**

## Packaging matrix

| Integration | Packaging | Windows | Linux | Container | Persistent state | Ports/devices |
|---|---|---|---|---|---|---|
| Native Zigbee | managed Node sidecar | Yes target | Yes target | Yes with USB/network | Zigbee network state + backup | leased USB serial or TCP coordinator |
| Zigbee2MQTT | user-managed v1; managed optional later | External | External | Common | Z2M data | MQTT + coordinator owned by Z2M |
| DALI gateway | built-in network protocol/profile | Yes | Yes | Yes | profile only | network |
| Native DALI | future native/vendor interface | TBD | TBD | device mapping required | bus/commission state | DALI interface |
| Home Assistant | built-in .NET | Yes | Yes | Yes, mDNS/network caveats | selected import/reconcile metadata | HTTPS/WSS |
| Matter | managed matterjs-server | Yes target | Yes primary | Yes, host networking/BLE caveats | **critical fabric DB** | WS + IPv6/mDNS + optional BLE |
| ESPHome | built-in .NET | Yes | Yes | Yes, mDNS caveat | device/entity mapping | TCP + mDNS |
| Shelly | built-in .NET | Yes | Yes | Yes, mDNS caveat | device/component mapping | HTTP/WS |
| Z-Wave JS | managed Node sidecar | Yes target | Yes target | Yes with USB | **critical controller/server state** | leased USB controller + WS |
| KNX/IP | built-in or sidecar | Yes | Yes | Yes | project/keyring mapping | UDP/TCP 3671 typical |
| BTHome | built-in after F4 Bluetooth | OS dependent | OS dependent | BLE mapping needed | device/bindkeys | Bluetooth adapter |

## LocalBridge contract

Matter, Z-Wave, Native Zigbee service and optional managed Z2M should share one product lifecycle.

### Required descriptor

- component key;
- implementation/version;
- immutable package/image digest;
- API/schema version;
- supported OS/arch;
- required host resources;
- network bindings;
- state directory;
- secret references.

### Lifecycle

- install/provision;
- start;
- readiness;
- health;
- graceful stop;
- bounded restart;
- crash-loop state;
- update;
- migrate state;
- rollback;
- backup/restore.

### Diagnostics

- version;
- uptime;
- restart count;
- health;
- queue/backpressure;
- sanitized recent errors;
- state migration status.

Do not expose arbitrary sidecar stdout containing secrets as user diagnostics.

## RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01

The above should be a common host capability. Individual drivers should not each invent service supervision, logs, volumes and upgrade semantics.

## Windows / Linux

### Direct .NET integrations

HA, ESPHome and Shelly should be straightforward cross-platform network clients.

### Node sidecars

Pin:
- Node major/runtime;
- package lock;
- production dependency set;
- checksum/SBOM.

Avoid “npm install latest” at runtime.

### USB

F4 Host Hardware provides stable server authority. Containers only see explicitly mapped devices.

### BLE

Matter commissioning/BTHome are the least portable host-resource cases. Test Windows/Linux separately and document container limitations.

## Container networking

mDNS/IPv6 multicast affects:
- Matter;
- ESPHome discovery;
- Shelly discovery;
- KNX routing;
- some HA discovery scenarios.

Support explicit endpoint configuration when multicast discovery is unavailable. Do not require host-network mode without documenting the security consequence.

## Upgrade policy

Sidecars:
- pin compatible ranges;
- store current schema/version;
- backup state before migration;
- health-check after update;
- rollback binary when state migration is reversible/supported;
- block downgrade when persistent state is incompatible.

Driver and sidecar versions should be visible together.

## Scale policy

There are few useful universal protocol limits. Therefore distinguish:

### FACT

Normative or implementation-documented limits, for example:
- DALI subnet address/group/scene limits;
- Shelly documented channel limits;
- current sidecar schema/version;
- protocol message limits.

### PROPOSED ELITESCADA LIMIT

A product safety/performance guardrail that must be validated before release.

## Common proposed load-test profile

For each integration:
- cold start with full inventory;
- steady-state normal event load;
- burst of state changes;
- network outage;
- sidecar/device reconnect storm;
- canonical Runtime backpressure;
- Historian enabled;
- subscription re-establishment;
- bounded memory check;
- slow consumer check.

## Suggested qualification targets

These are **not published protocol capacities**.

| Integration | Proposed L2/L3 qualification target |
|---|---|
| Native Zigbee | 100 joined devices / mixed reporting |
| Z2M | 1,000 exposed properties, reconnect burst |
| Home Assistant | 2,000 selected entities |
| Matter | 100 nodes / 1,000 subscribed attributes |
| ESPHome | 250 devices / 5,000 entities |
| Shelly | 250 devices / 1,500 components |
| Z-Wave | 100 software-fixture nodes; smaller meaningful L4 mesh |
| KNX | 5,000 group objects in import/dispatch fixture |
| BTHome | 500 advertisers in synthetic scan stream |

Freeze lower initial production limits if L3 evidence shows latency/memory pressure.

## Queue semantics

Do not use one generic “drop oldest” queue.

- latest sensor state may be coalesced;
- cumulative energy counters must preserve latest monotonic truth;
- edge events/button presses generally must not be coalesced away;
- commissioning progress should preserve terminal result;
- commands require explicit completion/failure correlation;
- alarms/historian semantics are canonical and must not be bypassed.

## Health model

Each integration should surface:
- connection/controller state;
- synchronized inventory state;
- event lag;
- queue depth;
- dropped/coalesced counts;
- last successful receive;
- last successful write;
- reconnect count;
- external component version;
- hardware identity if applicable.
