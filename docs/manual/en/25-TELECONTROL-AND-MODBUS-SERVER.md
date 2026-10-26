# Telecontrol and Modbus Server

## DNP3: association, events and commands

Configure the outstation and distinguish identity, scanning, representation and confirmation.

### Prepare the association

SCADIVO acts as a DNP3 TCP master/client. Obtain IP/port, link addresses, point list, variations, classes and permitted operations from the outstation owner. Static points can arrive by scan and events can arrive unsolicited. This descriptor advertises neither Engineering Browse nor Test connection; inspect the association and a known point in Runtime/Diagnostics after activation. A missing button does not mean the driver is absent.

### Data Source fields

DNP3 times use hh:mm:ss durations, not other drivers’ milliseconds. Event classes are not alarm priorities. Preserve the remote association profile when adjusting advanced options.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| DNP3 transport | Select tcp, the transport implemented by this profile. Entering a serial port as host does not turn it into serial DNP3. | tcp |  |
| Remote station address | IP or name reachable from the SCADIVO server. The browser does not open the protocol connection. | 192.0.2.10 |  |
| TCP port | Remote service port. Default 20000; check the station configuration and network path. | 20000 | 1–65535. |
| DNP3 master address | Local link-layer address the outstation must recognize as master. This is neither an IP, TCP port nor TAG index. | 1 | 0–65519; match the remote association. |
| DNP3 outstation address | Remote station link-layer address. Even with correct IP/port, an incorrect DNP3 address prevents the expected association. | 10 | 0–65519. |
| Connection timeout | Maximum time to establish TCP; default 5 s. This is not the read interval. | 00:00:05 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| DNP3 response timeout | Protocol response wait, default 5 s. Increasing it does not fix an incorrect link address or point index. | 00:00:05 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Minimum reconnect delay | Initial wait after disconnect, default 1 s. Must not exceed the maximum. | 00:00:01 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Maximum reconnect delay | Upper bound for progressive delay, default 30 s. Old commands are not retained for replay after reconnect. | 00:00:30 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Link supervision | Default 1 minute for link supervision. A live connection does not prove a current point value. Zero is invalid; clearing a field should not automatically be interpreted as disabling the feature. | 00:01:00 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Periodic integrity scan | Default 15 minutes. Rescans data to resynchronize the station image; this is neither Historian retention nor every event’s frequency. | 00:15:00 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Periodic Class 1 scan | Optional, with no default interval: scans events of this class alongside unsolicited traffic. The class comes from outstation configuration, not SCADIVO alarm priority. | 00:01:00 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Periodic Class 2 scan | Optional, with no default interval: scans events of this class alongside unsolicited traffic. The class comes from outstation configuration, not SCADIVO alarm priority. | 00:01:00 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Periodic Class 3 scan | Optional, with no default interval: scans events of this class alongside unsolicited traffic. The class comes from outstation configuration, not SCADIVO alarm priority. | 00:01:00 | Positive duration in hh:mm:ss; 00:00:05 = 5 s. |
| Startup integrity — Class 0 | Includes this class in startup resynchronization; default true. Class 0 is static data; 1–3 are event classes. At least one startup class must remain enabled. | true |  |
| Startup integrity — Class 1 | Includes this class in startup resynchronization; default true. Class 0 is static data; 1–3 are event classes. At least one startup class must remain enabled. | true |  |
| Startup integrity — Class 2 | Includes this class in startup resynchronization; default true. Class 0 is static data; 1–3 are event classes. At least one startup class must remain enabled. | true |  |
| Startup integrity — Class 3 | Includes this class in startup resynchronization; default true. Class 0 is static data; 1–3 are event classes. At least one startup class must remain enabled. | true |  |
| Disable unsolicited Class 1 at startup | Default true: requests disabling unsolicited traffic while the association is prepared. It does not delete outstation events or change their class. | true |  |
| Enable unsolicited Class 1 after integrity | Default true: requests re-enabling after startup integrity. Requires station support/configuration; selecting it does not cause a point to generate events. | true |  |
| Scan available Class 1 events | Default true: scans the class when events are reported available. It neither changes sensor sampling nor replaces the remote event queue. | true |  |
| Disable unsolicited Class 2 at startup | Default true: requests disabling unsolicited traffic while the association is prepared. It does not delete outstation events or change their class. | true |  |
| Enable unsolicited Class 2 after integrity | Default true: requests re-enabling after startup integrity. Requires station support/configuration; selecting it does not cause a point to generate events. | true |  |
| Scan available Class 2 events | Default true: scans the class when events are reported available. It neither changes sensor sampling nor replaces the remote event queue. | true |  |
| Disable unsolicited Class 3 at startup | Default true: requests disabling unsolicited traffic while the association is prepared. It does not delete outstation events or change their class. | true |  |
| Enable unsolicited Class 3 after integrity | Default true: requests re-enabling after startup integrity. Requires station support/configuration; selecting it does not cause a point to generate events. | true |  |
| Scan available Class 3 events | Default true: scans the class when events are reported available. It neither changes sensor sampling nor replaces the remote event queue. | true |  |
| Resynchronize after event-buffer overflow | Default true: requests integrity when the outstation reports event-buffer overflow. It recovers the available current image, not events already lost. | true |  |
| Remote clock synchronization | disabled is the default. lan or nonLan select the outstation-compatible DNP3 method. This can adjust the remote clock; coordinate with the station owner. It neither sets a time zone nor validates sensors automatically. | disabled |  |
| Pending request limit | Default 16: bounds user requests/commands queued for the association. This is not a user-license limit and does not allow replaying old commands after reconnect. | 16 | 1–1024. |

