# ADR-027 — Authorable CheckBox and ComboBox controls

**Status:** accepted implementation design for product issue [#790](https://github.com/brunolrogerio-collab/EliteSCADA/issues/790)
**Baseline:** `wave15/corrections-integration@cb9ff941e0c42369cb63efd13540da277970dad9`
**Date:** 2026-10-31

## Evidence at the baseline

- The frontend registry `web/scada-web/src/visual-runtime/builtinVisualObjectSchemas.ts` and backend registry `src/Scada.Engineering/VisualScripting/BuiltinVisualObjectSchemas.cs` expose Button, Slider and Numeric Input controls, but no authorable CheckBox or ComboBox.
- `VisualEngineeringPropertyValue` in `web/scada-web/src/engineering/types.ts` already supports JSON-native arrays and objects. The shared Visual Property Registry is scalar; object-owned structured settings therefore need an explicit schema, codec, validation and inspector editor.
- `CanonicalVisualRenderer` is the shared visual renderer; Runtime enters it through `RuntimeVisualDefinitionRenderer`. Existing TAG writes use the renderer's `onTagWrite` path and the Runtime writer, keeping write permission enforcement on the server.
- Alarm suppression is already a typed product contract: `RuntimeAlarmSuppressionSnapshot`, `loadRuntimeAlarmSuppressions`, `setRuntimeAlarmSuppression`, and `releaseRuntimeAlarmSuppression` use the API from #789. That API admits an interactive Runtime session and the server checks `AlarmShelve` against every affected resource.
- The frozen #789 API has exactly two target kinds: one alarm or one operational Location. A Location target can include descendants and alarms from sibling equipment at that location. It is therefore not the default for a motor checkbox.

## Decisions

1. Add canonical `core.checkBox` and `core.comboBox` object types in the existing frontend/backend catalogues. Keep them in the same element tree, save/import/export/publish pipeline and canonical renderer; do not add a second canvas or platform-specific control.
2. A CheckBox is a generic Boolean control. Its normal mode can display any compatible Boolean source, including a TAG or calculated expression. Changing a process value requires a writable Boolean TAG and uses only the existing TAG writer. Alarm suppression is one optional, separately configured use case: it targets exactly one stable Alarm Definition identity selected in Engineering by its current human-readable alarm name. The stable identity stays inside the saved definition. Do not infer the target from a display name at Runtime or silently map an equipment to a broad Location.
3. The suppression checkbox reads the canonical Runtime projection after mutations. Direct suppression can be checked/unchecked. If suppression is inherited from an ancestor Location, show a mixed/indeterminate state and the cause, and do not represent clearing the local target as clearing the inherited suppression. Pending/error never changes the saved checked state. Use the frozen #789 endpoints and authorization; do not add an endpoint or change #789 semantics in #790.
4. A ComboBox stores a typed list of primitive `value` / localized-ready `label` options in its object-owned structured properties. Its selected value is the existing `value` binding; it writes only through the canonical TAG writer. Reject empty/duplicate options, value-type mismatch, absent current values and bad/stale source quality with an explicit state; do not choose another option silently.
5. The shared object schemas/codec and backend import validation must recognize and validate the structured settings. IDs are selected through names in Engineering and never displayed as author-facing form fields. Preserve the existing distinction between `SetTagValue`, `ToggleTagBoolean` and `ExecuteCommand`; no scripts/HTTP are introduced as control shortcuts.
6. Both controls have no Intelligence-license dependency. Runtime licensing/session admission and Authority remain server-enforced. Historical playback is read-only. Help installed with the product and the three manual locales are updated in the same implementation candidate; screenshots are taken only from the accepted real interface.

## Product Owner clarification — 2026-10-31

The CheckBox must not be limited to alarms or to displaying TAGs. The user clarified that it is a general Boolean control; alarm suppression was an example of a possible use. Any compatible Boolean source can drive display-only state; process changes require an explicitly writable Boolean TAG. Alarm suppression remains an optional typed configuration that reuses the already frozen #789 contract.

## Verification

Focused schema/codec/import/export tests; selection and authoring round-trip tests; CheckBox Boolean TAG and ComboBox typed-value/quality tests; suppression direct/inherited/pending/error/read-only tests using the #789 API client; canonical editor and Runtime rendering tests; build and issue-specific T1, then T2 on the exact merge SHA. No Preview/DEMO changes.
