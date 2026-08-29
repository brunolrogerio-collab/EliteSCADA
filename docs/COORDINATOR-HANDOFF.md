# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. Read this with the mandatory current `main` documents, then verify live GitHub branch/PR/head/CI before acting.

**Handoff date:** 2026-08-29  
**Current stage:** `08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING`  
**Merged product:** **Wave 08 CLOSED / post-merge green**  
**Current status:** **FOLLOW-A INTEGRATED CANDIDATE — CI IN PROGRESS / NOT MERGED**  
**CI policy:** `NORMAL`; Actions authorized with conservative usage

## Wave 08 closure

- final integration CI #531 / run `33236703599`: **SUCCESS**;
- PR #96 merged;
- main merge: `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- post-merge CI #533 / run `33236999366`: **SUCCESS**.

## Follow-A live train — authoritative checkpoint

Canonical product contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Logical product BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Current exact integration head at chat handoff:

`4e8a3c76753c1ead815c790407601852c6f888e3`

Central Draft PR:

**#105 — FOLLOW-A: TAG bit access and Modbus bit-level binding**

Exact-head CI at handoff:

**CI #541 / run `33255556172` — IN PROGRESS** on `4e8a3c76753c1ead815c790407601852c6f888e3`.

Do not modify the integration head until #541 finishes unless a proven blocker requires it. Inspect job/log evidence first.

## Important Git topology state

At handoff:

- `main` head before this handoff-doc commit was `10e25b6d1de436ea86cca47096bd7629b2765773`;
- integration and main diverge from Wave 08 merge base `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- integration is ahead with Follow-A product work;
- main is ahead with documentation/product-north updates including Reporting, mandatory PDF/XLSX and DNP3 as a future driver;
- before final Follow-A merge, reconcile current `main` into the integration train without dropping either product code or current documentation.

## Worker slices — ALREADY INTEGRATED

Do **not** integrate these again.

Accepted exact worker delivery heads were:

- DEV 1 Core: `80b0911eda92b96a7c1945307cea402cf9ad4417`, CI #534 SUCCESS;
- DEV 2 Engineering: `bcd65ac4ee81ac399fb2d1882e426d90f419a225`, CI #537 SUCCESS;
- DEV 3 Modbus: `e0365c6dd0d81cd938ccc41e44b208726c73392e`, CI #535 SUCCESS.

They are already composed into the current integration train. Worker scope is no longer the next action.

## Follow-A implementation currently present in integration

### Core / logical TAG bit semantics

- `TagValueSelectorKind.Bit`;
- `TagValueSelector(Kind, Index)`;
- `TagValueReference(TagId, Selector?)` with stable Guid authority;
- Int16/Int32/Int64 zero-based LSB bit semantics;
- fixed-width two's-complement behavior including sign bit;
- Boolean projection preserves source quality/timestamps/context;
- logical bit writes preserve unrelated bits.

### Engineering / persistence

- `TagDefinition.AddressSelector` and `TagEngineeringDto.AddressSelector`;
- structured AddressSelector JSON/CSV/Preview/Apply/Export support;
- public `EngineeringBindingDto.TagReference` / frontend `BindingEngineering.tagReference` seam for stable logical TAG value references;
- concrete TAG binding validation prefers TagId + selector over friendly path;
- rename stability: friendly `Target` may change, authoritative TagId remains the same;
- bit range validated against TAG data width;
- schema-v13 content without selectors remains backward compatible.

### Modbus physical bit binding

- Holding/Input Register selected-bit reads `0..15`;
- Input Register selected-bit bindings are read-only;
- Holding Register bit writes use coordinated fresh-read RMW preserving unrelated bits;
- same-authority EliteSCADA writes are serialized to prevent lost updates;
- same-register physical reads remain coalesced where practical;
- whole-register/Coil behavior remains separate.

### Shared web reference seam

Current integration includes coordinator-owned work for:

- Project Reference catalog carrying stable `tagId` and bit-selector capability without expanding every integer TAG into 16/32/64 permanent tree nodes;
- friendly `.NN` resolver producing a Boolean derived source with `{ tagId, selector:{kind:'bit', index} }`;
- invalid/out-of-range bit suffixes fail closed;
- Binding Editor deduplication and persistence by stable TagId + selector when available;
- Binding Editor supports on-demand bit source authoring rather than coercing the whole integer TAG to Boolean;
- Development Monitor uses stable TAG identity and shared TAG-bit value projection;
- shared web bit projection preserves Quality/timestamps and does not map unavailable/bad data to false.

Latest integration commits include monitor quick-add stabilization and shared web TAG-bit projection. Re-fetch actual files/head before editing.

## Friendly notation versus authority

`Word_status.03` is authoring/display syntax only.

Canonical identity is:

`TagId + { kind: bit, index: 3 }`

Do not introduce a second private `.NN` parser, metadata-only identity, or path-only bit persistence.

## Final Follow-A gate

The final candidate must prove:

1. stable logical reference = TAG Guid + structured bit selector;
2. Int16 bits `00..15`, Int32 `00..31`, Int64 `00..63`, including sign bit;
3. bad/unavailable quality preserved, never silently converted to good `false`;
4. logical bit writes preserve unrelated bits;
5. structured/versioned physical Boolean bit binding;
6. Modbus HR/IR selected-bit reads;
7. safe HR selected-bit writes preserving unrelated register bits;
8. concurrent same-register EliteSCADA writes do not lose updates;
9. same-register reads remain coalesced where practical;
10. JSON/CSV/Preview/Apply/revision/PostgreSQL/package fidelity;
11. Project Reference Tree, Binding Editor and Development Monitor use the same canonical reference seam;
12. existing whole-register/Coil/DiscreteInput and prior-wave regressions remain green;
13. exact-head full CI green;
14. reconcile current `main`, final PR merge, then post-merge `main` CI green.

## Permanent future-driver rule

Bit access is driver-independent product direction.

Every future production driver exposing bit-addressable byte/word/register/integer storage must publish structured bit capability. If writable, bit writes must preserve unrelated bits via native atomic/mask operation where available or coordinated RMW otherwise. Read-only protocol areas remain read-only.

Future driver roadmap includes MQTT, OPC UA, BACnet, S7, Allen-Bradley and **DNP3** as applicable to each protocol's data model/capabilities. DNP3 is post-v0.1 and is not active development now.

## Wave 09 locked additions

Wave 09 remains NOT ACTIVE until Follow-A and Follow-B are green.

It already includes:

- Screens / Popups / Dynamos / navigation;
- Historical Data Browser / alarm history / historian queries;
- first-class Reporting and Report Designer;
- mandatory **PDF (`.pdf`)** report export;
- mandatory **Microsoft Excel (`.xlsx`)** report export with typed cells where practical.

Repository documentation contains only EliteSCADA generic requirements, not names of external products used during research.

## Next coordinator actions in a new chat

On `siga`:

1. read mandatory current-main documents;
2. verify live `main`, integration head, PR #105 and CI #541;
3. if #541 failed, inspect only failing job/log and fix proven cause; do not reassurance-rerun unchanged head;
4. if #541 is green, review exact-head Follow-A acceptance and inspect PR #105 diff;
5. reconcile current `main` documentation into integration while preserving Follow-A code;
6. run a final exact-head CI only if reconciliation changes the tested tree materially;
7. make the final non-Draft merge path if the connector Draft limitation still exists, using the already-established replacement-PR procedure rather than weakening gates;
8. merge Follow-A only when green and verify post-merge `main` health;
9. mark Follow-A CLOSED, update handoff/assignments;
10. then activate **08-FOLLOW-B — Typed Visual Expressions + Boolean Conditions + Analog Fill**;
11. Wave 09 stays blocked until Follow-B is green.