### Point and command fields

In TAGs select the DNP3 source and use the family/index assistant. Check TAG type and apply the binding; advanced fields describe the point contract and may require the project Engineering definition/files because the visible assistant does not expose every advanced profile. Address, family and index must agree.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| DNP3 point address | Format dnp3:kind:index, for example dnp3:analogInput:7. The source identifies the outstation; do not repeat IP or link address in the point. | dnp3:analogInput:7 |  |
| Point family | binaryInput/binaryOutputStatus use Boolean; doubleBitBinaryInput uses Enum; analogInput/analogOutputStatus use a compatible numeric type; counter/frozenCounter use Int32 or Int64. Family and index identify the point, not the message group number. | analogInput |  |
| Index within the family | Outstation index from 0 to 65535; gaps are allowed. analogInput index 7 is not binaryInput index 7. | 7 |  |
| Static representation | Optional GxVy format. Must match family and TAG type: G30V5 is analog Float; G30V6 is Double. Variation 0 requests the device default where supported; it does not guarantee an arbitrary type. | G30V5 |  |
| Event representation | Optional. G32V7 is a timestamped analog Float example. Static and event representations must agree with family/type. Variation does not change point identity. | G32V7 |  |
| Expected event class | Optional class1, class2 or class3 for validation/diagnostics. It neither reclassifies the remote point nor represents SCADIVO alarm priority. | class1 |  |
| Enable commands for this point | Default false. Only binaryOutputStatus/analogOutputStatus accept commands. ReadOnly, permission and command binding still apply; setting true does not turn an input into an output. | false |  |
| Select before operate | selectBeforeOperate is the default; use directOperate only when the station and procedure permit. Direct Operate No Response is not exposed. Protocol acknowledgement does not prove physical actuation. | selectBeforeOperate |  |
| Operation for true | Binary command: latchOn, latchOff, pulseOn or pulseOff. Default latchOn. true is the user-requested value; actual operation depends on this mapping. | latchOn |  |
| Operation for false | Same operation set; default latchOff. Check remote logic before reversing meanings, especially open/close or start/stop. | latchOff |  |
| Trip/Close indication | none, trip or close to match equipment. Default none. This qualifier is neither an interlock nor additional switching authorization. | none |  |
| Operation count | Default 1; count carried in the binary command. This is not the SCADIVO retry count. Agree interpretation with the outstation. | 1 | 1–255. |
| Command on-time | Binary-command duration in hh:mm:ss, default 00:00:00. For pulses use the equipment-specified duration; this is not response timeout. | 00:00:00 |  |
| Command off-time | Complementary binary-command duration, default 00:00:00. This is neither alarm delay nor acquisition interval. | 00:00:00 |  |
| Analog command type | int16, int32, float32 or float64 must match TAG Int16, Int32, Float or Double. Command representation G41 differs from received status representation. | float32 |  |

### Analog reading example

Parameter example, not a complete package. The outstation must publish analog point 7 in the stated representations. Create a read-only Float TAG; after project publication/activation compare value, quality and timestamp origin with a known measurement. Do not invent an expected value just because the association connected.

```json
{
  "source": {
    "transport": "tcp",
    "host": "192.0.2.10",
    "port": "20000",
    "masterAddress": "1",
    "outstationAddress": "10"
  },
  "address": "dnp3:analogInput:7",
  "binding": {
    "pointKind": "analogInput",
    "index": "7",
    "staticVariation": "G30V5",
    "eventVariation": "G32V7",
    "expectedEventClass": "class1",
    "writable": "false"
  },
  "tagDataType": "Float",
  "readOnly": true
}
```

