# Home/Building: devices and connections

## Home/Building: incorporate a device

Turn discovery into Equipment, TAGs and commands while checking what will actually be created.

### Source, device and Location differ

Incorporating turns a discovered device into project resources. Data Source says how to communicate. Equipment identifies the device and Location says where it is.

Capabilities such as OnOff, Temperature or Cover describe available functions: on/off, temperature or cover control, according to the provider. They are not security permissions. Finding and incorporating a device does not authorize your account to command it; Authority policy still decides access.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Integration source | Source already configured in Data Sources. Its type must offer discovery to appear in this workflow; an empty list does not prove the entire network is empty. |  |  |
| Discovered device | Select a candidate and inspect TAGs, commands, capabilities and warnings. Discovered does not mean incorporated; this form cannot incorporate candidates without materialization. |  |  |
| Device name | Readable name for the Equipment to create. Does not rename the physical device or its source identity. | Luz da sala |  |
| Device path | Organizes Equipment in the project. Check conflicts in preview; it is not an IP, NodeId or topic. | Home/Sala/Luz |  |
| Location | Optional: select an existing Location for the device. Create Locations in their area before association. This does not grant access permission. |  |  |

### From discovery to active revision

1. Configure and apply the source in Engineering → Data Sources with protected material available on the server.
2. Open Home/Building → Devices, select the source and discover candidates where supported.
3. Choose a device and read warnings plus TAG/command/capability lists.
4. Set name, path and Location and check preview; configuration has not changed yet.
5. Apply only a valid preview; if Workspace changes, generate a new preview before applying.
6. Save/publish/activate through the project workflow and check state, quality, unit and authorized commands.

### No candidate or missing function

Check saved source, server-side connectivity and driver-advertised features. KNX/IP Secure advertises no Engineering discovery; Home Assistant has the selection limitation in its guide. An unmapped function is not delivered: inspect warnings and actual generated fields. A gateway/bridge can expose a device without SCADIVO having its native protocol. Do not repeatedly import to repair network or permissions.

[Open this guide in installed Help](/help?topic=home-building.device-workflow)

## BACnet/IP: device, objects and properties

Configure the UDP network and select object identity; names and network addresses do not replace the binding.

### Before connecting

Start with a known read-only BACnet object, such as an analog input. Ask the administrator for device instance and network details: route, IP and port.

This driver uses BACnet/IP over UDP. The SCADIVO server must reach that network; broadcast discovery depends on installation topology. In Docker, also check networking and UDP forwarding. Browser access does not establish these conditions. This driver does not advertise BACnet Secure Connect.

### Connection and binding fields

Binding identity contains device, object type/instance and property with optional index. COV and priority configuration do not change that identity.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Device instance | BACnet device identity, not its IP or an object instance. The TAG binding device instance must match the source. Check it on the controller or through discovery. | 1201 | 0–4194302. |
| Manual target address | Optional: device IPv4 with UDP port. Useful for a known target; does not replace deviceInstance identity. | 192.0.2.20:47808 |  |
| Local UDP port | Port where the SCADIVO process listens for BACnet/IP. Another application/container may already use it; check firewall and UDP exposure on the server. | 47808 | 1–65535. |
| Polling interval (ms) | Requested polling pace for polled points. It is not a guaranteed COV-event frequency. | 1000 | 50–600000. |
| BACnet timeout (ms) | Wait for a BACnet request. Separate from discovery window and polling interval. | 3000 | 100–60000. |
| Discovery window (ms) | How long to collect discovery responses. Increasing may help a slow network but does not make broadcasts cross a blocked subnet. | 1500 | 100–30000. |
| BBMD address | Optional: administrator-configured BACnet distribution point for the network scenario. Do not invent a BBMD for a simple local network. |  |  |
| Foreign Device registration lifetime (s) | Optional: registration duration at the BBMD. Requires bbmdAddress; not TAG freshness or history retention. | 600 | 30–32767. |
| Object type | Numeric BACnet object code. In the example, 0 identifies Analog Input; use the browser/vendor map type, not the TAG Double type. | 0 |  |
| Object instance | Object number within the device. Analog Input 37 differs from another object type with instance 37. | 37 | 0–4194303. |
| Property | Code of the property to read. Example 85 is Present Value. Name, description and value are different properties; prefer browser selection to avoid incorrect codes. | 85 |  |
| Array index | Fill only for an array property when a specific element is needed. Empty preserves the whole property; do not add an index to a scalar value. |  |  |
| Use COV when available | Requests change notifications rather than relying solely on polling. Depends on the server/object and the driver retains read checks; no change does not imply lost connection. | true |  |
| BACnet write priority | Optional for a writable object accepting priority. Not an alarm priority or SCADIVO permission. An accepted write may not change the effective value if another command takes precedence; check feedback and equipment policy. | 8 | 1–16. |

