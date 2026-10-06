# KNX + DALI Lab and Validation Matrix — Checkpoint 3

Status: CHECKPOINT_3 / RESEARCH_ONLY / DOCS_ONLY  
Issue: #539 — HOME-RESEARCH-01  
Access date: 2026-10-06

## 1. Purpose

Define a concrete laboratory that can prove:

- KNXnet/IP tunneling;
- KNX IP Secure tunneling;
- KNXnet/IP routing and secure routing;
- representative DPT v1 values and writes;
- KNX Data Secure behavior where applicable;
- Modbus/DALI preferred path;
- BACnet/DALI fallback;
- KNX/DALI strategic path;
- DALI DT6;
- DALI DT8;
- DALI-2 input devices;
- occupancy/illuminance;
- lamp/gear fault;
- emergency-lighting state;
- EliteSCADA L0-L4 validation.

This is a research purchase/validation plan, not an authorization to buy, wire or implement.

Mains-voltage and emergency-lighting bench work must be assembled by a qualified electrical professional in an enclosed, protected lab panel.

## 2. Minimum KNX lab

### KNX-IP access

| Priority | Hardware | Exact model | Purpose |
| --- | --- | --- | --- |
| P0 | KNX/IP Secure interface | Weinzierl KNX IP Interface 732 secure, art. 5248 | primary secure-tunneling target |
| P0 | KNX/IP Secure router | Weinzierl KNX IP Router 752 secure, art. 5249 | routing + secure-routing + independent tunneling target |
| P0 | KNX TP power supply | MDT STC-0640.01, 640 mA | KNX TP bus power with diagnostics |

Weinzierl 732 evidence:

- KNXnet/IP tunneling;
- KNX IP Security AES-128;
- up to 8 simultaneous tunneling connections;
- Ethernet 100BaseT;
- KNX-bus powered.

Weinzierl 752 evidence:

- KNXnet/IP routing;
- KNXnet/IP tunneling;
- KNX Security;
- up to 8 simultaneous tunneling connections;
- multicast/IGMP-capable Ethernet behavior.

### KNX representative devices

| Priority | Device class | Exact model | DPT/capability coverage |
| --- | --- | --- | --- |
| P0 | switch actuator | MDT AKS-0416.03 | Boolean switch, status, scenes/timers |
| P0 | dimmer | MDT AKD-0401.02 | switching, relative/absolute dim, power measurement, diagnostics |
| P0 | room thermostat | Theben RAMSES 718 P KNX, item 7189210 | temperature, setpoint, heat/cool, HVAC operating mode |
| P0 | energy meter | MDT EZ-0320.01 | voltage/current/power/energy/counters |
| P1 | KNX/DALI secure application controller | Theben DALI-Gateway P64 KNX, item 4940303 | KNX Data Secure + downstream DALI semantics |

The P64 is intentionally both a KNX secure target and the combined KNX/DALI target.

### Optional KNX additions

Useful after P0:

- second-vendor switch/dimmer for DPT interoperability;
- additional KNX Data Secure sensor/actuator;
- line coupler for topology/routing tests;
- second KNX/IP Secure router for true multi-router secure-routing topology.

## 3. KNX commissioning prerequisites

### ETS

Recommended laboratory license:

`ETS6 Professional`

Reason:

- professional development/commissioning;
- no project/device-size ceiling;
- project export for repeatable fixtures;
- secure commissioning/keyring workflows;
- independent of product runtime.

ETS6 Lite may be technically sufficient for a small <=20-device laboratory, but Professional is the cleaner long-term lab authority.

ETS is a lab/commissioning dependency.

It is not a required EliteSCADA Runtime dependency.

### Network equipment

P0:

- managed Ethernet switch with VLAN and IGMP visibility/configuration;
- dedicated isolated lab VLAN;
- firewall/router capable of explicit UDP/multicast rules;
- Windows host;
- Linux host;
- container-capable Linux host.

P1:

- second VLAN/subnet for routed/BBMD/VPN-style deployment tests;
- packet-capture port/SPAN capability.

## 4. Minimum DALI lab — preferred Modbus path

### Gateway

P0:

`Intesis IN703DAL0640000`

Purpose:

