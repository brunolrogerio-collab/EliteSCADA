# DALI Gateway Selection Research — Checkpoint 2

Status: CHECKPOINT_2_COMPLETE / RESEARCH_ONLY / DOCS_ONLY  
Issue: #539 — HOME-RESEARCH-01  
Contract: C-HOME-KNX-DALI-RESEARCH-01  
Order: HOME-KNX-DALI-TIER-A-RESEARCH-01  
Research branch: research/home-knx-dali-execution  
Access date: 2026-10-06

## 1. Scope

This document is the bounded DALI gateway-market and architecture checkpoint.

It evaluates professional gateway-first paths where EliteSCADA reaches DALI through an existing standard/provider or through a documented local gateway API.

It does not authorize:

- a native DALI stack;
- a new DALI Runtime authority;
- product code;
- schema changes;
- Driver SDK changes;
- dependencies;
- CI/workflow changes;
- tests;
- merge.

The architecture rule is binding:

If a gateway projects DALI values as KNX group objects, Modbus registers or BACnet objects, the canonical process provider remains KNX, Modbus or BACnet.

DALI then becomes an Equipment/Capability semantic projection over canonical TAGs.

## 2. Checkpoint decision

### PREFERRED_FIRST_PATH

`EliteSCADA modbus.tcp -> Intesis Modbus Server / DALI-2 -> DALI bus`

Preferred hardware family:

- IN703DAL0640000 — current 700 Series, one DALI channel, selectable Modbus TCP/RTU or BACnet application through Intesis MAPS;
- IN704DAL1280000 — current 700 Series, two DALI channels, selectable Modbus TCP or BACnet/IP application through Intesis MAPS.

Final revalidation note (2026-10-06): HMS now marks the previously selected IN703DAL0640000 and IN704DAL1280000 pages as Legacy product. Their designated 700 Series successors are IN703DAL0640000 and IN704DAL1280000.

Why this is first:

1. EliteSCADA already has a real `modbus.tcp` provider/runtime at the release base.
2. The vendor manual explicitly targets SCADA/BMS systems with a Modbus master.
3. The public Modbus address map exposes process values, commands, faults, scenes, groups, input devices and emergency-lighting state without a vendor SDK.
4. DALI commissioning remains gateway-owned in Intesis MAPS instead of forcing EliteSCADA to implement DALI address assignment.
5. The Runtime architecture remains the existing Modbus TAG provider; no fake DALI driver is needed.

Gate:

Final revalidation against the current 700 Series manuals removes the earlier DT8 uncertainty. The IN703/IN704 Modbus register maps explicitly expose Type 8 colour control including colour temperature and RGB/RGBW fields, with group/broadcast read/write registers. The current 700 Series also documents DALI Part 252 energy data and Part 253 diagnostics/maintenance data. Hardware interoperability still requires L4 proof, but the public Modbus contract is now implementation-grade.

### FALLBACK_PATH

`EliteSCADA bacnet.ip -> LOYTEC LDALI-ME20x-U -> DALI bus`

Preferred family:

- LDALI-ME201-U — 1 DALI channel;
- LDALI-ME202-U — 2 DALI channels;
- LDALI-ME204-U — 4 DALI channels.

Why fallback:

- EliteSCADA already has `bacnet.ip`;
- LOYTEC exposes DALI ballasts, sensors and controller functions through normal BACnet objects;
- rich DT8, sensors/buttons, lamp/ballast failure and emergency-lighting functions are documented;
- BACnet/SC is supported by the gateway family for future secure BACnet convergence;
- DALI protocol analyzer and commissioning tooling are built into the controller.

It is not first only because it is a broader, more complex building controller than the deliberately simple Modbus gateway path.

### STRATEGIC_KNX_DALI_PATH

`future EliteSCADA KNX/IP -> KNX/DALI gateway -> DALI bus`

Preferred current KNX/DALI lab candidate:

`Theben DALI-Gateway P64 KNX / 4940303`

Why:

- DALI-2 certified;
- multi-master;
- DALI-2 sensors and input devices;
- full DT8;
- emergency lighting;
- energy reporting;
- KNX Data Secure;
- commissioning by device UI, web server or free ETS app;
- public current product documentation and current 2026 catalog presence.

This is not the first DALI product path because it depends on the future KNX/IP driver from Checkpoint 1.

