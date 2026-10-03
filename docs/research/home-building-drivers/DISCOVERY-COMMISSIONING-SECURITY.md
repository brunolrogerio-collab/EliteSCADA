# Discovery / Commissioning / Security

Research date: **2026-10-02**

## Canonical flow

Preserve #472:

\`discover -> candidate -> preview -> assign Location/name -> Apply -> Save/Publish/Activate\`

Discovery does not mutate canonical project state.

External-network commissioning may mutate an external network **before** canonical Apply, so those operations need a separate privileged confirmation/audit boundary.

## Matrix

| Integration | Discover | Pair/include/commission | Interview/inventory | Remove/unpair | Recovery | External mutation? |
|---|---|---|---|---|---|---|
| Native Zigbee | coordinator/network scan + join events | Permit join + device join | endpoint/cluster interview | leave/remove | coordinator backup/restore/rejoin | **Yes** |
| Zigbee2MQTT | retained bridge/devices | Z2M permit_join | Z2M interview/exposes | bridge remove | Z2M data/coordinator backup | **Yes via Z2M** |
| DALI gateway | gateway inventory | usually vendor/gateway commissioning | bus objects/maps | vendor-specific | gateway config/backup | Often **Yes** |
| Native DALI | bus scan | short-address assignment | type/features/groups | address/reset | bus rescan/re-address | **Yes** |
| Home Assistant | registry snapshot | none in EliteSCADA | device/entity/area registries | only remove import | reconcile ids/version | No external pairing |
| Matter | commissionable discovery | PASE/attestation/network/fabric | endpoints/clusters | fabric/node removal | sidecar/fabric backup | **Yes** |
| ESPHome | mDNS/manual host | none | entity list/device info | remove import only | reconnect | No |
| Shelly | mDNS/manual/GetDeviceInfo | none | component inventory | remove import only | reconnect | No |
| Z-Wave JS | controller/node events | inclusion/S2/SmartStart | node interview/values | exclusion | NVM backup/replace failed | **Yes** |
| KNX/IP | IP discovery/project import | ETS commissioning usually external | group/DPT/project | project operation | keyring/project restore | Potentially |
| BTHome | BLE advertisement | no pairing for passive receive | advertisement fields | stop import | key restore | No network mutation |

## Confirmation-required actions

At minimum:
- open Zigbee permit-join window;
- remove Zigbee device;
- restore Zigbee coordinator backup;
- DALI address assignment/reset/group/scene programming;
- Matter commissioning/fabric removal/factory-reset request;
- Z-Wave inclusion/exclusion/replace failed/controller reset/NVM restore;
- future KNX programming write operations;
- firmware updates when added.

Confirmation should display the external consequence and selected target, not only “Are you sure?”.

## Security material matrix

| Secret/material | Integration | Storage |
|---|---|---|
| Zigbee network key/install code | Native Zigbee | protected material store |
| MQTT username/password/cert key | Z2M | protected secret store |
| Z2M config secrets | managed Z2M | protected sidecar configuration |
| DALI gateway credential | gateway path | protected secret |
| HA Long-Lived Token/OAuth refresh token | Home Assistant | protected secret |
| Matter fabric operational credentials | Matter | protected sidecar state/material |
| Matter Wi-Fi/Thread credentials | Matter | protected material |
| Matter setup code | Matter | ephemeral; do not retain unless explicitly required |
| ESPHome Noise key/password | ESPHome | protected secret |
| Shelly password/derived auth material | Shelly | protected secret |
| Z-Wave S0/S2/LR keys | Z-Wave | protected sidecar material |
| Z-Wave NVM backup | Z-Wave | encrypted/protected backup |
| KNX Secure keyring/passwords | KNX | protected material |
| BTHome bindkey | BTHome | protected secret |
| Tuya/Intelbras client credentials/tokens | Cloud | protected secret |

## Rotation / revocation

Each integration should define a credential test/replace flow that does not require deleting canonical Equipment:
- HA token rotate;
- MQTT credentials rotate;
- ESPHome key replacement typically requires device reconfiguration;
- Shelly password rotate;
- cloud token/client secret renew;
- KNX keyring replacement via project reconciliation.

Radio network keys/fabric keys are higher-impact changes and require dedicated network recovery procedures.

## Backup/restore

Backups containing radio/fabric/security state must:
- be encrypted at rest;
- exclude plaintext from normal package exports;
- record component/version/hardware identity;
- verify compatibility before restore;
- require privileged confirmation.

## RESEARCH_CONTRACT_DELTA_REQUIRED — SECRET-REFERENCE-01

Integration settings need a canonical host-side secret/material reference type. Project export should carry references/metadata, not secret value by default.

## RESEARCH_CONTRACT_DELTA_REQUIRED — EXTERNAL-NETWORK-MUTATION-01

Discovery tooling needs an explicit mutation-operation contract with:
- operation type;
- target/controller;
- required privilege;
- confirmation text;
- correlation id;
- progress;
- cancelability where supported;
- audit result;
- recovery guidance.

No implementation in this research lane.