### Binary output example

Defines the command profile of a real output agreed with the outstation. It is not authorization to test switching on active equipment. Runtime must admit the command and the session needs the corresponding capability. Protocol command acceptance and status update are distinct evidence; check the point’s reported feedback.

```json
{
  "source": {
    "transport": "tcp",
    "host": "192.0.2.10",
    "port": "20000",
    "masterAddress": "1",
    "outstationAddress": "10"
  },
  "address": "dnp3:binaryOutputStatus:1",
  "binding": {
    "pointKind": "binaryOutputStatus",
    "index": "1",
    "writable": "true",
    "commandMode": "selectBeforeOperate",
    "binaryTrueOperation": "latchOn",
    "binaryFalseOperation": "latchOff",
    "tripCloseCode": "none",
    "commandCount": "1",
    "commandOnTime": "00:00:00",
    "commandOffTime": "00:00:00"
  },
  "tagDataType": "Boolean",
  "readOnly": false
}
```

### Investigate by symptom

No association: check IP/20000, master/outstation and network from the server. Associated without value: check family/index and startup integrity. Missing events: check class, unsolicited support, scans and remote configuration. Incompatible value: compare variation and TAG type; changing class does not fix type. An event without source time does not gain clock precision from being received now. Rejected command: check output, type, mode, permissions and association state; do not replay automatically after disconnect.

[Open this guide in installed Help](/help?topic=drivers.dnp3-setup)

## IEC 104: stations, observed points and commands

Configure CA/IOA and types without treating observation as a complete point list.

### Preparation and what Browse proves

SCADIVO is an IEC 60870-5-104 client/master. Obtain CA/IOA, types, clock and command lists from the remote station. Test connection proves only the scope stated in its result; Browse performs bounded observation of general interrogation and received traffic, not a guaranteed sweep of the full map. Results are partial; absence does not prove a point does not exist.

### Source and point fields

Source timers use seconds. CA/IOA is point identity; Type ID determines interpretation and command profile is separate. Do not add Type ID to the address to bypass a configuration conflict.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Remote station address | IP or name reachable from the SCADIVO server. The browser does not open the protocol connection. | 192.0.2.10 |  |
| TCP port | Remote service port. Default 2404; check the station configuration and network path. | 2404 | 1–65535. |
| Stations — Common Addresses | Comma-separated decimal CA list, for example 1,2. This is the logical station address, not IP or a point IOA. A TAG’s CA must belong to this list. | 1 | CA 0–65535 matching the remote profile. |
| Station clock time zone | Time-zone identifier recognized by the server for CP56Time2a interpretation. UTC is correct only when the station uses UTC. For local time configure the actual station zone; do not use the browser zone for convenience. Verify the identifier on the Windows/Linux installation. | UTC |  |
| Request originator | Protocol originator identifier; default 0. This is neither a login user nor an Authority capability. Match the station profile. | 0 | 0–255. |
| t0 — connection | Connection-establishment timeout in seconds. Default 30. | 30 | 0.1–3600 s. |
| t1 — transmission supervision | Supervised acknowledgement wait, default 15 s. Must exceed t2. | 15 | 0.1–3600 s. |
| t2 — receive acknowledgement | Delayed acknowledgement limit, default 10 s; must be lower than t1. This is not alarm delay. | 10 | 0.1–3600 s. |
| t3 — idle supervision | Idle-session supervision, default 20 s. This is not measurement expiry. | 20 | 0.1–86400 s. |
| k — send window | Maximum unacknowledged information units. Default 12; not a TAG count. Match the remote profile. | 12 | 1–32767. |
| w — acknowledgement window | Reception acknowledgement threshold, default 8; must not exceed k. It does not increase the available point count. | 8 | 1–32767; w ≤ k. |
| Point CA | Common Address containing the point. Use an address configured in the source; points with the same IOA and different CA are distinct. | 1 | 0–65535. |
| Point IOA | Information Object Address from the station point list. The assistant generates ca=1;ioa=100 for the example; this is not the table row number. | 100 | 0–16777215. |
| Monitored information type | MSpNa1/MSpTb1 use Boolean; MDpNa1/MDpTb1 use Enum; MBoNa1/MBoTb1 use Int32; MMeNb1/MMeTe1 use Int16; MMeNa1/MMeTd1 and MMeNc1/MMeTf1 use Float. A normalized family is not automatically volts or bar. Compatible timestamped/untimestamped types still refer to the same CA/IOA. | MMeNc1 |  |
| Command type | For writable TAGs only: CScNa1 Boolean, CDcNa1 Enum, CSeNa1/CSeNc1 Float or CSeNb1 Int16. Must match the monitored type and remote profile; it does not turn an input into an output. | CSeNc1 |  |
| Command execution mode | direct or sbo to match the station. The assistant starts with sbo when no option is saved; the configuration descriptor declares direct as default. Choose the intended mode explicitly. sbo selects before operating; it does not replace process interlocks. | sbo |  |
| Command qualifier | Value 0–31, default 0, matching remote operation. This is neither alarm priority nor a user role. | 0 |  |