## 3. Existing EliteSCADA provider evidence

Live repository evidence at the release base includes:

- `src/Scada.Drivers/Modbus/ModbusTcpDriverDescriptorProvider.cs` with driver type `modbus.tcp`;
- canonical Modbus TCP Runtime composition and diagnostics;
- `src/Scada.Drivers/Bacnet/BacnetDriverDescriptor.cs` with driver type `bacnet.ip`;
- `src/Scada.DriverHost/Engineering/BacnetCommunicationRuntimeComponents.cs`;
- BACnet Runtime and Engineering provider implementation.

Therefore neither the preferred Modbus gateway nor the BACnet fallback requires a new protocol Runtime provider merely because the downstream devices speak DALI.

The future KNX path remains conditional on Checkpoint 1 gates.

## 4. Candidate matrix

Legend:

- YES = explicitly supported in current official source reviewed;
- PARTIAL = support exists but the exact exposed process contract is incomplete for the requested capability;
- N/E = not established from the reviewed official source;
- ADMIN = commissioning/tooling function, not recommended as normal Runtime command.

| Candidate | EliteSCADA side | DALI-2 | DT6 | DT8 | Groups/scenes | Input devices / sensors | Fault diagnostics | Emergency | Commissioning | Bus monitoring | First-path disposition |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Intesis IN703DAL0640000 / 1280200 | Modbus TCP; RTU also on 1-line | YES | YES | PARTIAL: type 8 identified; rich colour write surface not proven in reviewed map | YES | YES: 301/302/303/304 | YES: lamp/ballast/device comm | YES: DT1 state/tests/results | ADMIN via Intesis MAPS | diagnostics/viewers, not selected as Runtime bus-monitor API | **PREFERRED_FIRST_PATH** |
| LOYTEC LDALI-ME201-U / ME202-U / ME204-U | BACnet/IP; BACnet/SC; also Modbus TCP | YES | YES | YES: tunable white/full colour | YES | YES: sensors/buttons/general input | YES | YES | ADMIN via L-INX/web/LCD | YES: built-in DALI analyzer | **FALLBACK_PATH** |
| Theben DALI-Gateway P64 KNX | KNX TP behind KNX/IP interface/router | YES | YES | YES: DT8 individual/group | YES | YES: motion/presence/light, push-buttons, generic inputs | YES/status; further mapping requires KNX object audit | YES: DT1 | ADMIN via device/web/DCA/ETS | tooling/web, not Runtime raw monitor | **PREFERRED_KNX_DALI_LAB** |
| Schneider SpaceLogic KNX DALI Gateway Pro MTN6725-0101 | KNX TP behind KNX/IP | YES, DALI Alliance product 3717 | YES | YES: Tc/xy/RGBWAF | YES | YES: DALI-2 motion/light and input support | YES including lamp feedback | YES | ADMIN via DCA/web/ETS | diagnostics supported | strong KNX alternate |
| ABB DG/S 1.64.5.1 / 2.64.5.1 Premium | KNX TP behind KNX/IP | YES | YES | YES: Tc/RGB(W)/HSV(W) | YES | N/E for modern DALI-2 input-device breadth in reviewed product overview | YES lamp/ballast/emergency converter | YES tests/results | ADMIN via ETS + ABB i-bus Tool | diagnostics via i-bus Tool | strong KNX alternate |
| Theben DALI-Gateway S64/S128 KNX | KNX TP behind KNX/IP | YES | YES | YES | YES | single-master family; use P64 when modern inputs are required | fault/replacement support | family-specific | ADMIN via free ETS DCA | N/E as Runtime surface | capacity alternate |
| Lunatone DALI-2 IoT / IoT4 | REST/WebSocket JSON or Modbus TCP | YES; 1-line gateway DALI Alliance product 3912 | YES | YES: RGB/RGBW/WAF/Tc/xy API | YES; groups/scenes/zones/sequences | API/device support, exact lab set must be verified | DALI bus status + monitor; device events | 2026 Emergency variant adds DT1 endpoints | ADMIN/API + DALI Cockpit as applicable | YES: WebSocket `daliMonitor` | research/lab candidate; not first product path |

## 5. Candidate A — Intesis Modbus Server / DALI-2

### Products

Current 700 Series products revalidated on 2026-10-06:

- `IN703DAL0640000`: one DALI line; the same hardware can be configured in Intesis MAPS for Modbus TCP/RTU or BACnet/IP/MS-TP applications;
- `IN704DAL1280000`: two DALI lines; the same hardware can be configured in Intesis MAPS for Modbus TCP or BACnet/IP applications.

HMS marks the earlier `IN703DAL0640000` and `IN704DAL1280000` as Legacy product and points to these 700 Series replacements.

Current capacity:

- up to 64 ECG/control-gear addresses per DALI line;
- up to 64 DALI-2 input devices per line, subject to bus-current and instance limits;
- up to 10,000 enabled signals per gateway;
- integrated DALI bus supply;
- current product applications support Modbus and BACnet without changing the physical 700 Series platform.

### DALI coverage

Current 700 Series official material documents DALI-2 compatibility and:

- Part 101;
- DT0 / Part 201;
- DT1 / Part 202 self-contained emergency lighting;
- DT6 / Part 207 LED modules;
- DT8 / Part 209 colour control;
- Part 252 energy data;
- Part 253 diagnostics/maintenance data;
- DALI-2 input-device profiles:
  - Part 301 push-buttons;
  - Part 302 absolute input devices;
  - Part 303 occupancy;
  - Part 304 light sensors.

The current IN703/IN704 Modbus maps explicitly expose Type 8 colour-temperature and RGB/RGBW read/write registers for individual/group/broadcast control.

Disposition:

`INTESIS_DT8_RICH_CONTROL = PUBLIC_CONTRACT_CONFIRMED / L4_INTEROP_REQUIRED`

### Runtime-exposed useful signals

The public map includes examples such as:

- actual light level;
- ballast/lamp failure;
- ballast status;
- device type;
- scene recall/store/remove;
- fade/min/max/power-on/failure levels;
- group controls;
- input-device data;
- emergency failure/mode/status;
- emergency battery charge;
- next function/duration test;
- test result;
- start/stop emergency test.

This is a strong SCADA-oriented contract.

### Commissioning boundary

Intesis MAPS performs:

- DALI channel scan;
- device discovery;
- short-address acquisition/assignment;
- group assignment;
- scene configuration;
- device parameter programming;
- configuration download;
- diagnostics.

These are gateway commissioning operations.

EliteSCADA should not reproduce them as ordinary HMI commands.

### Security/deployment

Modbus TCP itself does not provide a modern authenticated/encrypted application security boundary in this integration.

First deployment rule:

- trusted OT LAN/VLAN;
- firewall source restriction;
- no direct Internet exposure;
- VPN/private network for remote use;
- explicit gateway endpoint allowlisting.

No gateway credential should be invented if the product protocol does not define one.

## 6. Candidate B — LOYTEC L-DALI BACnet controllers

### Products

Current official family:

- LDALI-ME201-U — 1 channel;
- LDALI-ME202-U — 2 channels;
- LDALI-ME204-U — 4 channels.

Official current documentation lists:

- DALI-2 certification;
- BACnet/IP;
- BACnet/SC;
- BACnet MS/TP;
- Modbus TCP;
- OPC UA;
- HTTP/HTTPS and firewall functions;
- integrated DALI bus power.

### DALI coverage

The current L-DALI manual documents:

- up to 64 DALI devices per channel;
- 16 DALI groups per channel;
- scenes;
- lamp and ballast failure detection;
- DALI multi-master;
- DALI sensors/buttons;
- DALI-2;
- general-purpose sensor instances;
- DALI protocol analyzer.

Product sources explicitly document:

- DT8 tunable white;
- full colour control;
- emergency-light testing;
- DALI-2 input devices;
- DALI-2 certified interface.

### BACnet projection

The LOYTEC manual explicitly states that the BACnet interface controls DALI ballasts and accesses DALI ballast/sensor information using BACnet objects.

Documented object-level operations include:

- Analog Output: light level and selected configuration/application values;
- Analog Input: level feedback;
- Multi-State Output: commands including emergency test/burn-in/colour-temperature operations;
- Analog Input: emergency battery status.

This is exactly the gateway projection model wanted by EliteSCADA.

The provider remains `bacnet.ip`.

DALI semantics are metadata/Equipment/Capability projections over those BACnet objects.

### Security

The gateway family supports BACnet/SC with TLS/certificate authentication.

