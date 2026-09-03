# W14-C04 — Implementation Handoff

**Package:** W14-C04 — TAG Source selector + address assistants + OPC UA discovery/browse  
**Branch:** `wave14/c04-tag-address-assistants`  
**Integration target:** `wave14/corrections-integration`  
**Required dependency/base:** C02 final SHA `7a0515289bdabba157fb1f645b32647746c83371` / PR #217  
**Code-candidate SHA:** `6bb6480445e420aecf94d87a8c6594dafe67d0cc`  
**Draft PR:** #221

This document is a DEV handoff, not an acceptance declaration. GitHub refs and exact-SHA validation remain authoritative.

## 1. Dependency lineage

Live comparison before handoff confirmed:

- merge-base: exactly `7a0515289bdabba157fb1f645b32647746c83371`;
- C04 is ahead of that base and not behind it;
- no Stage-A base substitution was used.

The PR targets the Wave 14 correction integration branch as required. Until prerequisite work is present in that target, the PR comparison may contain prerequisite lineage as well as C04 changes; integration must judge the exact live diff, not commit-count appearance alone.

## 2. Stable TAG -> Data Source identity

C04 adds stable Data Source identity to the TAG contract while retaining the legacy Source key for compatibility and human-readable portability.

Rules implemented:

- `DataSourceId` is the authoritative durable reference when present;
- an unresolved stable GUID fails closed and is never silently redirected to a different Source that happens to reuse the old key;
- key-only legacy TAGs remain resolvable for migration;
- Apply can enrich a resolvable legacy TAG with the stable Data Source ID;
- Source rename is reconciled by GUID and keeps the compatibility key current;
- reselecting the same renamed Source preserves address, selector and binding;
- selecting a genuinely different Source clears protocol address/binding state;
- selecting no Source also clears stale address, selector and binding state.

The browser Source editor is a searchable selector populated only from configured Working Data Sources. Deleted/unresolved references are presented explicitly as invalid rather than converted into free text.

## 3. Driver-owned TAG binding contract

The backend Driver catalog now projects TAG binding schema identity independently from Data Source configuration schema identity:

- `tagBindingSchemaId`;
- `tagBindingSchemaVersion`;
- existing driver-owned `configurationSchema.tagBindingFields`.

When a Driver does not declare a distinct TAG binding schema, the catalog uses the configuration schema identity as the compatibility fallback. IEC-104 deliberately uses a distinct point-binding contract.

Frontend schema resolution is centralized in `TagBindingSchema.ts`. Generic and specialized assistants consume the backend catalog rather than duplicating schema IDs. DNP3 and IEC-104 additionally validate specialized choices against the live field definition before creating a binding.

## 4. Address authoring architecture

Manual portable Address remains available for compatibility and expert authoring.

When a TAG already has a canonical `CommunicationBinding`, editing the manual Address keeps `Address` and `CommunicationBinding.PortableAddress` synchronized. Clearing the Address removes the binding instead of leaving an invalid envelope.

Specialized UX is registered centrally only where protocol identity or discovery benefits from it:

- Modbus TCP;
- OPC UA;
- DNP3 Master;
- IEC 60870-5-104.

Other Drivers with published `tagBindingFields` use `GenericTagBindingAssistant`, which is schema-driven and validates required values, enum values and numeric bounds. Protected-material fields are not authored into TAG bindings; they remain on the Data Source `secretReferences` boundary.

## 5. Modbus

C04 introduces one driver-owned canonical address codec used by both Engineering and Runtime. The assistant makes the reference convention explicit instead of guessing 4xxxx-style notation.

The backend-authoritative builder validates:

- area;
- zero-based vs one-based reference input;
- Unit ID;
- value type;
- word order;
- scale/offset;
- bit selector bounds and applicable register areas.

Canonical runtime Address remains the zero-based `area:offset` representation.

The Modbus descriptor now publishes runtime-backed TAG binding fields for:

- `modbus.unitId`;
- `modbus.valueType`;
- `modbus.wordOrder`;
- `modbus.scale`;
- `modbus.offset`.

New Modbus assistant output also carries a canonical `CommunicationBinding` using the catalog-projected TAG schema identity. The same settings remain mirrored in legacy TAG metadata because the current Modbus runtime compiler still consumes that compatibility representation; this keeps existing packages/runtime behavior intact while moving Engineering authoring onto the common envelope.

Bit selection is intentionally not represented as Driver metadata because it is the canonical TAG `AddressSelector` contract.

## 6. DNP3

The specialized DNP3 assistant authors canonical `dnp3:<pointKind>:<index>` identity plus the binding settings required by Runtime.

Before producing a binding it checks its selected point kind, index bounds, writable field and command mode against the live backend TAG binding definition. The UI therefore fails visibly if the Driver descriptor and specialized assistant drift apart.

