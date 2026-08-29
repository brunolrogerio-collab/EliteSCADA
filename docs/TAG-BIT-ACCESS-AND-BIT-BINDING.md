# TAG Bit Access and Bit-Level Driver Binding — EliteSCADA

Status: **LOCKED PRODUCT DIRECTION / SPECIFIED / NOT IMPLEMENTED**  
Date: 2026-08-28

This document locks two related but distinct capabilities:

1. logical bit access over integer TAG values, for example `Word_comando.00`;
2. direct Engineering of a Boolean TAG whose physical source is one bit inside a word/register exposed by a driver, for example bit 7 of a Modbus Holding Register.

These capabilities are transversal TAG/driver contracts. They must not be implemented as visual-editor-only syntax, arbitrary metadata, driver-private aliases or Python-only behavior.

Canonical Engineering remains saved truth.

## 1. Logical bit selector over integer TAGs

Integer TAG values expose deterministic Boolean bit selectors.

Human-facing syntax:

`<tag-reference>.<bit-index-two-digits>`

Examples:

- `Word_comando.00` -> bit 0;
- `Word_comando.07` -> bit 7;
- `Word_comando.15` -> bit 15 of an Int16-width value;
- `Status_geral.31` -> bit 31 of an Int32-width value.

The two-digit suffix is the preferred Engineering/display notation for bit positions below 100. The parser may accept a non-padded numeric bit index if deliberately supported, but canonical UI presentation should remain unambiguous and consistent.

The visible text is an authoring convenience. Saved canonical references must retain the resolved TAG identity plus the bit selector, conceptually:

`{ tagId, selector: { kind: "bit", index: 7 } }`

A rename of the TAG must not silently retarget a saved bit reference.

## 2. Supported logical types and bit numbering

Initial canonical integer widths:

- `Int16`: bits `00..15`;
- `Int32`: bits `00..31`;
- `Int64`: bits `00..63`.

If unsigned canonical integer types are added later, they use their natural fixed width and the same bit numbering.

Protocol/UI terms such as Word/DWord may be offered as friendly aliases where appropriate, but they must map to an explicit canonical integer width rather than creating ambiguous storage semantics.

Bit numbering is always:

- bit 0 = least significant bit (LSB);
- highest bit = most significant bit (MSB) for the fixed-width integer.

For signed integers the selector observes the fixed-width two's-complement representation. Therefore the sign bit is also a normal selectable bit.

Normal Engineering bit selectors are not supported for Float/Double/String/DateTime/Enum. Exposing the raw IEEE-754 representation of a floating-point TAG would require a future explicit raw-binary contract rather than accidental reuse of this syntax.

## 3. Read semantics

Reading an integer TAG bit returns Boolean:

`bit = ((value >> index) & 1) == 1`

A bit selector inherits the source TAG sample identity context:

- timestamp;
- quality;
- source/runtime context;
- authorization visibility.

If the source TAG has no usable value/quality, the bit selector does not invent `false`; it is unavailable/bad according to the normal TAG/reference quality path.

Bit selectors are views over the authoritative TAG value. They are not independent retained values and do not create duplicate historian truth by themselves.

## 4. Where logical bit selectors may be used

The bit selector reference must become reusable anywhere a Boolean source reference is valid, subject to each subsystem's normal capability rules.

Initial intended consumers include:

- visual property bindings/typed visual expressions;
- boolean alarms/conditions where the alarm model accepts a Boolean TAG/reference;
- Client Visual Python read APIs through a stable TAG/bit reference surface;
- diagnostics and Engineering preview;
- future expression/derived-value surfaces that consume canonical TAG references.

Example visual expression:

`Word_status.03 or Falha_bomba`

The expression engine must resolve `Word_status.03` as a Boolean dependency on the canonical integer TAG plus bit index, not as a second unrelated TAG discovered by string concatenation.

## 5. Optional write semantics for logical TAG bits

If the underlying integer TAG is writable and the caller has normal write authorization, EliteSCADA may allow a Boolean write to a bit selector.

A write to `Word_comando.07 = true` means:

- set bit 7;
- preserve every other bit in `Word_comando`;
- do not replace the entire integer with `1`.

Likewise `false` clears only the selected bit.

Bit writes must be concurrency-safe. Two simultaneous writes to different bits of the same authoritative word must not lose one another through an unsafe read-modify-write race.