### Example: analog input value

The example identifies Present Value of Analog Input 37 on device 1201. It describes a binding, not a complete import package. Use discovery/browse and check type/unit before activation; replace identities with those of your equipment.

```json
{"deviceInstance":1201,"objectType":0,"objectInstance":37,"propertyIdentifier":85,"useCov":true}
```

### Verification and diagnosis

Test connection, discover/browse objects and review preview before applying; activate through the project workflow. No response: check UDP, instance, target and topology/BBMD. Found object but invalid value: check property, index and type. Unsupported COV calls for reviewing notifications and read checks; lack of change is not zero. Rejected writes require checking access, writable object and priority as well as SCADIVO authorization.

[Open this guide in installed Help](/help?topic=drivers.bacnet-setup)

## KNX/IP Secure: tunnel, DPT and confirmed state

Use KNX project addresses and keep commands separate from feedback.

<!-- HELP-REVIEW-AFTER-PRODUCT-786: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.knx-setup; fields=writeGroupAddress; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Requirements and scope

Use the KNX project details: addresses, gateway, each point’s type and security material. Obtain them from the administrator before filling the source. Keep the command point separate from the point reporting actual state.

This connection uses KNX IP Secure TCP tunneling and Data Secure. It depends on the installation’s XKNX helper process for Windows or Linux x64 and protected material prepared on the server. A container needs these resources too.

Do not assume unsecured IP tunneling, multicast routing or automatic Engineering discovery: this type does not promise them.

### Gateway and point fields

The generic TAG assistant still produces a binding incompatible with the KNX/IP contract. UI authoring correction is pending; the fields below explain the contract, but there is no validated walkthrough to complete this binding with the current assistant. Do not attempt to fix it by changing DPTs or disabling security.

Configure the source and binding using the actual DPT. Passwords/keyring are protected references, not secret files inside the application.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| KNX gateway host | IP/name of the KNX/IP Secure gateway reachable from the SCADIVO server. Not a group or individual address. | 192.0.2.40 |  |
| Secure tunnel port | Port of the Secure TCP endpoint configured on the gateway. Do not assume an unsecured IP tunneling gateway offers this service. | 3671 | 1–65535. |
| Individual address | Assigned KNX client address in area.line.device notation. Obtain from the KNX project/administrator to avoid duplication. | 1.1.250 |  |
| Secure tunnel user ID | Gateway Secure user ID, not a SCADIVO login. Must match the provisioned authentication material. | 1 | 1–127. |
| Secure password reference | Reference to protected material provisioned on the server for this integration. Ask the administrator for the reference; do not paste a password, token or secret key into ordinary settings or the application manual. |  |  |
| Device authentication reference | Reference to protected material provisioned on the server for this integration. Ask the administrator for the reference; do not paste a password, token or secret key into ordinary settings or the application manual. |  |  |
| Data Secure keyring reference | Reference to protected material provisioned on the server for this integration. Ask the administrator for the reference; do not paste a password, token or secret key into ordinary settings or the application manual. |  |  |
| Command group / binding address | Address in main/middle/subgroup notation for writes. In the portable binding this is the TAG Address; for a read-only TAG check the KNX project mapping. The generic TAG assistant still produces a binding incompatible with the KNX/IP contract. UI authoring correction is pending; the fields below explain the contract, but there is no validated walkthrough to complete this binding with the current assistant. Do not attempt to fix it by changing DPTs or disabling security. | 1/2/3 |  |
| Authoritative state group | Feedback source. For a writable TAG it must differ from the command group; the driver does not treat sending a command as confirmed state. | 1/2/4 |  |
| KNX datapoint type (DPT) | Defines telegram interpretation. 1.001/1.002 use Boolean TAGs; 5.001 and 9.001/9.004/9.007/9.024 use Float or Double in this driver. The example uses 9.001 for temperature. Do not choose DPT solely by apparent value size. | 9.001 |  |