Manual canonical DNP3 Address remains available as a migration/expert path.

## 7. IEC 60870-5-104

IEC-104 retains a distinct TAG point schema:

- schema ID `elite.iec60870.5.104.point`;
- schema version `1`.

The runtime composition registers the enriched IEC-104 descriptor, so the product catalog exposes the actual point-binding contract rather than the raw Data Source schema.

The Type-ID codec keeps the established runtime enum-style binding vocabulary while accepting standard underscore IEC names and numeric IDs at protocol/import compatibility boundaries. The descriptor advertises only monitored Type IDs the current decoder actually supports and only command Type IDs implemented by Runtime.

The specialized assistant validates monitored Type ID, command Type ID, command mode and qualifier bounds against the backend TAG binding definition before creating the binding.

## 8. OPC UA Engineering tooling

C04 exposes capability-driven Engineering tooling for a configured OPC UA Data Source:

- connection test;
- endpoint discovery;
- browse/browse-next;
- loaded-node search;
- current-TAG selection;
- multi-select and bulk TAG candidate creation.

The backend tooling:

- resolves the configured Data Source by stable GUID;
- requires Engineering Read authorization;
- creates Engineering-only driver providers;
- reuses the OPC UA protected-material/security boundary;
- does not mutate active Runtime;
- sanitizes unexpected provider failures instead of returning raw transport/security exception messages to the browser.

Discovery/browse results remain transient. They become canonical TAGs only through normal Engineering Preview/Apply.

Bulk OPC UA TAG creation preserves:

- Source compatibility key;
- stable `DataSourceId`;
- portable Address;
- canonical `CommunicationBinding`;
- backend TAG binding schema identity;
- suggested data type;
- engineering unit;
- read/write access where reported;
- stable discovery identity as metadata evidence.

The OPC UA binding builder deliberately uses `tagBindingSchemaId/tagBindingSchemaVersion`, even when those differ from Data Source configuration schema identity.

## 9. Coverage added

Backend/contract tests cover, among other cases:

- key-only Source migration to stable ID;
- Source rename by stable ID;
- orphaned stable ID without key fallback;
- runtime Source normalization;
- canonical Modbus parse/build round trips;
- Modbus TAG binding descriptor fields;
- Driver catalog TAG schema projection;
- IEC-104 TAG binding vocabulary/compatibility.

Browser/TypeScript coverage includes:

- Source selector GUID + compatibility key through an actual Preview request;
- no Workspace mutation during Preview;
- rename/reselection preservation and true Source-change cleanup;
- unresolved/deleted Source behavior;
- manual Address / canonical binding convergence;
- Modbus legacy metadata plus canonical binding convergence;
- centralized specialized-assistant registry and schema-driven generic fallback;
- OPC UA binding identity with deliberately different Data Source and TAG schema IDs;
- OPC UA connection-test and discovery UI boundaries with sanitized results;
- OPC UA browse with two selected nodes, bulk candidate generation, Preview and Apply version boundary.

Some browser cases use route-level mocks for external OPC UA responses so they validate EliteSCADA UI/contract behavior without claiming a real external server interoperability run.

## 10. Validation status at handoff

At code-candidate SHA `6bb6480445e420aecf94d87a8c6594dafe67d0cc`:

- PR #221 remained open, draft and mergeable at the last live inspection;
- no PR discussion/review findings were present at inspection time;
- no GitHub commit statuses/checks were registered for the preceding candidate inspection;
- no local build/test was executed by this DEV environment because the local runtime could not resolve GitHub for a repository clone.

The absence of Actions on this PR is consistent with repository workflow routing: the universal `EliteSCADA CI` is configured for pull requests targeting `main`, while this package correctly targets `wave14/corrections-integration`.

Per `docs/CI-VALIDATION-POLICY.md`, coordinator/integration acceptance still requires exact-head execution of the universal gate plus affected specialized communication validation. Because C04 changes DriverHost/Driver contracts and communication Engineering behavior, L3 Seven-Driver Lab belongs in final evidence. Runtime and Licensing checks must follow the conservative-override/release rules selected by the coordinator for the integrated candidate.

Do not convert authored tests into claimed green evidence until the corresponding exact-SHA runs exist.

## 11. Integration notes

1. Re-fetch PR #221 and branch HEAD before integration.
2. Confirm C02 dependency is present in the integration target or preserve the exact dependency lineage when merging/cherry-picking.
3. Re-run the full C04 browser/contract surface after any conflict resolution in shared Engineering editor/catalog files.
4. Pay special attention to overlapping C02 catalog changes and other Wave 14 work touching `SecuredEngineeringEditors`, shared Driver descriptors or canonical TAG contracts.
5. Run exact-head CI/L3 according to policy before marking C04 accepted.
6. Keep PR #221 draft until coordinator acceptance evidence exists or until the coordinator explicitly changes that state.
