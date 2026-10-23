# Engineering, Data Sources, and TAGs

This baseline chapter covers the stable parts of Parts III and IV. UI surfaces that have not completed visual review do not receive final screenshots in this phase.

## Working, Save, Revision, Publish, and Activate

Working is editable. Save persists work without silently turning it into Published or Active. A Revision should represent an identifiable, validated state. Publish publishes the revision; Activate is the explicit transition that changes the backend's Active authority.

**Related Help:** `/help?topic=engineering.lifecycle`.

## Data Sources and Drivers

Create Data Sources using types registered in the product catalog. Fields, formats, limits, defaults, and protected references come from the schema for the selected type.

Communication drivers are distinct from internal source providers. Simulation is a development/test tool and is not counted as a production Driver in the Help catalog.

### Procedure: configure a source without inventing parameters

**Goal:** create/edit a Data Source using only the actual contract for the selected type.

**Prerequisites:** Engineering access and a Data Source type available in the catalog.

**Steps:**
1. Select the Data Source type.
2. Fill in only the fields exposed by that type's editor/schema.
3. Use examples and formats supplied by the surface itself when available.
4. Run Connection Test, Discover, Browse, Import, or Reconcile only if the type declares that capability.
5. Correct errors in the Data Source before trying to mask them in a TAG or presentation.

**Expected result:** the configuration remains compatible with the registered schema.

**Troubleshooting:** do not transplant syntax from another protocol. A backend rejection should be corrected in the source configuration.

**Related Help:** `/help?topic=sources.data-sources` and `driver.<type-key>` topics.

## TAGs

TAGs use stable identity and a binding compatible with the Data Source. Values, quality, and timestamps arrive through the Runtime pipeline under backend authority.

### Addressing

Use only fields and formats exposed by the Driver's TagBinding schema. Addresses from different protocols are not interchangeable.

**Related Help:** `/help?topic=tags.addressing`.

### Scaling / normalization

Scaling changes the engineering value before operational use and can affect logic, alarms, history, and screens. Visual formatting controls presentation only. Do not use formatting to simulate scaling.

**Related Help:** `/help?topic=tags.scaling-formatting`.

### Quality and timestamps

Quality represents the trust/state of a sample and must remain faithful to the pipeline. Failure, absence, or stale data must not be presented as Good merely to stabilize the UI.

Preserve timestamps provided by the source/Runtime according to the contract; do not replace an industrial timestamp with the browser clock for persistence.

**Related Help:** `/help?topic=tags.quality`, `/help?topic=tags.timestamps`.

## Writes and commands

A visible control does not make a TAG writable. Writing depends on the source/TAG contract and effective capabilities, enforced by the server. A View Only session or missing capability does not receive write authority.

**Related Help:** `/help?topic=tags.writeability`, `/help?topic=bindings-commands.overview`.

## TAG testing/commissioning

The final commissioning UI remains subject to visual review, but the diagnostic sequence is stable:

1. confirm the backend and authentication;
2. confirm the Active revision when the read is operational;
3. validate the Data Source;
4. validate Driver configuration/connection;
5. validate the binding/address;
6. inspect quality and timestamp;
7. only then investigate presentation.

## Copy / Paste / Duplicate

Duplication copies Engineering configuration and creates a new stable identity. Transient Runtime data, such as the current value, quality, timestamp, history, alarm history, and diagnostics, is not part of the copy.

### Procedure: duplicate TAGs with Preview

**Goal:** create copies without collisions and without mutating Working before validation.

**Prerequisites:** one or more selected TAGs.

**Steps:**
1. Select a TAG, or enable multi-selection.
2. Use Copy/Paste or Duplicate.
3. Review the generated drafts.
4. Correct ID, path, and name collisions.
5. Run Preview.
6. Apply only the validated candidate; if Working has changed, run Preview again.

**Expected result:** new TAGs receive their own IDs, and Working is mutated only by the validated Apply.

**Troubleshooting:** collisions block Preview. Do not reuse the source TAG's stable identity.

## Sequential TAG generation

The current sequential-generation surface accepts 1 to 200 TAGs per operation and requires name/path patterns containing `{n}`. The numeric suffix step and address step must be non-zero integers.

Automatic address generation on this surface is specific to canonical Modbus addresses `coil`, `discrete`, `holding`, or `input`, with a reference from 0 to 65535. Validation detects address collisions within the same Data Source before Preview.

### Procedure: generate a Modbus sequence

**Prerequisites:** exactly one source TAG linked to a `modbus.tcp` Data Source and a canonical address.

**Steps:**
1. Open sequential generation for the selected TAG.
2. Set the quantity, name/path patterns, suffix start/step, and address step.
3. Generate the drafts.
4. Review each item's name, path, and address.
5. Resolve all collisions.
6. Run Preview.
7. Apply the validated candidate.

**Expected result:** TAGs are created only after Preview/Apply, with new identities and addresses derived from the configured pattern.

**Limit:** do not automatically convert opaque addresses from other protocols into a Modbus sequence.

## Historian profile

Associating a TAG with a capture profile is Engineering configuration and must preserve the profile's stable reference. The full capture policy remains dependent on completion of the Historian workstream; do not extrapolate behavior that has not been integrated.
