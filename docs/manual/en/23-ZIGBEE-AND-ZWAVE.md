# Zigbee and Z-Wave: connection, identity and operation

## Zigbee and Z-Wave: choose the connection path

Check who owns the network and which source type represents the installation.

### Before filling fields

Before choosing a source, identify what maintains your Zigbee or Z-Wave network: a coordinator/controller attached to the server or an existing external installation. The source type must match that actual path.

This guide helps you decide; it is not a new configuration screen. Do not open the same radio simultaneously in SCADIVO and another process controlling its port.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Connection path | Native Zigbee uses the supported TI coordinator on the SCADIVO host. Zigbee2MQTT Bridge uses an external Zigbee2MQTT installation and its broker. Z-Wave JS Controller uses a prepared serial Z-Wave controller on the host. They are not interchangeable options for one device. |  |  |
| Prepared network and devices | Have the network and devices already commissioned through the coordinator/installation procedure. Engineering discovery queries what exists; it does not open inclusion, reset devices or create a network. |  |  |
| Received value and confirmation | A discovered device may not have every function mapped. Old state, transport acknowledgement and fresh feedback differ. Check quality and updates before using a TAG in a command, alarm or screen. |  |  |

### Physical network, TAG and UI have separate steps

1. Identify whether the network uses a supported local coordinator, external Zigbee2MQTT or local Z-Wave controller.
2. Confirm prepared network/devices, server access and protected material without accidentally creating another network.
3. Configure the matching source and check its specific authoring/selection limits.
4. With a valid binding check TAG, type, quality and feedback; save/publish/activate through the project workflow.
5. Add screens, alarms and authorized commands only after verifying a known reading.

[Open this guide in installed Help](/help?topic=drivers.radio-connections)

## Native Zigbee: coordinator and On/Off state

Prepare the source for the supported TI coordinator and understand this version’s bounded scope.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-ZIGBEE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.zigbee-native-setup; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Required installation

For this path, the Zigbee network and device must already be prepared. Use a compatible TI zStack/CC2652 serial coordinator attached to the server. This version offers no Engineering discovery or inclusion wizard and does not promise all Zigbee devices or clusters.

The distribution uses a managed helper process for Windows or Linux x64. In a container, the host must expose the physical radio and persistent state storage. Check those resources before filling the source.

### Fields and authoring limit

Network parameters must match the existing coordinator/state. Binding authoring in the generic assistant is incomplete: Runtime rejects its extra fields. The fields below describe driver requirements; the current assistant does not provide a corrected creation workflow.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Host coordinator resource | ID of an available serial physical resource registered by the installation as ZigbeeCoordinator, TI zStack family and CC2652 model. It is not a COM/tty path entered here. Ask the administrator for its ID and device access on Windows/Linux/container. | radio.zigbee1 |  |
| Network identity | Identity of the prepared network, which must match the evidence registered on the coordinator resource. Does not rename the physical network or replace PAN ID/key. | zigbee.lab1 |  |
| PAN ID | Existing network identifier. Use the installation value; prefer decimal in the numeric field. Changing it is not a repair procedure. | 4660 | 1–65534. |
| Extended PAN ID | Network identity with 16 hexadecimal digits matching the prepared coordinator. Not an actuator IEEE address or secret key. | 00124b0000000001 |  |
| Network channel | Zigbee channel used by the existing network. Changing only this project number does not safely migrate every device. | 15 | 11–26. |
| Serial baud rate | Coordinator serial link speed matching its firmware. Not measurement frequency or radio channel. | 115200 | 9600–1000000. |
| Network key reference | Reference to protected material provisioned on the host for this network. The administrator supplies the reference/slot; do not paste the secret key or put it in an Engineering package. Do not replace an existing key with a new value to troubleshoot connectivity. |  |  |
| Device IEEE address | 16-hex-digit identity of the already commissioned device. A friendly name or short network address does not replace IEEE. | 00124b0000000001 |  |
| Endpoint | Function endpoint on the device from its actual mapping. Not a TCP port or dashboard channel number. | 1 | 1–240. |
| Supported cluster | This native driver accepts genOnOff in this version; it does not offer every Zigbee cluster or analog measurement through this binding. | genOnOff |  |
| Supported attribute | onOff attribute with a Boolean TAG. A different attribute or numeric TAG type is rejected. | onOff |  |
| Complete TAG address | Combines IEEE/endpoint/cluster/attribute. The example is an On/Off function; replace with the existing device identity. | 00124b0000000001/1/genOnOff/onOff |  |
| Binding authoring in this version | The current generic assistant stores identity fields as extra settings, but this driver requires them only in the address. Runtime rejects the generated object. UI authoring awaits correction; do not bypass validation. |  |  |

### Example of a prepared function

