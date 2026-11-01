# Matter Wi-Fi: test version

## Matter Wi-Fi: prepare and understand the test version

Check availability, prepare the source and distinguish reading an already-commissioned attribute from pairing a device.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-MATTER: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.matter-test-workflow; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Start with availability

Before configuring, check whether Matter Wi-Fi is available in your installation. SCADIVO is multiplatform, but this connector is an isolated preview for a Linux x64 host with a local helper process called a sidecar. It is not Matter availability for every installation.

The default composition does not offer the type. It appears only when the administrator enables preview in Development/Test, without HA. If absent from Data Sources, check eligibility with that administrator; do not choose another driver to imitate it. Help remains available to explain these limits.

### What you can prepare

The current version handles persistent attributes of already-commissioned Matter Wi-Fi devices. Commissioning means adding the device to a Matter logical network, called a fabric. Creating a source or host key does not perform that pairing. This connector has no commissioning wizard, Thread, device administration, Browse, Discover or Test connection. Attribute read/write does not imply support for every device command. Keep the test-version designation; future qualification is unfinished.

### Before opening the editor

Ask the administrator for the numbers identifying the point: node, endpoint, cluster and attribute, plus actual value type. These are not the internal TAG ID.

Node identifies the device in the Matter logical network, called a fabric. Endpoint identifies a function, such as an output. Cluster groups service functions and attributes, such as On/Off. An attribute is one value in that group, such as on/off state. A device can have several endpoints; check its numbers rather than copying another unit’s.

The host needs the sidecar and IPv6 network prepared. The declared dependency requires Node.js 22.13+ or 24+. Container networking can differ from the machine. Fabric and encrypted storage stay on the host: .escadapkg import neither transfers them nor recovers a lost key. This procedure asks for no keys or credentials in project fields.

### Prepare the source and host key

1. In an eligible isolated environment, open Engineering → Data Sources and create a Matter Wi-Fi source. Give it a readable name.
2. Check changes and apply the source to the Workspace; this is not publication/activation.
3. Select the applied source. In its Matter key panel, choose Create host key and confirm. Apply pending edits first: the button is unavailable while the source differs from its applied state.
4. Wait for the response and page refresh. The expected result is the configured-key indication; the button then offers Verify host key.
5. Continue TAG preparation, review, Save, Publish and Activate only a validated candidate. Creating/verifying a key does not contact devices or prove communication. The operation requires Engineering modification permission and the current Workspace version.

### Understand fields and references

The source accepts no public host, port or generic protocol settings. Its protected reference is prepared through the host action. The other items below explain a TAG binding; the technical example is neither a complete package nor an import form.

| Field | Purpose and how to fill it | Example | Bounds |
| --- | --- | --- | --- |
| Fabric storage key | Protects local Matter storage. Use the host action; do not enter a key, password or path. | Key configured | Host reference; the secret is never displayed or exported. |
| TAG source | Associate the point with the prepared Matter source. Naming a source does not discover a device. | matter.lab | The association must identify the same source as the binding. |
| TAG type | Match the actual attribute type; Boolean fits an on/off state. | Boolean | Boolean, Int16, Int32, Int64, Float, Double or String; do not assume other types. |
| Read only | Start with reads to check value and quality before testing actuation. | true | Allowing TAG writes grants neither user permission nor device-attribute writability. |
| Attribute address | Assemble the four real numbers in the shown format; not an IP address or pairing code. | node/123/endpoint/1/cluster/6/attribute/0 | Unsigned decimal without leading zeros; node 1 through UInt64 maximum, endpoint UInt16, cluster/attribute UInt32. |
| Communication binding | Declares the representation identity and version understood by Runtime. Address text alone does not create this object. | Matter v1 binding | The current generic assistant does not build this binding when the catalog declares no point fields; no complete graphical procedure is promised here. |
| Contract version | Identifies the binding-object format, not the commercial Matter version. | 1 | The current binding contract is 1. |
| Schema identity | Identifies the Matter interpretation of the address; this is technical reference. | elitescada.driver.matter.wifi | Do not substitute the source configuration schema. |
| Schema version | Specifies how binding content is interpreted. | 1 | The current planner accepts Matter schema v1. |
| Address inside the binding | Repeats the complete attribute identity inside the binding. | node/123/endpoint/1/cluster/6/attribute/0 | When Address is provided, it must equal PortableAddress. |
| Extra settings | Do not add generic configuration to work around a binding failure. | No extra settings | Source/point Settings, selectors and nonidentity transforms are rejected in this version. |
| Point count | Keep only persistent TAGs you intend to exercise on this source. | 1 read-only TAG | 1 through 512 associated TAGs with valid internal identities; node numbers do not replace those IDs. |

### On/off attribute example

This is only a configuration excerpt for explaining identity and validating the contract. Node 123 is fictional and need not exist on your network. Cluster 6/attribute 0 represents an On/Off state when the device provides it at endpoint 1. The controlled environment supplies internal IDs and the host-prepared reference. Do not paste this as an .escadapkg or add a key. Planner acceptance checks configuration, not connection, real values or quality. Today an Address field alone and the key action do not complete binding authoring in the interface; a technical candidate must be prepared/validated within the controlled exercise.

```json
{
  "source": {
    "key": "matter.lab",
    "driver": "matter.wifi"
  },
  "tag": {
    "path": "Lab.Lamp.On",
    "dataType": "Boolean",
    "readOnly": true,
    "address": "node/123/endpoint/1/cluster/6/attribute/0",
    "communicationBinding": {
      "contractVersion": 1,
      "schemaId": "elitescada.driver.matter.wifi",
      "schemaVersion": 1,
      "portableAddress": "node/123/endpoint/1/cluster/6/attribute/0"
    }
  }
}
```

### Reading, writing and operational safety

After activating a valid configuration in the exercise, inspect Diagnostics and the Runtime TAG: value, quality and sample time. Displayed zero/false without a valid sample does not prove the lamp is off. Before writing, confirm attribute writability and that the TAG, Authority and session class allow the action. Attribute value writes use ProcessValueWrite; do not reinterpret this as CommandExecute or promise unimplemented device commands. Check the actual result and feedback read. A screen button and a created key do not confirm actuation.

### Investigate by symptom

Missing type: check preview availability and absence of HA. Missing key: apply the source and use the host action; editing a JSON reference does not create a key. Disabled button: unapplied edits or an operation is in progress. Binding required: the address was entered without CommunicationBinding. Invalid address: check order, numbers, bounds and absence of leading zeros. Incompatible schema: check Matter schema and version 1. Activation rejected in HA: this version does not support that mode; do not bypass validation. Configuration accepted but no samples: check sidecar, host storage, network and already-commissioned attributes. Never recreate a key as the first recovery attempt for an existing fabric. Lost key/state requires planned recovery with the owner; an engineering file cannot restore these secrets.

[Open this guide in installed Help](/help?topic=drivers.matter-test-workflow)
