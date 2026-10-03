# DALI Research

Research date: **2026-10-02**

Overall first-path readiness: **\`GO_AFTER_FOUNDATION\`** for gateway integration.  
Native DALI readiness: **\`NOT_FIRST_RELEASE\`**.

## Decision summary

Use two distinct product paths:

### First path

\`EliteSCADA -> existing supported protocol driver -> certified professional DALI gateway -> DALI bus\`

Practical gateways expose KNX, Modbus, BACnet or vendor IP interfaces. If the gateway has a truthful, documented mapping through an already supported protocol, EliteSCADA can integrate lighting without owning DALI bus timing or commissioning in v1.

Do not create a fake “DALI driver” that merely renames opaque Modbus registers.

### Future native path

\`EliteSCADA -> Host DALI Interface/Controller -> DALI bus\`

Native DALI should wait until:
- F4 Host Hardware can own a DALI interface;
- a cross-platform interface with documented host protocol is selected;
- IEC/DALI specification access is settled;
- certification/trademark positioning is explicit;
- L4 lab and commissioning hardware exist.

## DALI/DALI-2 model

Current DALI Alliance material distinguishes:
- **control gear**: LED drivers/ballasts and other output gear;
- **control devices**:
  - application controllers;
  - input devices such as sensors, switches and pushbuttons;
- bus power supplies.

A DALI subnet supports up to:
- 64 control-gear addresses;
- 64 control-device addresses.

Control gear has 16 group memberships and 16 scenes. DALI-2 adds standardized control-device/input-device behavior and stronger interoperability certification.

Important Device Type examples:
- DT6: LED modules/drivers;
- DT8: colour control;
- DT1: self-contained emergency lighting.

Emergency lighting must not be claimed in first release merely because a gateway exposes some status registers.

## Commissioning semantics

Native commissioning eventually needs:
- bus scan;
- detection of unaddressed/random-address devices;
- short-address assignment;
- collision/retry handling;
- read device type/features;
- group membership;
- scene content;
- identify/test;
- replacement workflow;
- persisted stable protocol identity beyond a human short address where possible.

Address assignment changes the external DALI installation and therefore requires explicit confirmation/audit.

## DALI via gateway

### KNX/DALI

Professional certified gateways commonly expose:
- individual gear;
- groups;
- dim level;
- DT8 colour/tunable-white parameters;
- scenes;
- lamp/gear fault state.

A KNX/DALI gateway is especially attractive once KNX/IP is promoted from Tier B, because the mapping can remain semantic and local.

### Modbus/DALI

Use only products with public, stable register documentation and sufficient read/write/fault coverage. EliteSCADA must import a model-specific gateway map; a generic Modbus connection alone does not create standardized DALI semantics.

### BACnet/IP or other IP gateway

Same rule: use the documented gateway semantic model and preserve gateway identity/firmware in the integration record.

### Gateway acceptance

For a supported gateway model prove:
- commissioning remains truthful about whether it is performed by gateway software or EliteSCADA;
- every exposed level/colour/fault point has documented mapping;
- restart preserves address/group state;
- diagnostics identify gateway vs DALI-bus failure;
- firmware/model version is recorded.

## Native DALI

No broadly adopted permissive, cross-platform, host-level DALI stack comparable to Z-Wave JS or zigbee-herdsman was identified that removes the hardware-interface dependency.

Native v1 therefore should not start with a generic software stack decision. Select the **interface/controller hardware and host protocol first**.

Candidate hardware classes:
- USB DALI interface with documented host protocol;
- Ethernet/IP DALI controller with documented local API;
- industrial fieldbus terminal such as a DALI-2 master terminal where an existing fieldbus driver can expose the controller cleanly.

## Bus timing

DALI is a dedicated two-wire bus with protocol-specific timing and bus power. The Host Hardware adapter or controller must handle electrical layer requirements. EliteSCADA must not bit-bang DALI from an ordinary serial port.

## Capability mapping

| DALI concept | EliteSCADA |
|---|---|
| gear on/off | OnOff / Light |
| arc level | Dimmer / Light |
| DT8 colour / colour temperature | ColorLight |
| input presence sensor | OccupancySensor |
| input light sensor | IlluminanceSensor |
| pushbutton/input instance | Button |
| gear lamp/device failure | diagnostic/alarm source |
| group | protocol-side target/group metadata |
| scene | Scene command/reference when deliberately modeled |
| emergency status | dedicated diagnostics; safety semantics require separate scope |

DALI groups/scenes are external bus configuration. They are not a replacement for canonical EliteSCADA automation or templates.

## Certification and trademark

Facts from current DALI Alliance material:
- DALI-2 certification is independently verified;
- only certified products may carry DALI-2 trademarks;
- only eligible DALI Alliance members can certify products;
- current FAQ lists Associate and Regular membership fees and separate certification credits; these commercial terms can change.

A certified gateway does **not** make EliteSCADA itself DALI-2 certified.

Safe wording before certification/legal review:
- “integrates with [specific gateway/model]”;
- “DALI integration via certified gateway” where the gateway itself is verified in the DALI Product Database.

Do not use the DALI-2 logo or claim “DALI-2 Certified EliteSCADA” without completing the applicable process.

\`LEGAL_REVIEW_REQUIRED\` before public branding.

## Packaging

Gateway path:
- no DALI native dependency;
- existing network/fieldbus driver;
- model-specific mapping package/documentation;
- user-provided certified gateway.

Native future:
- driver plus Host DALI resource;
- optional vendor library only if redistribution terms are acceptable;
- device/interface mapping for containers where applicable.

## Performance / scale

**FACT:** one conventional DALI subnet supports 64 control gear + 64 control devices; individual gear supports 16 scenes, and control gear uses 16 groups.

**PROPOSED ELITESCADA LIMIT:** treat each gateway bus/channel as an explicit topology node and never flatten multiple buses into one fake address space. Query/poll pacing must respect gateway and DALI bus limits.

## Recommended architecture

First release:
\`EliteSCADA -> KNX/IP or documented Modbus/BACnet/IP gateway -> DALI\`

Future:
\`EliteSCADA -> leased DALI host interface -> dali.native -> bus\`

## Candidate stack

Gateway:
- existing EliteSCADA protocol drivers;
- gateway-specific semantic profile.

Native:
- \`RESEARCH_MORE\` at hardware/interface selection time; no software stack selected yet.

## License / distribution

DALI/IEC specifications and DALI Alliance certification/trademark rights are separate from any gateway API license.

Gateway API/register documentation terms vary by vendor.

\`LEGAL_REVIEW_REQUIRED\` for native commercial certification claims, logo use and any redistributed vendor SDK.

## Hardware

L4 gateway lab:
- one current DALI-2 certified KNX/DALI or local-IP gateway;
- one DALI bus power supply if not integrated;
- DT6 LED driver + luminaire/load;
- DT8 tunable-white or colour driver;
- DALI-2 occupancy/input device;
- pushbutton/input device.

Native later:
- selected host DALI interface/controller;
- same gear/input matrix.

## EliteSCADA dependencies

Gateway path:
- canonical capabilities/location/discovery;
- existing protocol driver;
- device template/mapping.

Native path additionally:
- F4 DALI interface resource;
- protected commissioning state;
- external-network mutation confirmation.

## Implementation slices

Gateway first:
1. choose one documented certified gateway;
2. capture model/firmware map;
3. semantic profile -> capabilities;
4. discovery/import;
5. fault diagnostics;
6. L4 bus commissioning and restart;
7. add additional gateway profiles only with independent evidence.

Native future:
1. select interface + host protocol;
2. codec/timing abstraction;
3. scan/address;
4. groups/scenes;
5. DT6;
6. DT8;
7. input devices;
8. recovery/replacement;
9. certification decision.

## Tests

- **L0:** profile/register mapping; DALI object/capability conversions.
- **L1:** fake gateway and deterministic failure modes.
- **L2:** real gateway protocol peer simulator where available.
- **L3:** canonical TAG/Command/Gateway/Alarm/Historian integration.
- **L4:** certified gateway + real DALI-2 gear/input devices; native path later with selected interface.

## Risks

- vendor-specific gateway maps;
- bus commissioning semantics hidden behind vendor software;
- specification access/certification cost;
- confusing “compatible” with “certified”;
- emergency-lighting safety claims;
- native interface portability.

## Open decisions

- first gateway family to support;
- whether KNX/IP Tier B should be promoted before DALI gateway implementation;
- business decision on pursuing DALI Alliance membership/certification;
- native DALI hardware interface.

## Readiness

Gateway: \`GO_AFTER_FOUNDATION\`  
Native: \`NOT_FIRST_RELEASE\`

## Sources

Accessed 2026-10-02:
- https://www.dali-alliance.org/dali/keyfeatures.html
- https://www.dali-alliance.org/dali/systems.html
- https://www.dali-alliance.org/dali2/
- https://www.dali-alliance.org/about-us/faqs.html
- https://www.dali-alliance.org/about-us/terms.html
- https://www.dali-alliance.org/certification
- DALI Alliance Product Database examples of DALI-2 gateways.
