# PLCs: connections, symbols and memory

## Siemens S7: CPU, memory and first reading

Configure ISO-on-TCP while distinguishing connection, DB access and write permission.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-S7: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.siemens-setup; fields=communicationBinding,valueOrder; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Prepare the PLC side

Use the correct CPU and PLC project. This driver accesses absolute memory through S7 ISO-on-TCP; it is neither OPC UA nor full TIA symbolic browsing. For S7-1200/1500, have the PLC owner check permitted external access, PUT/GET where required and an absolute-compatible DB layout without optimization changing expected offsets. SCADIVO does not change these settings or replace installation protection.

### Data Source fields

Choose RackSlot or explicit TSAP to match the installation. A connection test negotiates a session; DB and individual point access still require verification.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| PLC address | IP or name reachable from the SCADIVO server. This is neither the browser address nor the project address. | 192.0.2.10 |  |
| TCP port | Port of the enabled PLC or adapter service. This driver defaults to 102. Check the firewall and actual service before changing it. | 102 | 1–65535. |
| CPU family | Select S7300, S7400, S71200 or S71500 to match the installed CPU. Unknown does not define an operational profile. This does not enable PLC services or permissions automatically. | S71200 |  |
| How to locate the connection | RackSlot derives the destination from rack, slot and role. ExplicitTsap uses the destination TSAP from the installation configuration. Choose one mode and fill its corresponding fields. | RackSlot |  |
| Rack | In RackSlot mode, the rack containing the CPU. This is not a project equipment number. | 0 | 0–7; required in RackSlot. |
| CPU slot | In RackSlot mode, actual CPU position. Example 1 does not fit every family; check the configured hardware. | 1 | 0–31. |
| Connection role | In RackSlot: OperatorPanel, ProgrammingDevice or Basic, matching the PLC connection. This is not an Authority user role. | OperatorPanel |  |
| Enable Data Source writes | Defaults to false. Writes also require a writable binding, non-ReadOnly TAG, writable destination and permission. This does not enable PUT/GET or change CPU protection. | false |  |
| Source TSAP | Client-side identifier in the ISO session. Default 0x0100. This is not a TCP port; keep the communication configuration value. | 0x0100 |  |
| Destination TSAP | Required in ExplicitTsap mode. Use the identifier configured for the PLC connection; check it before copying the example. | 0x0201 |  |
| Connection timeout | Milliseconds allowed to establish the connection. | 5000 | 1–300000 ms. |
| Response timeout | Milliseconds allowed for a request response. Increasing this does not fix an incorrect address, route or type. | 3000 | 1–300000 ms. |
| Reconnect delay | Milliseconds before retrying a failed connection. This is not the measurement interval; zero does not fix access denial. | 1000 | 0–300000 ms. |
| Requested message size | PDU bytes requested during S7 negotiation. Default 480; accepted size depends on the PLC. This is neither the TAG count nor database size. | 480 | 240–960 bytes. |

### TAG binding fields

The SCADIVO TAG path identifies the application variable; area, DB, byte and bit locate PLC memory. Portable address and binding fields must agree.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Memory area | Input: inputs; Output: outputs; Merker: M memory; DataBlock: DB. Match the actual program address. Reading inputs does not make that area writable. | DataBlock |  |
| DB number | For DataBlock, nonzero block number. Outside this area use zero. The number is not a byte offset. | 1 | 1–65535 for DB; zero in other areas. |
| Starting byte offset | Zero-based position within the area/DB. DB1.DBD0 starts at byte 0; Float32 occupies four bytes. Do not enter 32 by confusing bits with bytes. | 0 | 0–2097151; the entire value must fit. |
| Boolean bit | Only Boolean uses bit 0–7 within the byte. Keep zero for other types; do not replace this field with the generic bit selector. | 0 |  |
| Physical PLC type | Choose actual storage: Boolean, Byte, SInt, UInt16, Int16, UInt32, Int32, Int64, Float32, Float64, String, WString, Date or DateTime. Float32 maps to TAG Float, Float64 to Double, text to String, dates to DateTime. A TAG name does not convert memory contents. | Float32 |  |
| Text capacity | String/WString only: declared capacity of 1–254 characters; this is not numeric read size. Use zero for other types. Allow actual DB space for the format header. | 0 |  |
| Allow writes to this point | Start with false while checking reads. true only enables a point that is also writable in memory/protocol; TAG ReadOnly, session permissions and other restrictions still apply. | false |  |
| Byte and word order | The descriptor still declares Normal, ByteSwap, WordSwap and ByteAndWordSwap, but the current binding does not accept this setting or order in the address. Ordering is represented by the shared physical byte/word transform. This mismatch is part of the pending authoring correction; do not copy the catalog field into the binding as a manual solution. | Normal |  |
| Binding authoring in this version | The current catalog gives the generic assistant a binding identity different from the one required by the S7 Runtime. Creation through this path is rejected and awaits correction. Other fields and the example describe the correct S7 binding, not an already corrected authoring flow. Do not change internal IDs to bypass validation. The valueOrder field must also be projected to the physical transform rather than saved as an extra setting. |  |  |