- one DALI channel;
- Modbus TCP;
- lower-cost/compact first proof;
- current register map for gear, groups, scenes, sensors, faults and emergency.

P1:

`Intesis IN704DAL1280000`

Purpose:

- two DALI channels;
- Modbus TCP;
- scale/multi-line validation.

Final revalidation delta (2026-10-06): HMS marks the older INMBSDAL0640200 and INMBSDAL1280200 as legacy and designates IN703DAL0640000 and IN704DAL1280000 as their 700 Series successors. The 700 Series can be late-configured through Intesis MAPS for Modbus or BACnet applications and adds explicit Part 209 colour, Part 252 energy and Part 253 diagnostics support.

Do not buy the two-line gateway as the only unit if the budget can support two different gateway families; cross-vendor coverage is more valuable than capacity for the first lab.

## 5. DALI control gear

### DT6 / LED dimming

P0 reference:

`Tridonic LCA 50W 350-1050mA one4all lp PRE`

- product part number 28000656;
- GTIN 9006210522661;
- DALI Alliance product ID 149;
- Certified DALI-2;
- parts 101/102/207;
- newer listed firmware also supports 251/252/253 data.

Use a compatible constant-current LED load within the manufacturer's electrical range.

### DT8 / tunable white

P0 reference:

`Tridonic LCA 50W 350-1050mA DT8 lp PRE`

- product part number 28001909;
- GTIN 9006210623023;
- DALI Alliance product ID 3356;
- Certified DALI-2;
- parts 101/102/207/209/251/252/253;
- DT8 colour control;
- tunable-white reference.

Use a compatible two-channel/tunable-white LED module within the driver's electrical rating.

### Why these exact certification records matter

At receiving time record:

- brand;
- product part number;
- GTIN;
- hardware version;
- firmware version;
- DALI product ID.

DALI-2 certification is valid for the exact certified product identity/version conditions, not merely for a similar family name.

## 6. DALI-2 input devices

### Presence + light sensor

P0:

`Tridonic MSensor G3 SFI 30 16DPI CR WH`

- product part number 28005996;
- GTIN 9006210891293;
- DALI Alliance product ID 11557;
- Certified DALI-2 / D4i;
- input-device support includes parts 301, 303 and 304;
- suitable to prove presence and illuminance.

Alternative P0/P1 if sourcing is easier:

`Tridonic MSensor OTD SFI 30 PIR 10DP DA WH`

- part 28004440;
- GTIN 9006210798134;
- DALI Alliance product ID 5821;
- Certified DALI-2 / D4i;
- parts 303/304/351.

### Push-button / digital input

P1:

`Tridonic DALI XC G3 CFI 30 PBI3 DA2`

Current DALI Alliance database identifies it as:

- DALI-2 certified;
- D4i certified;
- parts 101/103/301/351.

Verify exact GTIN/hardware/firmware in the DALI Alliance database before purchase because the search result used in this checkpoint did not expose the exact record page/GTIN.

If exact certification identity cannot be verified at order time, replace it with another currently certified Part 301 input device whose GTIN/firmware/hardware are visible in the database.

## 7. DALI emergency sample

P1 only, unless emergency lighting is included in first product scope:

`Tridonic EM pLED PRO FX 202 LiFePO4 2W SCREW`

- part 89800806;
- GTIN 9006210758404;
- DALI Alliance product ID 4797;
- Certified DALI-2;
- Part 202 emergency;
- Type B;
- also exposes data parts 251/252/253.

The emergency sample is useful to prove:

- battery charge/status;
- function test;
- duration test;
- result/status;
- failure mapping.

It is not required to unblock first non-emergency DALI release.

## 8. Additional gateway families

### KNX/DALI

P0 combined-path gateway:

`Theben DALI-Gateway P64 KNX / 4940303`

Required for:

- future KNX -> DALI L4;
- KNX Data Secure;
- DT8;
- DALI-2 sensor/input projection;
- fault state;
- emergency;
- groups/scenes;
- energy data.

### BACnet/DALI

P1 fallback gateway:

`LOYTEC LDALI-ME201-U`

Required for:

- BACnet object projection;
- independent gateway-vendor semantics;
- full DT8;
- sensors/buttons;
- emergency;
- lamp/gear failure;
- built-in DALI analyzer.