The example describes an existing Boolean function identity. It is not an import package and does not open inclusion. The driver binding stores identity in the complete address without duplicate extra settings. ReadOnly still controls write eligibility alongside Authority authorization.

```json
{"address":"00124b0000000001/1/genOnOff/onOff","ieeeAddress":"00124b0000000001","endpoint":1,"cluster":"genOnOff","attribute":"onOff","dataType":"Boolean"}
```

### Diagnosis and network preservation

Check available resource, model/family, serial access and network identity before reviewing TAGs. Then check IEEE/endpoint and Boolean/genOnOff/onOff. Do not repair failures by deleting persistent state, changing keys or creating another network without the appropriate maintenance procedure. A write followed by a read checks state, not physical actuation.

[Open this guide in installed Help](/help?topic=drivers.zigbee-native-setup)

## Z-Wave JS Controller: prepared network and persistent values

Use the host-registered controller and preserve each value’s exact identity.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-ZWAVE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.zwave-setup; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### What this type does

Use this path to read persisted values from an already prepared Z-Wave network. The serial controller is local to the server; its managed helper uses internal communication limited to that host.

Installation supports Windows or Linux x64. In Docker, also provide the device and persistent state directory. This type offers no inclusion, exclusion, SmartStart, reset, controller administration or Engineering discovery. Prepare the network through its own procedure before using values in SCADIVO.

### Source and address interpretation

S0/S2 references are optional in source format, but secure devices need material matching the network. RF region and NVM identity belong to the host resource. New UI binding authoring is pending: the descriptor without extra fields does not produce the binding required by Runtime.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Host controller resource | ID of an available serial ZWaveController resource with RF region and network NVM identity registered by the administrator. Do not enter a serial path or RF parameters in this source. Region/identity must match the prepared controller. | radio.zwave1 |  |
| S0 Legacy key reference | Reference to protected material provisioned on the host for this network. The administrator supplies the reference/slot; do not paste the secret key or put it in an Engineering package. Do not replace an existing key with a new value to troubleshoot connectivity. |  |  |
| S2 Access Control key reference | Reference to protected material provisioned on the host for this network. The administrator supplies the reference/slot; do not paste the secret key or put it in an Engineering package. Do not replace an existing key with a new value to troubleshoot connectivity. |  |  |
| S2 Authenticated key reference | Reference to protected material provisioned on the host for this network. The administrator supplies the reference/slot; do not paste the secret key or put it in an Engineering package. Do not replace an existing key with a new value to troubleshoot connectivity. |  |  |
| S2 Unauthenticated key reference | Reference to protected material provisioned on the host for this network. The administrator supplies the reference/slot; do not paste the secret key or put it in an Engineering package. Do not replace an existing key with a new value to troubleshoot connectivity. |  |  |
| Persistent value address | Identifies node, endpoint, Command Class, property and optional key, preserving string/number type. Not the operator display name. Must match the prepared controller inventory. | node/5/endpoint/0/cc/37/property/string/currentValue/key/none/- |  |
| Node | Device number assigned by the existing Z-Wave network; not an IP address. Exclusion/reinclusion can require binding review. | 5 | 1–232. |
| Endpoint | Value endpoint on the device, including 0 for a root-node value. Not a serial/TCP port. | 0 | 0–127. |
| Command Class | Code of the class containing the value. Example 37 is Switch Binary; the driver admits bounded persistent values and rejects classes outside its scope. Not an Authority permission. | 37 |  |
| Property and its type | Use string/currentValue for the sample textual property. number/1 and string/1 properties have different identities; do not remove the type. Do not assume currentValue is writable. | string/currentValue |  |
| Optional key and its type | key/none/- means no additional key. Where one exists keep its number/string type and escaped address contents; do not omit format components. | none/- |  |
| Authoring a new binding | This driver requires a versioned communication binding as well as Address. The generic assistant does not create a new binding when the descriptor has no extra fields, as here. Entering only a new TAG address is insufficient; authoring correction is pending. An existing valid binding received through package/API is a separate case. |  |  |

### Example: read binary switch state

The example is a Boolean currentValue read from node 5. Use the installation’s actual values. The property may not be writable; commands require a controller-advertised writable value plus SCADIVO authorization. This is not an import package.

```json
{"address":"node/5/endpoint/0/cc/37/property/string/currentValue/key/none/-","nodeId":5,"endpoint":0,"commandClass":37,"property":"currentValue","dataType":"Boolean","readOnly":true}
```

### Verification and common failures

Admitted TAG types are Boolean, Int16/Int32/Int64, Float/Double and String within accepted persistent properties. Check resource, region, NVM identity and security material, then complete address and actual value metadata. Missing values, incompatible types or unadvertised writes are not fixed by removing validation. Transient events do not automatically become persistent TAGs. Check feedback before treating a command as complete.

