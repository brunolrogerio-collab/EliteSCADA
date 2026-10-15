# ADR-013 — Asset & Process Intelligence: controlled signal semantics and unit dimensions

- Status: **PROPOSED — Main review required; design only.**
- Exact design base: `wave15/corrections-integration@15dfa117b7342f75352233bdd856a756a084148a` (merged PR #612).
- Related decisions: [ADR-011 — Stage 3A descriptive KPIs](ADR-011-ASSET-PROCESS-INTELLIGENCE-STAGE3A.md) and [ADR-012 — Stage 3B profile signal coverage](ADR-012-ASSET-PROCESS-INTELLIGENCE-STAGE3B-PROFILE-COVERAGE.md).
- Scope: define an additive, portable semantic-role and unit-dimension contract for a later Stage 3B implementation. This ADR does not authorize implementation, physical KPIs, or work in Stages 4–6.

## Context and current contract

The integrated Stage 3B profile-coverage slice intentionally treats each signal's `SignalRole` as opaque, carries the TAG's engineering-unit string unchanged, and does not calculate cross-signal physical KPIs. The current code has no canonical unit dimension, unit conversion table, or controlled process-role catalog. In particular, matching strings such as `"bar"`, `"pressure"`, or `"pump"` is not evidence that two values are dimensionally or physically comparable.

The relevant current contracts and lifecycle are:

- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`: `TagEngineeringDto.EngineeringUnit` is nullable free text; `AssetProcessIntelligenceSignalEngineeringDto.SignalRole` is a required free-text field; the Intelligence DTO supports configuration versions 1 and 2. V2 carries profiles, assignments, and process stages.
- `src/Scada.Engineering/Intelligence/AssetProcessIntelligenceEngineeringRegistry.cs`: validates stable IDs/references, keys, and non-empty role text, but does not interpret roles or compare TAG dimensions.
- `src/Scada.Api/Licensing/AssetProcessIntelligenceApi.cs`: `PUT /api/engineering/asset-process-intelligence/configuration` currently accepts profiles/assignments/stages and constructs a v2 configuration. It is a full-list replacement, not a field-level patch.
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`: Engineering package schema is currently version 25; the parser rejects newer package versions. V2 Intelligence configuration requires package schema 25 or newer. The package is the canonical import/export boundary.
- `src/Scada.Engineering/Persistence/EngineeringProjectPersistenceService.cs`: saves normalized Engineering JSON from the exchange service and restores by parsing, validating, and applying that package.
- `web/scada-web/src/engineering/types.ts` and `SecuredEngineeringEditors.tsx`: the TAG editor exposes `engineeringUnit` as a text field. `AssetProcessIntelligenceWorkspace.tsx` edits the free-text `signalRole` and submits the v2 profile configuration. Neither editor currently offers controlled semantic/unit catalogs.
- The Stage 3A/3B analytics paths currently require Intelligence configuration version 2. They must continue to return the same results and status meanings for legacy configurations.
- `src/Scada.Api/Runtime/DemoProcessModel.cs` contains illustrative TAGs labelled Pump Current (`A`), Pump Frequency (`Hz`), Discharge Pressure (`bar`), and Flow (`m³/h`). These are synthetic SimulationPoints, not proof of field-device measurement conventions. Pump Frequency is writable in this fixture; it must not be equated with shaft speed or silently treated as a measured drive output.

## Decision proposed for Main

Keep the current v2 profile and TAG text fields intact. Introduce **opt-in typed semantic annotations as additive fields**, with independently versioned role and unit catalogs. The annotations are separate from display/source text and are the only inputs eligible for future dimensional validation or unit conversion.

Conceptual wire shape (illustrative, not an approved DTO or implementation):

```json
{
  "signalRole": "legacy free text, preserved exactly",
  "semanticBinding": {
    "catalogVersion": 1,
    "roleCode": "pump.discharge_pressure",
    "qualifiers": { "pressureReference": "gauge" }
  }
}
```

The canonical TAG would retain `engineeringUnit` unchanged and could separately carry an optional controlled unit reference, for example `{ "catalogVersion": 1, "unitCode": "unit.pressure.bar" }`. The dimension is derived from the controlled catalog entry; users cannot enter an arbitrary dimension label. The exact field names and qualifier schemas remain for Main to approve before implementation.

The ownership boundary is deliberate:

- The **role binding belongs to a profile signal**, because the meaning of a TAG in an asset/process profile is contextual.
- The **unit reference belongs to the canonical TAG**, because the unit describes the TAG value and must not be independently duplicated in every profile.
- Legacy `SignalRole` and `EngineeringUnit` remain visible, source-facing metadata. Neither is normalized, overwritten, converted, or used to infer an annotation.
- A role catalog entry defines a quantity/dimension and any required qualifiers. A unit catalog entry defines one dimension, a stable code, and a conversion to that dimension's declared canonical unit. Dimension is computed from the catalogs, not trusted from project JSON.

### Versioning and package compatibility

Keep `AssetProcessIntelligenceEngineeringDto.Version == 2` for this additive extension so Stage 3A/3B readers that gate on v2 do not needlessly change their existing read path. Version the semantic binding and unit catalog independently. Require an Engineering package schema bump from 25 to **26** before serializing any controlled annotations in export/persisted Engineering JSON. Schema 25 packages remain the legacy shape and receive no inferred annotations. The v26 decision is proposed and requires Main approval.

The current parser already rejects schema versions newer than it supports. Therefore a pre-v26 application must reject a v26 package explicitly rather than silently discard the new fields. A v26-capable application must preserve annotations across export/import, project save/reopen, and editor round-trips. Do not claim compatibility with binaries older than the schema they understand.

The current Intelligence PUT replaces complete profile/assignment/stage lists. A future legacy-shaped request that omits semantic fields must never erase stored annotations silently. The implementation must distinguish omission from an explicit authorized removal: either preserve by stable signal/TAG identity when all relevant references and legacy values are unchanged, or return a stable conflict requiring a v26-aware editor. Import/replace flows must likewise preview and explicitly confirm any lossy removal; merge must not convert omission into deletion. The TAG editor must preserve an existing unit annotation when changing unrelated fields, and changing a TAG's unit text or a signal's TAG reference must require revalidation/confirmation of the controlled binding. This rule covers list-replacement APIs, imports, and UI save flows—not just a happy-path round-trip.

### Catalog proposal and evidence limits

Use stable ASCII codes and localized labels kept outside the identity. Do not claim UCUM or another external standard's compliance until Main selects and validates that mapping. Catalog version changes are explicit; codes are never repurposed. Parsing and conversion use invariant numeric rules, deterministic coefficients, finite-value checks, overflow checks, and a closed conversion table. V1 should contain only reviewed linear conversions; offsets, arbitrary expressions, user-defined conversion formulas, and free-text aliases are out of scope.

The repository's demo model is enough to identify candidate **unit** entries, not to infer them from arbitrary TAG text. A conservative candidate unit set for review is:

| Candidate code | Dimension | Canonical unit | Demo evidence / proposed exact conversion |
|---|---|---|---|
| `unit.ratio.percent` | dimensionless ratio | `1` | `%`; multiply by 0.01 |
| `unit.electric_current.ampere` | electric current | `A` | `A`; identity |
| `unit.frequency.hertz` | frequency | `Hz` | `Hz`; identity |
| `unit.pressure.bar` | pressure | `Pa` | `bar`; multiply by 100000 |
| `unit.volumetric_flow.cubic_metre_per_hour` | volumetric flow | `m³/s` | `m³/h`; divide by 3600 |

These are candidate codes only. A display string equal to the shown symbol does **not** automatically assign the code. The `%` candidate is dimensionless; it must not be mistaken for an absolute level quantity. No offset conversions (for example, temperature), compound-unit algebra, or conversion for shaft speed is proposed in v1.

The demo labels support only these candidate role concepts for a first reviewed catalog discussion: pump electrical current, pump frequency, discharge pressure, and discharge volumetric flow. They do **not** establish which electrical current is measured, whether frequency is a command/setpoint or measured output, pressure reference (gauge/absolute/differential), or flow measurement point/direction. Therefore a role binding must carry the required controlled qualifiers; absent/unknown qualifiers leave the binding incomplete and ineligible for physical calculations. In particular, do not map Hz to shaft speed or derive motor speed from drive frequency. Do not include motor power, pump head, or shaft speed in this initial catalog: the repository has no corresponding supported production TAG evidence. Unit/role catalog entries and the exact required qualifiers are Main decisions, not assumed facts.

### Validation and analytics boundary

For future annotations, require all of the following before a semantic binding is considered complete:

1. Supported role catalog version and role code.
2. Supported unit catalog version and unit code on the canonical TAG.
3. Derived dimensions agree exactly.
4. Role-required qualifiers are present, recognized, and valid for the selected TAG/profile context.
5. Numeric values are finite and conversion remains within the destination numeric type's range.

Unknown codes/versions, a role-unit dimension mismatch, missing required qualifiers, nonnumeric TAGs, or invalid conversion never fall back to free-text interpretation. They remain unsupported/incomplete and cannot produce a derived process KPI. A validator must report stable issue codes and the affected profile signal/TAG identity without leaking unrelated protected data.

Even a complete role/unit binding is **not sufficient** to claim a physical process KPI. ADR-011's completeness and source-acquisition alignment remain unknown/unverified. ADR-012's exact shared Historian event timestamp is only event-time overlap; it does not prove simultaneous acquisition, expected cadence, process regime, or acceptable skew. A later KPI decision must separately approve quality/completeness/gap limits, time basis and maximum skew (or an authoritative source-time contract), measurement-boundary qualifiers, formulas, and resource bounds. No physical KPI is authorized by this ADR alone.

### Migration, editor, and platform behavior

- Existing v2 profiles, opaque `SignalRole` strings, and TAG unit strings load unchanged and continue using ADR-011/ADR-012 behavior. No automatic mapping from names, paths, descriptions, role strings, or unit symbols is allowed.
- A future migration/editor may offer suggestions, but each binding requires explicit human selection and confirmation of its qualifiers. Leaving a signal unannotated is valid and remains non-semantic.
- The Engineering TAG editor will need to show the original unit string alongside a separate controlled-unit picker; the Intelligence profile editor will need to retain free text while providing a separate role/qualifier editor. A user must be able to see incomplete/unrecognized bindings and why they are ineligible.
- Backend validation is authoritative. Browser locale affects labels only; stable codes, conversion coefficients, serialization, and validation are culture-independent. Save/reopen/import/export must preserve exact legacy strings and controlled codes.
- Use existing Engineering workspace mutation/revision/persistence/export boundaries and existing authorization. Do not add a parallel semantic database, policy store, license entitlement, Authority capability, HA behavior, analytics worker, or alternate TAG/History authority.

## Acceptance matrix for the later implementation proposal

| Case | Required result |
|---|---|
| Schema-25 package with v2 profiles and arbitrary legacy strings | Opens, saves, exports, and reopens with strings unchanged and no inferred annotations. Existing Stage 3A/3B output/status remains unchanged. |
| V2 profile with a recognized role and compatible recognized TAG unit | Accepted only when required qualifiers are complete; binding is persisted and round-trips. No KPI is implied merely by acceptance. |
| Role and unit dimensions differ | Rejected before apply/save with a stable validation issue; no partial Engineering mutation. |
| Unknown role/unit code or unsupported catalog version in an incoming write/import | Reject the whole mutation transactionally with a stable issue; never interpret or convert. Existing stored annotations that become unsupported after a catalog upgrade remain opaque and are blocked from analytics until explicitly resolved. |
| Legacy free text resembles a known role/unit symbol | Remains opaque; no auto-binding or auto-conversion. |
| Existing annotation edited through a legacy-shaped Intelligence PUT, old TAG editor, or schema-25 import | Annotation is preserved only when identity and relevant source fields are demonstrably unchanged; otherwise the operation returns a conflict/loss preview and requires an explicit v26-aware resolution. No silent deletion. |
| TAG unit text or profile signal TAG reference changes | Existing controlled annotation is revalidated; mismatch/incomplete qualifiers block semantic use and require explicit user confirmation to change/clear. |
| Save/reopen and schema-26 export/import on the supported version | Stable IDs, catalog versions, codes, qualifiers, and legacy display strings round-trip without locale-dependent changes. |
| Different OS/browser locales and decimal separators | Same validation and conversion result; labels may localize, codes and numeric interpretation do not. |
| Historian values share event timestamps but source acquisition times/cadence are unknown | Report only ADR-012 event-time overlap; do not label samples simultaneous or compute a physical KPI. |
| Read-only Stage 3A/3B queries against legacy and annotated v2 profiles | Existing descriptive results and fences remain unchanged; semantic metadata does not broaden authority, licensing, scope, or write access. |

## Decisions requested from Main before implementation

1. Approve or reject additive optional annotations plus Engineering schema 26 while keeping Intelligence v2, versus requiring Intelligence configuration v3. The additive/schema-26 path is recommended to preserve current v2 consumers.
2. Select the unit-code source/standard (repo-owned codes are recommended until formal external-standard compatibility is reviewed) and approve the proposed five candidate units, especially `%` as ratio.
3. Approve the four demo-derived role candidates, their names, and mandatory qualifier schemas—or require real equipment examples before any role catalog v1 is frozen. In particular, decide current measurement basis, frequency intent/point, pressure reference, and flow location/direction.
4. Select legacy-write/import conflict behavior: safe preservation by stable ID when provably unchanged, or fail-closed conflict for all legacy-shaped writes when semantic annotations exist. Both prohibit silent loss.
5. Defer all physical KPI definitions until time alignment, quality/completeness, cadence/gap limits, measurement boundaries, and formulas receive a separate explicit decision.

## Non-goals and checkpoint

This ADR does not modify product code, database state, the schema, license or Authority contracts, Stage 1/2 fences, HA, existing Stage 3A/3B behavior, dashboards/baselines, or Stages 4–6. No implementation tests or CI are claimed. Status remains **PROPOSED / MAIN REVIEW REQUIRED**. The next authorized step is Main review of this ADR; a separate authorization/implementation task follows only after the requested contract and compatibility decisions are resolved.
