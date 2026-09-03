# W14-C04 — Implementation Handoff

**Package:** W14-C04 — TAG Source selector + address assistants + OPC UA discovery/browse  
**Branch:** `wave14/c04-tag-address-assistants`  
**Integration target:** `wave14/corrections-integration`  
**Required dependency/base:** C02 final SHA `7a0515289bdabba157fb1f645b32647746c83371` / PR #217  
**Code-candidate SHA before this documentation commit:** `ed95008176e1bb242f165efb987f16ece03c381c`  
**Draft PR:** #221

This is a DEV handoff, not an acceptance declaration. GitHub refs and exact-SHA validation remain authoritative.

## 1. Coordinator directive applied

The final review follows the binding Wave 14 decision recorded in issue #211 comment `5520438147` and PR #221 comment `5520438393`.

C04 is the Wave 14 authority for:

- TAG Source selection;
- stable Source identity/reference semantics;
- Driver communication/configuration contracts relevant to TAG authoring;
- address assistants;
- discovery/browse tooling.

The implementation preserves backend authority, backend-side authorization, canonical Working -> Preview/Apply lifecycle, shared contracts and Driver isolation. It does not add compatibility shims merely to keep the historical DEMO fixture working.

## 2. Dependency lineage

Live comparison at code-candidate SHA `ed95008176e1bb242f165efb987f16ece03c381c` confirmed:

- merge-base exactly `7a0515289bdabba157fb1f645b32647746c83371`;
- C04 `90` commits ahead of that base;
- C04 `0` commits behind that base.

The PR remains targeted at `wave14/corrections-integration`.

## 3. Stable TAG -> Data Source identity

C04 adds stable Data Source identity to the TAG contract while retaining the legacy Source key as a compatibility/human-readable projection.

Rules implemented:

- `DataSourceId` is authoritative when present;
- unresolved stable GUIDs fail closed and never fall back to a coincident legacy key;
- key-only legacy TAGs remain resolvable for migration;
- Apply enriches resolvable legacy TAGs with stable Source ID;
- Source rename is reconciled by GUID and refreshes the compatibility key;
- reselecting the same renamed Source preserves Address, AddressSelector and CommunicationBinding;
- selecting a genuinely different Source clears stale protocol address/binding state;
- selecting no Source clears stale Address, AddressSelector and CommunicationBinding;
- deleted/unresolved references are shown explicitly as invalid.

The browser Source field is a searchable selector populated only from configured Working Data Sources. Ordinary UI interaction cannot create an arbitrary Source reference.

## 4. Driver-owned TAG binding contract

The Driver catalog projects TAG binding schema identity separately from Data Source configuration schema identity:

- `tagBindingSchemaId`;
- `tagBindingSchemaVersion`;
- `configurationSchema.tagBindingFields`.

Drivers without a distinct TAG binding contract fall back to their configuration schema identity. IEC-104 deliberately retains a distinct point schema.

Frontend schema resolution is centralized in `TagBindingSchema.ts`. Specialized assistants validate choices against backend `tagBindingFields`; generic Drivers use `GenericTagBindingAssistant` instead of protocol switches or duplicated React catalogs.

Protected material remains on the Data Source `secretReferences` boundary and is not authored into TAG binding settings.

## 5. Address authoring

Manual portable Address remains available for expert authoring and real-data migration.

When a canonical `CommunicationBinding` exists:

- manual Address edits keep `Address` and `CommunicationBinding.PortableAddress` synchronized;
- clearing Address removes the binding rather than leaving an invalid envelope.

Specialized UX is centrally registered only where useful:

- Modbus TCP;
- OPC UA;
- DNP3 Master;
- IEC 60870-5-104.

Other Drivers with published `tagBindingFields` use the schema-driven generic assistant.

## 6. Modbus

C04 introduces a Driver-owned canonical Modbus address codec shared by Engineering and Runtime. Canonical persistence remains zero-based `area:offset`; the assistant makes zero-based vs one-based user input explicit and never guesses 4xxxx notation.

The backend builder validates area, reference, Unit ID, value type, word order, scale/offset and bit selector rules.

The Modbus descriptor publishes runtime-backed TAG binding fields:

- `modbus.unitId`;
- `modbus.valueType`;
- `modbus.wordOrder`;
- `modbus.scale`;
- `modbus.offset`.

New assistant output carries canonical `CommunicationBinding`. The same settings remain mirrored in legacy metadata because the current Modbus runtime compiler still consumes that representation. This is real product-data/runtime compatibility, not a historical DEMO shim.

Bit selection remains the canonical `AddressSelector`, not Driver metadata.

## 7. DNP3

The DNP3 assistant authors canonical `dnp3:<pointKind>:<index>` identity plus Runtime-backed settings.

Before creating a binding it validates point kind, index bounds, writable semantics and command mode against the current backend catalog. Display labels for DNP3 point families are localized while canonical tokens remain unchanged in persisted data.

## 8. IEC 60870-5-104

IEC-104 retains TAG binding schema:

- `elite.iec60870.5.104.point`;
- version `1`.

The runtime composition registers the enriched descriptor. The Type-ID codec preserves the established Runtime binding vocabulary while accepting standard underscore IEC names and numeric IDs at compatibility/protocol edges.

The descriptor advertises only monitored Type IDs actually supported by the decoder and command Type IDs actually implemented. The assistant validates Type ID, command Type ID, command mode and qualifier bounds against the backend catalog. Command mode display labels are localized without changing canonical persisted values.

## 9. OPC UA Engineering tooling

C04 exposes capability-driven Engineering tooling for configured OPC UA Data Sources:

- connection test;
- endpoint discovery;
- browse/browse-next;
- loaded-node search;
- current-TAG selection;
- multi-selection;
- bulk TAG candidate creation.

Backend boundaries:

- configured Data Source resolved by stable GUID;
- Engineering Read authorization required;
- Engineering-only Driver providers;
- protected-material/security seams reused;
- no mutation of active Runtime;
- unexpected provider failures sanitized instead of echoing raw transport/security exception messages.

Discovery/browse results remain transient. They become canonical TAGs only through Engineering Preview/Apply.

Bulk TAG candidates preserve Source key + stable DataSourceId, portable Address, canonical CommunicationBinding, backend TAG binding schema identity, suggested data type/unit/access and stable discovery identity metadata.

## 10. Multilingual audit required by coordinator

C04 user-facing copy is centralized in:

- `web/scada-web/src/engineering/c04I18n.ts`;
- `web/scada-web/src/engineering/c04ProtocolLabels.ts`.

The new/modified C04 surfaces no longer keep independent `copy(locale)` tables:

- `TagSourceSelector`;
- `TagAddressEditor` / Modbus assistant;
- `GenericTagBindingAssistant`;
- DNP3 assistant;
- IEC-104 assistant;
- OPC UA tooling.

`pt-BR`, `en` and `es` cover C04 labels, help, validation errors, schema/catalog failures, empty/unresolved states, actions, OPC UA bulk errors and confirmation text. Protocol display labels are localized for Modbus areas, DNP3 point families and IEC-104 command modes while canonical protocol values remain invariant.

Browser coverage `c04-i18n-browser.spec.ts` verifies `pt-BR -> en -> es` switching on Source and Address surfaces while stable Source identity remains unchanged. `c04-i18n-contract.spec.ts` prevents reintroduction of per-component copy tables and checks all three locales plus protocol label resources.

### Known combined-audit gap

The C02 Data Source catalog contract already exposes `DisplayNameResourceKey` / `DescriptionResourceKey`, but the current shared `DataSourceCatalogEditor` renders backend fallback `displayName` / `description` and does not yet resolve those resource keys. The generic C04 binding assistant intentionally does **not** create a second React Driver-field translation catalog because that would violate backend catalog authority.

Therefore full locale resolution of arbitrary backend Driver field names/descriptions belongs in the combined Wave 14 multilingual audit/shared catalog infrastructure. C04 records the gap instead of hiding it with duplicated protocol metadata.

Backend Driver problem details may likewise remain in the backend-provided language when no localized message resource is supplied; C04 localizes its own frontend errors but does not rewrite backend diagnostics speculatively.

## 11. Historical DEMO compatibility decision

Per coordinator decision, the old DEMO is not C04's acceptance authority.

