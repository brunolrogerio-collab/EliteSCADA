# Connections, Modbus, MQTT and OPC UA

## Data Source: from connection to a verified TAG

Configure a connection before linking values to screens, alarms and history.

### Before entering values

Start with a known reading, such as pressure you can check on the equipment. In Engineering > Data Sources, a source stores how to reach the equipment. The driver speaks its protocol; the binding identifies the point to read; the TAG is the variable receiving the value.

Have the manufacturer’s map and network or serial-port settings ready. Start with a read-only TAG. Communication comes from the SCADIVO server: reaching equipment in a browser does not prove that server can reach it. Fields below are for lookup during configuration.

### Prepare, check and activate

1. Create or select the source and choose the driver matching the equipment protocol.
2. Fill basic fields. Open advanced fields when you need to review or change their values.
3. Use Test connection if the driver offers it. The test uses the draft; it does not apply the edit or change Runtime.
4. Review changes and diagnostics. Apply to Workspace puts configuration in the working draft.
5. Create a TAG with that source, value type and point binding. Test reading when available.
6. Save, publish and activate through project controls so Runtime uses the configuration.
7. Compare value and unit with the known reference. Check quality and updates before adding a screen, alarm or command.

### Look up source fields

Use this table while preparing the connection. The procedure above gives the sequence; names and examples here help when a field is unclear.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Source key | Identifies the connection in TAG references. Use a readable key distinct from other sources; this is not an IP address. | plc.bomba01 |  |
| Name | Name shown in the list. Describe the equipment or service this connection serves. | Bomba 01 |  |
| Connection type / driver | Selects the protocol and its fields. Modbus TCP, Modbus RTU Master, MQTT Raw and OPC UA are different connections; changing type requires reviewing TAG bindings. | modbus.tcp |  |
| Enabled | Includes the source in operational configuration when its revision is activated. Enabling does not repair the network, credentials or an incorrect TAG binding. | true |  |
| Protected references | Password/certificate fields take the reference to material provisioned on the server. Ask the administrator for the correct reference; do not paste the password, private key or certificate contents into ordinary configuration fields. |  |  |

### Connected but no value

Connecting and reading a point are different checks. A successful connection test does not verify the whole map or a physical action. For no value, check enabled source, TAG binding, active revision and diagnostics.

Read the reason: denied access is not a timeout; an invalid reading is not zero. Even if the protocol supports writing, ReadOnly and account permissions still apply. Discovery and test actions depend on the driver. MQTT Raw, for example, offers no Engineering connection test.

[Open this guide in installed Help](/help?topic=sources.connection-workflow)

## Modbus: TCP, serial and register maps

Prepare the connection and use the equipment map to check a first reading.

### Choose client or master

First choose who queries whom. Use Modbus TCP for SCADIVO to read a controller over the network. Use Modbus RTU Master to query equipment on the server’s serial cable; equipment then responds as slaves.

Have the manufacturer’s register map, the list identifying each value’s address and representation. Start with one known reading, without writes. Modbus Server has a different role: SCADIVO responds to an external client. Use its separate guide for that purpose.

### TCP connection and shared timings

Host and port belong to TCP. Unit ID, scan, timeout and gap also apply to RTU, with its Unit ID limit.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Controller host | DNS name or IP reachable from the SCADIVO server running the driver. localhost means that server or container, not the browser computer. | 192.0.2.10 |  |
| TCP port | Port where the controller serves Modbus TCP. It is not the SCADIVO web or API port. | 502 | 1–65535; default 502. |
| Source Unit ID | Logical device address; with a TCP/serial gateway it may select a slave. Applies to TAGs without an individual override. Use the equipment manual value, not the last part of its IP address. | 1 | TCP: 0–255. RTU: 0–247. Default 1; the target must respond to reads. |
| Scan interval (ms) | Requested time between read cycles. 1000 ms is 1 second; reducing it increases traffic and load. Reducing this field cannot make a slow response faster. | 1000 | 10–600000; default 1000. |
| Response timeout (ms) | How long to wait for a request. Unlike scan time, a short timeout may fail on a slow network/bus; a long timeout delays absence detection. | 3000 | 50–60000; default 3000. |
| Maximum address gap | Advanced: combines nearby reads in one block, including the gap. Some controllers reject blocks crossing unimplemented addresses; reduce the gap in that case. It does not change TAG addresses. | 8 | 0–125; default 8. |

### Serial on Windows, Linux and Docker