That does not mean current EliteSCADA `bacnet.ip` automatically supports BACnet/SC. A future BACnet/SC connection must use the product's existing BACnet security roadmap rather than being smuggled into DALI work.

Until then, BACnet/IP deployment remains local/private-network scoped.

### Commissioning

Commissioning and maintenance are performed using LOYTEC configuration/web tooling and device UI.

The built-in DALI analyzer is useful for L4 debugging but is not a reason to create a second EliteSCADA diagnostics framework.

## 7. Candidate C — Theben DALI-Gateway P64 KNX

### Why it leads the KNX/DALI lab list

Current product 4940303 is a multi-master KNX/DALI gateway and the strongest current KNX candidate found for the combined path.

Documented current features:

- DALI-2 certified;
- KNX Data Secure;
- 64 DALI devices;
- 16 groups plus individual control;
- 8 DALI-2 motion/presence/light sensors;
- from firmware 2.x / ETS application V2.1:
  - 8 DALI-2 push-buttons;
  - 8 generic input devices;
  - virtual input-device composition;
  - GTIN identification;
  - DALI Part 252 energy reporting;
  - API/MQTT interface;
- DT8 colour and colour-temperature control, group or individual;
- scenes;
- effects/sequences;
- DT1 emergency lighting;
- device UI;
- integrated web server;
- free ETS DCA commissioning.

Current official German list price observed:

- EUR 569.96 MSRP excluding VAT on 2026-10-06.

Price is informational only and can change by region/channel.

### Runtime projection

For normal EliteSCADA operation:

`KNX group objects -> canonical KNX TAG bindings -> Equipment/Capability semantics`

Do not call the DALI bus directly.

The future KNX/DALI architecture must map which KNX communication objects expose:

- switch;
- absolute dim;
- relative dim;
- actual level;
- group;
- scene;
- DT8 colour/Tc;
- sensor values;
- input events;
- emergency state;
- fault state;
- energy data.

Commissioning remains on the gateway/ETS side.

## 8. Candidate D — Schneider SpaceLogic KNX DALI Gateway Pro

Product:

`MTN6725-0101`

The DALI Alliance current product database records product ID 3717 as DALI-2 certified.

Current certification record and Schneider documentation establish:

- application controller/gateway;
- DALI/DALI-2 control gear;
- 64 ECGs;
- 16 groups;
- individual control;
- up to 8 DALI-2 motion/light sensors;
- input-device event support;
- Part 301/302/303/304/306 support in certification properties;
- DT8 / Part 209;
- xy, Tc and RGBWAF colour types;
- lamp-failure feedback;
- emergency Part 202;
- integrated bus supply;
- Ethernet;
- commissioning through DCA/web/ETS;
- Schneider application documentation also describes API/MQTT functionality.

This is an excellent alternate for KNX/DALI L4 interoperability because its public DALI Alliance certification record is unusually explicit.

It is not selected over the Theben P64 only because Theben's current product documentation explicitly combines the desired modern KNX Data Secure + current firmware 2.x input/energy/IoT feature set.

## 9. Candidate E — ABB DG/S x.64.5.1 Premium

Products:

- DG/S 1.64.5.1;
- DG/S 2.64.5.1.

ABB current product pages continue to list the family.

Official documentation establishes:

- DALI-2 certification;
- one or two channels;
- 64 DALI devices per channel;
- individual, group and broadcast control;
- 16 groups per channel;
- 16 scenes;
- 4 sequences in premium application;
- DT0;
- DT1 emergency;
- DT8:
  - tunable white;
  - RGB(W);
  - HSV(W);
- lamp/ballast/emergency-converter fault reporting to KNX;
- emergency function/duration/battery test triggering/results through KNX;
- DALI commissioning/diagnostics with ABB i-bus Tool.

This remains a strong industrial KNX/DALI reference.

The reviewed product overview did not establish the same breadth of standardized DALI-2 input-device/sensor support as the newer multi-master candidates, so Theben P64 and Schneider Pro rank higher for the first combined lab.

## 10. Candidate F — Lunatone DALI-2 IoT / IoT4

### Why it is technically attractive

Current vendor product line provides:

- DALI-2 IoT, one DALI line;
- DALI-2 IoT4, four DALI lines;
- Ethernet;
- documented REST API;
- WebSocket JSON event stream;
- Modbus TCP;
- current DALI-2 certification links;
- Emergency variants.

