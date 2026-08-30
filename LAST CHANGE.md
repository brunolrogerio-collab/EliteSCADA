# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

**WAVE 10 remains ACTIVE and has product priority.** This branch is isolated Driver interoperability-lab infrastructure and does not alter the Wave 10 integration train or `main`.

### OPC UA interoperability lab — IMPLEMENTED IN PR / NOT MERGED

PR: **#148 — Interop Lab: independent open62541 OPC UA peer**  
Branch: `coordination/driver-interop-opcua-v1`  
Functional tested head: `3a181fcf3fb600fdfd5af3d303c03b8470628bae`

Delivered laboratory scope:

- independent open62541 **1.5.4** OPC UA server built from SHA-256-pinned official amalgamation assets;
- explicit MbedTLS build/runtime dependency required by the official 1.5.4 amalgamation;
- independent `node-opcua` reference client from the Node-RED lab image;
- isolated `compose.opcua.yaml` overlay, not a product runtime dependency;
- stable writable nodes for Double, Int32, Boolean and String;
- browse verifies all four stable NodeIds;
- typed reads verify all four scalar types and expected values;
- Double write/readback is verified;
- Int32 monitored-item subscription notification is verified after write;
- base MQTT round-trip smoke remains mandatory in the same lab workflow.

Exact-head evidence on functional head `3a181fcf3fb600fdfd5af3d303c03b8470628bae`:

- **Interop Lab Smoke #16 — SUCCESS**, run `33330354791`;
- **EliteSCADA CI #830 — SUCCESS**, run `33330354787`;
- normal CI passed Web build, backend build/tests/runtime smoke and Chromium E2E;
- dedicated lab gate passed scenario/flow JSON validation, base/CIP/OPC-UA Compose validation, base-lab startup, MQTT smoke, open62541 build/start, OPC UA interoperability smoke and cleanup.

### Evidence classification

This establishes **L2 independent-software OPC UA interoperability evidence for the laboratory scenario**.

It does **not** establish:

- acceptance of the `driver9/opc-ua` EliteSCADA product path;
- username/password session acceptance;
- certificate trust or secure policy/mode acceptance;
- unknown/custom datatype acceptance;
- reconnect/resubscribe acceptance after server restart;
- vendor simulator/device or hardware certification.

The interoperability lab remains test infrastructure. Its success must not be reported as production certification of Driver 9.

## Next laboratory direction

After this OPC UA L2 milestone, the next recommended isolated laboratory work is to wire the existing Driver 10 broker-integration tests to the lab Mosquitto instance so that the **actual EliteSCADA MQTT product transport path** is exercised instead of only the generic MQTT control-plane smoke.

## CI policy

CI mode remains **NORMAL**. Exact functional integration/evidence heads require green evidence. Documentation-only checkpoint commits use `[skip ci]`; the functional evidence above belongs to the tested head `3a181fcf3fb600fdfd5af3d303c03b8470628bae`, not to this documentation-only successor commit.
