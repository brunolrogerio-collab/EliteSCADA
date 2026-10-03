# Device / Capability Mapping

Research date: **2026-10-02**

## Rule

Do not create a different semantic model per protocol.

Protocol identity stays in source bindings. Canonical user-facing semantics come from #472 F3 Capabilities and remain backed by Equipment/TAG/Command.

## Common mapping matrix

| Capability | Zigbee | Z2M exposes | Matter | ESPHome | Shelly | Z-Wave | DALI | HA |
|---|---|---|---|---|---|---|---|---|
| OnOff | On/Off cluster | binary state/set | OnOff | switch/light | Switch | Binary Switch | gear power | switch/light |
| Dimmer | Level Control | brightness | LevelControl | light brightness | Light brightness | Multilevel Switch | arc level | light brightness |
| Light | OnOff+Level | light expose | light device type | light | Light | switch/multilevel | control gear | light |
| ColorLight | Color Control | color/color_temp | ColorControl | RGB/CCT light | applicable Light | Color Switch | DT8 | light |
| TemperatureSensor | Temp Measurement | temperature | TemperatureMeasurement | sensor | Temperature | Multilevel Sensor | input device if present | sensor |
| HumiditySensor | Relative Humidity | humidity | RelativeHumidity | sensor | Humidity | Multilevel Sensor | input device if present | sensor |
| OccupancySensor | Occupancy | occupancy | OccupancySensing | binary_sensor | input where semantic | Notification/Sensor | presence input | binary_sensor |
| IlluminanceSensor | Illuminance | illuminance | IlluminanceMeasurement | sensor | sensor/dynamic if semantic | Multilevel Sensor | light sensor input | sensor |
| ContactSensor | IAS/contact | contact | Boolean/contact device semantics | binary_sensor | input if semantic | Notification Access Control | n/a | binary_sensor |
| LeakSensor | IAS/water | water_leak | WaterFreeze/Boolean semantic where supported | binary_sensor | input if semantic | Notification Water Alarm | n/a | binary_sensor |
| SmokeSensor | IAS/smoke | smoke | smoke/CO alarm device type where supported | binary_sensor | input if semantic | Notification Smoke | n/a | binary_sensor |
| Battery | Power Config | battery | PowerSource/Battery | sensor/device info | device component/status if exposed | Battery CC | n/a | sensor |
| PowerMeter | Electrical Measurement | power | ElectricalPowerMeasurement | sensor | EM/EM1 | Meter | gateway-specific | sensor |
| EnergyMeter | Simple Metering | energy | ElectricalEnergyMeasurement | sensor | EM/EM1Data | Meter | gateway-specific | sensor |
| Voltage | Electrical Measurement | voltage | electrical measurement | sensor | EM/EM1 | Meter/Sensor | n/a | sensor |
| Current | Electrical Measurement | current | electrical measurement | sensor | EM/EM1 | Meter/Sensor | n/a | sensor |
| Cover | Window Covering | cover | WindowCovering | cover | Cover | Window Covering / multilevel semantic | gateway/device | cover |
| Lock | IAS/door lock where mapped | lock expose | DoorLock | lock if API supports | not generic | Door Lock | n/a | lock |
| Thermostat | Thermostat | climate expose | Thermostat | climate | thermostat where documented | Thermostat CCs | gateway-specific HVAC, not DALI core | climate |
| Climate | Thermostat + sensors | climate | thermostat/device features | climate | applicable | thermostat/fan CCs | n/a | climate |
| Fan | Fan cluster where supported | fan | FanControl | fan | applicable | Fan State/Multilevel | n/a | fan |
| Scene | Scenes cluster | scene/group API | scene semantics vary | button/script not automatically Scene | scripts not automatically Scene | Central Scene is event, not stored scene | DALI scenes | scene |
| Button | OnOff/level command/input | action exposes | switch/button device types | button | Input events | Central Scene | input device | button |

## Mapping rules

1. A protocol object does not become a Capability solely because its name resembles one.
2. Device class/cluster/command metadata must make the semantics explicit.
3. Unit/scale matters for numeric sensors.
4. Readable and writable directions map separately.
5. Command/event semantics are not persisted as fake state.
6. Availability/quality is canonical Runtime metadata, not a Capability.
7. Protocol diagnostics such as Zigbee LQI, Z-Wave route data or Matter fabric id remain advanced metadata unless a common diagnostic vocabulary is later approved.

## Features that do not fit cleanly

### Zigbee

- arbitrary manufacturer clusters;
- binding tables;
- Green Power;
- OTA metadata;
- network topology/LQI.

### Zigbee2MQTT

- raw manufacturer exposes;
- action strings that are transient events;
- external groups/scenes.

### Matter

- fabric/ACL management;
- arbitrary cluster features;
- commissioning/DCL data;
- binding/ACL topology.

### ESPHome

- user-defined custom entities;
- text/select/number semantics that may be configuration rather than process values.

### Shelly

- scripts/webhooks/schedules;
- dynamic components;
- device configuration objects.

### Z-Wave

- associations;
- configuration parameters;
- raw Command Class values;
- Central Scene key attributes as event detail.

### DALI

- short addresses;
- bus groups/scenes;
- device-type commissioning;
- emergency-lighting test semantics.

### Home Assistant

- helpers/automations/scripts;
- configuration/diagnostic entities;
- duplicated representations from multiple integrations.

## Event vs state

Important distinction:
- button press, Z-Wave Central Scene, some Z2M \`action\` values are **events**;
- they should not be modeled as persistent truthful TAG state unless the canonical event/TAG contract explicitly represents last event;
- prefer Command/Event projection rather than fake Boolean state.

## Identity

One discovered physical device should normally become one Equipment. Endpoints/channels remain child capability bindings unless they are clearly independent physical equipment.

Source identities are protocol-specific and stable:
- Zigbee IEEE address + endpoint;
- Z2M IEEE id, not friendly name;
- Matter fabric/node/endpoint;
- ESPHome device identity/entity key;
- Shelly device id/MAC + component id;
- Z-Wave home/controller context + node/endpoint/value id;
- HA registry ids/entity_id with reconciliation;
- DALI gateway/bus + addressed object identity.

They must never replace canonical Equipment ids.