### First reading of a known point

1. Create/save the source and test the server-side connection.
2. Use Browse to check observed points, or the station point list for manual authoring. Review candidates before applying.
3. In TAGs select the source and open the IEC 104 assistant; enter CA, IOA and a compatible monitored type. Start with a read-only TAG.
4. Publish/activate through the project workflow and inspect value, quality and time in TAG Monitor. The station must actually supply the example point; parameters do not generate a fictitious measurement.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "2404",
    "commonAddresses": "1",
    "stationTimeZone": "UTC"
  },
  "address": "ca=1;ioa=100",
  "binding": {
    "iec104.typeId": "MMeNc1"
  },
  "tagDataType": "Float",
  "readOnly": true
}
```

### Compatible command profile

This example configures the same point as a Float setpoint using select before operate. Use it only if the station defines that CA/IOA for the command. Permission, Runtime admission, protocol confirmation and process feedback are distinct checks. Do not enable writing just to remove an incompatible-type message.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "2404",
    "commonAddresses": "1",
    "stationTimeZone": "UTC"
  },
  "address": "ca=1;ioa=100",
  "binding": {
    "iec104.typeId": "MMeNc1",
    "iec104.commandTypeId": "CSeNc1",
    "iec104.commandMode": "sbo",
    "iec104.qualifier": "0"
  },
  "tagDataType": "Float",
  "readOnly": false
}
```

### Layered diagnosis

No session: check IP/2404 and APCI parameters, including t2 < t1 and w ≤ k. Session without points: check requested CA, general-interrogation completion, observation window and station point list. Incompatible value: check IOA and Type ID family; normalized, scaled integer and Float are not interchangeable. Shifted time: check station clock, configured zone and CP56Time2a validity; changing only the browser clock does not correct data. Rejected/ambiguous command: check type, mode, qualifier and session state; timeout does not prove the station did not operate.

[Open this guide in installed Help](/help?topic=drivers.iec104-setup)

## Modbus Server: publish TAGs over TCP or RTU

Build a bounded map and distinguish external clients, internal writes and current values.

### Server differs from master

In Server mode SCADIVO serves another master/client and publishes mapped TAGs in Holding Registers. This source does not poll a remote PLC. To publish a value from another driver create a destination Server TAG and a compatible Gateway route; a TAG does not acquire two sources. TCP listens on an interface/port; RTU responds through the server’s serial device.

### Source and map fields

