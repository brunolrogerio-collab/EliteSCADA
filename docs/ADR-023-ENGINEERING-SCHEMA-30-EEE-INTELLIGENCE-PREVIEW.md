# ADR-023 — Engineering schema 30 for EEE and Intelligence preview

**Status:** owner-authorized; implementation complete. Focused local tests pass 60/60, and the supplied package passes inspection, preview, and in-memory apply/export. Exact-head CI and application to the live local preview are pending.

## Context

The supplied EEE package is a local test-validation input, not product content or the final #306 productization deliverable. It is a valid `elitescada.project-package` format v3 package whose Engineering payload declares schema 29. It contains one `tags` resource organization with 36 folders and 74 assignments. The current integration baseline writes schema 28 and has no typed reader for that field, so an import followed by save/export could discard the organization metadata. Its Asset & Process Intelligence configuration is present but empty.

The owner asked to move Engineering to schema 30, incorporate this package into the local EEE preview, and prepare an Intelligence demonstration. The local preview is not the final Wave 15 Preview gate from #300.

## Decision

1. Engineering schema 30 is the current writer/export version. Project-package format remains v3; the two version numbers remain independent.
2. Schema 29 packages remain readable. `resourceOrganizations` is typed, validated, persisted through the normal Engineering package lifecycle, and exported from schema 30 without losing folder IDs, hierarchy, membership, or seeded state. Import updates only resource kinds present in the package; absent kinds remain unchanged, and an explicit empty organization clears that kind.
3. Schema versions below 29 cannot carry resource-folder data. If such an import would erase already stored folder organization, Preview fails closed and asks for a schema-29-or-newer export.
4. The preview-only Intelligence demonstration will use the existing `AssetProcessIntelligenceEngineeringDto` contract to configure Pump 01 with its existing Current, Voltage, Frequency, Discharge Flow, and Discharge Pressure TAGs and link it to an EEE process stage. Runtime views remain ordinary editable Screens. The data source is the EEE simulation; this does not introduce stored samples, diagnosis, a second dashboard model, or field-performance claims. Scope activation still requires the existing license and Authority grant.
5. Import continues through `ProjectPackageService` Preview/Apply. Security Authority policy, licensing state, users, and credentials remain outside the Engineering package and unchanged by this migration.

## Consequences

- Importing the supplied schema-29 package and exporting it again produces a schema-30 package while preserving its resource organization.
- The supplied file passed an in-memory Preview/Apply/Export round-trip: input schema 29, output schema 30, 241 TAGs, 22 Screens, 2 Equipment, and the 36-folder/74-assignment organization preserved.
- The original supplied package remains unchanged and is not committed to the product repository; a protected rollback export was saved before preview work. The local preview login request returned HTTP 401, so no import or Authority mutation has been applied.
- Runtime dashboard views, if generated, remain ordinary editable canonical Screens and use the existing Runtime navigation.
- This is a local demonstration checkpoint only. It does not close #588, #306, #300, Stage 4, or Wave 15 acceptance.