Current official firmware observed:

`8.6.2 / 2026-09`

Record actual firmware on receipt and do not assume a future shipment matches this value.

### Independent KNX/DALI alternate

P2:

`Schneider SpaceLogic KNX DALI Gateway Pro MTN6725-0101`

Purpose:

- second-vendor KNX/DALI interoperability;
- DALI-2 certification;
- Part 209 DT8;
- input devices;
- emergency.

Do not require P2 before initial KNX/DALI implementation unless one-vendor overfitting is discovered.

## 9. Lab physical topology

Recommended first panel:

```
Managed Ethernet lab switch
├─ EliteSCADA Windows host
├─ EliteSCADA Linux host
├─ container Linux host
├─ Weinzierl 732 secure
├─ Weinzierl 752 secure
├─ Intesis gateway
└─ LOYTEC gateway (P1)

KNX TP line
├─ MDT STC-0640.01
├─ Weinzierl 732
├─ Weinzierl 752
├─ MDT AKS-0416.03
├─ MDT AKD-0401.02
├─ Theben RAMSES 718 P
├─ MDT EZ-0320.01
└─ Theben P64 KNX

DALI line A
├─ gateway under test
├─ DT6 driver + LED load
├─ DT8 driver + tunable-white LED load
├─ DALI-2 occupancy/light sensor
├─ Part 301 input device
└─ optional Part 202 emergency gear
```

Do not electrically connect multiple gateways/application controllers to one DALI line unless the devices/mode are explicitly designed for multi-master coexistence.

For initial tests, use one DALI application-controller/gateway configuration at a time.

## 10. Fault injection — safe methods

Required fault cases should use reversible test points, not destructive damage.

### KNX

- disconnect Ethernet;
- disconnect KNX TP bus connector;
- block UDP 3671;
- wrong secure credentials/keyring;
- remove routing multicast;
- exhaust available tunnel slots using controlled test clients;
- reboot interface/router;
- change endpoint IP in a lab-only configuration;
- duplicate/replayed secure fixture at L0/L1, not by attacking production equipment.

### DALI

- open DALI communication line through a protected lab switch/test terminal;
- disconnect one gear from DALI bus;
- disconnect LED load according to manufacturer-safe procedure to provoke lamp/load fault where supported;
- power-cycle gateway;
- isolate one DALI device;
- use intentionally unassigned/unknown short address in test configuration;
- force Modbus/BACnet mapping mismatch in Engineering, not by corrupting gateway firmware.

### Emergency

- use manufacturer-supported test commands;
- simulate/report failure only through supported test facility;
- do not defeat safety circuitry or intentionally deep-discharge batteries outside manufacturer procedure.

## 11. L0 — deterministic unit/codec level

### KNX L0

Required:

- group-address parse/format;
- individual-address parse/format;
- cEMI adaptation if owned by EliteSCADA;
- KNXnet/IP frame adaptation if owned;
- exact V1_DPT_SET encode/decode;
- boundary/range values;
- NaN/invalid data rejection where relevant;
- malformed APDU/frame;
- unknown DPT rejection;
- secure known-answer vectors if EliteSCADA owns crypto;
- sequence/freshness/replay logic if EliteSCADA owns it.

If Falcon owns secure/wire codecs:

- do not duplicate Falcon crypto;
- test EliteSCADA parameterization, key selection, fail-closed policy and adaptation against independent known fixtures.

### DALI gateway-profile L0

No native DALI codec is required for gateway-first.

Test:

- Intesis Modbus address formula/profile mapping;
- data width/signedness/scaling;
- fault bits/status fields;
- scene/group mapping;
- emergency state mapping;
- invalid register/profile input;
- BACnet object mapping profile when LOYTEC path is implemented;
- KNX DPT/object mapping profile when KNX/DALI path is implemented.

## 12. L1 — fake protocol peers

### KNX L1

Use a purpose-built test peer that is independent from the production adapter where practical.

Cases:

- search/discovery reply;
- tunnel connection;
- secure tunnel auth success/failure;
- state/heartbeat;
- telegram RX/TX;
- acknowledgement timeout;
- reconnect;
- disconnect;
- duplicate frame;
- malformed frame;
- packet loss;
- flood/backpressure;
- replay/sequence rejection.

