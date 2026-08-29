# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-29 — Wave 08 is CLOSED / MERGED / POST-MERGE GREEN. `08-FOLLOW-A` is ACTIVE. The canonical TAG selector seed is frozen at `9a8cb931cf851e6cad2ddb8d5efcae428db51d01`; DEV 1/2/3 are now ACTIVE on three non-overlapping bounded slices.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/COORDINATOR-HANDOFF.md`, and current MustReadSpecific documents. Then verify real branch/head/PR/CI and execute only the current authorized assignment.

## Current product gate

`08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING` is **ACTIVE**.

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Shared ContractSHA:

`9a8cb931cf851e6cad2ddb8d5efcae428db51d01`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Canonical contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

### Shared implementation seed now frozen

The integration train now defines one shared public seam before worker implementation:

- `TagValueReference(TagId, Selector?)` uses canonical TAG Guid identity;
- `TagValueSelector(Kind, Index)` currently declares the public `Bit` selector kind;
- `TagDefinition.AddressSelector` carries a driver-independent structured selector over the physical source address;
- `TagEngineeringDto.AddressSelector` carries the same structured selector through public Engineering;
- friendly `.NN` syntax remains authoring/display only and is not persisted identity.

Workers MUST extend these shared types rather than introducing a second selector/reference model or encoding bit identity in free-form metadata/strings.

### Wave 08 closure evidence

Final integration head:

`9ea0eace15aa925133005f40e16403a2c0f3deb1`

- final integration CI #531 / run `33236703599`: **SUCCESS**;
- replacement final PR #96: **MERGED**;
- main merge: `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- post-merge CI #533 / run `33236999366`: **SUCCESS**.

Draft PR #90 was closed unmerged only because the available connector failed while removing Draft state; #96 used the exact same branch/head and merged normally.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `08-FOLLOW-A`  
**Status:** `ACTIVE — CONTRACT FROZEN / PARALLEL IMPLEMENTATION`  
**LogicalBaseSHA:** `bfd17d035d905e9bcae263f68244cfb2b6453aa2`  
**ContractSHA:** `9a8cb931cf851e6cad2ddb8d5efcae428db51d01`  
**IntegrationBranch:** `integration/tag-bit-access-wave-08-follow-a`

**CurrentTask:** protect the shared selector/reference contract, perform early contract review of worker deliveries, implement only coordinator-owned integration seams, then integrate Core + Engineering + Modbus evidence into one exact-head Follow-A gate.

**MustReadSpecific:**
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `docs/COORDINATOR-HANDOFF.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `src/Scada.Core/Tags/TagValueReference.cs` from ContractSHA or integration branch;
- current canonical TAG contracts/registries/current-value models;
- current Engineering import/export/validation/persistence TAG schemas;
- current Modbus point/address/codec/poll/write implementation;
- current Project Reference Tree / Development Monitor reference contracts.

**AllowedScope:** Follow-A integration branch; shared contract arbitration; integration/cherry-pick/merge of accepted worker commits; Runtime/reference/catalog composition not owned by workers; security/Audit adjustments required by bit access; focused and final tests; Follow-A PR/CI/merge.

**ForbiddenScope:** implementing 08-FOLLOW-B expression language/Analog Fill; Wave 09 Screen navigation/Popup/Dynamo/Historical Data Browser implementation; new unrelated protocols; Server Python; monitor-private or visual-private `.NN` parsing; unsafe whole-register overwrite for Boolean register bits; duplicating worker implementation while their assignment remains ACTIVE.

**CompletionCriteria:**
- logical Int16/Int32/Int64 bit selectors are canonical and stable by TAG identity + bit index;
- quality/timestamp/source timestamps and signed fixed-width semantics are correct;
- direct physical Boolean bit binding is represented publicly/versionably;
- Modbus Holding/Input Register bit reads are correct;
- Holding Register bit writes preserve unrelated bits and coordinate same-register EliteSCADA writes;
- shared/coalesced physical reads are retained where practical;
- import/export/Preview/Apply/revision/PostgreSQL/package fidelity is green;
- Project Reference Tree/Development Monitor can consume the canonical bit seam without a private parser;
- existing whole-register/Coil/DiscreteInput and prior Wave regressions remain green;
- final exact-head CI and post-merge `main` health are green.