The implementation must serialize/coordinate bit mutations against the same underlying write authority. If the source protocol offers a native atomic bit-mask operation, the driver may use it. Otherwise a read-modify-write implementation must use a per-source/per-address coordination strategy that prevents lost updates within EliteSCADA.

External PLC/device writers can still change the same word concurrently. Diagnostics/documentation must not pretend a client-side read-modify-write can provide stronger atomicity than the protocol/device actually supports.

## 6. Direct physical bit binding for Boolean TAGs

A Boolean TAG may be engineered directly against one bit within a protocol word/register when the driver declares this capability.

Conceptually the binding contains:

- normal physical address / portable address;
- underlying storage width/type;
- `bitIndex`;
- Boolean logical TAG type.

For Modbus TCP this enables, for example:

- area: `HoldingRegister`;
- register address: the configured Holding Register address;
- bit index: `7`;
- TAG data type: `Boolean`.

The user may enter/select the register through the normal Modbus address UI and then choose `Use bit` / `Bit index = 7`, or an equivalent compact notation if the UI supports it.

The saved contract must not depend only on a free-form address string such as `400001.7`. Address family, zero/one-based presentation policy, protocol offset and bit selector must remain deterministically parseable/versioned.

## 7. Modbus semantics

Current EliteSCADA Modbus runtime already distinguishes `Coil`, `DiscreteInput`, `HoldingRegister` and `InputRegister` and uses 16-bit protocol addresses. The bit-level extension must preserve that model rather than inventing a parallel Modbus stack.

### 7.1 Reading Boolean TAG from register bit

For `HoldingRegister` or `InputRegister` with a Boolean bit binding:

- read the 16-bit register value;
- extract the configured bit `0..15`;
- publish a Boolean TAG sample;
- inherit communication quality from the register read.

Multiple Boolean bit TAGs sharing the same Unit ID / area / register should be poll-coalesced where practical. EliteSCADA should not issue 16 identical register reads merely because 16 TAG definitions expose 16 bits.

### 7.2 Writing Boolean TAG to Holding Register bit

A Boolean bit TAG in `HoldingRegister` may be writable when Engineering, driver capability and security allow it.

Writing must change only the selected bit. Acceptable implementation strategies include:

1. native Modbus Mask Write Register (function 0x16 / decimal 22) when supported by the selected transport/device capability; or
2. coordinated read-modify-write of the 16-bit Holding Register when mask-write is unavailable.

The implementation must not encode Boolean `true` as register value `1` or `false` as `0` and overwrite unrelated bits.

`InputRegister` remains read-only. `DiscreteInput` remains read-only. `Coil` is already a native Boolean address and therefore does not require a register bit selector.

### 7.3 Address notation

Engineering must distinguish the user's familiar register notation from the protocol's zero-based wire offset.

A display such as `400001` / `4xxxxx` is a human addressing convention, while Modbus function/address on the wire uses a 16-bit offset. EliteSCADA must have one explicit conversion policy and show it in the editor so users do not suffer the traditional Modbus off-by-one ritual.

Bit selection is applied after the register address is resolved. The bit index is always `0..15` for one Modbus register.

## 8. Driver-independent mandatory contract

Direct physical bit binding is not Modbus-only and is a permanent driver requirement.

Every future production driver that exposes physical byte/word/register/integer storage from which individual bits can be meaningfully addressed **must** expose that bit capability through its public versioned TagBinding/capability schema rather than forcing masks, scripts or driver-private address strings.

For each such storage family the driver declares:

- whether bit selection is supported;
- allowed storage widths/types;
- valid bit range and numbering;
- readable behavior;
- writable behavior;
- whether the underlying protocol/address is intrinsically read-only;
- whether a native atomic bit-write primitive exists;
- the fallback coordination strategy when native atomic bit write is unavailable;
- whether multiple bit points can share/coalesce the same physical read.

### Mandatory read behavior

If the protocol permits reading the underlying byte/word/register, Engineering must be able to expose a Boolean TAG/reference for a valid selected bit.

The selected bit inherits the same communication quality/source context as the underlying read. Communication failure must not be converted to Boolean false.

### Mandatory write behavior

If the underlying protocol/address is writable, the driver must support writing an engineered Boolean bit point without corrupting unrelated bits.

The preferred order is:

1. use a protocol-native atomic bit/mask operation when one exists and is deliberately supported;
2. otherwise perform a coordinated read-modify-write against the same physical write authority.

Concurrent EliteSCADA writes targeting different bits of the same physical word must not lose one another through an uncoordinated local race.

