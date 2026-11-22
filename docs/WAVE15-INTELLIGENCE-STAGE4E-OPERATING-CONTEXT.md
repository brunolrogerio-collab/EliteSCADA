# Intelligence Stage 4E — operating contexts and comparison eligibility

Main order `W15-INTELLIGENCE-4E-OPERATING-CONTEXT-01`, issue #652, 2026-10-19. Parent #588; design decision [ADR-022](ADR-022-INTELLIGENCE-OPERATING-CONTEXT-PROFILES.md), under owner-authorized ADR-021. Stage 4D #649 is accepted at `wave15/corrections-integration@dd67bac5c4ca937e028b0fb9890b00dfeedd03a4`; exact-merge T2 #39321693285 passed. Exact 4E implementation base will be revalidated and activated in #652 after this documentation order merges.

## Real project seams

- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`: `AssetProcessIntelligenceEngineeringDto` version 2 currently contains measurement profiles, signal TAG references, Equipment assignments and process stages.
- `src/Scada.Engineering/Intelligence/AssetProcessIntelligenceEngineeringRegistry.cs`: `AssetProcessIntelligenceEngineeringValidator` validates canonical identities and TAG references.
- `src/Scada.Api/Licensing/AssetProcessIntelligenceApi.cs`: `GET /scopes` and `PUT /configuration` expose and save configuration under Engineering authorization.
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`: project `.escadapkg` export/import includes the Intelligence configuration; package schema is 27. Import preview has explicit compatibility guards, while reusable-library fragments deliberately omit the whole Intelligence configuration.
- `web/scada-web/src/engineering/AssetProcessIntelligenceWorkspace.tsx`: current authoring surface edits measurement profiles, Equipment assignments and process stages; en/es/pt copy is local to the workspace.
- `src/Scada.Api/Licensing/AssetProcessIntelligenceAnalyticsService.cs`: reuse profile preparation, 4D Historian-v2 read/quality/source-time rules, execution permits, all-plan revalidation and no-partial error behavior. Do not change the frozen 4A–4D contracts.

## Bounded objective

Implement persistent, operator-authored context profiles and an additive read-only eligibility report. A profile belongs to one existing Equipment assignment and references one or more TAG IDs from that assignment's measurement profile. Its stable ID/version and all required typed predicates are explicit. The same exact profile ID/version is selected for the reference and optional later comparison window. The server reports which selected measurement observations satisfy the declared predicates at the same exact synchronized UTC source timestamp; it creates no score.

Use ADR-022 as the binding schema and semantic decision. The authoring surface must expose a configurable human label, typed predicate editor and current unit. It must show the profile version and reject stale saves clearly. String/Enum matching is exact ordinal; numeric intervals are inclusive and expressed in the TAG's current Engineering unit; Boolean matching is exact. DateTime and other unsupported predicate forms fail closed. No fixed regime labels or learned thresholds.

## Persistence and compatibility

- Add context profiles to the canonical `AssetProcessIntelligenceEngineeringDto` as version 3; preserve version 1/2 projects as empty-context configuration.
- Include profiles in ordinary Engineering project `.escadapkg` schema 28. Prevent an older package or older API client from silently erasing existing contexts; import preview must explain the conflict. Keep reusable-library fragments unchanged.
- Server owns profile version assignment/increment. Update API save guards and tests so a stale profile edit conflicts. Importing changed content must not roll a current profile version backward.
- Save/load/publish/reopen and package round-trip preserve IDs, versions, predicate types/values, assignment and label. Configuration remains Engineering-owned; no SQL/table/cache/secret storage.

## Read-only eligibility report

- Add a new `context-eligibility.v1` catalog/query route; do not modify `contextual-deviation-vector.v2` or its two numeric context intervals.
- Query binds assignment, context profile ID + expected version, one explicit reference window, and an optional later non-overlapping comparison window. Apply the same profile version to both windows.
- For every predicate and measurement signal/window, report raw/Good-finite/synchronized/rejected/ambiguous counts and stable insufficient/mismatch reasons. A signal row is context-eligible only when its Good finite accepted value and every required context predicate share the same exact unique UTC source timestamp; every predicate evaluates true under its declared type.
- Missing/unsupported/poor-quality/unsynchronized predicates do not become a guessed context. A stale profile, invalid assignment/config, denied capability, dirty/incoherent workspace, permit/fence change, cancellation, invalid/repeated cursor, provider violation or over-budget query fails closed with no partial report.
- Preserve the canonical Intelligence license, active scope, `TrendUse`, all TAG `TagRead`, explicit permit and Authority/HA checks. Authorize all selected TAGs before the first read and revalidate plans around every page and before return.
- Bounds: <=16 signals/predicates, <=31 days per window, page size 200, <=50,000 raw Historian rows across signals and windows. Capture/completeness, context health, regime equivalence and statistical sufficiency remain unknown/not assessed.

## Required acceptance

- Domain/API tests for supported predicate types and boundary values; stable identities; duplicate/unknown TAG; assignment membership; invalid predicate unions; unsupported DateTime; and clear insufficient states.
- Profile-version tests for create, unchanged save, semantic edit increment, stale edit rejection, and changed-package import without version rollback.
- Package tests for schema-28 round-trip and preview conflicts when a legacy package would erase stored contexts; schema-27/version-2 compatibility; library fragments remain without Intelligence configuration.
- Query tests for exact profile-version binding across both windows, exact synchronized UTC matching only, inclusive numeric boundaries, Boolean and ordinal String/Enum values, missing/bad/unsynchronized/duplicate evidence, stale profile denial before any read, TAG/permit/fence revocation, cursors, cancellation, aggregate row cap/no partial, and frozen 4A–4D regressions.
- Local Release API + Web builds and focused Intelligence/API/UI/package tests on isolated resources; `git diff --check`; Windows/Linux-compatible implementation and no weakening of checks.
- One draft PR with exact base/head/tree, changed symbols, local results and T1 profile `UI_EDITOR` plus any classifier-inferred backend profile. CODEX owns local/T1; Main owns review, merge and exact-merge T2. No next stage until Main accepts this slice.

## Explicitly outside 4E

No joint multivariate score/calibration (4F), representative validation dataset or Stage 4 closure (4G), Stage 5B dashboards, Stage 6, HA/driver/licensing changes, AI/learning, automatic baseline selection, diagnosis/fault probability/RUL, process/control writes, new SQL persistence, deployment, or changes to the stable owner Preview. The owner continues evaluating the delivered Preview in parallel; development/tests remain isolated.

Superseding Stage 4 status: the Product Owner closed Stage 4 engineering on 2026-10-21 within the experimental scope documented in [ADR-021](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md). Field-validation evidence remains deferred to future-wave [#701](https://github.com/brunolrogerio-collab/EliteSCADA/issues/701); this historical 4E exclusion list does not describe the current completion state.
