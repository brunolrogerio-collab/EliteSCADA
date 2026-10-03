# Hardware Lab Matrix

Research date: **2026-10-02**

Prices are intentionally omitted unless a current reliable source is captured at procurement time. Availability changes quickly.

## Core lab

| Item | Example family/model target | Protocol | Why buy | Test level |
|---|---|---|---|---|
| TI Zigbee coordinator | reputable CC2652P/CC1352P-class USB coordinator supported by chosen zStack stack | Zigbee | primary zStack radio/backup/restart | L4 |
| Silicon Labs Zigbee coordinator | reputable EFR32MG21/MG24 Ember coordinator supported by chosen stack | Zigbee | independent second adapter family | L4 |
| Zigbee mains router | certified smart plug/light | Zigbee | routing + OnOff | L4 |
| Zigbee power plug | model with electrical measurement/metering | Zigbee | Power/Energy mapping | L4 |
| Zigbee temp/humidity sensor | battery sleepy device | Zigbee | sleep/reporting/battery | L4 |
| Zigbee contact sensor | battery IAS/contact | Zigbee | IAS + sleepy behavior | L4 |
| Zigbee occupancy sensor | battery/mains model | Zigbee | occupancy reporting | L4 |
| Zigbee colour/CCT light | standards-compliant model | Zigbee | level/color clusters | L4 |
| Zigbee cover actuator | supported/tested model | Zigbee | Window Covering | L4 |
| Z-Wave USB controller | current 800-series controller validated by Z-Wave JS | Z-Wave | primary network | L4 |
| Z-Wave second controller | 700-series if supported/current | Z-Wave | migration/compatibility | L4 |
| Z-Wave smart plug | S2 + Meter | Z-Wave | switch/meter/routing | L4 |
| Z-Wave contact/motion | battery S2 | Z-Wave | sleep/interview/notification | L4 |
| Z-Wave lock | S2 Access Control, only if scope includes Lock | Z-Wave | security-class workflow | L4 |
| Matter Wi-Fi plug/light | current certified device | Matter | Wi-Fi commissioning/subscriptions | L4 |
| Matter Thread plug/light | current certified device | Matter/Thread | Thread path | L4 |
| Matter Thread battery sensor | ICD/sleepy device | Matter/Thread | ICD/subscription/reconnect | L4 |
| Thread Border Router | current supported OTBR/Home-class border router | Thread | Matter-over-Thread | L4 |
| ESP32 dev board | ESP32-S3 or common supported board | ESPHome | controllable fixture firmware | L4 |
| ESPHome battery/deep-sleep board | ESP32 + sensor | ESPHome | reconnect/sleep behavior | L4 |
| Shelly relay/meter Gen2 | one representative RPC device | Shelly | older Gen2 API baseline | L4 |
| Shelly Gen3 | switch/sensor model | Shelly | generation compatibility | L4 |
| Shelly Gen4 | Shelly 1 Gen4 or EM Gen4 class | Shelly | current RPC + Gen4 features | L4 |
| Shelly cover | cover-capable current model | Shelly | Cover capability | L4 |
| Home Assistant host | x86/ARM host running current HA OS/container | HA | real registry/API/upgrade | L2/L4 |
| Bluetooth adapters | one Windows-compatible + one Linux-compatible | BLE/BTHome/Matter | cross-platform host radio | L4 |
| BTHome sensors | temp/contact/button, encrypted-capable | BTHome | passive BLE mapping/encryption | L4 |

## DALI lab

| Item | Target | Why |
|---|---|---|
| DALI-2 gateway | one product verified in DALI Alliance Product Database, with documented KNX/IP or local IP/Modbus/BACnet mapping | first gateway path |
| DALI bus power | compliant bus power supply if gateway does not provide it | physical bus |
| DT6 LED driver | DALI-2 certified | dimming/basic gear |
| DT8 driver | tunable white/colour | ColorLight |
| DALI-2 presence sensor | certified input device | Occupancy |
| DALI-2 pushbutton/input | certified input device | Button/events |
| future native DALI interface | selected only after host protocol research | native lane |

Verify certification/model/firmware at purchase time. Do not infer DALI-2 certification from vendor marketing text alone; use official product database.

## KNX lab if promoted

- KNX/IP Secure interface/router;
- KNX TP power supply;
- one relay actuator;
- one dimming actuator;
- one sensor/pushbutton;
- ETS project/keyring fixture.

This lab also enables testing KNX/DALI gateway integration.

## Network lab

Include:
- managed Ethernet switch;
- isolated IoT VLAN;
- Wi-Fi AP with 2.4 GHz coverage;
- IPv6/mDNS capable LAN;
- controllable packet loss/latency software fixture;
- separate MQTT broker host/container.

## Procurement rules

For every purchased item record:
- manufacturer;
- exact model/SKU;
- hardware revision;
- firmware;
- certification id where relevant;
- coordinator/controller chipset;
- purchase date/source;
- tested driver/sidecar version.

No device becomes “EliteSCADA supported” solely because another project lists the same brand.