C04 did **not** add model adapters to preserve historical fixture assumptions.

The C04 Source browser acceptance previously depended on a historical `builtin.simulation` / `Demo.*` fixture. That test was classified as an obsolete fixture dependency, not a product-contract requirement, and was replaced with an isolated canonical schema-v15 project fixture. The replacement still proves the stronger C04 contract:

- searchable configured Source;
- stable GUID selection;
- compatibility Source key;
- Preview payload round-trip;
- no Workspace mutation during Preview.

No test assertion was weakened to obtain green.

Real compatibility intentionally preserved includes legacy key-only Source migration, existing canonical Addresses and the Modbus metadata bridge required by the current Runtime compiler.

## 12. Coverage

Backend/contract tests cover:

- legacy Source migration to stable ID;
- rename by stable ID;
- orphaned ID without key fallback;
- Runtime Source normalization;
- canonical Modbus parse/build round trips;
- Modbus TAG binding descriptor fields;
- Driver catalog TAG schema projection;
- IEC-104 binding vocabulary/compatibility.

Browser/TypeScript coverage includes:

- Source GUID + compatibility key through Preview;
- Source rename/reselection and true Source-change cleanup;
- unresolved/deleted Source behavior;
- manual Address / binding convergence;
- Modbus legacy metadata + canonical binding convergence;
- specialized-assistant registry + generic schema fallback;
- OPC UA binding identity when Data Source and TAG schema IDs intentionally differ;
- OPC UA connection-test/discovery UI boundaries;
- OPC UA browse, two-node multi-select, bulk candidate generation, Preview and Apply version boundary;
- pt-BR/en/es locale switching on C04 surface;
- static guard against C04 localization regression.

Route-level mocks are used where the test is proving EliteSCADA browser behavior rather than claiming external protocol interoperability.

## 13. Validation evidence

The DEV environment could not clone GitHub locally, so validation relied on repository Actions that were triggered by C04's DNP3-touched paths.

A real build defect in C04 was found and corrected twice before the green smoke:

1. `EngineeringDataSourceTypeCatalog.cs` nullable conditional inference (`CS0173`);
2. `EngineeringDriverTooling.cs` provider disposal array inferred as `object[]` (`CS0266`).

Exact smoke evidence:

- SHA `0a80e83cf4cb5f70507ac84e8d7c8a078dc5ff1e`;
- workflow `Wave 14 C03 DNP3 Adapter` run #49 / `33716369828`;
- conclusion: **SUCCESS**;
- managed build/tests: SUCCESS;
- DNP3 convergence tests: SUCCESS;
- Linux native host build: SUCCESS;
- Windows native host build: SUCCESS;
- OpenDNP3 <-> dnp3py L3 interop: SUCCESS.

That run proves a useful C# / DriverHost / API build smoke and DNP3 convergence on that SHA. It is **not** the universal EliteSCADA CI, is **not** the complete Seven-Driver L3 workflow, and does not execute the newly authored browser suite.

The current code-candidate SHA `ed95008176e1bb242f165efb987f16ece03c381c` contains later frontend-only localization/test changes and has no exact-SHA universal CI evidence at handoff.

Per `docs/CI-VALIDATION-POLICY.md`, coordinator/integration acceptance still requires exact-head universal CI and affected communication validation. The final C10 sequence must follow the coordinator decision: converged corrections -> multilingual audit -> new canonical DEMO -> full CI -> clean Codespace -> real browser homologation.

## 14. Integration notes

1. Re-fetch PR #221 and branch HEAD before integration.
2. Preserve the exact C02 lineage or ensure that dependency is already present in integration.
3. Treat C04 as authority for Source/communication/address/discovery conflicts with older consumers.
4. Do not restore historical DEMO assumptions during conflict resolution.
5. Re-run the full C04 browser/contract surface after conflicts in shared Engineering/catalog files.
6. Resolve the shared catalog resource-key localization gap in the combined multilingual audit rather than duplicating Driver metadata in C04.
7. Run exact-head universal CI + applicable L3/Runtime/Licensing gates before acceptance.
8. Keep PR #221 draft until coordinator acceptance evidence exists or the coordinator explicitly changes state.