### Example: separate command and return

The example describes a Double TAG, DPT 9.001, with command group 1/2/3 and state group 1/2/4. Replace with groups existing in the KNX project. This is not an import package and contains no security material. Equal groups are rejected for writable points; feedback must exist on the device.

```json
{"writeGroupAddress":"1/2/3","stateGroupAddress":"1/2/4","dpt":"9.001","tagType":"Double"}
```

### Verify in stages

Check sidecar availability and protected references first, then host/port/user/individual address and groups/DPT/TAG type. Authoring with the current assistant requires the correction noted above. With a correctly validated binding, save/publish/activate through the project and check feedback before writing. Accepted sending without feedback must not artificially update state. This driver advertises no Engineering discovery.

[Open this guide in installed Help](/help?topic=drivers.knx-setup)

## Home Assistant Bridge: connect and select entities

One source connects to a user-managed Home Assistant; only selected entities enter the project.

<!-- HELP-REVIEW-AFTER-PRODUCT-785: https://github.com/brunolrogerio-collab/EliteSCADA/issues/785; topic=drivers.homeassistant-setup; fields=selectedEntityIds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Local bridge with explicit identity

Use this source to connect to a Home Assistant installation already prepared by you or the network administrator. It must be reachable from the SCADIVO server. Prepare its authorized token as protected material before using the connection.

HAB abbreviates Home Assistant Bridge; HA in SCADIVO still means High Availability. The bridge integrates supported entities, but does not manage Home Assistant accounts or copy its database. Home Assistant integrations do not thereby become native SCADIVO drivers.

### Connection, identity and current selection limit

Connection testing verifies transport/authentication, not incorporation. In the current version the generic discovery page does not supply entity selection; empty selection does not necessarily mean network failure. For a known TAG, configure its source/binding explicitly in the TAG editor and check the read before activation. There is no automatic whole-instance import.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Home Assistant host | DNS name or IP reachable from the SCADIVO server. Use the source device or service without a URL path; localhost means the server/container, not the browser. | 192.0.2.30 |  |
| Port | Source service port, separate from SCADIVO UI/API ports. When changing TLS also check the actual service port. | 8123 | 1–65535. |
| Use TLS | Protects transport where the service offers TLS. Does not install certificates or automatically change the port. | false |  |
| Base path | Optional path prefix when Home Assistant is exposed through a proxy. The client appends /api/websocket. With no prefix leave empty; do not repeat the complete endpoint. |  |  |
| Home Assistant token reference | Reference to protected material provisioned on the server for this integration. Ask the administrator for the reference; do not paste a password, token or secret key into ordinary settings or the application manual. |  |  |
| Connection timeout (ms) | Deadline for establishing the WebSocket connection. Separate from each request timeout. | 10000 | 100–300000. |
| Response timeout (ms) | Deadline for a request response; neither the measurement period nor proof of physical actuation. A value that is too small can reject a slow source. | 10000 | 100–300000. |
| Minimum reconnect wait (ms) | Minimum wait after connection loss; avoid continuous attempts. It does not repair rejected authentication. | 1000 | 100–300000. |
| Maximum reconnect wait (ms) | Limit on increasing wait; must be greater than or equal to the minimum. | 30000 | 100–3600000. |
| Entity and field | Use the exact entity and field: entity:sensor.temperature:state for state; entity:light.room:attribute:brightness for an attribute. A display name is not entity_id. Renaming entity_id in Home Assistant requires reviewing the binding. | entity:sensor.temperature:state |  |
| Entity selection for incorporation | The driver requires explicit selection, but this version of the Home/Building page does not yet let you choose entities. Discovery can return an empty selection with no device to incorporate even with a valid connection. There is no ready selector on that page; the correction is pending. |  |  |

