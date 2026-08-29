# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. Read this with the mandatory current `main` documents, then verify live GitHub branch/PR/head/CI before acting.

**Handoff date:** 2026-08-29  
**Current stage:** `08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING`  
**Merged product:** **Wave 08 CLOSED / post-merge green**  
**Current status:** **FOLLOW-A ACTIVE — CONTRACT FROZEN / THREE PARALLEL WORKER SLICES**  
**CI policy:** `NORMAL`; Actions authorized with conservative usage

## Wave 08 closure

- final integration head: `9ea0eace15aa925133005f40e16403a2c0f3deb1`;
- final integration CI #531 / run `33236703599`: **SUCCESS**;
- PR #96 merged;
- main merge: `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- post-merge CI #533 / run `33236999366`: **SUCCESS**.

The final `core.polygon.points` Preview/Apply asymmetry was fixed before merge and has regression coverage.

## Follow-A live train

Canonical product contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Shared ContractSHA:

`9a8cb931cf851e6cad2ddb8d5efcae428db51d01`

No full Actions matrix was spent on the seed commits; they intentionally used `[skip ci]`.

## Architecture inspection completed before implementation

The actual merged code establishes these useful boundaries:

- Core `TagDefinition` remains protocol-independent;
- Engineering TAG physical configuration currently flows through public `TagEngineeringDto` and then into runtime planning;
- `EngineeringDriverCompiler` is the current Modbus compilation boundary;
- `ModbusPoint` owns resolved area/address/value-type semantics;
- `ModbusValueCodec` currently treats register Boolean as whole-register zero/non-zero and therefore must get an explicit selected-bit path;
- `ModbusTcpDriver` currently writes whole Holding Register values and therefore must branch for selected-bit writes;
- `ModbusTcpTransport` serializes one request at a time, but a read + write RMW pair needs a broader driver coordination gate to prevent another EliteSCADA write from interleaving;
- current Modbus poll-block construction already groups compatible overlapping/adjacent points, so multiple Boolean bit TAGs on the same register can share one physical read.

## Shared contract frozen on the integration branch

The coordinator seeded only the public seam, not behavior:

- `TagValueSelectorKind.Bit`;
- `TagValueSelector(Kind, Index)`;
- `TagValueReference(TagId, Selector?)` with stable Guid authority;
- `TagDefinition.AddressSelector` for a driver-independent structured selector over the physical source address;
- `TagEngineeringDto.AddressSelector` for public Engineering.

Friendly notation such as `Word_status.03` is presentation/authoring only. It must resolve to canonical TAG Guid + selector and must not be stored as the only identity.

Do not create another selector/reference model in a worker branch.

## Worker assignments — ACTIVE

The authoritative details are in `docs/CHAT-WORK-ASSIGNMENTS.md`. All branches start from exact ContractSHA `9a8cb931cf851e6cad2ddb8d5efcae428db51d01`.

### DEV 1 — Core logical semantics

Branch:

`feature/tag-bit-wave-08-follow-a-core`

Owns:
- Int16/Int32/Int64 bit-range semantics;
- zero-based LSB numbering and signed fixed-width two's-complement handling;
- Boolean projection preserving timestamp, quality, Source, SourceTimestamp and ServerTimestamp;
- logical bit set/clear preserving all unrelated bits;
- focused Core tests.

Must not touch Engineering, drivers, frontend or create a path-string identity model.

### DEV 2 — Engineering versioning/persistence

Branch:

`feature/tag-bit-wave-08-follow-a-engineering`

Owns:
- `AddressSelector` JSON/CSV round-trip;
- Preview validation;
- Apply -> `TagDefinition.AddressSelector` -> Export fidelity;
- schema-version/backward compatibility;
- focused Engineering tests.

Generic Engineering may validate Boolean/source/address/basic bounded index; protocol-specific Modbus area/range rules stay in the driver/compiler.

Must not hide public bit identity in metadata.

### DEV 3 — Modbus physical bit binding

Branch:

`feature/tag-bit-wave-08-follow-a-modbus`

Owns:
- compiler mapping of structured bit selector;
- `ModbusPoint` selected-bit validation;
- Holding/Input Register bit `0..15` reads;
- Input Register read-only rule;
- Holding Register bit writes that preserve all other bits;
- coordination so same-authority EliteSCADA writes cannot interleave inside a bit RMW;
- same-register read coalescing evidence;
- existing Coil/whole-register regression coverage.

Coordinated fresh-read RMW is acceptable. Do not require FC22 unconditionally because device support is not yet an explicit capability contract.

## Final Follow-A gate

The integrated candidate must prove:

1. stable logical reference = TAG Guid + structured bit selector;
2. Int16 bits `00..15`, Int32 `00..31`, Int64 `00..63` including sign bit;
3. bad/unavailable quality is preserved and never silently becomes good `false`;
4. logical bit write preserves unrelated bits;
5. public/versioned physical Boolean bit binding;
6. Modbus HR/IR selected-bit reads;
7. safe HR selected-bit writes preserving unrelated register bits;
8. concurrent EliteSCADA same-register bit writes do not lose updates;
9. shared/coalesced same-register reads remain intact;
10. JSON/CSV/Preview/Apply/revision/PostgreSQL/package fidelity;
11. Project Reference Tree and Development Monitor consume the canonical reference seam without a private `.NN` parser;
12. whole-register/Coil/DiscreteInput and prior-wave regressions remain green;
13. exact-head full CI and post-merge `main` health are green.

## Coordinator-owned work while workers are active

- perform early contract review as soon as a worker branch moves;
- do not duplicate active worker scope;
- inspect/prepare the shared Project Reference Tree + Development Monitor reference composition only;
- integrate accepted worker heads into `integration/tag-bit-access-wave-08-follow-a`;
- add shared Runtime/reference/catalog composition after Core semantics are stable;
- use focused tests first and reserve the full matrix for one coherent integrated checkpoint.

## Follow-up order

1. **08-FOLLOW-A** — ACTIVE.
2. **08-FOLLOW-B** — WAITING ON Follow-A; typed visual expressions/Boolean conditions/Analog Fill consume the canonical bit reference.
3. **Wave 09** — NOT ACTIVE until both mandatory follow-ups are green.

## Resume procedure

On coordinator `siga`:

1. reread current-main mandatory docs;
2. verify `main`, integration head, all three worker branch heads, PRs and CI;
3. review any worker delta immediately against ContractSHA;
4. reject duplicate selector models, metadata-only identity, free-form bit address authority or unsafe whole-register bit writes;
5. integrate coherent worker deliveries;
6. implement coordinator-owned shared reference/catalog seams only after Core contract behavior is proven;
7. run full CI only at a meaningful integrated candidate;
8. merge only green and verify post-merge `main` before activating Follow-B.
