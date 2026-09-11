# W14-C04 — Final DEV Handoff

**Package:** W14-C04 — TAG Source selector + address assistants + OPC UA discovery/browse  
**Branch:** `wave14/c04-tag-address-assistants`  
**Integration target:** `wave14/corrections-integration`  
**Required dependency/base:** C02 final SHA `7a0515289bdabba157fb1f645b32647746c83371` / PR #217  
**Validated product-code SHA:** `dc1c9e7beba19737713c7f29b61c661cb8e26a7c`  
**Authoritative intake PR:** #221  
**Validation-only PR:** #226 — must be closed without merge

This is the final DEV handoff for C04. It does not itself merge or accept the package into Wave 14 integration; the Coordinator owns intake and conflict resolution. GitHub refs and exact-SHA Actions remain authoritative.

## 1. Coordinator directives applied

C04 is the Wave 14 authority for:

- TAG Source selection;
- stable Source identity/reference semantics;
- Driver communication/configuration contracts relevant to TAG authoring;
- address assistants;
- OPC UA connection test, discovery and browse;
- migration from historical Source/address assumptions where required by the corrected contract.

The implementation preserves backend authority, backend-side authorization, Working -> Preview/Apply lifecycle, canonical Driver contracts and Runtime isolation. It does not deform the new model merely to keep the historical DEMO or stale fixtures compatible.

The later multilingual directive is also applied: modified C04 user-facing surfaces are covered in `pt-BR`, `en` and `es` while persisted protocol identifiers remain invariant.

## 2. Exact lineage

Final live comparison from required C02 base to product candidate:

- base / merge-base: `7a0515289bdabba157fb1f645b32647746c83371`;
- head: `dc1c9e7beba19737713c7f29b61c661cb8e26a7c`;
- status: ahead;
- ahead: `117` commits;
- behind: `0` commits.

PR #221 remains targeted at `wave14/corrections-integration`. The validation-only PR #226 targets `main` only to trigger universal PR gates and must not be merged.

## 3. Stable TAG -> Data Source identity

C04 adds stable Data Source identity to the TAG contract while retaining the textual Source key only as a compatibility/human-readable projection.

Implemented rules:

- `DataSourceId` is authoritative when present;
- unresolved stable GUIDs fail closed and never fall back to a coincident legacy key;
- key-only legacy TAGs remain resolvable for explicit migration;
- Apply enriches resolvable legacy TAGs with stable Source ID;
- Source rename is reconciled by GUID and refreshes the compatibility key;
- reselecting the same renamed Source preserves Address, AddressSelector and CommunicationBinding;
- selecting a genuinely different Source clears stale address/binding state;
- selecting no Source clears Address, AddressSelector and CommunicationBinding;
- deleted/unresolved references are shown explicitly as invalid.

The TAG editor uses a searchable Source selector populated only from configured Working Data Sources. Ordinary UI interaction no longer authors arbitrary Source free text.

## 4. Driver-owned TAG binding contract

The Driver catalog projects TAG binding identity separately from Data Source configuration identity:

- `tagBindingSchemaId`;
- `tagBindingSchemaVersion`;
- `configurationSchema.tagBindingFields`.

Drivers without a distinct TAG schema fall back to their configuration schema identity. IEC-104 deliberately retains its distinct point schema.

Frontend schema resolution is centralized; specialized assistants validate against backend `tagBindingFields`, while Drivers without custom UX use the schema-driven generic assistant. Protected material remains on the Data Source `secretReferences` boundary and is never moved into TAG binding settings.

## 5. Address authoring

Manual portable Address remains available for expert authoring and migration.

When a canonical CommunicationBinding exists:

- manual Address edits keep `Address` and `CommunicationBinding.PortableAddress` synchronized;
- clearing Address removes the binding rather than leaving an invalid envelope.

Specialized UX is registered for:

- Modbus TCP;
- OPC UA;
- DNP3 Master;
- IEC 60870-5-104.

Other Drivers with published `tagBindingFields` use the generic schema-driven assistant rather than protocol-specific React condition sprawl.

## 6. Modbus

C04 introduces a Driver-owned canonical Modbus address codec shared by Engineering and Runtime.

Canonical persistence remains zero-based `area:offset`. The assistant makes zero-based versus one-based user input explicit and does not guess human 4xxxx notation.

The backend builder validates area, reference, Unit ID, value type, word order, scale/offset and bit selector rules.