### Address examples

Use state for entity state and attribute for an existing attribute. The example shows a sensor read and light brightness; not an import file. Check type/unit: numeric readings are not display text, and brightness commands use the driver-supported conversion. Renaming the source entity requires reviewing bindings.

```json
{"state":"entity:sensor.temperature:state","attribute":"entity:light.room:attribute:brightness"}
```

### Commands and unavailability

Support is limited to implemented bindings: switch/light state, light brightness/color and cover position as supported by equipment. This form does not offer arbitrary calls to every Home Assistant service. unknown and unavailable are not zero or off. After commands check authoritative state; request success does not prove physical actuation.

[Open this guide in installed Help](/help?topic=drivers.homeassistant-setup)

## ESPHome: native API, entities and deep sleep

Connect directly to the device and incorporate only functions the driver maps.

### What must be available

First check that device firmware offers the ESPHome native API, the service used to read its entities over the local network. The SCADIVO server makes the connection; the browser does not talk directly to the microcontroller.

This path requires neither Home Assistant, ESPHome Dashboard nor a Python helper process. For Noise mode, prepare the API key on the server before connecting.

### Connection and binding

Prepare the source, test connection and use discovery to obtain actual entity keys. Advanced fields do not repair a disabled API or incompatible key.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| ESPHome host | DNS name or IP reachable from the SCADIVO server. Use the source device or service without a URL path; localhost means the server/container, not the browser. | 192.0.2.30 |  |
| Port | Source service port, separate from SCADIVO UI/API ports. When changing TLS also check the actual service port. | 6053 | 1–65535. |
| API encryption | noise is preferred with a provisioned key. plaintext is an explicit compatibility option on a trusted network and must match firmware. Do not confuse the Noise key with a Wi-Fi password. | noise |  |
| Noise key reference | Reference to protected material provisioned on the server for this integration. Ask the administrator for the reference; do not paste a password, token or secret key into ordinary settings or the application manual. |  |  |
| Response timeout (ms) | Deadline for a request response; neither the measurement period nor proof of physical actuation. A value that is too small can reject a slow source. | 5000 | 100–300000. |
| Write reconciliation timeout (ms) | How long to wait for authoritative state after a command. Accepted sending is not feedback; increasing the deadline does not guarantee movement/actuation. | 3000 | 100–300000. |
| Minimum reconnect wait (ms) | Minimum wait after connection loss; avoid continuous attempts. It does not repair rejected authentication. | 500 | 100–300000. |
| Maximum reconnect wait (ms) | Limit on increasing wait; must be greater than or equal to the minimum. | 30000 | 100–3600000. |
| Keep Alive (ms) | Connection supervision interval; does not increase sensor measurement frequency. | 30000 | 1000–300000. |
| Deep-sleep policy | normal for continuously running devices; expected only for intentionally sleeping nodes. Changes reconnect defaults: minimum 500→10000 ms, maximum 30000→60000 ms. Absence remains unavailable, never healthy. | normal |  |
| Entity address | Format kind:device-id:hex-key.field. Discovery supplies the 8-digit hexadecimal key; do not use the display name as a key. Example switch:0:00000010.state. | switch:0:00000010.state |  |
| Write kind | Defines a supported entity write, not an arbitrary service: switchstate, lightstate, lightbrightness, coverposition, fanstate, fanspeedlevel, numberstate or selectstate. Prefer the discovery-generated binding. Entity/value type and ReadOnly remain checked. | switchstate |  |