### From Data Source to a checked reading

Manual creation through the current assistant encounters the binding limitation described above. The example demonstrates valid binding parameters for review; it does not instruct you to force an internal identity in the UI. For a project with an already valid S7 binding, verify the source, a known reading and quality after publishing/activating through the project workflow. A successful connection test does not establish tested reads or writes.

### Example: REAL at the start of DB1

Configuration example, not a complete package. The PLC program must actually store a REAL at DB1.DBD0. Expected result is a Float reading of that value with writes disabled, not a test value generated by the driver.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "102",
    "cpuFamily": "S71200",
    "connectionMode": "RackSlot",
    "rack": "0",
    "slot": "1",
    "connectionRole": "OperatorPanel",
    "writeEnabled": "false"
  },
  "address": "s7iso:v1;area=DataBlock;db=1;byte=0;bit=0;type=Float32;string=0;writable=false",
  "binding": {
    "area": "DataBlock",
    "dbNumber": "1",
    "byteOffset": "0",
    "bitOffset": "0",
    "valueType": "Float32",
    "stringLength": "0",
    "writable": "false"
  },
  "tagDataType": "Float"
}
```

### TIA import and diagnosis

Advertised import accepts compatible PLC TAG exports in XLSX, XML and SDF; this is not a full TIA project import or TIA Openness. Review candidates, types and addresses before applying. No connection: check IP, port 102, mode, rack/slot/TSAP and permitted access. Connected without readings: check DB number, absolute layout, offset and size. Implausible value: compare type, bytes and order with the program. Rejected write: check writeEnabled, writable, ReadOnly and permission; do not relax PLC protection as a test.

[Open this guide in installed Help](/help?topic=drivers.siemens-setup)

## Allen-Bradley Logix: routing, symbols and reads

Use ControlLogix/CompactLogix symbolic access and check limits before importing or writing.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-LOGIX: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.logix-setup; fields=communicationBinding,bitIndex; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Which connection you are configuring

This driver uses explicit EtherNet/IP messages for ControlLogix/CompactLogix symbols. It is not cyclic I/O scanning for every EtherNet/IP device or a generic SLC/PLC-5/MicroLogix driver. Obtain the IP, profile, actual route and symbol external-access settings.

### Data Source fields

A CIP route differs from the TCP port. Identifying the controller proves neither a symbol read nor a write.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| PLC address | IP or name reachable from the SCADIVO server. This is neither the browser address nor the project address. | 192.0.2.10 |  |
| TCP port | Port of the enabled PLC or adapter service. This driver defaults to 44818. Check the firewall and actual service before changing it. | 44818 | 1–65535. |
| Controller profile | ControlLogix or CompactLogix to match the equipment. The profile does not automatically discover a slot or configure the PLC. | CompactLogix |  |
| CIP route to the CPU | Ordered port,link hops separated by /. 1,0 represents a particular hop, not TCP port 1. Use the documented route; a direct destination may use an empty route according to profile. Do not guess additional hops. | 1,0 |  |
| Read interval | Requested milliseconds between acquisition cycles. 1000 means approximately one cycle per second, subject to network and processing time. This does not guarantee the PLC program update rate. | 1000 | 50–3600000 ms. |
| Response timeout | Milliseconds allowed for a request response. Increasing this does not fix an incorrect address, route or type. | 3000 | 1–60000 ms. |
| Minimum reconnect delay | Initial wait after a connection failure, in milliseconds. Must not exceed the maximum delay. | 500 | 1–60000 ms. |
| Maximum reconnect delay | Upper bound for progressive reconnect delay, in milliseconds. This is neither data validity nor command confirmation time. | 15000 | 1–300000 ms. |
| Points per batch | Limits request grouping. Default 16; increasing it can raise load and message size. It does not change the licensed TAG count. | 16 | 1–64. |
| Transport security requirement | Unsecured is the available implementation. CipSecurityRequired is an explicit requirement rejected by this version; it neither enables CIP Security nor silently downgrades. If the installation requires CIP Security, this path does not meet that requirement. | Unsecured |  |

### Symbol binding fields

Identity includes scope, program where applicable, path and native type. Do not copy a portable address while leaving fields aimed at a different symbol. Bool is readable; native Bool writing is not enabled in this version.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Symbol scope | Controller for a global symbol; Program for a program-local symbol with programName. This is not an Authority authorization scope. | Controller |  |
| PLC program | For Program scope, enter the actual program name. This is neither the SCADIVO application name nor the routine name. |  |  |
| Symbol path | Actual name and supported path, including member/index where applicable. Example TankLevel must exist in the PLC as REAL. Renaming the SCADIVO TAG does not rename the symbol. | TankLevel |  |
| Native symbol type | Runtime reads in this version: Bool, Sint, Int, Dint, Lint and Real, mapping to Boolean, Int16, Int16, Int32, Int64 and Float. Lreal/String may appear in the catalog/import but are not enabled for Runtime reading. Whole structures are not scalars. | Real |  |
| External access defined in the PLC | ReadOnly permits reading; ReadWrite also permits compatible writing; None blocks access. Unknown does not prove write permission. This field describes the symbol; it does not change the PLC setting. | ReadOnly |  |
| Constant symbol | true identifies a PLC constant and prevents writing. Do not use false to try making a constant writable. | false |  |
| Physical integer bit | Optional Boolean projection from Sint/Int/Dint/Lint: maximum indices 7/15/31/63. Not applicable to Real or Bool. It does not turn a write into a safe bit operation; check support before issuing commands. An accepted binding uses the shared physical selector, not an extra bitIndex setting; assistant projection is also pending. |  |  |
| Binding authoring in this version | Runtime stores symbolic identity in the portable address and requires empty extra settings. The current generic assistant copies the fields in this table into extra settings and its binding is rejected. Creation through this path awaits correction; the example shows a valid point contract, not a corrected assistant. |  |  |

### From Data Source to a checked reading

Manual creation through the current assistant encounters the binding limitation described above. The example shows a valid read identity for review. For a project with an already valid Logix binding, verify source, symbol, type and quality after publishing/activating. Advertised Browse/import/reconciliation services require candidate and binding review before applying; opening a file does not establish a successful import.

### Example: read-only REAL symbol

TankLevel must exist at Controller scope as REAL. Use a Float, ReadOnly TAG for the first check. The example does not configure a rack route for you.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "44818",
    "profile": "CompactLogix",
    "securityMode": "Unsecured",
    "scanIntervalMs": "1000"
  },
  "address": "logix:v1;scope=controller;symbol=TankLevel;native=real;access=readonly;constant=false",
  "binding": {},
  "tagDataType": "Float"
}
```