Do not use the future production codec on both ends as the only proof.

### Modbus/DALI L1

Use an independent Modbus TCP server that emulates the documented Intesis register surface.

Cases:

- normal DT6 level;
- group;
- scene recall;
- sensor input;
- lamp failure;
- ballast failure;
- DALI communication error;
- emergency status;
- timeout/disconnect;
- illegal function/address;
- stale/no-response.

This validates the existing Modbus provider + DALI semantic profile without physical DALI hardware.

### BACnet/DALI L1

Use an independent BACnet peer/object fixture for the LOYTEC object profile.

Cases:

- Analog Input/Output;
- Multi-State objects;
- COV/poll fallback;
- Reliability/Status_Flags;
- write priority;
- unavailable object;
- gateway/device fault.

## 13. L2 — independent interoperability peer

### KNX L2

Primary:

`XKNX current stable line`

Secondary:

`Calimero current 3.x line`

Use at least one independent implementation to prove:

- discovery;
- tunneling;
- group read/write;
- representative DPTs;
- reconnect;
- routing where supported;
- Secure where peer support and fixture permit.

Do not use the same EliteSCADA codec/crypto on both sides.

### Gateway-profile L2

For Intesis/Modbus:

- independent Modbus client/server tooling;
- compare EliteSCADA interpretation against the vendor register map.

For LOYTEC/BACnet:

- independent BACnet tooling such as BACpypes/another protocol-faithful client;
- compare exact object values/properties.

For KNX/DALI:

- XKNX/Calimero observes KNX telegrams emitted by Theben P64;
- ETS Group Monitor may be supporting evidence but should not be the only independent peer.

## 14. L3 — EliteSCADA integrated runtime

### KNX L3

Required:

1. create KNX Data Source;
2. bind multiple TAGs with DataSourceId;
3. bind exact group address + DPT;
4. create Equipment/Capability only from valid metadata;
5. Save;
6. Publish;
7. Activate;
8. acquire live values;
9. `Runtime.WriteAsync(TAG)`;
10. validate quality/timestamp;
11. common diagnostics;
12. PointReadTest;
13. restart;
14. recover/reconnect;
15. wrong protected material fails closed;
16. no plaintext leak in package/log/diagnostics.

### DALI-via-Modbus L3

Required:

- existing `modbus.tcp` Data Source;
- canonical TAGs for Intesis register mappings;
- DALI semantic metadata;
- Equipment projection;
- Runtime writes;
- faults/status;
- common diagnostics;
- Save/Publish/Activate/restart.

No DALI provider registration.

### DALI-via-BACnet L3

Same, using `bacnet.ip` and BACnet object bindings.

### KNX/DALI L3

Same future KNX Data Source and normal KNX TAG bindings.

No gateway-specific Runtime shortcut.

## 15. L4 — real hardware test matrix

### L4-A — KNX secure tunneling

Hardware:

- Weinzierl 732;
- STC-0640.01;
- AKS-0416.03;
- AKD-0401.02;
- RAMSES 718 P;
- EZ-0320.01.

Prove:

- discovery;
- secure tunnel;
- multiple DPT reads;
- switch write/readback;
- dim write/readback;
- temperature;
- setpoint/HVAC;
- voltage/current/power/energy;
- reconnect;
- restart;
- bad key/keyring failure;
- session/slot diagnostics.

### L4-B — KNX routing

Hardware:

- Weinzierl 752;
- managed switch/VLAN;
- same KNX TP devices.

Prove:

- routing;
- selected NIC;
- multicast;
- secure routing;
- firewall/VLAN behavior;
- duplicate/loop avoidance;
- container host topology;
- Linux/Windows.

Do not make routing a release claim until this passes.

### L4-C — Modbus/DALI preferred path

Hardware:

- Intesis IN703DAL0640000;
- DT6 driver/load;
- sensor;
- optional emergency.

Prove:

- register read/write;
- on/off/dim;
- group;
- scene recall;
- input device;
- occupancy/lux;
- lamp/gear fault;
- DALI comm error;
- reconnect;
- gateway reboot;
- optional emergency status/test.

DT8 colour is not acceptance-critical on Intesis until vendor/hardware proves the public mapping.

### L4-D — BACnet/DALI fallback