### Entity example

This address represents state of a Switch entity, device-id 0, hexadecimal key 00000010. Use the key returned by your device; do not invent IDs. A writable Boolean TAG requires the compatible write kind. This is not an import package.

```json
{"address":"switch:0:00000010.state","writeKind":"switchstate"}
```

### Function map and diagnosis

Materialization includes supported process entities such as sensor, binary sensor, switch, light, cover, fan, number and select. Configuration/diagnostic entities and unmapped functions can be omitted with warnings; finding climate, lock or button in inventory does not imply full control of them. Check API, mode/key and connection first, then address/type and discovery warnings. For a sleeping node expected adjusts the connection cycle but still shows absence as unavailable.

[Open this guide in installed Help](/help?topic=drivers.esphome-setup)

## Shelly Gen2+: components, measurements and commands

Use local RPC and review channels/fields actually offered by the device.

### Local connection with a compatible generation

Check the device generation and choose one known channel to test. This driver uses local Shelly Gen2+ RPC communication over HTTP/WebSocket. Have device address, port and authentication ready.

Component means a Shelly channel or function, not a SCADIVO user or Location. The connection does not use a cloud API or promise compatibility with every Shelly Gen1.

### Source and TAG fields

Test connection and discover the device to obtain actual components. Preview shows only mapped measurements/commands; a friendly name does not replace the component key.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Shelly host | DNS name or IP reachable from the SCADIVO server. Use the source device or service without a URL path; localhost means the server/container, not the browser. | 192.0.2.30 |  |
| Port | Source service port, separate from SCADIVO UI/API ports. When changing TLS also check the actual service port. | 80 | 1–65535. |
| Use TLS | Protects transport where the service offers TLS. Does not install certificates or automatically change the port. | false |  |
| Device user | Shelly RPC authentication account; default admin. Neither the SCADIVO user nor a Shelly cloud account. | admin |  |
| Shelly password reference | Reference to protected material provisioned on the server for this integration. Ask the administrator for the reference; do not paste a password, token or secret key into ordinary settings or the application manual. |  |  |
| Response timeout (ms) | Deadline for a request response; neither the measurement period nor proof of physical actuation. A value that is too small can reject a slow source. | 5000 | 100–300000. |
| Minimum reconnect wait (ms) | Minimum wait after connection loss; avoid continuous attempts. It does not repair rejected authentication. | 1000 | 100–300000. |
| Maximum reconnect wait (ms) | Limit on increasing wait; must be greater than or equal to the minimum. | 30000 | 100–3600000. |
| Component and field | Format component.field, for example switch:0.output or switch:0.aenergy.total. The number identifies the channel; do not use the friendly name as an address. | switch:0.output |  |
| Write RPC method | For a writable binding, method supported by the equipment and driver; in the example Switch.Set. Prefer the discovery-generated binding without assuming every method can become a command. | Switch.Set |  |
| Write RPC parameter | Field receiving the write value; Switch.Set uses on for the Boolean. Requires a writable TAG, compatible method and authorization. Confirmed state is read separately. | on |  |

### Example: output of channel zero

The example binds switch:0 output to a Boolean TAG; the compatible write uses Switch.Set with parameter on. Check the actual channel before creating commands. JSON illustrates the binding; it is neither a direct request nor an import package.

```json
{"address":"switch:0.output","writeMethod":"Switch.Set","writeParameter":"on"}
```

### Check measurements and feedback

Fields depend on model: for example output, apower power, voltage, current and aenergy.total when present. The driver also maps bounded light/cover functions and supported sensors. A missing field is not a zero reading; inspect warnings and type/unit. For writes check response and confirmed state separately. For authentication failures check the protected reference and device account rather than switching to cloud credentials.

[Open this guide in installed Help](/help?topic=drivers.shelly-setup)