### Browse, import and reconciliation

Online Browse enumerates Controller-level symbols with pagination; it does not promise full program browsing. Compatible L5X/L5K import can include program scope. Review supported candidates, arrays, members and External Access; a visible symbol can be incompatible. Reconcile compares identities; it does not automatically modify the PLC program. For connection failure check IP/44818 and route. For missing symbols check scope, program and name. For rejected writes check supported type, constant, External Access, ReadOnly and permission.

[Open this guide in installed Help](/help?topic=drivers.logix-setup)

## Mitsubishi MELSEC: profile, routing and devices

Configure MC/SLMP and check numeric radix, value width and CPU limits.

### Prepare the MC service

Have a compatible MC/SLMP TCP service and port enabled in the PLC. This profile uses binary 3E frames; it does not assume every model, ASCII, UDP or all Mitsubishi memory is supported. A familiar driver name does not replace the equipment profile.

### Data Source fields

Start with basic fields. Advanced routing and grouping must match the installation; defaults do not discover the topology automatically.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| PLC address | IP or name reachable from the SCADIVO server. This is neither the browser address nor the project address. | 192.0.2.10 |  |
| TCP port | Port of the enabled PLC or adapter service. This driver defaults to 5007. Check the firewall and actual service before changing it. | 5007 | 1–65535. |
| Mitsubishi family profile | Fx5U32MtDs or IqR04EnCpu: specific memory limits, not certification of all MELSEC CPUs. If the model differs, check compatibility before use. | Fx5U32MtDs |  |
| Read interval | Requested milliseconds between acquisition cycles. 1000 means approximately one cycle per second, subject to network and processing time. This does not guarantee the PLC program update rate. | 1000 | 10–600000 ms. |
| Connection timeout | Milliseconds allowed to establish the connection. | 5000 | 100–60000 ms. |
| Response timeout | Milliseconds allowed for a request response. Increasing this does not fix an incorrect address, route or type. | 6000 | 250–60000 ms. |
| MC monitoring timer | Time requested in the MC frame, in 250 ms units. Default 20 equals 5 s. This differs from the local response timeout, which must allow for that time and the network. | 20 | 1–65535 units. |
| MC network number | Installation MC routing; default 0 for the directly configured destination. This is not the computer Ethernet-interface number. | 0 | 0–239. |
| MC destination station | Frame station field. Default 255 (FF). Use the MC route value, not a Modbus Unit ID. | 255 | 1–255. |
| Module I/O number | Hexadecimal routing field, default 03FF. This is not an output Y address; preserve the installation value. | 03FF |  |
| Multidrop station | Additional MC routing field; default 0. Match the actual route without confusing it with IP or the frame station. | 0 | 0–31. |
| Words per request | Word grouping limit, default 64. A 32-bit value occupies two words; do not confuse words with TAG count. | 64 | 2–960. |
| Bits per request | Bit-device read limit, default 64. Adjust according to PLC capacity and measured load. | 64 | 1–7168. |
| Sparse points per request | Limits grouping of noncontiguous addresses, default 16. It neither creates missing addresses nor changes the memory map. | 16 | 1–192. |
| Blocks per request | Limits grouped blocks, default 8. More blocks do not guarantee faster reads on saturated networks or CPUs. | 8 | 1–120. |
| Explicit R address limit | Optional. With no value, R is disabled. Enter the maximum validated for the installed memory; the current parser still caps R at 32767. The broad field maximum does not increase that profile limit. |  |  |

