# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-29  
**Merged product state:** **WAVE 08 CLOSED / POST-MERGE GREEN**  
**Active development state:** **08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING / PARALLEL IMPLEMENTATION**  
**CI mode:** **NORMAL — Actions authorized with conservative usage**

## Mandatory resume reading

Before any action read current `main`:

- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/DEVELOPMENT-WAVES.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `docs/CI-USAGE-POLICY.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/COORDINATOR-HANDOFF.md`
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`
- current assignment `MustReadSpecific`.

Then verify live GitHub branch/PR/head/CI. GitHub is operational truth.

## Wave 08 — CLOSED

- final integration head: `9ea0eace15aa925133005f40e16403a2c0f3deb1`;
- final integration CI #531 / run `33236703599`: **SUCCESS**;
- PR #96: **MERGED**;
- `main` merge commit: `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- post-merge CI #533 / run `33236999366`: **SUCCESS**.

The final Wave 08 polygon Preview/Apply defect was fixed before merge: `core.polygon.points` remains structural geometry and is no longer sent through the scalar visual-property codec. Regression coverage is included.

## Current active work — 08-FOLLOW-A

Canonical product contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Shared ContractSHA:

`9a8cb931cf851e6cad2ddb8d5efcae428db51d01`

### Architecture inspection completed

The merged architecture was inspected before implementation:

- Core `TagDefinition` is protocol-independent;
- Engineering currently persists TAG physical identity through `Source + Address` and public DTOs;
- `EngineeringDriverCompiler` converts those DTOs into runtime `ModbusPoint` definitions;
- Modbus polling already coalesces compatible overlapping/adjacent points, so several bit TAGs on one register can share one physical register read;
- Modbus Holding Register writes currently encode/write the full register and therefore require an explicit bit-write branch to avoid clobbering unrelated bits;
- `ModbusTcpTransport` serializes individual requests but not an entire read-modify-write pair, so same-authority bit writes need driver-level coordination around the full RMW sequence.

### Shared canonical implementation seam frozen

The Follow-A integration branch now contains a small coordinator-owned seed:

- `TagValueSelectorKind.Bit`;
- `TagValueSelector(Kind, Index)`;
- `TagValueReference(TagId, Selector?)`, where the Guid is authoritative identity;
- `TagDefinition.AddressSelector` for a structured driver-independent selector over the physical source address;
- `TagEngineeringDto.AddressSelector` for the public Engineering representation.

Friendly references such as `Word_status.03` remain authoring/display notation only. They must resolve to canonical TAG Guid + structured bit selector and must never become the only persisted identity.

No full CI was spent on this seed; the three seed commits use `[skip ci]`. Worker implementation/focused tests will provide the first behavior evidence.

## Worker state — ACTIVE

All three worker branches were created from exact ContractSHA `9a8cb931cf851e6cad2ddb8d5efcae428db51d01` and are explicitly authorized in `docs/CHAT-WORK-ASSIGNMENTS.md`.

### DEV 1 — Core logical bit semantics

Branch:

`feature/tag-bit-wave-08-follow-a-core`

Owns logical Int16/Int32/Int64 bit read/write semantics, fixed-width two's-complement behavior, quality/timestamp inheritance and focused Core tests.

### DEV 2 — Engineering persistence/validation

Branch:

`feature/tag-bit-wave-08-follow-a-engineering`

Owns `AddressSelector` JSON/CSV/Preview/Apply/Export/schema compatibility and focused Engineering tests. Public bit identity must not be hidden in metadata.

### DEV 3 — Modbus physical register-bit binding

Branch:

`feature/tag-bit-wave-08-follow-a-modbus`

Owns compiler mapping, ModbusPoint/codec/read/write behavior, safe coordinated Holding Register RMW, Input Register read-only semantics, shared read behavior and focused driver tests.

Workers must not merge their own PRs or broaden scope. After delivery they stop at `WAIT_FOR_COORDINATOR`.

## Follow-A required final outcome

1. canonical integer bit selector by TAG Guid + bit index;
2. Int16/Int32/Int64 low/high/sign-bit correctness;
3. source quality/timestamp/source timestamps preserved;
4. logical bit writes preserve all unrelated bits;
5. direct physical Boolean bit binding represented publicly/versionably;
6. Modbus HR/IR bit `0..15` reads correct;
7. HR bit writes preserve unrelated register bits and coordinate EliteSCADA writes;
8. same-register physical reads remain coalesced where practical;
9. Engineering JSON/CSV/Preview/Apply/revision/PostgreSQL/package fidelity;
10. shared Project Reference Tree/Development Monitor can consume the canonical seam without private `.NN` parsing;
11. existing whole-register/Coil/DiscreteInput and prior-wave regressions stay green.

## Ordered work after 08-FOLLOW-A

1. **08-FOLLOW-B** — Typed Visual Expressions + Boolean Conditions + Analog Fill, consuming the canonical TAG-bit reference semantics;
2. **Wave 09** — remains NOT ACTIVE until both mandatory follow-ups are green.

## Next coordinator execution

1. verify the three worker branches/PRs and perform early contract review immediately when they move;
2. reject duplicate selector/reference models, metadata-only identity or free-form bit-address authority;
3. integrate accepted worker heads into `integration/tag-bit-access-wave-08-follow-a`;
4. add coordinator-owned Project Reference Tree / Development Monitor / shared Runtime reference composition after Core semantics are stable;
5. run focused validation during integration;
6. spend one full matrix only at a meaningful coherent Follow-A candidate;
7. merge only green and confirm post-merge `main` before activating 08-FOLLOW-B.
