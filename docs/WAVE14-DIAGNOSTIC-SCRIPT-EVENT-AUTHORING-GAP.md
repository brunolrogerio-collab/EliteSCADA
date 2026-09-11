# Wave 14 — Diagnostic Closure: Script Entry-Point Event Authoring Gap

**Closure state:** `CONFIRMED_GENERIC_PRODUCT`  
**Priority transferred to Wave 15:** P1  
**Area:** Script Engineering / event and entry-point authoring  
**Boundary:** diagnosis/documentation only in Wave 14; correction belongs to Wave 15.

> GitHub live is the official authority. Revalidate these paths on the exact Wave 15 baseline before implementation.

## 1. Confirmed defect

Script Engineering exposes event kinds that the visible entry-point editor cannot fully configure.

The current event catalog includes at least:

- `tagChanged`;
- `timer`;
- `clientMemoryChanged`;
- `objectInteraction`;
- `initialize` / `dispose`;
- `propertyChanged`;
- `frameTick`;
- `serverRuntimeEvent`.

However, the entry-point row rendered by `ScriptEngineeringWorkspace.tsx` exposes only:

- event kind;
- handler name;
- `targetReference` text input;
- remove action.

There is no entry-point control in that row for:

- `tagReference` / TAG selector;
- `timerIntervalMs`.

## 2. Exact validation mismatch

`web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.logic.ts` defines event-specific validation:

### `timer`

A timer entry point is invalid unless `timerIntervalMs` is an integer at least `MINIMUM_SCRIPT_TIMER_INTERVAL_MS` (currently 50 ms). For `timer`, `tagReference` and `targetReference` are rejected as unexpected.

### `tagChanged`

A TAG-change entry point is invalid unless `tagReference.tagId` is present. `targetReference` is rejected for this event. Optional bit selector structure is validated when present.

### `clientMemoryChanged`

This event requires a non-empty `targetReference`.

Therefore the workspace can offer `timer` and `tagChanged` in its event selector while lacking the fields required to make a newly authored entry point valid.

## 3. Stale hidden field problem

The UI updates event kind by spreading the existing entry-point object and replacing only `eventKind`. Event-specific fields that are not shown remain on the object.

This creates a second deterministic authoring trap:

- an existing `tagChanged` entry with a persisted `tagReference` can be changed to another event kind;
- validation then reports `tagReferenceUnexpected` where that field is prohibited;
- the entry-point row has no visible `tagReference` control through which the developer can inspect/change/clear the stale value.

Equivalent stale-field behavior applies to `timerIntervalMs`: changing a persisted timer to a non-timer event can leave an invisible interval that validation reports as `timerIntervalUnexpected`.

## 4. Responsible layer

Primary owner:

`web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.tsx`

Validation contract owner:

`web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.logic.ts`

Related model:

`ScriptEngineeringEntryPoint` in the Script Engineering types/contracts.

The validation rules themselves are not the defect. The defect is that the editor does not project the event-specific contract into an authorable UI and does not normalize incompatible fields when event kind changes.

## 5. Wave 15 correction contract

Wave 15 must make entry-point authoring event-aware.

Minimum behavior:

1. Selecting `tagChanged` exposes a canonical TAG picker/reference authoring surface, including supported selector/bit semantics where applicable.
2. Selecting `timer` exposes a numeric interval field with the same minimum/bounds semantics as canonical validation.
3. Selecting `clientMemoryChanged` exposes an appropriate Client Memory target picker/reference surface rather than relying only on opaque free text where a structured reference is available.
4. Event kinds that do not permit TAG/timer/target fields must not retain incompatible hidden values after the user changes event type.
5. Changing event kind must either deliberately migrate compatible event-specific configuration or explicitly clear incompatible fields in the draft, with predictable user feedback.
6. Validation feedback must point to the visible field/action needed to resolve the error.
7. Entry-point authoring must use stable/canonical project references where the underlying model provides them; display names alone must not become authority.
8. Server and Client Visual event kinds must be contextually valid for the selected script scope; unsupported combinations should not masquerade as usable options.
9. No relaxation of backend/canonical validation is allowed merely to make the form submit.

## 6. Required deterministic regressions

### R1 — new `tagChanged`

- create a Client Visual script;
- add entry point;
- select `tagChanged`;
- choose a real TAG through the product UI;
- optionally select a supported bit selector;
- Preview/Apply succeeds with canonical `tagReference`;
- reload preserves the association.

### R2 — new `timer`

- select `timer`;
- enter valid interval;
- prove interval below minimum is rejected at the visible field;
- valid interval Preview/Apply/reload succeeds.

### R3 — event-kind transition clears stale TAG state

- start from a persisted/valid `tagChanged` entry;
- change event kind to one that disallows TAG reference;
- prove draft no longer contains incompatible hidden `tagReference`;
- Preview/Apply succeeds without manual JSON/API intervention.

### R4 — event-kind transition clears stale timer state

Equivalent test from `timer` to a non-timer event; no invisible `timerIntervalMs` may remain and block Preview.

### R5 — Client Memory event

- select `clientMemoryChanged`;
- select/author the intended canonical target;
- required target validation is visible/actionable;
- Preview/Apply/reload succeeds.

### R6 — invalid scope/event combination

Prove a Server-only event is not falsely presented as a valid Client Visual association and vice versa, according to the canonical contract finalized in Wave 15.

## 7. Relation to broader Script Engineering maturity

This confirmed defect is one concrete reason Script Engineering can feel non-functional even though substantial infrastructure exists.

Separate static analysis in `docs/WAVE14-STATIC-SCRIPT-ENGINEERING-CAPABILITY-MAP.md` shows that Monaco, project reference discovery, primitive TAG/visual-property snippets, diagnostics and Preview/Apply already exist. Wave 15 should preserve those foundations and make the developer workflow coherent rather than rebuilding blindly.

The broader A8 real-UI verification remains open because static analysis cannot prove discoverability, runtime debugging or the full Working -> Revision -> Published -> Active -> Runtime developer flow.

## 8. Wave 14 closure decision

This subfinding satisfies the Wave 14 diagnostic bar as `CONFIRMED_GENERIC_PRODUCT` because:

- the UI event options are statically confirmed;
- the visible entry-point fields are statically confirmed;
- the event-specific validation contract is statically confirmed;
- the mismatch is deterministic without relying on Codespaces transport behavior;
- the responsible files are identified;
- the Wave 15 correction and regression contracts are explicit.

No product code was changed.