### Memory and binding fields

Bit and word devices use different storage. Check this driver’s format even if another application writes the address differently.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Device address | X/Y/B/W use hexadecimal numbers; M/L/D/R use decimal. D100 is a decimal word address; X10 is hexadecimal 10, not another application’s octal convention. The profile limits the allowed range. | D100 |  |
| Physical type | Bit for bit areas; Int16, UInt16, Int32, UInt32 or Float32 for word areas. Int32/UInt32/Float32 occupy two consecutive words. Choose a compatible TAG and preserve PLC order; UInt32 uses TAG Int64. In SCADIVO, physical Int16/UInt16 use TAG Int32; physical Int32/UInt32 use Int64; Float32 uses Float; Bit uses Boolean. | Int16 |  |
| Allow writes to this point | Start with false while checking reads. true only enables a point that is also writable in memory/protocol; TAG ReadOnly, session permissions and other restrictions still apply. | false |  |

### From Data Source to a checked reading

1. In Engineering > Data Sources, create the appropriate source and enter installation parameters.
2. Save and use Test connection when available. A successful connection does not prove all points.
3. In TAGs, select this source, choose a compatible type and fill the address and binding through the assistant. For manual authoring, address and fields must describe the same point; examples are not complete import packages.
4. Check a known reading using the point test when available, or after publishing/activating through the project workflow. Inspect value, quality and update in TAG Monitor.
5. Then configure a screen, alarm or authorized write. Compare the result with the PLC program and actual equipment condition.

### Example: integer at D100