Use the actual equipment parameters. The host manages the bus; shared settings must be compatible. If the port cannot open, check the device, process/container permission and another program holding the port before changing TAG maps.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Server serial port | Device visible to the server/DriverHost: for example COM3 on Windows or /dev/ttyUSB0 on Linux. In Docker the host must expose the device to the container and grant access. The browser serial port is not used; configuration can be prepared offline. |  |  |
| Baud rate | Must match devices on the same serial bus. A higher speed works only if the equipment and installation support it. | 9600 | 300–4000000; default 9600. |
| Data bits | Part of serial framing. Match the equipment; this is not the TAG numeric type size. | 8 | 5–8; default 8. |
| Parity | Serial frame check; all devices sharing the line need compatible configuration. | None | None / Odd / Even / Mark / Space; default None. |
| Stop bits | Completes serial framing with baud, data bits and parity. Match the equipment. | One | One / OnePointFive / Two; default One. |

### TAG binding and conversion

The address selects where to read; wire type selects interpretation. These are separate decisions. discrete/input are read-only; enabling TAG writes does not make the area writable. The TAG type must hold the conversion result.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| TAG area and address | Use coil for Boolean output, discrete for Boolean input, holding for read/write register and input for input register. Canonical addresses start at zero: holding:0. If the vendor calls its first holding register 40001, confirm that convention before conversion; do not automatically enter 40001 as the offset. | holding:0 | Offset 0–65535; the complete value must fit the area. |
| Individual Unit ID | Overrides the source Unit ID only for this TAG. Leave empty to inherit; for RTU also check slave/bus limits. | 1 |  |
| Wire value type | Defines interpretation of equipment bits. If omitted, inferred from the TAG type. Int16/UInt16 use 1 register, Int32/UInt32/Float32 use 2, Int64/UInt64/Float64 use 4; confirm the vendor map and compatibility with the TAG type. | Int16 |  |
| Word order | Only affects values using several registers: HighWordFirst places the most significant word first, LowWordFirst the least significant. It is not byte order inside a word; do not change randomly to mask an incorrect address/type. | HighWordFirst |  |
| Scale multiplier | Converts the read value: displayed value = raw × scale + offset. For tenths of bar, 253 × 0.1 + 0 = 25.3 bar. Avoid applying the same conversion again in another layer. | 0.1 | Finite and nonzero; default 1. |
| Offset | Addition after scaling. Use only when the equipment map requires an origin correction. | 0 | Finite; default 0. |

### Example and diagnosis by layer

1. Configure the TCP source with the actual host, port 502, controller Unit ID and initial 1000/3000 ms timings.
2. Create a read-only Double TAG, holding:0, wire type Int16 for a register representing tenths of bar.
3. Use scale 0.1, offset 0; if the known raw value is 253, expect 25.3 bar.
4. Test the read and check before activating the revision. Timeout calls for checking connectivity/Unit ID; illegal address calls for area/base; absurd values call for type/order/conversion. Do not use a write as a read test.

```json
{"address":"holding:0","wireType":"Int16","raw":253,"scale":0.1,"offset":0,"expected":25.3}
```

[Open this guide in installed Help](/help?topic=drivers.modbus-setup)

## MQTT: topics, JSON and value freshness

Receive a measurement sent in a message and check that its value is current before using it.

### A broker is not a sensor

In MQTT, equipment publishes messages and the broker distributes them to subscribers. A topic names the message channel; payload is its content. MQTT Raw receives what is published on configured topics.

Before creating the source, have broker address/port, TLS protection, authentication and a real message example. Choose one quantity to test, using a read-only Double TAG, a number with decimal places, for a compatible quantity. The driver does not scan registers like Modbus or automatically turn arbitrary JSON into TAGs.

### Connection, session and resources