**NextActions:**
1. review worker branches/PRs as soon as they move;
2. reject any private bit syntax, duplicate selector type or driver-only persistence authority;
3. integrate coherent worker heads into the Follow-A train;
4. add coordinator-owned shared reference/catalog composition after Core semantics are green;
5. use focused validation while slices are independent;
6. run the full matrix only after the integrated Follow-A candidate is coherent.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `08-FOLLOW-A`  
**Status:** `ACTIVE — AUTHORIZED`  
**Branch:** `feature/tag-bit-wave-08-follow-a-core`  
**Base/ContractSHA:** `9a8cb931cf851e6cad2ddb8d5efcae428db51d01`

**CurrentTask:** implement canonical logical TAG bit semantics in Core using the frozen `TagValueReference` / `TagValueSelector` seam.

**MustReadSpecific:**
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `src/Scada.Core/Tags/TagValueReference.cs`
- `src/Scada.Core/Tags/TagDefinition.cs`
- `src/Scada.Core/Tags/TagValue.cs`
- `src/Scada.Core/Tags/TagDataType.cs`

**AllowedScope:**
- `src/Scada.Core/Tags/TagValueReference.cs` only to add canonical validation/display helpers consistent with the seed;
- new focused Core TAG-bit implementation files under `src/Scada.Core/Tags/**`;
- focused tests under `tests/Scada.Core.Tests/TagBit*.cs`.

**Required behavior:**
- Int16 bits `00..15`, Int32 `00..31`, Int64 `00..63`;
- bit 0 = LSB; signed values use fixed-width two's-complement representation including sign bit;
- projection returns Boolean while preserving authoritative TAG timestamp, quality, Source, SourceTimestamp and ServerTimestamp;
- bad/unavailable source quality is never silently converted into a good `false`;
- mutation helper for a writable logical integer bit sets/clears only that bit and preserves every other bit;
- friendly display formatter may emit `.NN`, but no path parser may become canonical identity;
- invalid type/index fails closed.

**ForbiddenScope:** Engineering DTO/schema/import/export; Modbus/driver code; frontend Project Reference Tree/Development Monitor; visual bindings/expressions; new TAG data types; metadata-based bit identity; editing `main`.

**CompletionCriteria:** focused tests prove Int16/Int32/Int64 low/intermediate/high/sign bits, quality/timestamp inheritance, stable Guid+selector reference, set/clear preservation, invalid ranges/types. Existing Core tests remain green in the worker's focused checkpoint.

**AfterCompletion:** open/update a Draft PR when useful, report exact delivery head/evidence, then `WAIT_FOR_COORDINATOR`. Do not merge.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `08-FOLLOW-A`  
**Status:** `ACTIVE — AUTHORIZED`  
**Branch:** `feature/tag-bit-wave-08-follow-a-engineering`  
**Base/ContractSHA:** `9a8cb931cf851e6cad2ddb8d5efcae428db51d01`

**CurrentTask:** make `TagEngineeringDto.AddressSelector` a fully versioned Engineering field with validation and round-trip fidelity.