Hardware:

- LOYTEC LDALI-ME201-U;
- DT6;
- DT8;
- sensor/input;
- emergency sample.

Prove:

- BACnet discovery/object mapping;
- COV/poll;
- full DT8/Tc;
- input/sensor events;
- lamp/gear faults;
- emergency state;
- DALI analyzer corroboration;
- gateway reboot/restart.

### L4-E — KNX/DALI strategic path

Hardware:

- Weinzierl 732 secure;
- Theben P64;
- DT6;
- DT8;
- sensor/input;
- optional emergency.

Prove:

- secure KNX access;
- exact ETS-exported group objects/DPTs;
- group/individual control;
- dimming;
- feedback;
- DT8;
- sensor/input;
- fault;
- emergency;
- energy where configured;
- Data Secure group-object behavior;
- EliteSCADA restart without recommissioning gateway/DALI.

## 16. Cross-platform validation

Future KNX DEV acceptance must include:

| Host | Tunneling | Secure tunneling | Routing | Secure routing | Container |
| --- | --- | --- | --- | --- | --- |
| Windows | required | required | required before routing claim | required before secure-routing claim | n/a |
| Linux native | required | required | required before routing claim | required before secure-routing claim | n/a |
| Linux container | required | required | explicit lab topology only | explicit lab topology only | required if product claims container support |

For Modbus/DALI and BACnet/DALI:

- Windows native required;
- Linux native required where existing driver support claims it;
- container follows existing provider contract.

## 17. Performance / soak

Minimum future acceptance recommendation:

### KNX

- 24 h tunneling soak;
- repeated network flap;
- repeated interface reboot;
- sustained telegram load;
- mixed read/write;
- no unbounded memory/task growth;
- no permanent quality stuck Good after loss;
- reconnect counters/diagnostics stable.

### DALI gateway

- 24 h polling/subscription soak;
- gateway reboot;
- DALI device disconnect/reconnect;
- repeated writes;
- scene calls;
- sensor events;
- fault recovery.

No artificial “1,000 luminaires on one DALI line” benchmark: physical DALI channel limits remain authoritative.

## 18. Evidence capture

Every L4 run must capture:

- date;
- EliteSCADA exact SHA;
- OS/runtime;
- gateway/device model;
- GTIN where available;
- hardware revision;
- firmware;
- ETS application version;
- gateway configuration version/export;
- DALI Alliance product ID for certified DALI devices;
- network topology;
- secure/plain mode;
- exact test result;
- packet/log evidence sanitized of secrets.

Do not capture:

- keyring plaintext;
- authentication code;
- tunnel passwords;
- group keys;
- protected-material reference in public logs if security policy forbids it.

## 19. Acceptance gates

### KNX future DEV can claim Tier A v1 only after

- L0 approved;
- L1 approved;
- independent L2 approved;
- L3 canonical runtime approved;
- L4 secure tunneling on real hardware approved;
- Windows + Linux proof;
- protected-material convergence decision implemented;
- legal gate approved.

Routing may remain `EXPERIMENTAL/DEFERRED` if L4-B is not complete.

### DALI preferred first path can claim gateway support only after

- exact Intesis model/firmware recorded;
- vendor register profile validated;
- L1 profile emulator proof;
- L3 canonical Modbus path;
- L4 DT6/group/scene/sensor/fault proof;
- no native-DALI runtime added.

### Rich DT8 claim

The protocol contract is now explicit on the current Intesis 700 Series: IN703/IN704 Modbus maps expose colour-temperature and RGB/RGBW Type 8 read/write registers at individual/group/broadcast levels.

Recommended L4 proof order:

- Intesis 700 Series / Modbus — prove the preferred first path directly;
- LOYTEC/BACnet — independent rich-building fallback;
- Theben P64/KNX — strategic KNX/DALI path after KNX exists.

Claim DT8 support only after real gateway + certified DT8 gear interoperability succeeds on the recorded firmware/hardware.

## 20. Purchase priority

### Purchase pack A — mandatory