Session fields differ between MQTT 3.1.1 and 5. Check the version before advanced settings. Size/queue limits bound memory use; they are not history storage.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Broker host | Server receiving/publishing messages, reachable from the SCADIVO server. It is not a topic or a URL path. | mqtt.example.invalid |  |
| Broker port | Use the port actually configured on the broker; it must match TLS use. | 8883 | 1–65535; default 8883. |
| TLS connection | Enables protected transport. Disabling does not automatically change the port or repair an invalid certificate. Match the broker mode and trust chain. | true |  |
| MQTT client ID | Identifies this connection to the broker. Choose a unique value per active client; reusing it in another connection can disconnect the previous session. It is not the login. | scadivo-pump01 |  |
| MQTT version | mqtt5 uses Clean Start and session expiry; mqtt311 uses Clean Session. Select the broker-supported version without mixing their options. | mqtt5 |  |
| Broker user name | Account authorized by the broker for the topics in use; it is not the SCADIVO login. |  |  |
| Broker password reference | Protected reference resolved by the server. Without provisioned material or broker permission, authentication cannot succeed; do not enter a literal password instead of the reference. |  |  |
| Keep Alive (seconds) | Broker connection supervision interval; it does not request fresh measurements from a device. | 30 | 1–65535; default 30. |
| Connection timeout (ms) | Deadline for a connection attempt, separate from TAG value freshness. | 10000 | 100–300000. |
| Minimum reconnect wait (ms) | Minimum wait between attempts after losing connection. Coordinate with the maximum to avoid continuous attempts. | 1000 | 100–300000. |
| Maximum reconnect wait (ms) | Caps the growing wait. Must be greater than or equal to the minimum. | 30000 | 100–3600000. |
| MQTT 3.1.1: Clean Session | false requests session continuity on the broker with the same Client ID. true requests a clean session. Do not use this option in MQTT 5 mode. | false |  |
| MQTT 5: Clean Start | true starts without reusing the previous session; false requests continuity. It is separate from expiry time after disconnection. | false |  |
| MQTT 5: session expiry (s) | How long the broker keeps the session after disconnect. With Clean Start false it must exceed zero. It does not keep values forever fresh or replace history. | 3600 | 0–4294967295. |
| Maximum message size (bytes) | Memory limit per received payload; larger messages are rejected. Size for the actual document, not the number of TAGs. | 1048576 | 1–67108864. |
| Consecutive connection failure limit | Consecutive failure limit used by readiness supervision. Increasing it does not fix credentials or an unavailable broker. | 5 | 1–1000. |
| Maximum buffered messages | Limits the transport message queue. A larger buffer consumes more memory; it is not archival or a promise to retain everything through an outage. | 4096 | 1–1000000. |

### Read, quality and write

Each TAG uses an exact topic and a type. One document can feed separate TAGs with different Pointers. QoS, retention and freshness serve different purposes; a value can exist and be Stale.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Subscribe topic | Exact topic feeding this TAG, preserving case. Do not use + or # as TAG identity; give separate topics/fields their own TAGs. | plant/pump01/state |  |
| Payload format | utf8Scalar reads one textual value, such as 12.5 or true. json reads JSON and can extract a field with JSON Pointer. The result must match the TAG type. | json |  |
| JSON value field | JSON Pointer path, not Python or JSONPath: /current reads current at the root; /motor/current reads inside motor; /values/0 reads the first item. ~1 represents / in a name; ~0 represents ~. Empty selects the root, which must be a compatible scalar. | /current |  |
| Source timestamp field | JSON field stating when the source measured the value. Use an explicit UTC Z timestamp in the example; measurement time differs from broker arrival. | /timestamp |  |
| Require source timestamp | With true, also configure the timestamp Pointer and publish a valid instant; otherwise the sample is rejected. | true |  |
| Freshness timeout (ms) | Optional: after this interval without a fresher sample the value becomes Stale. It does not request a publication from the device. Allow margin above the normal send period. | 5000 | 1–2147483647 when set. |
| Broker-retained value | staleWithoutSourceTimestamp marks a retained value without source timestamp as Stale. acceptAsCurrent treats it as current; choose only if the application can assume that. A retained message does not prove the equipment is connected now. | staleWithoutSourceTimestamp |  |
| Subscribe QoS | 0: at most one delivery; 1: at least one, possible duplicates; 2: exactly-once MQTT delivery within the session. QoS does not confirm physical actuation or sensor validity. | 1 |  |
| Publish topic | Required for a writable point and must be exact. Use the equipment command topic, separate from feedback where its contract requires. Do not accidentally publish to the measurement topic. |  |  |
| Publish QoS | Requested delivery quality when publishing, using the same 0/1/2 values. Broker acknowledgement is not process feedback. | 1 |  |
| Retain publication | false by default. true asks the broker to retain the last published payload; consider the risk of a new subscriber receiving an old command. Only applies to writable points. | false |  |

### Payload example: motor current

Publish this example on plant/pump01/state on the test broker. For a Double TAG use json format, Pointer /current, timestamp /timestamp and required timestamp. Expected decoded value is 12.5. The instant is illustrative: in a live test use a current timestamp and check quality/age, not just the number. Do not paste this JSON as source configuration.

```json
{"current":12.5,"timestamp":"2026-10-25T12:00:00Z"}
```

### Publication limits and diagnosis

Extracting /current for reads does not create a command-document template. JSON writes with a nonempty field Pointer are rejected: the product does not reconstruct an envelope. For writes use a scalar or root-JSON contract compatible with the equipment, a writable TAG, publish topic and authorization. If connected without values, check exact topic, publication, Pointer, type and timestamp. For Stale check retention, source time and freshness interval; do not mask missing measurements by accepting everything as current.

[Open this guide in installed Help](/help?topic=drivers.mqtt-setup)

## OPC UA: endpoints, certificates and node import

Locate the server, check connection trust and choose a known variable to read.

### Discovery is not activation

