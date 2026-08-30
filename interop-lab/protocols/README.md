# Protocol peer strategy

This file records what the lab should use on the *other side* of each EliteSCADA Driver. The goal is implementation diversity and repeatability, not to make Node-RED pretend to be every industrial device ever invented.

## MQTT

Base peer: Eclipse Mosquitto.

Use Node-RED to generate and observe payloads while the EliteSCADA MQTT Driver connects to Mosquitto independently. The base flow already supports scripted stimulus and observation under `elitescada/lab/#`.

Later add a second broker implementation for independent MQTT 5 interoperability evidence.

## Allen-Bradley EtherNet/IP / CIP

Overlay: `compose.cip.yaml`.

The overlay builds the simulator directly from pinned commit `baf21c625c4f1250fa3cbae6cdd636ce15620ef2` of `blanpa/node-red-contrib-cip-suite`, using only its `simulator/` build context.

Initial peers:

- ControlLogix on host port 44818;
- CompactLogix on host port 44819.

The Node-RED image also contains `node-red-contrib-cip-suite` 0.0.5 so it can serve as an independent reference client when both Node-RED and EliteSCADA are pointed at the same peer.

Do not infer real Rockwell firmware compatibility from this simulator alone. It is L2 software interoperability evidence.

## OPC UA

The Node-RED image contains `node-red-contrib-opcua-suite` 0.1.5 and exposes host port 4840. The next lab slice should add a deterministic embedded-server flow with:

- stable NodeIds;
- Boolean/Int16/Int32/Int64/Float/Double/String/DateTime;
- writable and read-only nodes;
- subscription updates;
- anonymous, username and certificate/security scenarios where supported;
- server restart/session-loss fault injection;
- an explicit unsupported/unknown datatype case.

The server flow must not persist credentials in repository JSON.

## IEC 60870-5-104

Use an independent IEC-104 server/outstation implementation as a sidecar. Node-RED should orchestrate scenario state over an HTTP/MQTT control adapter rather than becoming the protocol implementation itself.

Priority evidence: STARTDT, GI, COT, quality, CP56Time2a, spontaneous/backlogged events, command confirmation and reconnect behavior.

## DNP3

Use an independent outstation implementation that is **not Step Function**. Driver 7 already has strong same-stack Step Function wire evidence; this lab should increase implementation diversity rather than repeat it.

Priority evidence: startup integrity, event classes, unsolicited responses, flags/quality, synchronized versus unsynchronized source time, CROB SBO/Direct Operate, analog output and reconnect/no-replay.

Step Function licensing is recorded as a future commercial-release review item only. It must not block current lab work.

## Siemens S7 ISO-on-TCP

Node-RED can be a reference S7 client, but the EliteSCADA Driver requires an independently reachable S7 server/PLC simulator on TCP 102 (or a mapped lab port). A Node-RED internal simulated value backend is not enough because it does not prove the EliteSCADA wire implementation.

Priority evidence: COTP/S7 setup, rack-slot/TSAP, PDU negotiation, I/Q/M/DB addressing, supported typed values, ordering transforms, write rejection and reconnect.

## BACnet/IP

Use an independent BACnet/IP device/server peer. Node-RED may act as a reference client and scenario controller.

Broadcast discovery can be awkward in bridged Docker networking. Dedicated host networking or an explicitly configured BACnet/IP network/BBMD profile may be necessary for Who-Is/I-Am and BBMD/FDR tests. Do not hide that topology detail behind a driver-specific shortcut.