DALI Alliance product 3912 records the 1-line `DALI-2 IOT` as certified DALI-2.

### Rich API surface

Public API documentation provides high-level control of:

- switch;
- dim;
- scenes;
- individual devices;
- groups;
- zones;
- RGB/RGBW;
- WAF;
- colour temperature;
- xy colour;
- sequences;
- schedules;
- circadian progressions.

WebSocket events include:

- scan progress;
- device changes;
- DALI bus status;
- `daliMonitor` bus traffic;
- direct DALI frame operations.

Current 2026 vendor material adds an Emergency variant with REST endpoints for DT1 function/duration/communication testing and results.

### Why it is not the first product path

Using the direct API as an EliteSCADA provider would require a new gateway-specific runtime integration even though Modbus/BACnet/KNX gateways already fit existing providers.

More importantly, the reviewed public API documentation:

- uses `http://` examples;
- uses `ws://` examples;
- contains no documented API authentication mechanism found in the reviewed API manual;
- contains no HTTPS API contract found in that manual.

Therefore direct REST/WebSocket integration must be considered local-trusted-network only until current hardware proves a stronger security mode.

The device remains valuable for laboratory use because it exposes rich DALI semantics and a bus-monitor event stream.

If selected through its Modbus TCP interface, the provider should remain `modbus.tcp`, not become a fake native-DALI provider.

## 11. Gateway-path ranking

### Rank 1 — Modbus/DALI / Intesis

Best for:

- minimum EliteSCADA runtime delta;
- first gateway proof;
- fixed, inspectable process map;
- SCADA-oriented operation;
- DT6/basic lighting;
- groups/scenes;
- sensors;
- diagnostics;
- emergency lighting.

Main residual risk:

- DT8 write semantics are now documented in the 700 Series Modbus map, but exact gear/gateway interoperability still requires L4 proof on the purchased firmware/hardware.

### Rank 2 — BACnet/DALI / LOYTEC

Best for:

- semantically rich building integration;
- full DT8;
- sensors/buttons;
- emergency;
- diagnostics;
- multi-channel scale;
- future BACnet/SC.

Main risks:

- more complex controller;
- BACnet object discovery/mapping must be deliberately bounded;
- secure BACnet transport is separate future product work if current driver remains BACnet/IP.

### Rank 3 — KNX/DALI / Theben P64

Best for:

- combined KNX + DALI professional path;
- KNX Data Secure;
- full modern DALI-2 application-controller semantics;
- rich sensor/input/DT8/emergency/energy integration.

Main risk:

- depends on the future KNX/IP driver and its Checkpoint 1 legal/platform gates.

### Rank 4 — KNX/DALI / Schneider Pro

Excellent interoperability alternate to prevent overfitting to one KNX/DALI vendor.

### Rank 5 — KNX/DALI / ABB Premium

Strong industrial alternate, particularly for DT8/emergency/faults.

### Research-only local-IP candidate — Lunatone IoT

Keep as:

- L2/L4 comparison candidate;
- bus-monitor oracle;
- possible later direct local API integration.

Do not prioritize it over existing protocol providers until authentication/TLS and product need are proven.

## 12. Runtime versus commissioning

### Normal Runtime operations

Safe to expose when projected through the selected gateway protocol:

- on/off;
- absolute dim level;
- relative dim where canonical capability supports it;
- readback/actual level;
- DT8 colour/Tc only where the gateway's standard provider mapping is proven;
- occupancy/illuminance and other sensor state;
- scene activation if represented as a normal bounded process command;
- lamp/gear failure;
- bus/gateway health;
- emergency-lighting status/readback;
- emergency test start/stop only if Product Owner classifies this as an operational maintenance command rather than commissioning.

### Commissioning/admin operations

Do not expose as ordinary HMI commands:

- random-address initialization;
- short-address assignment;
- bus scan that mutates address state;
- group membership programming;
- scene-value programming when it mutates gateway/DALI commissioning state;
- device enrollment;
- factory reset;
- DALI bus reset;
- firmware update;
- KNX ETS programming;
- KNX key provisioning;
- gateway firmware management.

These remain in ETS/DCA, Intesis MAPS, LOYTEC tools, Lunatone tooling or a future explicitly privileged commissioning workflow.