Start by locating the OPC UA server and a known variable. In the source, discovery shows endpoints, the connection addresses and modes the server offers. Selecting a candidate fills parameters.

Then check authentication and certificate trust. Selection does not create protected credentials or automatically approve the server. Engineering tests and browsing are temporary: they help prepare the source without changing the active revision.

### Connection and identities

Secure channel and user authentication are separate settings. The client must be accepted by the server, and SCADIVO must recognize the approved server. Ask the administrator for provisioning and references; the Engineering file must not become a secret store.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| OPC UA server endpoint | Complete opc.tcp URL reachable from the SCADIVO server. Discover endpoints and select one the server actually offers. | opc.tcp://192.0.2.20:4840 |  |
| Security mode | None leaves the channel unprotected; Sign signs; SignAndEncrypt signs and encrypts. The mode must exist on the selected endpoint and match the policy; do not downgrade just to hide a certificate failure. | SignAndEncrypt |  |
| Security policy URI | Use the complete URI supplied by the endpoint, not only its short name. None requires a policy ending in #None; a secured mode cannot use that policy. | http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256 |  |
| Approved ApplicationUri | Server application identity when pinned. This is not the connection address. Compare with the administrator-approved identity. |  |  |
| Approved certificate SHA-256 | Secured Runtime requires explicit approval of the server certificate. Verify its fingerprint through a known channel before recording it; a changed certificate requires another check. This is not a private key. |  |  |
| OPC UA server authentication | Anonymous, UserName or Certificate as permitted by the server. Anonymous does not remove channel protection; this is separate from SCADIVO login. | Anonymous |  |
| OPC UA user name | Fill in UserName mode using an account authorized on the source server. |  |  |
| OPC UA password reference | In UserName mode, protected reference to the password provisioned on the host; not the plaintext password. |  |  |
| Client certificate reference | Client application identity for a secured channel. Must be provisioned on the host and accepted by the server. Do not confuse with the user certificate. |  |  |
| User certificate reference | In Certificate mode identifies the user separately from the client application certificate. The administrator supplies the protected reference. |  |  |
| Session timeout | Requested session duration; not a sampling interval. Format hours:minutes:seconds. | 00:01:00 | 00:00:05–01:00:00; default 00:01:00. |
| Publishing interval | Requested notification delivery pace for the subscription. Separate from each node sampling interval; the server can negotiate supported values. | 00:00:01 | 00:00:00.050–00:05:00. |
| Temporary Engineering trust | For a temporary Engineering session only. Does not approve the Runtime certificate: activation with auto-trust is rejected. After inspection record the approved identity and leave this option disabled in operational configuration. | false |  |

### Browse and bind TAGs

In the OPC UA browser select suitable variables, check type/access and review the import preview. Apply the candidate only after checking conflicts. The binding stores node identity; renaming the TAG should not mean selecting another node.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| NodeId | Server node identifier; for example ns=2;s=Pump.Current. It is neither the display name nor the SCADIVO TAG path. Prefer selecting it in the OPC UA browser to avoid typing errors. | ns=2;s=Pump.Current |  |
| Namespace URI | Stable namespace identity. When available it identifies the same node even if its ns= index changes. Use the server URI; do not invent one from the equipment name. | urn:example:plant |  |
| Sampling interval | Requested pace for observing this node. Format hours:minutes:seconds; choose a frequency suitable for the process and check server-negotiated behavior. | 00:00:01 |  |
| Node queue size | Number of samples the item queue can retain between deliveries. Larger uses more resources; it does not replace Historian. | 1 | 1–10000. |
| Discard oldest | When the queue fills, true favors new samples; false requests preserving older samples rather than replacing the oldest. Check the server queue policy. | true |  |

### Identity example, not an import file

This example shows two binding fields: NodeId ns=2;s=Pump.Current and namespaceUri urn:example:plant. Replace with your server values or use the browser. If its namespace index changes from 2 to 4 with the same URI and s=Pump.Current identifier, the stable identity remains the same. Do not use the sample name to guess an existing node.

```json
{"nodeId":"ns=2;s=Pump.Current","namespaceUri":"urn:example:plant"}
```

### Verify and troubleshoot

1. Discover and select compatible endpoint/mode/policy.
2. Verify server identity/certificate and provision client material for its authentication.
3. Test connection and browse to a known variable.
4. Review/apply import of at least one TAG and check the read.
5. Remove temporary trust and confirm approved Runtime configuration.
6. Save/publish/activate through the project workflow and check quality/timestamps. Rejected certificates require trust/identity checks; missing nodes require NodeId/namespace checks; denied access requires account/permission checks; incompatible types require TAG review. Accepted writes do not prove actuation: check equipment feedback.

[Open this guide in installed Help](/help?topic=drivers.opcua-setup)
