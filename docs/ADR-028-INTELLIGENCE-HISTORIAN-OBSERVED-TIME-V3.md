# ADR-028 — Explicit Historian observed-time analysis

**Status:** accepted implementation design for [#868](https://github.com/brunolrogerio-collab/EliteSCADA/issues/868)

**Baseline:** `wave15/corrections-integration@b6c2cc48c59645672d34fed10c123cb4abb403fc`
**Date:** 2026-11-05

## Context

The frozen `contextual-deviation-vector.v2` contract in `src/Scada.Api/Licensing/AssetProcessIntelligenceContextualDeviationVectorV2.cs` requires an explicitly synchronized source timestamp. The EEE preview's Memory-source Historian rows have a valid SCADIVO observation time but no device source timestamp (`source_ts` is null and evidence is `NotProvided`). Treating those clocks as equivalent would invent measurement-time evidence and could hide transport delay.

## Decision

1. Preserve V2 and its synchronized-source requirements unchanged. Add a separate `/contextual-deviation-observed/v3` catalog/query contract. V3 requires the request field `timestampBasis: "historian_observed"`, returns the same basis, and uses explicit V3 rule/catalog identities.
2. V3 aligns on the Historian observation/record timestamp (`Historian timestamp` / `TagValue.Timestamp`). Its response names the anchor and matched times as observed times, exposes observation evidence/rejection names, and does not publish source-synchronization fields. Quality, finite numeric values, UTC/window bounds, duplicate rejection, unit conversion, whole-vector tolerance, tie rejection, no reuse, bounded reads, cancellation, final revalidation and existing authorization remain enforced.
3. Timestamp basis is a visible Runtime choice. The source-time mode continues to call V2; the observed-time mode calls V3. Changing it clears prior results. There is no automatic fallback between modes.
4. Help and the Portuguese, English and Spanish manuals explain that record time is when SCADIVO/Historian received the reading; it can include communication/processing delay and is not the equipment's exact measurement time. Stored timestamps and historical data are never rewritten.
5. Reuse the existing bounded Historian query and analysis pipeline internally, but project a distinct V3 wire contract. Do not move policy, evidence or timestamps into the EEE package or Engineering workspace.

## Consequences and acceptance

- Rows with no source timestamp may form descriptive vectors only when the caller explicitly chooses observed-time V3. The same rows remain ineligible for V2.
- Zero vectors remain insufficient data, not evidence that no deviation occurred. The analysis does not certify physical simultaneity, health, efficiency or a fault.
- Tests cover the V3 catalog, explicit-basis fail-closed validation, observed-time alignment with source timestamps absent, duplicate rejection and response naming; existing V2 analytics tests remain green. Runtime E2E verifies explicit selection and result invalidation. Help/manual explain the limitation.
- Validate locally, run the issue-scoped exact-head CI, merge only after checks pass, then confirm both EEE pump screens on the integrated Preview without modifying demo data or license state.