## 13. Semantic projection recommendation

The gateway does not become a second source of truth merely because it speaks to DALI.

Example preferred path:

```
Intesis gateway (Data Source = modbus.tcp)
  -> Modbus register 7000 + ...
  -> canonical TAG
  -> Equipment = luminaire / emergency light / sensor
  -> Capability = switch / dim / level / fault / occupancy / lux / emergency state
```

Example BACnet path:

```
LOYTEC gateway (Data Source = bacnet.ip)
  -> BACnet object/property
  -> canonical TAG
  -> Equipment / Capability projection
```

Example future KNX path:

```
KNX/IP interface/router (Data Source = future KNX)
  -> KNX group address + DPT
  -> canonical TAG
  -> DALI Equipment / Capability projection
```

No `dali.runtime` wrapper is needed for these paths.

## 14. Discovery/import implications

### Modbus/Intesis

EliteSCADA cannot infer the complete DALI topology from arbitrary Modbus register probing.

Preferred future Engineering helper:

- known-gateway template/profile;
- fixed address formulas;
- user-provided or exported Intesis MAPS metadata if a supported documented export becomes available;
- optional bounded register probing for declared slots only.

Do not perform unbounded reads across the full gateway map as “discovery.”

### BACnet/LOYTEC

BACnet discovery/object enumeration can locate gateway objects.

Still separate:

- discovering BACnet objects;
- understanding which object represents which DALI luminaire/group/sensor.

Use vendor-described object structure and names/metadata where reliable.

### KNX gateways

KNX network discovery finds interfaces/routers, not gateway application semantics.

KNX/DALI object mapping normally comes from ETS/project knowledge or deliberate manual bindings.

### Lunatone direct API

The API supports device scan and device-list operations, but DALI address assignment/commissioning remains privileged and must not be treated as passive process discovery.

## 15. Diagnostics

Only extend #500 common diagnostics.

### Generic gateway details

Useful protocol details:

- gateway reachable;
- underlying provider connected;
- gateway model/firmware when available;
- DALI line online;
- DALI bus power/fault;
- device communication error;
- lamp failure;
- ballast/gear failure;
- emergency-light fault/status;
- last successful provider read/write;
- mapped DALI line/address/group;
- reconnect/error counters.

### Candidate-specific

Intesis:

- Modbus connection state;
- DALI communication-error registers;
- lamp/ballast status;
- MAPS diagnostic evidence during L4.

LOYTEC:

- BACnet connection/COV state;
- standard BACnet reliability/status;
- DALI failure objects;
- analyzer as lab evidence.

KNX gateways:

- KNX session/security state;
- gateway group-object state;
- DALI fault objects emitted over KNX.

Lunatone:

- DALI bus status events;
- `daliMonitor` only as an optional lab/debug source, not a replacement diagnostics framework.

## 16. Certification and legal

### DALI-2 certification rule

DALI Alliance states that certification is product/version-specific.

A lab record must capture:

- exact brand;
- GTIN;
- product ID where available;
- hardware version;
- firmware version;
- DALI Alliance database record/date.

Do not claim certification from a family name alone if the purchased unit/version does not match the database record.

### Trademark

DALI and DALI-2 marks/logos are DALI Alliance trademarks.

EliteSCADA documentation/UI must not display a certification logo or imply EliteSCADA itself is DALI-2 certified merely because a gateway or downstream device is certified.

`LEGAL_REVIEW_REQUIRED` before marketing/trademark use.

### Vendor tools

Intesis MAPS, ETS/DCA, ABB i-bus Tool, L-INX Configurator and vendor web tooling are commissioning dependencies.

Do not assume redistribution rights.

The first Runtime path does not require copying their code or SDKs.

## 17. Price / availability

Public vendor list pricing is inconsistent across manufacturers and regions.

Official price evidence found:

- Theben DALI-Gateway P64 KNX 4940303: EUR 569.96 MSRP excluding VAT on German vendor site, observed 2026-10-06;
- Theben DALI-Gateway S128 KNX 4940302: EUR 535.00 MSRP excluding VAT on German vendor site, observed 2026-10-06.

For Intesis, LOYTEC, Schneider, ABB and Lunatone:

- vendor pages generally route to distributor/quote flows;
- no stable globally comparable official MSRP was established in this checkpoint.