[Open this guide in installed Help](/help?topic=drivers.zwave-setup)

## Zigbee2MQTT Bridge: inventory, selection and freshness

Connect to an external installation and distinguish inventory, incorporation and fresh reports.

<!-- HELP-REVIEW-AFTER-PRODUCT-785-Z2M: https://github.com/brunolrogerio-collab/EliteSCADA/issues/785; topic=drivers.zigbee2mqtt-setup; fields=selectedIeeeAddresses; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-787: https://github.com/brunolrogerio-collab/EliteSCADA/issues/787; topic=drivers.zigbee2mqtt-setup; fields=inventoryFormat; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### External installation and available workflow

Choose this bridge when Zigbee2MQTT and its broker are managed outside SCADIVO and devices are already joined. It queries that installation; it does not open inclusion or administer the coordinator. Native Zigbee is another path.

The connector advertises connection testing, discovery and read testing. Discovery lists inventory; incorporation requires choosing what enters the project. The current UI still limits that selection. Reading some nested inventory structures also awaits a separate correction. Steps below identify those limits rather than promising a complete incorporation where unavailable.

### Source fields

Host, base topic and credentials must match the broker actually used by the installation. Check connectivity from SCADIVO server, not only the browser.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| MQTT broker host | Name/IP of the broker used by the external Zigbee2MQTT installation, reachable from SCADIVO server. Not the Zigbee2MQTT UI URL or a topic. | mqtt.example.invalid |  |
| Broker port | Actual broker port. The form defaults to 1883; with TLS check its corresponding port rather than assuming automatic switching. | 1883 | 1–65535. |
| Use TLS | Enables protected transport where offered by the broker. Does not install certificates or replace broker authentication. | false |  |
| Base topic | Prefix configured on external Zigbee2MQTT. The driver uses, for example, baseTopic/bridge/state and baseTopic/bridge/devices. Do not put a device friendly name here. | zigbee2mqtt |  |
| MQTT client ID | Optional; empty generates a stable ID from source identity. If set, do not reuse the same ID on another active client. Not a user name or password. |  |  |
| MQTT version | mqtt5 or mqtt311 as supported by the broker. The driver uses its own session defaults; do not copy every MQTT Raw advanced setting into this source. | mqtt5 |  |
| Broker user | Account permitted on the external installation topics. Separate from SCADIVO login. |  |  |
| Broker password reference | Reference to protected material provisioned on the host for this network. The administrator supplies the reference/slot; do not paste the secret key or put it in an Engineering package. Do not replace an existing key with a new value to troubleshoot connectivity. |  |  |
| Keep Alive (s) | Broker connection supervision. Does not request sensor measurements or wake a battery device. | 30 | 1–65535. |
| Connection timeout (ms) | Deadline for connecting to the broker, separate from request or write-confirmation time. | 10000 | 100–300000. |
| Request timeout (ms) | Deadline for a query operation. Not a polling interval for every sensor. | 5000 | 100–30000. |
| Write confirmation wait (ms) | Deadline for a matching sufficiently fresh state report after a command. MQTT acceptance without feedback does not confirm equipment state. | 10000 | 100–60000. |
| Minimum reconnect wait (ms) | Initial wait after connection loss. Reducing it does not repair an offline broker or rejected authentication. | 1000 | 100–300000. |
| Maximum reconnect wait (ms) | Caps increasing wait and must be greater than or equal to minimum. | 30000 | 100–3600000. |
| Maximum message size (bytes) | Limits received payload including inventories; increase only if actual documents require it and resources allow. Not history capacity. | 1048576 | 1–67108864. |
| Connection failure limit | Consecutive failure limit used by readiness supervision. Does not grant operation with an unreachable source. | 10 | 1–1000. |
| Maximum queued messages | Limits the transport buffer. More messages use more memory; not persistent storage. | 2048 | 1–65535. |
| Maximum inventory devices | Inventory/selection limit for this source. Not a radio-capacity promise and does not remove license quotas. | 256 | 1–4096. |
| Maximum exposes per device | Limits property descriptions processed per device. An expose is a function/property advertised by Zigbee2MQTT, not an automatically incorporated TAG. | 256 | 1–2048. |

### Generated bindings and pending selection