**MustReadSpecific:**
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`
- `src/Scada.Engineering/ImportExport/EngineeringDtoMapper.cs`
- `src/Scada.Engineering/ImportExport/EngineeringCsvExchange.cs`
- `src/Scada.Engineering/ImportExport/Handlers/TagEngineeringHandler.cs`
- `src/Scada.Engineering/Validation/EngineeringValidator.cs`

**AllowedScope:**
- TAG-related Engineering mapper/handler/validator/import-export files above;
- schema-version compatibility required for this field;
- focused TAG Engineering tests under `tests/Scada.Core.Tests/EngineeringTagBit*.cs` or a clearly equivalent existing Engineering test file.

**Required behavior:**
- bump public Engineering schema only if required by the implementation, retaining backward compatibility with v13 asset packages;
- JSON and TAG CSV round-trip structured selector without relying on `metadata` or concatenated address text;
- Apply -> registry -> Export preserves selector through `TagDefinition.AddressSelector`;
- physical AddressSelector requires a Boolean logical TAG, a source/address and a non-negative bounded bit index at generic Engineering level;
- protocol-specific range/area rules remain driver responsibility;
- malformed/unsupported selector fails Preview closed;
- old packages without selector continue to parse/apply unchanged.

**ForbiddenScope:** changing the frozen Core selector shape without coordinator approval; Modbus/compiler/runtime driver code; frontend; visual expression work; metadata as public bit authority; editing `main`.

**CompletionCriteria:** focused tests prove JSON/CSV/Preview/Apply/Export fidelity, invalid selector rejection, and v13 compatibility. Existing Engineering Core tests stay green in the focused checkpoint.

**AfterCompletion:** open/update a Draft PR when useful, report exact delivery head/evidence, then `WAIT_FOR_COORDINATOR`. Do not merge.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `08-FOLLOW-A`  
**Status:** `ACTIVE — AUTHORIZED`  
**Branch:** `feature/tag-bit-wave-08-follow-a-modbus`  
**Base/ContractSHA:** `9a8cb931cf851e6cad2ddb8d5efcae428db51d01`

**CurrentTask:** implement direct Modbus register-bit Boolean binding using the frozen structured selector.

**MustReadSpecific:**
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `src/Scada.DriverHost/Engineering/EngineeringDriverCompiler.cs`
- `src/Scada.Drivers/Modbus/ModbusPoint.cs`
- `src/Scada.Drivers/Modbus/ModbusValueCodec.cs`
- `src/Scada.Drivers/Modbus/ModbusTcpDriver.cs`
- `src/Scada.Drivers/Modbus/ModbusTcpTransport.cs`
- `tests/Scada.Drivers.Tests/EngineeringDriverCompilerTests.cs`
- `tests/Scada.Drivers.Tests/ModbusTcpDriverTests.cs`
- `tests/Scada.Drivers.Tests/TestModbusTcpServer.cs`

**AllowedScope:**
- `src/Scada.DriverHost/Engineering/EngineeringDriverCompiler.cs`;
- `src/Scada.Drivers/Modbus/**` only as required for register-bit support;
- focused driver tests under `tests/Scada.Drivers.Tests/*Modbus*` and `EngineeringDriverCompilerTests.cs`.

**Required behavior:**
- HoldingRegister/InputRegister structured bit selector `0..15` produces Boolean TAG values;
- Coil remains native Boolean and must not accept a register-bit selector; DiscreteInput remains native read-only Boolean;
- InputRegister bit binding is read-only;
- HoldingRegister bit writes preserve all unrelated bits;
- coordinate EliteSCADA writes so simultaneous bit writes on the same authoritative register cannot lose one another; coordinated fresh-read RMW is acceptable and preferred over unconditionally requiring FC22 on devices that may not support it;
- existing whole-register writes must not interleave unsafely with the bit RMW authority;
- same-register bit points continue to share/coalesce physical poll reads through the existing block model;
- communication failure quality remains BadCommunication rather than false;
- no `400001.7`/free-form bit parsing as persistence authority.

**ForbiddenScope:** Core selector redesign; Engineering schema/import/export; frontend; FOLLOW-B expressions; unrelated Modbus refactors; assuming FC22 support without an explicit capability contract; editing `main`.

**CompletionCriteria:** focused tests prove compiler mapping, HR/IR bit reads, invalid bit/area rejection, HR bit write preserves other bits, concurrent same-register bit writes do not lose updates, coalesced same-register read, and existing Coil/whole-register regressions remain green.

**AfterCompletion:** open/update a Draft PR when useful, report exact delivery head/evidence, then `WAIT_FOR_COORDINATOR`. Do not merge.

## Follow-up ordering

1. `08-FOLLOW-A` — ACTIVE now.
2. `08-FOLLOW-B` — WAITING ON Follow-A.
3. Wave 09 — NOT ACTIVE until both mandatory follow-ups are green.
