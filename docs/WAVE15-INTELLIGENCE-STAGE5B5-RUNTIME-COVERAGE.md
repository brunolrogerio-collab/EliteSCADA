# Wave 15 Intelligence — Stage 5B.5 profile coverage in Runtime

Snapshot: 2026-10-21. Owning issue: [#716](https://github.com/brunolrogerio-collab/EliteSCADA/issues/716). This is a bounded contract audit and implementation record, not Stage 5B or Wave 15 acceptance.

## Exact base and objective

Product base: `wave15/corrections-integration@4133af7e8eb379e65a468148f127bcc944a34aff`, the documentation-only closeout after product PR #714. The gap is narrow: #709 already provides protected, transient Equipment/profile coverage in Engineering, but no canonical Screen element can invoke that report from Runtime. Stage 5B.5 surfaces the existing response on a normal Screen after an explicit operator action.

## Audited contracts

| Concern | Existing source contract |
| --- | --- |
| Canonical Screen persistence/validation | `src/Scada.Engineering/VisualScripting/BuiltinVisualObjectSchemas.cs` registers `IntelligenceProfileCoverageV1Type`/`IntelligenceProfileCoverageV1`; `BuiltinVisualEngineeringValidation.ValidateIntelligenceProfileCoverageV1` in `BuiltinVisualEngineeringValidation.cs` enforces Screen-only use and scope/profile UUIDs. `ScreenEngineeringDto` and `VisualElementEngineeringDto` remain the canonical persisted model. The frontend registry is `web/scada-web/src/visual-runtime/builtinVisualObjectSchemas.ts` (`BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProfileCoverageV1`). No separate model or migration |
| Existing report route | `AssetProcessIntelligenceAnalyticsApi.Route` is `/api/engineering/asset-process-intelligence/analytics`; `MapAssetProcessIntelligenceAnalyticsEndpoints` maps `POST /profile-coverage` to `AssetProcessIntelligenceAnalyticsService.ExecuteProfileCoverageAsync` |
| Existing authorization | `ExecuteProfileCoverageAsync` checks Runtime `TrendUse` and Intelligence entitlement. `PrepareProfileCoverageAsync` checks workspace/Runtime coherence, active HA ownership, entitlement again, the configured Authority/HA policy fence, unique active scope, execution permit, unique profile, source resolution, and `CheckRuntimeTagAsync(..., TagAccessOperation.Read)` for each referenced TAG |
| Query limits and freshness | `ValidateProfileCoverageRequest` requires canonical non-empty IDs, valid UTC window and an allowed bucket. The existing catalog bounds the window to 31 days, at most 16 signals and 50,000 raw rows per window. Execution uses the existing cancellable read plan, permit token, workspace mutation/version fence and `RevalidateAsync`; these checks are not duplicated or weakened by the UI |
| Response/non-claims | `AssetProcessIntelligenceProfileCoverageResponse` returns per-signal coverage, quality summaries, temporal-alignment evidence and provenance. The service explicitly labels Historian event-time equality as not source-acquisition time; source acquisition alignment, cadence and completeness remain unverified/unknown; roles are metadata only; no process KPI is calculated |
| Runtime-only rendering | `RuntimeVisualDefinitionRenderer` provides the existing Intelligence analytics Runtime context and sets it disabled for historical playback or missing Runtime View/TrendUse. `CanonicalVisualRenderer` routes the registered type to `IntelligenceProfileCoverageVisualElement.tsx`; that visual calls the existing route only after explicit Analyze, stores results only in component state, and clears/aborts on session, screen context, revision, scope/profile or window changes |
| Unsaved authoring route | `AssetProcessIntelligenceWorkspace.tsx` calls `generateIntelligenceProfileCoverageScreenDraft` from `intelligenceDashboardGenerator.ts`; `EngineeringApp.openIntelligenceDashboardDraft` opens its `ScreenEngineering` result as an unsaved Screen; `VisualEditorWorkspace.initialScreenDraft` enters the existing canonical editor. The existing Preview/Apply and workspace change-version path remains the only persistence route |

## Implemented boundaries

- Stable Engineering fields are only the existing scope/profile references. The query window, report, sampled values, output, and errors are never put into the Screen model or metadata.
- The new visual is registered for canonical Screen validation/rendering, but is intentionally not a general Object Palette insertion. It is created through the authorized Equipment/profile workflow.
- Runtime never queries on mount, navigation, or configuration change. An explicit valid window is required. Invalid/over-31-day windows disable the request locally; the server retains final validation and authorization authority.
- Response scope, profile, project, active revision and requested window are matched before rendering. Aborted/stale requests cannot repopulate a cleared result. Historical playback disables the query and clears transient results.
- The existing report remains read-only. No process command/write, calculation/formula, new endpoint, query store, persistent result, DB schema, cross-equipment aggregation, comparison/deviation view, or field-performance claim is added.

## Local verification recorded for this candidate

- Web production `npm run build`: passed; existing Vite chunk-size advisory remains.
- `playwright.intelligence-ui.config.ts`: 73/73 passed. Includes Runtime explicit-query/transient behavior, playback cancellation, 31-day/window bounds, entitlement/scope/permit errors, stale project response rejection, signal quality/alignment/provenance rendering, unsaved Screen creation, and existing Intelligence UI regressions.
- Core `EngineeringIntelligenceProfileCoverageVisualValidationTests`: 5/5 passed, including Screen-only validation and JSON round-trip of stable references with no result/window properties.
- PostgreSQL `BuiltinVisualObjectSchemasTests`: 3/3 passed.
- Local SDK resolved to .NET `10.0.402` under the repository's `10.0.400` roll-forward setting. CI remains the independent exact-head/exact-merge gate.

Stage 5B.5 remains pending until review, exact-head T1, integration, and exact-merge T2 pass. Comparisons/deviations and cross-equipment process-stage aggregation remain unclaimed; do not start Stage 6 or modify Stage 4/5A, HA, Preview/DEMO, or licensing as part of this slice.