Network fields belong to TCP; line fields belong to RTU. Ranges and bindings define the same map model. The range editor does not automatically convert other tools’ 40001 reference labels.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| TCP server local interface | Local listening address, not a remote PLC IP. Default 0.0.0.0 listens on IPv4 interfaces; 127.0.0.1 restricts to the host. In Docker also check published ports and container networking. Inside a container, 127.0.0.1 is the container loopback, not the host computer loopback. | 127.0.0.1 |  |
| TCP listening port | Default 502. The example uses 1502 for a test installation; the external client must use the same reachable port. Another source/process cannot occupy the same endpoint. | 1502 | 1–65535. |
| Served Unit ID | Identifier accepted by the server, default 1. TCP accepts 0–247; RTU uses 1–247. Binding Unit ID must match the source. This is not an IP address. | 1 |  |
| Holding Register ranges | Use the structured start/end editor. Inclusive zero-based ranges without overlap: v1:0-9 represents ten registers. Every register of a value must fit in one range; a range does not create TAGs automatically. | v1:0-9 | Up to 32 ranges and 16384 total registers; addresses 0–65535. |
| Concurrent TCP clients | TCP only: maximum connections, default 16. This is neither licensed View count nor user authorization. | 16 | 1–128. |
| TCP client idle timeout | TCP only: maximum wait for the next request, default 30000 ms. This is not TAG update rate. | 30000 | 1000–600000 ms. |
| Server RTU port | RTU only: device accessible to the service on Windows/Linux/container, such as COM4 or /dev/ttyUSB0. It is exclusive while the server is active; do not share it with a master controlling the same physical port. | /dev/ttyUSB0 |  |
| RTU line speed | Bits per second matching the master and other devices. Default 9600; not server polling. | 9600 | 300–4000000. |
| RTU data bits | Default 8; match the actual line. The field range does not guarantee every combination is accepted by the adapter/host. | 8 | 5–8; check the installation. |
| RTU parity | None, Odd, Even, Mark or Space; default None. Must match the master and port support. | None |  |
| RTU stop bits | One, OnePointFive or Two; default One. Use a combination valid for the line and host, not the register count. | One |  |
| RTU frame completion timeout | Default 1000 ms to receive the rest of a frame after traffic starts. This is not measurement interval. | 1000 | 50–10000 ms. |
| TAG address in the map | Use zero-based holding:N in this server. Tools using reference labels often call holding:0 40001; verify the client convention. Coils/Input Registers are outside this profile. | holding:0 |  |
| Binding Unit ID | Optional: uses the source when omitted. In Server mode an override must equal the served Unit ID; it does not create multiple stations in one map. | 1 |  |
| Register value type | Boolean/Int16/UInt16 use one register; Int32/UInt32/Float32 use two; Int64/UInt64/Float64 use four. Physical type, TAG type and width must be compatible. The example uses physical Int16 and an Int16 TAG without conversion. | Int16 |  |
| Word order | HighWordFirst is default; LowWordFirst reverses words of multiregister values. Match the client. A one-word Int16 has no two words to reverse. | HighWordFirst |  |
| Value multiplier | Default 1: engineering value = raw × scale + offset. Must be finite and nonzero. Publishing to registers uses the inverse conversion; do not apply the same scale again in the client. | 1 |  |
| Value offset | Amount added after multiplication, default 0. This is not an address offset and does not change holding:0. | 0 |  |
| External client writes | ReadOnly is default; ReadWrite enables external writes to this mapped TAG. It is separate from ReadOnly governing internal SCADIVO writes. Modbus does not request a SCADIVO login on each request; plan network access and the permitted map before enabling ReadWrite. | ReadOnly |  |

### Publish and verify a read

1. Create the TCP or RTU Server source and configure interface/line, Unit ID and ranges.
2. TCP Test connection temporarily reserves and releases the local interface/port; RTU tests opening/exclusive line reservation and releases it. Neither proves a remote client read nor leaves the server active.
3. In TAGs select the Server source and Modbus assistant. Choose Holding Register, zero-based address and type; start with external access ReadOnly. Allow internal TAG writes if it receives Gateway data or authorized internal writes.
4. Publish/activate. Feed a known value into the TAG and verify its update in Monitor. In the external client read the corresponding Unit ID/address; check type and order.

```json
{
  "source": {
    "bindAddress": "127.0.0.1",
    "port": "1502",
    "unitId": "1",
    "holdingRanges": "v1:0-9"
  },
  "address": "holding:0",
  "metadata": {
    "modbus.valueType": "Int16",
    "modbus.wordOrder": "HighWordFirst",
    "modbus.scale": "1",
    "modbus.offset": "0",
    "modbus.server.clientAccess": "ReadOnly"
  },
  "tagDataType": "Int16",
  "readOnly": false,
  "internalValue": 123
}
```

### Expected example and restart behavior

After an authorized internal write of 123, the client should read 123 at holding:0, Unit ID 1, port 1502. A value does not appear automatically because it is in the example. The in-memory map starts at zero: reading zero does not prove the TAG received a current sample, and registers do not automatically carry TAG quality/time. After restart check map feeding; this is neither Historian nor guaranteed retentive memory.

### External writes and diagnosis

An allowed range does not permit arbitrary writes: every written register must belong to an externally ReadWrite mapped TAG. The server rejects partial multiregister values, unmapped gaps, overlapping TAGs and out-of-range spans. FC03 reads allow 1–125 registers; FC06 writes one register and FC16 writes 1–123 registers; an operation must preserve each complete value. No listener: check port conflicts, interface, firewall and Docker. Incorrect reads: check Unit ID, zero base, type/order and actual fed value. Rejected internal write: check ReadOnly/permission; rejected external write: check clientAccess and complete map.

[Open this guide in installed Help](/help?topic=drivers.modbus-server-setup)