A driver must never claim bit-write support when the protocol/address itself is read-only. Read-only areas remain explicitly read-only; the product requirement is to expose the real capability, not fabricate impossible writes.

Native Boolean protocol points do not need an artificial word-bit selector merely to satisfy this contract.

### Driver conformance rule

Bit access becomes part of the standard acceptance matrix for every future driver with bit-addressable scalar storage. A new driver is not considered feature-complete for that storage family until its declared bit read/write behavior, range validation, quality propagation, unrelated-bit preservation and concurrency guarantees are covered by focused tests.

Canonical TAG Engineering must represent the logical Boolean result without leaking a protocol library object into Core.

## 9. Engineering UI

TAG Engineering must eventually provide an explicit bit-level authoring experience.

For integer TAG reference consumers:

- TAG selector;
- optional Bit selector when the chosen TAG is a supported integer type;
- visible bit range derived from TAG width;
- preview of the friendly reference, e.g. `Word_comando.07`.

For protocol bindings:

- normal address fields;
- logical TAG type;
- optional `Use bit` control where the driver binding schema supports it;
- validated bit index;
- clear read-only/write behavior;
- explicit address-base convention/help for protocols such as Modbus.

Invalid combinations fail before Apply, for example Boolean bit binding to a Modbus register with bit 16, or bit selection on Float32 without a future explicit raw-binary contract.

## 10. Import/export and persistence

Bit selectors and physical bit bindings are first-class canonical Engineering and must round-trip through:

- JSON export/import;
- Preview/Apply/CAS;
- Working state;
- immutable revisions;
- PostgreSQL persistence;
- `.escadapkg` backup/restore;
- future Engineering Fragments/copy-paste;
- driver reconcile/import workflows where relevant.

Human-friendly `.07` syntax must not be the only persisted identity. Stable TAG ID / driver binding fields must survive rename and reordering.

## 11. Historian and alarms

A logical bit selector by itself is a projection over an integer TAG and does not automatically create a second historian series.

If the engineer needs independent historical retention/alarm identity for one physical bit, they may create a first-class Boolean TAG bound to that physical bit. That Boolean TAG then participates in historian/alarm policies normally.

This distinction prevents accidental multiplication of retained data while still allowing first-class process points where required.

## 12. Security and audit

Reading a bit cannot bypass read authorization on its source TAG/physical point.

Writing a logical bit or Boolean bit-bound TAG follows the same protected process-write/command rules as any other write. Bit notation is never an authorization shortcut.

Auditable writes must identify the logical TAG/reference and, where useful for diagnostics, the affected underlying word/register and bit index without leaking secrets.

## 13. Acceptance requirements

Before this contract is considered implemented, automated validation must prove at least:

1. Int16 bit reads for `00`, an intermediate bit and `15`;
2. Int32/Int64 range validation and two's-complement behavior;
3. bit selector inherits source TAG quality and does not turn bad quality into false;
4. canonical reference survives TAG rename/export/import through stable identity;
5. visual/expression binding can consume an integer TAG bit as Boolean;
6. Modbus Holding Register bit-bound Boolean TAG reads the correct bit;
7. Modbus Input Register bit-bound Boolean TAG is read-only;
8. invalid Modbus bit index fails validation;
9. writing one Holding Register bit preserves all other bits;
10. concurrent EliteSCADA writes to two bits of the same register do not lose one another;
11. multiple bit TAGs sharing a register are not required to perform duplicate physical reads when they can share one poll result;
12. import/export/revision/package round-trip preserves bit binding configuration;
13. existing whole-register/coil/discrete-input Modbus behavior remains green;
14. every future driver with bit-addressable word/byte/register storage passes a common bit-read conformance test for its declared width/range;
15. every writable bit-capable future driver proves unrelated-bit preservation and local concurrent-write safety, while read-only protocol areas explicitly reject writes.

## 14. Implementation ordering

Do not broaden the current active Wave 08 worker missions.

This TAG-bit contract is a prerequisite for the queued typed visual-expression follow-up because expressions should be able to consume `Word_status.03` naturally.

Preferred order after the current Wave 08 interaction delivery is integrated:

`TAG bit access + driver bit binding -> typed visual expressions/boolean conditions/Analog Fill -> Wave 09`

The coordinator may combine the two follow-ups into one coherent integration wave if scope/CI remains manageable, but TAG/driver bit semantics must be stabilized before visual-expression syntax depends on them.
