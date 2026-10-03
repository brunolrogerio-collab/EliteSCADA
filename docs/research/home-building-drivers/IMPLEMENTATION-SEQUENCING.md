# Proposed Implementation Sequencing

Research date: **2026-10-02**

This is a research recommendation for Main/Product Owner. It does **not** release branches.

## Gate 0 — complete foundation first

Before any Tier A driver DEV:
1. revalidate #469 final Host Serial/resource result;
2. F0 contract audit;
3. F1 Integration Catalog metadata;
4. F2 Locations;
5. F3 Capability vocabulary;
6. F4 Host Hardware/controller authority;
7. F5 Integration/Locations/Devices UX;
8. F6 discovery candidate;
9. protected secret/material references;
10. common external-network mutation contract;
11. common LocalBridge lifecycle if the first released driver needs it.

Some direct-network drivers can start once the subset they consume is frozen, but Main must assign the actual concurrency-safe base.

## Suggested future order

### Wave H1 — direct local integrations with low infrastructure risk

#### 1. Shelly RPC

Why:
- local;
- no sidecar;
- no host radio;
- public JSON-RPC;
- immediate OnOff/Power/Energy/Cover value;
- exercises Device/Capability UX cleanly.

Readiness: \`GO_AFTER_FOUNDATION\`.

#### 2. ESPHome Native

Why:
- local;
- documented protobuf/Noise wire protocol;
- no sidecar required;
- broad DIY/residential sensor/actuator coverage;
- strong testability.

Readiness: \`GO_AFTER_FOUNDATION\`.

These can potentially execute in parallel if Main proves path separation.

### Wave H2 — bridge breadth + building protocol

#### 3. Home Assistant Bridge

Why:
- broad compatibility without new radios;
- validates large import/reconciliation;
- reuses same capabilities;
- user-managed dependency.

Readiness: \`GO_AFTER_FOUNDATION\`.

#### 4. KNX/IP Tier A promotion

Why:
- professional building relevance;
- enables a strong DALI gateway path;
- KNX IP Secure must be first-class.

Research more before DEV, but current evidence supports promotion.

### Wave H3 — Zigbee breadth and native ownership

#### 5. Zigbee2MQTT Bridge

Why:
- fastest broad Zigbee compatibility path;
- lets Product validate discovery/capability UX;
- user-managed v1 avoids GPL packaging risk.

Readiness: \`GO_AFTER_FOUNDATION\`.

#### 6. Native Zigbee

Why:
- strategic no-MQTT native path;
- mature permissive radio/quirk libraries available;
- requires F4 coordinator lease and sidecar lifecycle.

Readiness: \`GO_AFTER_FOUNDATION\`.

Bridge and native must remain separate integrations sharing the same Device/Capability model.

### Wave H4 — DALI gateway

#### 7. DALI via one documented professional gateway

Prefer a gateway path after KNX/IP if the chosen gateway is KNX/DALI; alternatively select a documented BACnet/Modbus/IP product.

Why:
- professional lighting;
- low risk compared with native bus stack;
- can validate DALI semantics before native hardware investment.

Readiness: \`GO_AFTER_FOUNDATION\`.

### Wave H5 — Z-Wave

#### 8. Z-Wave JS Bridge

Why:
- mature permissive stack;
- requires F4 controller lease + common sidecar lifecycle;
- hardware/security/NVM L4 burden is significant.

Readiness: \`WAIT_DEPENDENCY\`.

### Wave H6 — Matter

#### 9. Matter Controller

Why later:
- strong strategic relevance;
- controller sidecar is improving rapidly;
- current matterjs-server still labels the new matter.js controller Beta;
- Thread/BLE/container/persistent fabric add deployment complexity.

Readiness: \`WAIT_DEPENDENCY\`.

Re-evaluate sidecar maturity/certification at release time; Matter may move earlier if Main values it and current sidecar exits Beta with adequate L2/L4 evidence.

### Wave H7 — BTHome receive-first

#### 10. BTHome

Could move earlier once F4 BluetoothAdapter exists. It is low-code but needs cross-platform BLE scanning evidence. Keep first scope sensor receive only.

### Cloud connectors — product/commercial decision

Tuya and Intelbras GDI should not block local foundation.

Promote only when:
- Product Owner explicitly wants cloud dependence;
- commercial plan/terms are approved;
- credential/account lifecycle is defined.

Intelbras GDI has specific Brazilian market value and currently requires company/CNPJ + paid plans according to official manual.

### Native DALI — later

Native DALI should follow:
- proven DALI gateway semantics;
- selected host interface/controller;
- certification/business decision.

Readiness: \`NOT_FIRST_RELEASE\`.

## Dependency graph

\`\`\`
#469 outcome
   |
   v
F0 -> F1 -> F2 -> F3 -> F4 -> F5/F6
                  |      |       |
                  |      |       +--> Shelly / ESPHome / HA
                  |      +----------> Zigbee / Z-Wave / BLE
                  +-----------------> all capability mappings

Secret refs + mutation boundary -----> all secured commissioning integrations
LocalBridge lifecycle ---------------> Native Zigbee / Z-Wave / Matter / managed Z2M

KNX/IP ------------------------------> strong DALI gateway path
\`\`\`

## Stacks rejected for first implementation

- copying Zigbee2MQTT GPL code into proprietary core;
- writing Zigbee radio stacks from scratch;
- GPL \`zigpy\` embedded in proprietary core;
- direct connectedhomeip C++ integration before testing a sidecar path;
- native Z-Wave stack before Z-Wave JS bridge;
- native DALI before hardware/interface selection;
- cloud-first Tuya/Intelbras before local foundation.

## Main release checklist

Before each future DEV:
- re-search official latest version/release;
- re-read LICENSE at exact commit/tag;
- revalidate certification/trademark terms;
- pin hardware model/firmware;
- assign exact integration base;
- list allowed product paths;
- define L0-L4 acceptance;
- define support statement narrowly;
- carry any \`RESEARCH_CONTRACT_DELTA_REQUIRED\` into the foundation lane rather than letting the driver invent it.