1. Weinzierl KNX IP Interface 732 secure, art. 5248.
2. MDT STC-0640.01.
3. MDT AKS-0416.03.
4. MDT AKD-0401.02.
5. Theben RAMSES 718 P KNX, 7189210.
6. MDT EZ-0320.01.
7. Intesis IN703DAL0640000.
8. Tridonic LCA 50W 350-1050mA one4all lp PRE / 28000656 or another current certified Part 207 DT6 equivalent.
9. compatible protected LED load/module.
10. managed Ethernet switch with VLAN/IGMP controls.

### Purchase pack B — KNX/DALI + DT8

1. Weinzierl KNX IP Router 752 secure, art. 5249.
2. Theben DALI-Gateway P64 KNX, 4940303.
3. Tridonic LCA 50W 350-1050mA DT8 lp PRE / 28001909.
4. compatible tunable-white LED module.
5. Tridonic MSensor G3 SFI 30 16DPI CR WH / 28005996.

### Purchase pack C — independent fallback

1. LOYTEC LDALI-ME201-U.
2. certified Part 301 push-button/input device.
3. optional Tridonic EM pLED PRO FX 202 LiFePO4 2W SCREW / 89800806.
4. Schneider MTN6725-0101 only if second-vendor KNX/DALI proof is required.

## 21. Sources

Official/current sources revalidated 2026-10-06:

- Weinzierl KNX IP Interface 732 secure: https://weinzierl.de/en/products/knx-ip-interface-732-secure/
- Weinzierl KNX IP Router 752 secure: https://weinzierl.de/en/products/knx-ip-router-752-secure/
- MDT STC-0640.01: https://www.mdt.de/download/MDT_DS_Bus_Power_Supply_STC.pdf
- MDT AKS family: https://www.mdt.de/
- MDT AKD-0401.02 technical manual: https://www.mdt.de/download/MDT_TM_AKD_02_Dimming_Actuator.pdf
- MDT EZ-0320.01 technical manual: https://www.mdt.de/fileadmin/user_upload/user_upload/download/MDT_TM_EZ_01_Energy_Meter_V10.pdf
- Theben RAMSES 718 P KNX: https://www.theben.de/en/ramses-718-p-knx-7189210
- Theben P64: https://www.theben.de/en/dali-gateway-p64-knx-4940303
- Intesis IN703DAL0640000 current Modbus/DALI application: https://www.hms-networks.com/p/in703dal0640000-mbs-dal-dali-2-to-modbus-tcp-rtu-server-application-with-1-dali-channel
- Intesis IN704DAL1280000 current Modbus/DALI application: https://www.hms-networks.com/p/in704dal1280000-mbs-dal-dali-2-to-modbus-tcp-server-application-with-2-dali-channels
- HMS 700 Series replacement delta: https://support.hms-networks.com/hc/en-us/articles/14923465292050-What-is-the-difference-between-my-DALI-gateway-and-the-new-700-Series
- LOYTEC L-DALI: https://www.loytec.com/products/dali/l-dali-wired/l-dali-bacnet
- DALI Alliance Product Database: https://api.dali-alliance.org/products
- Tridonic LCA DT6 product ID 149: https://api.dali-alliance.org/products/149/lca-50w-350-1050ma-one4all-lp-pre
- Tridonic LCA DT8 product ID 3356: https://api.dali-alliance.org/products/3356/lca-50w-350-1050ma-dt8-lp-pre
- Tridonic MSensor product ID 11557: https://api.dali-alliance.org/products/11557/msensor-g3-sfi-30-16dpi-cr-wh
- Tridonic emergency product ID 4797: https://api.dali-alliance.org/products/4797/em-pled-pro-fx-202-lifepo4-2w-screw
- KNX ETS licensing/pricing: https://support.knx.org/hc/en-us/articles/21546945829010-ETS6-and-other-KNX-software-licenses-types-and-prices

## 22. Checkpoint-3 lab disposition

`LAB_PLAN_READY`

Primary KNX endpoint:
`WEINZIERL_732_SECURE`

Routing target:
`WEINZIERL_752_SECURE`

Primary DALI gateway:
`INTESIS_IN703DAL0640000`

Strategic KNX/DALI:
`THEBEN_P64`

Fallback:
`LOYTEC_LDALI_ME201_U`

Independent KNX peer:
`XKNX + optional CALIMERO`

Native DALI:
`NO_NATIVE_STACK_REQUIRED`

DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
NO MERGE PERFORMED