Technical fields below are produced by incorporation from selection/inventory. They are not IDs for the developer to invent. Until the page sends selection, listing a device does not guarantee ready Equipment/TAGs. Mapped scalar exposes are binary and numeric; functional capabilities depend on supported properties.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| IEEE, endpoint and property | Stable address generated from IEEE, optional endpoint and property. Friendly name can change and must not become binding identity. | ieee:0x00124b0024cafe01/endpoint:/property:temperature |  |
| Internal source identity — automatic | Technical field generated by incorporation. Links to the saved source stable ID. Do not invent or copy IDs across sources. |  |  |
| Source key — automatic | Technical field generated by incorporation. Links to the readable source key. Do not invent or copy IDs across sources. |  |  |
| IEEE — automatic | Technical field generated by incorporation. Normalized IEEE of the selected device, not friendly name. Do not invent or copy IDs across sources. | 0x00124b0024cafe01 |  |
| Endpoint — automatic | Technical field generated by incorporation. Endpoint advertised by the expose; may be absent. Do not invent or copy IDs across sources. |  |  |
| Property — automatic | Technical field generated by incorporation. Advertised property name such as temperature or state. Do not invent or copy IDs across sources. | temperature |  |
| Expose type — automatic | Technical field generated by incorporation. binary produces Boolean TAG; numeric produces Double TAG. Other types do not become scalar TAGs through this workflow. Do not invent or copy IDs across sources. | numeric |  |
| Advertised access — automatic | Technical field generated by incorporation. Combinable bits: 1 state, 2 write, 4 request read. Must include 1; 5 means state plus request read. Does not grant security authorization. Do not invent or copy IDs across sources. | 5 |  |
| Unit — automatic | Technical field generated by incorporation. Must match the TAG unit; not an arbitrary scale conversion. Do not invent or copy IDs across sources. | °C |  |
| Minimum — automatic | Technical field generated by incorporation. Advertised numeric lower bound, where supplied. Do not invent or copy IDs across sources. |  |  |
| Maximum — automatic | Technical field generated by incorporation. Advertised numeric upper bound, where supplied. Do not invent or copy IDs across sources. |  |  |
| Step — automatic | Technical field generated by incorporation. Positive numeric step where advertised; not read frequency. Do not invent or copy IDs across sources. |  |  |
| On value — automatic | Technical field generated by incorporation. Explicit JSON value for true on a binary expose. Text ON and Boolean true have different types. Do not invent or copy IDs across sources. | "ON" |  |
| Off value — automatic | Technical field generated by incorporation. Explicit JSON value distinct from the on value for a binary expose. Do not invent or copy IDs across sources. | "OFF" |  |
| Point identity — automatic | Technical field generated by incorporation. Combines source, IEEE, endpoint and property; copying from another source is invalid. Do not invent or copy IDs across sources. |  |  |
| Equipment function — automatic | Technical field generated by incorporation. For example Temperature or OnOff; not a security capability granted to a user. Do not invent or copy IDs across sources. | Temperature |  |
| Confirmation contract — automatic | Technical field generated by incorporation. Where supported requires matching non-retained state after the command with source freshness evidence. Not proof of physical actuation. Do not invent or copy IDs across sources. | non-retained-state-match |  |
| Explicit incorporation selection | The service accepts IEEE selection, but the current Home/Building page does not send it. Without selection discovery lists inventory without incorporation content; selecting a row does not reissue the request with IEEE. UI incorporation awaits correction. |  |  |
| Inventory format — current limitation | The external format puts exposes in definition.exposes; this parser version looks for exposes at the root. A device can therefore be listed without producing TAGs. Compatibility correction is pending separately from UI selection. | definition.exposes |  |

### Advertised inventory example

The example below preserves the external format. The current parser does not extract its exposes; it demonstrates the limitation, not a completed import. Do not rewrite broker messages as an operational workaround.

```json
[
  {
    "ieee_address": "0x00124b0024cafe01",
    "friendly_name": "sensor_sala",
    "supported": true,
    "type": "EndDevice",
    "definition": {
      "model": "Example",
      "vendor": "Example",
      "exposes": [
        {
          "type": "numeric",
          "property": "temperature",
          "access": 5,
          "unit": "°C"
        },
        {
          "type": "binary",
          "property": "state",
          "access": 7,
          "value_on": "ON",
          "value_off": "OFF"
        }
      ]
    }
  }
]
```

### State and quality example

A retained message is Stale in this connector. Without source last_seen newer than the connection/bridge epoch, a report can be Uncertain even with retain false. Operation confirmation must also be newer than dispatch. Configure measurement information on the external installation and check times; receiving now does not prove measurement now. The sample instant is illustrative: use a current measurement for live tests.

```json
{"temperature":21.5,"state":"OFF","last_seen":"2026-10-25T12:00:01Z"}
```

### Troubleshoot by layer

No connection: check host/port/TLS/authentication. Connected without inventory: check baseTopic and bridge/state/devices. Inventory without incorporation: check the UI selection limit rather than recreating the network. Uncertain/Stale values: check retention, last_seen and reconnection. Unconfirmed write: check advertised access, confirmation binding, availability and fresh report; do not confirm a TAG just because the broker accepted publication.

[Open this guide in installed Help](/help?topic=drivers.zigbee2mqtt-setup)