Do not rank gateways on reseller price snippets.

## 18. Hardware acquisition priority for later L4

Checkpoint 3 will finalize the full lab matrix, but gateway acquisition should currently prioritize:

P1:
- Intesis IN703DAL0640000 — preferred first Modbus/DALI proof;
- Theben DALI-Gateway P64 KNX — preferred combined KNX/DALI proof.

P2:
- LOYTEC LDALI-ME201-U — BACnet/DALI independent fallback;
- Schneider MTN6725-0101 — independent KNX/DALI interoperability alternate.

P3:
- Lunatone DALI-2 IoT — rich direct API/bus-monitor comparison;
- ABB DG/S 1.64.5.1 or 2.64.5.1 — additional industrial KNX/DALI coverage if budget permits.

The exact purchase list must also include representative DT6, DT8, sensor/input and fault/emergency devices in Checkpoint 3.

## 19. Rejected first-release paths

### Native DALI bus stack

Not selected.

Reason:

Certified professional gateways already project the required process functions through protocols EliteSCADA either already owns or is independently researching.

A native bus stack would add:

- physical transport/interface ownership;
- addressing;
- commissioning;
- multi-master behavior;
- DALI certification/interoperability scope;
- device database/quirks;
- more hardware dependencies.

No evidence in Checkpoint 2 justifies that cost for the first release.

Provisional native-DALI disposition:

`NOT_JUSTIFIED`

This can be revisited if gateway paths prove unable to expose a future required capability.

### Direct Lunatone API as first runtime provider

Not selected because:

- it requires a new provider;
- current reviewed API docs do not establish authenticated HTTPS/WSS;
- existing standard-provider gateways already satisfy the first integration goal.

### Vendor-specific commissioning APIs in Runtime

Rejected.

Commissioning stays administrative.

## 20. Sources reviewed

All facts were revalidated on 2026-10-06.

| Organization | Source | Current evidence used | URL |
| --- | --- | --- | --- |
| HMS Networks / Intesis | 700 Series DALI Gateway IN703/IN704 current product pages + User Manuals v1.0.11 | Current replacement hardware, Modbus/BACnet late configuration, DALI-2, Part 209 colour, Part 252 energy, Part 253 diagnostics, current register/object maps and commissioning | https://www.hms-networks.com/p/in703dal0640000-mbs-dal-dali-2-to-modbus-tcp-rtu-server-application-with-1-dali-channel |
| HMS Networks / Intesis | Legacy-to-700-Series replacement/support guidance | INMBSDAL0640200 -> IN703DAL0640000; INMBSDAL1280200 -> IN704DAL1280000 | https://support.hms-networks.com/hc/en-us/articles/14923465292050-What-is-the-difference-between-my-DALI-gateway-and-the-new-700-Series |
| DALI Alliance | Product database / certification overview | certification meaning and version-specific validity | https://www.dali-alliance.org/dali2/ |
| DALI Alliance | Product database brand listing | Intesis, LOYTEC, Lunatone, ABB, Schneider, Theben are represented DALI Alliance member brands | https://api.dali-alliance.org/products/brands |
| LOYTEC | L-DALI BACnet/DALI Controllers | current product family, DALI-2, BACnet/SC/IP, DT8, emergency, interfaces | https://www.loytec.com/products/dali/l-dali-wired/l-dali-bacnet |
| LOYTEC | L-DALI User Manual 8.6 | groups, scenes, sensors/buttons, failures, BACnet object projection, DALI analyzer | https://www.loytec.com/dl/manual/LDALI_User_Manual.pdf |
| LOYTEC | L-DALI download pages | current manuals/configurator as of Sep 2026 and DALI-2 certificates | https://www.loytec.com/support/download/ldali-me202-u |
| Theben | DALI-Gateway P64 KNX 4940303 | current features, firmware 2.x / ETS 2.1 features, KNX Data Secure, sensors, DT8, emergency, price | https://www.theben.de/en/dali-gateway-p64-knx-4940303 |
| Theben | DALI gateways | P64 and S64/S128 family comparison | https://www.theben.de/solutions-en-gb/dali-2-lighting-control/dali-gateways/ |
| Theben | DALI-Gateway S128 KNX 4940302 | capacity, DT8, scenes and public MSRP evidence | https://www.theben.de/en/dali-gateway-s128-knx-4940302 |
| Schneider Electric | SpaceLogic KNX DALI Gateway Pro MTN6725-0101 | gateway, DT8, sensors, emergency, API/MQTT, commissioning | https://www.se.com/ww/en/product/MTN6725-0101/ |
| DALI Alliance | SpaceLogic KNX DALI Gateway Pro product 3717 | exact DALI-2 certification and detailed product properties | https://api.dali-alliance.org/products/3717/spacelogic-knx-dali-gateway-pro |
| ABB | ABB i-bus KNX DALI Gateway Premium | current family availability and feature overview | https://new.abb.com/low-voltage/products/building-automation/news-and-highlights/abb-i-bus-knx-dali-gateways-premium |
| ABB | DG/S x.64.5.1 product overview | DT0/DT1/DT8, group/individual/scene/fault/emergency behavior | https://library.e.abb.com/public/0ceff6cd04284c10850641dedef62062/DGS_X6451_PH_EN_V2-0_9AKK107680A0534_Rev_B.pdf |
| Lunatone | DALI-2 IoT Gateway | Ethernet, REST/WebSocket, Modbus TCP, certification link | https://www.lunatone.com/en/product/dali-2-iot-gateway/ |
| Lunatone | DALI-2 IoT API M0023 | rich DT8/group/scene/zone API and WebSocket DALI monitoring | https://www.lunatone.com/wp-content/uploads/2021/08/89453886_DALI2_IOT_API_Dokumentation_EN_M0023.pdf |
| Lunatone | 2026 Emergency announcement | DT1 test/control extensions | https://www.lunatone.com/en/news/ |
| DALI Alliance | Lunatone DALI-2 IOT product 3912 | exact DALI-2 certification record | https://api.dali-alliance.org/products/3912/dali-2-iot |
| EliteSCADA live repo | Modbus/BACnet implementations | existing `modbus.tcp` and `bacnet.ip` canonical provider/runtime evidence | current release base |