D100 must contain a signed 16-bit integer in the PLC. Value 123 should appear as 123 in the Int32 TAG; check the raw value before applying unit conversion.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "5007",
    "familyProfile": "Fx5U32MtDs",
    "scanIntervalMilliseconds": "1000"
  },
  "address": "D100",
  "binding": {
    "address": "D100",
    "physicalDataType": "Int16",
    "writable": "false"
  },
  "tagDataType": "Int32"
}
```

### Targeted diagnosis

No connection: check TCP service, port and MC routing. Invalid address: check area, decimal/hexadecimal and family limit; R needs an explicit limit. Misinterpreted value: check type and occupied words. Slow requests: compare the 250 ms MC timer units, local timeout, batches and load; do not increase everything together. A point test is not full Browse and does not certify writes or physical actuation.

[Open this guide in installed Help](/help?topic=drivers.melsec-setup)

## Panasonic MEWTOCOL: TCP or serial and memory map

Choose the actual transport and interpret words and contacts without mixing other protocols.

### Choose profile and transport

MEWTOCOL-COM TCP accesses a compatible TCP service or adapter; Host Serial uses the physical port visible on the server. These are distinct Data Sources. For serial, check cabling, RS-232/RS-485 and line settings. The driver does not turn a generic Ethernet converter into a server for every Panasonic protocol.

### Source and binding fields

For TCP fill host/port/connection timeout; for serial fill the port and line framing. Station, family, frame mode and acquisition apply to both. The final three fields describe the point.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| PLC address | IP or name reachable from the SCADIVO server. This is neither the browser address nor the project address. | 192.0.2.10 |  |
| Adapter TCP port | Required for TCP, with no assumed default port. Use the PLC/adapter MEWTOCOL-COM port; do not copy the Mitsubishi MC port. | 9094 | 1–65535; example, not a default. |
| Connection timeout | Milliseconds allowed to establish the connection. | 5000 | 100–60000 ms. |
| Server serial port | Host Serial only: device accessible to the SCADIVO service, such as COM4 on Windows or /dev/ttyUSB0 on Linux. In Docker, device access and permissions belong to the container/host, not the browser. | /dev/ttyUSB0 |  |
| Serial speed | Serial only: bits per second matching the PLC. Default 9600; this is not the read interval. | 9600 | 300–230400. |
| Data bits | Serial only: 7 or 8 to match the installation. Default 8. Together with parity and stop bits this defines serial framing. | 8 |  |
| Parity | Serial only: None, Odd or Even, matching the PLC. Default None. Incorrect parity can produce a timeout without an intelligible response. | None |  |
| Stop bits | Serial only: One or Two to match the PLC. Default One; do not use another driver’s values outside this list. | One |  |
| Turnaround delay | Serial only: pause in milliseconds for the line/adapter after a transaction. Default 0; adjust to actual needs, not to fix an incorrect station. | 0 | 0–1000 ms. |
| MEWTOCOL station | Configured PLC station, required from 1 to 99. This is neither rack slot, IP nor a Unit ID inherited from another protocol. | 1 |  |
| Panasonic family profile | Fp0rF32, FpXhCommon or Fp7RClassicCom. Each limits areas, addresses and writes. FP7 here is the classic COM subset, not all FP7 memory/features. | Fp0rF32 |  |
| Frame mode | Standard is the default. Expanded increases capacity only in profiles that permit it; Fp0rF32 rejects it. It neither enables MEWTOCOL7 nor extends memory areas. | Standard |  |
| Read interval | Requested milliseconds between acquisition cycles. 1000 means approximately one cycle per second, subject to network and processing time. This does not guarantee the PLC program update rate. | 1000 | 10–600000 ms. |
| Response timeout | Milliseconds allowed for a request response. Increasing this does not fix an incorrect address, route or type. | 3000 | 250–60000 ms. |
| Panasonic address | Use an area and number admitted by the profile: contacts X/Y/R/L/T/C or words WX/WY/WR/WL/DT/LD. DT100 is a decimal word; for X/Y/R/L contacts the final digit is hexadecimal bit 0–F within a decimal group. Do not reinterpret the entire address as hexadecimal. | DT100 |  |
| Physical type | Boolean for a contact; UInt16 or Int16 for a word, with matching TAG type. This driver does not expose a 32-bit REAL or string as though it were one 16-bit word. In this driver both physical Int16 and UInt16 use TAG Int32. Boolean uses TAG Boolean. | Int16 |  |
| Allow writes to this point | Start with false while checking reads. true only enables a point that is also writable in memory/protocol; TAG ReadOnly, session permissions and other restrictions still apply. | false |  |

### From Data Source to a checked reading

1. In Engineering > Data Sources, create the appropriate source and enter installation parameters.
2. Save and use Test connection when available. A successful connection does not prove all points.
3. In TAGs, select this source, choose a compatible type and fill the address and binding through the assistant. For manual authoring, address and fields must describe the same point; examples are not complete import packages.
4. Check a known reading using the point test when available, or after publishing/activating through the project workflow. Inspect value, quality and update in TAG Monitor.
5. Then configure a screen, alarm or authorized write. Compare the result with the PLC program and actual equipment condition.

### Example: DT100 word over TCP

Port 9094 is only an example of a configured adapter, not a universal Panasonic default. DT100 must contain a signed 16-bit integer; the Int32 TAG should display the same number. For Host Serial replace network fields with installation serial settings.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "9094",
    "station": "1",
    "familyProfile": "Fp0rF32",
    "frameMode": "Standard",
    "scanIntervalMilliseconds": "1000"
  },
  "address": "DT100",
  "binding": {
    "address": "DT100",
    "physicalDataType": "Int16",
    "writable": "false"
  },
  "tagDataType": "Int32"
}
```

### Diagnose without guessing memory-map changes

No TCP response: check service port and station. No serial response: check server device access, wiring, baud/parity/stop bits and station. Address error: compare area and profile limit, including the contact digit. Incorrect value: check Int16 versus UInt16 and word versus contact. Writes rejected in X/T/C or another protected area cannot be resolved just by changing ReadOnly. Connection and point tests do not prove all memory or physical actuation.

[Open this guide in installed Help](/help?topic=drivers.panasonic-setup)
