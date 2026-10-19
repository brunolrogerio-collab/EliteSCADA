# ADR-022 — Versioned operating-context profiles and comparison eligibility

| Field | Value |
| --- | --- |
| Status | **Main-approved design for the owner-authorized Stage 4E package under ADR-021** |
| Date | 2026-10-19 |
| Parent | [#588 — Asset & Process Intelligence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588) |
| Predecessor | Stage 4D #649 accepted at merge `dd67bac5c4ca937e028b0fb9890b00dfeedd03a4`; exact T2 #39321693285 passed |
| Implementation child | #652 |
| Frozen boundary | Stage 4A–4D contracts and results remain unchanged |

## Decision

1. Add an `OperatingContextProfile` as a distinct Engineering object. It has a stable ID, stable key, operator-authored display label, server-managed profile version, one existing Equipment assignment, and one or more required predicates. It is separate from the existing measurement profile, whose signals remain TAG references.
2. Each predicate references a stable TAG ID already present in the assigned measurement profile. The declared predicate type must agree with the canonical TAG data type. Every predicate is required to establish the declared context; no hard-coded `off`, `startup`, `stable`, or other regime vocabulary is introduced.
3. Version 1 typed predicates are: finite inclusive numeric interval (Int16/Int32/Int64/Float/Double, at least one bound); exact Boolean equality; and exact ordinal equality for String or Enum. Numeric bounds use the current displayed Engineering unit without conversion. DateTime predicates, regular expressions, wildcards, inferred thresholds, and implicit tolerance are unsupported and fail validation.
4. Profile identity and version are the context contract for a query. The server assigns version 1 at creation and increments it when identity, assignment, label, or predicate semantics change. Save requests carry the expected version; stale writes conflict. Imports must not roll a changed local profile back to a version that could validate a stale request. A query with a missing, incompatible, or stale profile version fails closed before reading Historian data.
5. Add a separately versioned read-only `context-eligibility.v1` report. One exact profile ID/version is applied to the caller-selected reference and optional later comparison windows. It reports typed predicate outcomes and per-signal observations eligible at exact, unique, UTC source timestamps with explicit `Synchronized` evidence. A target observation is context-eligible only when every required predicate is satisfied at that same source timestamp. No nearest join, carry-forward, interpolation, or imputation is allowed.
6. The report means only that observations match the operator-declared context profile version. It reports missing, poor-quality, unsynchronized, mismatched, and sparse evidence explicitly. It does not assert physical regime equivalence, reference health, statistical sufficiency, a score, or model validity. Stage 4F owns any later joint scoring method and validation gate.
7. Persist context profiles inside the existing Engineering project configuration and `.escadapkg`, not in a hidden workspace store. Add Engineering Intelligence configuration version 3 and package schema version 28. A package below schema 28 that would erase existing context profiles must fail import preview. Existing schema 27 and configuration version 2 projects continue to load with an empty context-profile collection. Context profiles do not enter reusable-library fragments.
8. Reuse the canonical Intelligence entitlement, active scope, explicit execution permit, `TrendUse`, TAG-level `TagRead`, workspace/runtime coherence, Authority/HA fences and per-page/final revalidation. Authoring retains `EngineeringModify` and resource-scoped `AssetProcessIntelligenceManage`. Read paths stay on-demand and read-only.
9. Bounds for the eligibility report are at most 16 configured measurement signals/context predicates, 31 days per window, 200-row pages, and 50,000 raw Historian rows across both windows and all queried TAGs. No partial report is returned on stale configuration, provider/cursor, cancellation, authorization, workspace, or budget failure.

## Rationale and implementation boundary

The current Engineering model already separates reusable TAG profiles, Equipment assignments, and process stages. A context profile per assignment composes with those identities and leaves the frozen two numeric intervals in `contextual-deviation-vector.v2` untouched. A process stage can continue grouping assignments; any multi-equipment joint context/model belongs in the 4F method design, not in a relabeling of 4C.

The operator selects both windows and the exact profile version. Only the configured predicates define eligibility. Sensor quality or missing data can produce an insufficient result; the system never invents an operating regime from observed values.

Explicitly excluded: 4F joint scoring, 4G representative-data validation, 5B dashboards, 6 hardening, HA/driver work, licensing changes, persistence outside Engineering project packages, SQL migrations, workers/learners, process writes, diagnosis, fault probability, RUL, or AI/generative services.

## Canonical source seams verified at the Stage 4D merge

- Contracts and configuration: `src/Scada.Engineering/Contracts/EngineeringContracts.cs`, `AssetProcessIntelligenceEngineeringDto`.
- Validation and in-memory registry: `src/Scada.Engineering/Intelligence/AssetProcessIntelligenceEngineeringRegistry.cs`, `AssetProcessIntelligenceEngineeringValidator`.
- Engineering authoring API: `src/Scada.Api/Licensing/AssetProcessIntelligenceApi.cs`, `GET /scopes` and `PUT /configuration`.
- Project-package persistence and import preview: `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`, `ExportPackage`, `PreviewAssetProcessIntelligence`, and `Apply`; current package schema is 27.
- Existing authoring UI: `web/scada-web/src/engineering/AssetProcessIntelligenceWorkspace.tsx` and its localized copy tables.
- Analytics and canonical fences: `src/Scada.Api/Licensing/AssetProcessIntelligenceAnalyticsApi.cs`, `AssetProcessIntelligenceAnalyticsService.PrepareProfileCoverageAsync`, the Stage 4D source-evidence reader, `RevalidateContextualReferencePlansAsync`, and `AssetProcessIntelligenceExecutionGate`.