## 21. Facts established

1. Gateway-first remains justified.
2. A new DALI Runtime provider is not required for first release.
3. Existing Modbus and BACnet providers give two immediate gateway routes.
4. Intesis gives the smallest runtime delta and the clearest SCADA/register contract.
5. LOYTEC gives a stronger rich-DALI semantic surface through BACnet.
6. Theben P64 is the strongest current KNX/DALI combined-path lab candidate found.
7. Schneider Pro is an excellent independent KNX/DALI interoperability alternate with explicit current DALI Alliance certification data.
8. ABB Premium remains useful for independent industrial KNX/DALI coverage.
9. Lunatone proves that rich local-IP DALI APIs exist, but a direct API driver is not justified before security/product-need gates.
10. DALI commissioning must remain separate from normal Runtime operation.

## 22. Uncertainties

- Intesis 700 Series rich DT8 control is now documented in the current public Modbus register maps; remaining uncertainty is real-hardware interoperability and the exact purchased firmware/hardware certification identity.
- Exact purchased firmware/hardware must be matched against DALI Alliance certification records.
- Regional availability and acquisition price for most candidates remain distributor-specific.
- Existing EliteSCADA BACnet/IP support does not imply BACnet/SC support.
- KNX/DALI Runtime operation still depends on a future accepted KNX/IP implementation.
- Vendor API/MQTT contracts on KNX gateways are not selected as canonical Runtime paths and were not exhaustively security-audited in this checkpoint.
- Lunatone direct API authentication/TLS was not found in the reviewed API document and requires current-hardware confirmation before any product consideration.

## 23. Checkpoint 2 disposition

`CHECKPOINT_2_COMPLETE / GATEWAY_FIRST_CONFIRMED`

`PREFERRED_FIRST_PATH = MODBUS_TCP / INTESIS DALI-2`

`FALLBACK_PATH = BACNET_IP / LOYTEC L-DALI`

`PREFERRED_KNX_DALI_LAB = THEBEN P64 KNX`

`NATIVE_DALI_PROVISIONAL = NOT_JUSTIFIED`

Next work belongs to Checkpoint 3:

1. combined KNX -> DALI architecture and exact Runtime/commissioning separation;
2. legal/certification/deployment matrix;
3. full lab purchase list and L0-L4 validation plan.

DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
NO MERGE PERFORMED