The Modbus descriptor publishes runtime-backed TAG binding fields:

- `modbus.unitId`;
- `modbus.valueType`;
- `modbus.wordOrder`;
- `modbus.scale`;
- `modbus.offset`.

New assistant output carries canonical CommunicationBinding. Legacy metadata is still mirrored because the current Runtime compiler consumes that representation. This is real runtime/data compatibility, not a historical fixture shim. Bit selection remains the canonical AddressSelector.

## 7. DNP3

The DNP3 assistant authors canonical `dnp3:<pointKind>:<index>` identity plus Runtime-backed settings.

Before binding creation it validates point kind, index bounds, writable semantics and command mode against the live backend catalog. Display labels are localized while persisted protocol tokens remain unchanged.

## 8. IEC 60870-5-104

IEC-104 retains TAG binding schema:

- `elite.iec60870.5.104.point`;
- version `1`.

The Runtime composition registers the enriched descriptor. The Type-ID codec preserves the established Runtime binding vocabulary while accepting standard underscore IEC names and numeric IDs at compatibility/protocol edges.

The descriptor advertises only monitored Type IDs actually supported by the decoder and command Type IDs actually implemented. The assistant validates Type ID, command Type ID, command mode and qualifier bounds against the backend catalog.

## 9. OPC UA Engineering tooling

C04 exposes capability-driven Engineering tooling for configured OPC UA Data Sources:

- connection test;
- endpoint discovery;
- browse / browse-next;
- loaded-node search;
- current-TAG selection;
- multi-selection;
- bulk TAG candidate creation.

C04 additionally closes the new-Source discovery loop:

`new OPC UA Data Source -> discovery URL -> Discover -> choose endpoint/security -> Test Connection on draft -> Preview/Apply`

Draft tooling uses request-owned transient providers when no persisted DataSourceId exists. Persisted Sources continue to use stable GUID scope and cached provider state where Browse/BrowseNext requires it.

Security/lifecycle boundaries:

- Engineering Read authorization required for test/discovery/browse;
- configured Sources resolve by stable GUID;
- draft providers are transient and disposed;
- Runtime session/security material boundaries are reused;
- protected references remain backend-side;
- discovery/test never mutate Working or active Runtime;
- suggested settings enter only the local draft after explicit user selection;
- settings outside the backend schema and protected keys are rejected/ignored;
- unexpected provider exceptions are sanitized before returning to the browser.

Discovery/browse results become product configuration only through canonical Preview/Apply.

Bulk candidates preserve Source key + DataSourceId, portable Address, canonical CommunicationBinding, backend TAG schema identity, suggested data type/unit/access and stable discovery identity metadata.

## 10. Multilingual audit

C04 user-facing copy and protocol labels are centralized and covered in:

- `pt-BR`;
- `en`;
- `es`.

Covered surfaces include Source selector, Address/Modbus assistant, generic binding assistant, DNP3, IEC-104, OPC UA TAG tooling and OPC UA Data Source discovery/test tooling.

The shared Data Source catalog now resolves backend `DisplayNameResourceKey` / `DescriptionResourceKey` through the frontend resource resolver with invariant fallback. Modbus and OPC UA descriptors publish resource keys through the canonical Driver contract. No parallel Driver-specific metadata catalog was introduced in React.

Persisted Driver keys, schema IDs, enum/protocol tokens and settings remain invariant across locale changes.

## 11. Historical DEMO decision

The historical DEMO is not an acceptance authority for C04.

The Source browser acceptance test that depended on `builtin.simulation` / `Demo.*` was replaced with an isolated canonical schema-v15 fixture. The replacement still proves the stronger contract:

- searchable configured Source;
- stable GUID selection;
- compatibility Source key;
- Preview round-trip;
- no Workspace mutation during Preview.

No product adapter was added merely to preserve the old fixture.

Compatibility intentionally preserved includes legacy key-only Source migration, canonical manual Addresses and the Modbus metadata bridge required by the current Runtime compiler.

## 12. Coverage

Backend/contract coverage includes:

- legacy Source migration to stable ID;
- rename by stable ID;
- orphaned ID without key fallback;
- Runtime Source normalization;
- canonical Modbus parse/build round trips;
- Modbus TAG binding descriptor fields;
- Driver catalog TAG schema projection;
- Driver resource-key projection;
- IEC-104 binding vocabulary/compatibility;
- Engineering tooling authorization and provider ownership/disposal boundaries.

Browser/TypeScript coverage includes:

- Source GUID + compatibility key through Preview;
- Source rename/reselection and real Source-change cleanup;
- unresolved/deleted Source behavior;
- manual Address/binding convergence;
- Modbus legacy metadata + canonical binding convergence;
- specialized-assistant registry + generic schema fallback;
- OPC UA binding identity when Data Source and TAG schema IDs differ;
- configured OPC UA connection test/discovery;
- new OPC UA Source discovery -> endpoint/security selection -> draft test -> Preview;
- OPC UA browse, two-node multi-select, bulk candidate generation, Preview and Apply version boundary;
- Driver catalog resource-key localization;
- `pt-BR -> en -> es` switching without changing canonical identifiers.

## 13. Exact validation evidence

**Validated product-code SHA:** `dc1c9e7beba19737713c7f29b61c661cb8e26a7c`.

All required validation workflows associated with this exact SHA completed successfully:

### EliteSCADA CI

- workflow run: #1220 / `33764803698`;
- conclusion: **SUCCESS**;
- Web build: SUCCESS;
- backend build: SUCCESS;
- .NET tests: SUCCESS;
- Runtime smoke: SUCCESS;
- Chromium end-to-end: SUCCESS;
- Playwright result: **375 passed**;
- Playwright artifact ID: `9897402438`.

The first backend attempt on this same SHA encountered two pre-existing timing/teardown flakes outside C04 (Modbus diagnostics counter timing and S7 test-server socket cancellation). The failed backend job was rerun on the **same SHA**, then completed build, all .NET tests and Runtime smoke successfully. No C04 product code was altered to mask those flakes.

### L3 Seven-Driver Lab

- run #127 / `33764803734`;
- conclusion: **SUCCESS**;
- seven peers/startup/control plane: SUCCESS;
- heterogeneous Gateway slice: SUCCESS;
- seven-Driver acquisition: SUCCESS;
- supported writes: SUCCESS;
- serial fault/recovery: SUCCESS;
- Gateway source/destination fault/recovery: SUCCESS.

### Wave 11 Active HMI Runtime

- run #150 / `33764803633`;
- conclusion: **SUCCESS**.

### Preview Licensing CI

- run #172 / `33764803804`;
- conclusion: **SUCCESS**;
- licensing/capacity tests: SUCCESS;
- Runtime/host licensing smoke: SUCCESS;
- Windows x64 License Generator publish/smoke: SUCCESS.

### Wave 14 C03 DNP3 Adapter

- run #83 / `33764805573`;
- conclusion: **SUCCESS**;
- managed protocol tests/convergence: SUCCESS;
- native Linux: SUCCESS;
- native Windows x64: SUCCESS;
- OpenDNP3 <-> dnp3py L3 interop: SUCCESS;
- Windows commercial publish dependency gate: SUCCESS.

## 14. Architecture/security review

Final DEV review found no C04-specific reason to block coordinator intake:

- backend remains authority for Driver catalog and validation;
- Source identity is stable-ID-first and fails closed;
- discovery/test/browse do not mutate Runtime;
- protected material is not exposed to browser-owned configuration;
- provider failures are sanitized;
- Preview/Apply/CAS remains the only persistence path;
- assistant specialization is registry-based with schema-driven fallback;
- no historical-DEMO shim was introduced;
- locale changes do not alter canonical data.

## 15. Integration notes for Coordinator

1. Re-fetch PR #221 and branch HEAD before intake.
2. Treat `dc1c9e7beba19737713c7f29b61c661cb8e26a7c` as the exact validated **product-code SHA**. Documentation-only commits after it do not supersede that product baseline.
3. Preserve C02 dependency or confirm it is already present in integration.
4. C04 is authoritative for Source/communication/address/discovery conflicts with older consumers.
5. Do not restore historical DEMO assumptions while resolving conflicts.
6. Re-run C04 browser/contract coverage if shared Engineering/catalog files conflict during intake.
7. PR #226 is validation-only and must be closed without merge.
8. PR #221 remains the authoritative intake surface for `wave14/corrections-integration`.
9. Coordinator acceptance/integration, combined Wave 14 audit, canonical DEMO regeneration and clean Codespace/browser homologation remain coordinator-owned steps.

## 16. DEV status

**C04 DEV implementation: COMPLETE / HANDOFF READY.**

This statement means the package is ready for Coordinator intake with exact-SHA green evidence. It does not mean it has already been integrated or accepted into the Wave 14 integration branch.
