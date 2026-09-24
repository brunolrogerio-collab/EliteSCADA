# Wave 15 — FC0-A Post-FND06 Audit — Second-Pass Deep Review

> MAIN COORDINATOR deep review.
> GitHub live is the authority.
> This report supplements, but does not replace, the first audit result:
> `docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`.

`SECOND_PASS_ID: FC0A-POST-FND06-DEEP-AUDIT-02`

`STATE: COMPLETED / SCOPE_REFINED / NO_NEW_PRE_FC0A_BLOCKER_IDENTIFIED`

`AUDITOR: MAIN_COORDINATOR`

`EXACT_FROZEN_BASE_SHA: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`EXACT_FROZEN_BASE_TREE: 674019fbbc21001a2d68deb853c2c0b293e0a5cb`

`FINAL_BASELINE_BROAD: 35953557122 / EliteSCADA CI #1565 / SUCCESS`

## 1. Purpose

The first audit answered the release question and found two blocking items:
- W15-P1-01 Server Script recovery;
- W15-P1-06 truthful Engineering fallback.

This second pass asks a different question:

> What is already more mature than the first matrix suggested, what additional gaps remain below blocker level, and where can a frozen Foundation contract still be consumed incorrectly by a downstream DEV?

It therefore focuses on:
- cross-Foundation seams;
- downstream consumer correctness;
- live-data observability/freshness;
- Script Engineering maturity already present vs still missing;
- Runtime Session/Authority/Licensing interaction;
- FND-07 neutral-bootstrap feasibility;
- latent contract-consumption gaps in Editor/Script surfaces.

The active P1-01 correction branch is intentionally not used as audit authority. At second-pass completion it remained identical to this exact frozen base.

## 2. First-audit blockers remain authoritative

Second pass does not downgrade the two first-audit blockers.

### W15-P1-01 — Server Script recovery

Status:
`BLOCKED_FOUNDATION`

Permanent throttle-latch semantics remain a release blocker until the active bounded correction is integrated and revalidated.

### W15-P1-06 — truthful Engineering fallback

Status:
`BLOCKED_PRODUCT / SHARED_ENGINEERING_SHELL`

Synthetic `Demo Project` / no-model `unsaved|clean` presentation remains queued as a separate correction after P1-01.

No FC0-A lane is released by this second pass.

## 3. Deep finding A — Authority × Runtime Session × licensing contract is stronger than first-pass risk suggested

Exact source shows a coherent server-owned admission path:

- `ApiAuthorizationService.ResolveRuntimeSessionAdmissionAsync` evaluates canonical Authority before class selection;
- concrete command grants, writable TAG grants, Alarm acknowledge/shelve and TrendSave are considered;
- a user without any allowed Runtime mutation is downscoped to ViewOnly even if the client requests Interactive;
- `RuntimeSessionCapabilityProjection` keeps ViewOnly as a restrictive ceiling;
- logical lease identity remains subject + `ClientInstanceId`;
- lease carries `ServerNode`, `ClusterId`, generation and Authority revision;
- capacity reservation is atomic in the logical lease store.

Capacity behavior is already implemented:
- Interactive capacity available -> Interactive;
- Interactive quota full + ViewOnly seat available -> `InteractiveQuotaFallbackViewOnly`;
- Authority-read-only request -> ViewOnly pool;
- stable reason codes distinguish fallback/exhaustion/transition/revision cases.

### Disposition

`FND-03 / FND-02 INTERSECTION = PASS / FROZEN CONTRACT CONSUMABLE`

This materially reduces contract risk for:
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05.

No Foundation redefinition is required for those lanes.

## 4. Deep finding B — Web Runtime session UX is incomplete, but backend contract does not need reopening

Exact Web helper:
`web/scada-web/src/runtime/runtimeSessionAdmissionApi.ts`

Current behavior:
- always requests `connectionClass: 'interactive'`;
- returns only logical session headers to callers;
- does not expose/display `requestedClass`;
- does not expose/display `grantedClass`;
- does not expose/display admission/capacity reason codes;
- therefore automatic Interactive -> ViewOnly fallback can be correct on the server while remaining invisible to the user.

The public server response already carries:
- `requestedClass`;
- `grantedClass`;
- `admissionReasonCode`;
- `capacityReasonCode`.

### Disposition

`DOWNSTREAM GAP / DEV-LICENSING-UX`

DEV-LICENSING-UX must add:
- explicit View Only request path;
- truthful requested vs granted class;
- automatic fallback messaging;
- quota/usage/status UX using server truth.

This is **not** a Foundation blocker and must not rewrite FND-03.

### Minor API vocabulary debt

The parser accepts `viewer | viewonly | view-only | interactive`, and the canonical public response emits `viewOnly`.

One bad-request message still says:
`connectionClass must be 'viewer' or 'interactive'`.

Classification:
`MINOR API/UX CONSISTENCY DEBT`

Preferred owner:
DEV-LICENSING-UX or a minimal shared Runtime API cleanup in that lane.

## 5. Deep finding C — Script Engineering is more mature than the first audit matrix implied

Exact frozen product already contains:

### Cursor-safe insertion — implemented

`PythonMonacoEditor` has:
- insert-at-current-selection/cursor;
- indentation-aware multiline insertion;
- explicit undo boundaries;
- focus preservation.

Therefore the Wave14 A8 “consecutive insertion blindly concatenates whole snippets” class is substantially addressed at the editor seam.

### Event-specific authoring — substantially implemented

`EventsEditor` exposes:
- `timer`;
- `tagChanged`;
- timer interval;
- canonical TAG target;
- optional bit selector;
- Client Memory target;
- canonical Preview/Apply flow.

Each new reference is initialized with irrelevant fields null and then only the active event-kind field is populated, reducing the historical hidden-stale-field problem.

Mounted Wave 10 regression already exercises canonical timer/TAG-bit association persistence through Preview/Apply/reload.

### Project object/property discovery — partially mature

`scriptAssistantModel` already discovers:
- TAGs;
- Screens;
- Popups;
- visual objects;
- canonical object references;
- visual properties for canonical built-ins;
- Dynamo public parameters;
- Client Memory;
- capability names;
- snippets for TAG, Client Memory and visual-property operations.

### What still remains

`pythonEditorDescriptors.ts` API Help descriptors still primarily expose title + summary, not a formal signature/parameter/return/example contract.

The Product Owner representative UI-only proof remains important:
- compare two TAGs;
- evaluate condition;
- change a visual state/property;
- discover all required APIs/properties without guessing.

### Disposition

`W15-P1-05 DOWNSTREAM SCOPE NARROWED`

DEV-SCRIPT-ENGINEERING should **not** reimplement cursor insertion or basic timer/tagChanged authoring. Its remaining acceptance should emphasize:
- formal callable signatures/parameters/examples;
- complete discoverability;
- event-switch regression where relevant;
- representative compare-two-TAG/change-visual-state mounted workflow;
- contract-consumption gaps described below.

## 6. Deep finding D — Script Assistant bypasses the frozen FND-06 known-legacy compatibility boundary

Exact frozen source:
`web/scada-web/src/engineering/scripts/scriptAssistantModel.ts`

It imports and calls:
`getBuiltinVisualObjectSchema(element.type)`

On exception it returns:
`schemaStatus: 'unknown', properties: []`.

But FND-06 froze the central Engineering compatibility seam:
`getVisualSchemaForEngineering()`

for the known persisted legacy types:
- `tank`;
- `value`;
- `dynamo`;
- `status`.

Result:
- the main Property Inspector / Binding / Dynamic paths recognize those known legacy objects through the FND-06 compatibility boundary;
- Script Assistant can still classify the same object as unknown and hide its shared compatible properties.

### Disposition

`CONFIRMED DOWNSTREAM CONTRACT-CONSUMPTION GAP / DEV-SCRIPT-ENGINEERING`

Required DEV behavior:
- consume the frozen FND-06 Engineering compatibility seam;
- do not create another legacy table/alias;
- preserve arbitrary unknown fail-closed behavior;
- expose only properties the frozen compatibility schema actually declares.

This does **not** require reopening FND-06.

## 7. Deep finding E — DEV-EDITOR still has legacy-object manipulation paths that bypass the FND-06 adapter

Exact frozen files still use direct `getBuiltinVisualObjectSchema(element.type)` in authoring paths, including:

- `canvas/visualEditorSelectionModel.ts` — effective geometry/visibility used by marquee/topmost selection;
- `visualEditorZOrderModel.ts` — effective/updated zIndex;
- `visualEditorAuthoringModelImpl.ts` — geometry used by align/distribute/size/group operations.

Meanwhile the FND-06 corrected paths:
- Property Inspector;
- Binding Editor;
- Dynamic Property Editor

already consume `getVisualSchemaForEngineering()`.

Implication:
- A7 mounted click-selection and safe shared property edit are closed;
- but known legacy persisted objects can still hit strict-schema failures in advanced authoring interactions such as marquee geometry, z-order and multi-object authoring operations.

### Disposition

`CONFIRMED DOWNSTREAM CONTRACT-CONSUMPTION GAP / DEV-EDITOR`

This is not a reason to reopen FND-06 because:
- the compatibility authority already exists and is frozen;
- DEV-EDITOR owns the full single-primary-canvas/direct-manipulation authoring convergence.

DEV-EDITOR acceptance must explicitly prove known legacy objects can:
- participate in marquee/topmost selection;
- move/resize using shared geometry;
- change z-order;
- participate safely in align/distribute/size where semantically allowed;
- remain fail-closed for truly unknown types;
- use `getVisualSchemaForEngineering()` or the same frozen authority rather than a second compatibility table.

## 8. Deep finding F — W15-P2-01 Trends remains a real bounded observability gap

Exact component:
`web/scada-web/src/engineering/visual-editor/TrendVisualElement.tsx`

Current live state is essentially:
- idle;
- loading;
- ready;
- error.

It combines:
- REST snapshot polling;
- WebSocket realtime messages.

But the component does not expose a complete realtime-channel state contract:
- no explicit socket open/close/error/reconnecting state;
- no `lastRequestAt`;
- no `lastSuccessAt`;
- no freshness age/reason;
- no bounded request timeout visible at this layer;
- a closed realtime socket can coexist with REST polling without a clear user-facing distinction.

### Disposition

`W15-P2-01 = CONFIRMED BOUNDED PRODUCT GAP / NON-BLOCKING FOR FC0-A`

Do not invent a broad protocol rewrite.

Required later follow-up should expose truthful live-channel/freshness state and prove reconnect/REST fallback behavior.

Owner:
Wave 15 bounded Runtime/visual observability follow-up after FC0-A blockers; not DEV-EDITOR Foundation redesign by default.

## 9. Deep finding G — W15-P2-02 Popup/live-value quality is partly correct; freshness telemetry remains incomplete

Exact shared hook:
`web/scada-web/src/engineering/visual-editor/visualEditorLiveValues.ts`

It is also consumed by Runtime:
`RuntimeVisualDefinitionRenderer`.

Positive evidence:
- REST + WebSocket paths exist;
- quality and timestamps propagate;
- disconnected/unavailable state exists;
- numeric zero is not treated as falsy/unavailable;
- bad quality is not rendered as Good value.

Remaining gap:
- no explicit `lastRequestAt`;
- no explicit `lastSuccessAt`;
- no freshness age/threshold/reason model;
- no user-visible distinction rich enough to explain stale vs disconnected vs polling fallback;
- prior value/timestamp can remain in the sample object while state moves to unavailable/disconnected, which is safe for display but weak for diagnosis.

### Disposition

`W15-P2-02 = CONFIRMED BOUNDED FRESHNESS/DIAGNOSTIC GAP / NON-BLOCKING FOR FC0-A`

The original “Good numeric zero” concern appears already avoided in code; the remaining issue is freshness/diagnostic truth.

## 10. Deep finding H — FND-07 compatibility risk is lower than the first audit's conservative estimate

Production host registration uses:

`new EngineeringWorkspace(seedDemo: false)`

With `seedDemo:false`:
- project key/name begin null;
- no Demo content is silently seeded;
- explicit `InitializeDemo()` is separate.

Therefore a truthful neutral workspace state is already representable without changing the core workspace descriptor contract.

Existing Authority primitives are substantial:
- durable/fail-closed `AuthorityDetachService`;
- `AuthorityAttachService`;
- `AuthoritySwitchService`;
- lifecycle epochs/session fencing;
- System Recovery application services.

Authority detach explicitly documents that Application lifecycle is outside its scope, which is appropriate.

### What FND-07 still needs

FND-07 remains necessary to orchestrate:
- Runtime/process-effect fencing;
- Application detach;
- Working-state consequence handling;
- Authority detach/session invalidation;
- neutral bootstrap;
- explicit license keep/remove/replace;
- no silent Historian deletion.

### Disposition

`FND-07 = COMPOSITIONAL / COMPATIBLE — CONFIDENCE INCREASED`

No frozen FC0-A consumer contract change is identified.

## 11. Deep finding I — FND-05 compatibility remains additive; no new break found

Second pass found no evidence requiring a change to:
- FND-03 logical lease identity;
- requested/granted Runtime class semantics;
- shared seat accounting;
- Authority ceiling;
- machine-license lifecycle;
- FND-04 TAG/source identity;
- FND-06 visual authority.

The server already exposes:
- `ServerNode`;
- `ClusterId`;
- Authority revision/generation;
- server-owned class admission;
- atomic shared-seat fallback/reason codes.

These are suitable extension points for HA continuity/fencing.

### Disposition

`FND-05 = ADDITIVE / COMPATIBLE — NO NEW CONTRACT RISK IDENTIFIED`

The existing hard guard on future HA/redundancy entitlement remains binding.

## 12. Updated downstream scope/risk matrix

| Lane | Second-pass status | New/refined mandatory item |
| --- | --- | --- |
| DEV-EDITOR | HOLD on first-audit blockers; contract consumable | consume FND-06 adapter in legacy marquee/geometry/z-order/align/distribute/size; no second compatibility authority |
| DEV-SCRIPT-ENGINEERING | HOLD on first-audit blockers; scope narrowed | do not redo cursor/event authoring; fix Script Assistant compatibility bypass; add formal API signatures/examples and representative UI-only flow |
| DEV-AUTHORITY-UX | HOLD on first-audit blockers; risk reduced | consume server Authority semantics; no hidden role-name/client authority |
| DEV-LICENSING-UX | HOLD on first-audit blockers; backend contract strong | expose ViewOnly request, requested/granted class, fallback/reason/usage UX; minor viewer/viewOnly wording cleanup |
| FND-05 | HOLD on first-audit blockers | remain additive around frozen lease/license/Script/visual contracts |
| FND-07 | HOLD on first-audit blockers; confidence increased | compose existing neutral workspace + Authority/System Recovery primitives; own Application/runtime detach orchestration |

## 13. Additional gap ledger

### New confirmed downstream gaps

1. `GAP-DEEP-01` — DEV-EDITOR known-legacy geometry/z-order/authoring bypasses FND-06 compatibility seam.
2. `GAP-DEEP-02` — Script Assistant known-legacy property discovery bypasses FND-06 compatibility seam.
3. `GAP-DEEP-03` — Web Runtime admission UI ignores server `grantedClass` / reason codes and has no explicit ViewOnly request UX.
4. `GAP-DEEP-04` — API validation message uses legacy word `viewer` while canonical wire vocabulary is `viewOnly`.
5. `GAP-DEEP-05` — Trends lacks explicit realtime/reconnect/freshness observability contract.
6. `GAP-DEEP-06` — shared live visual values lack explicit last-request/last-success/freshness-reason telemetry.

### Scope refinements / positive findings

7. `REFINE-DEEP-01` — cursor-aware Monaco insertion already exists; remove from DEV-SCRIPT implementation burden except regression.
8. `REFINE-DEEP-02` — timer/tagChanged authoring fields already exist and persist through canonical Preview/Apply; downstream scope is refinement/regression, not greenfield.
9. `REFINE-DEEP-03` — Authority-derived Runtime class + Interactive-quota fallback already exist server-side; DEV-LICENSING is UX consumption, not backend redesign.
10. `REFINE-DEEP-04` — production EngineeringWorkspace already supports true neutral no-Demo state; FND-07 does not need to break lifecycle descriptor semantics.

## 14. Release impact

Second-pass conclusion:

`NO NEW PRE-FC0A BLOCKER IDENTIFIED`

The release remains blocked by the two first-audit blockers only:
- W15-P1-01;
- W15-P1-06.

The six newly confirmed gaps are either:
- explicit downstream DEV contract-consumption work; or
- bounded P2 observability/freshness follow-up.

They must be recorded so they are not lost, but they do not justify serializing another Foundation correction before the four DEV lanes.

FND-05/FND-07 still do not require breaking changes to frozen FC0-A-consumed contracts.

## 15. Main sequencing consequence

Current sequence remains:

1. close P1-01;
2. exact-head T1 + merge + broad post-merge validation;
3. close P1-06 separately;
4. exact-head T1 + merge + broad post-merge validation;
5. re-run affected release audit rows on the new exact checkpoint;
6. if no new blocker/contract contradiction exists, release the four FC0-A DEV lanes + FND-05 + FND-07 in parallel.

At release, Main must inject the second-pass mandatory items above into the exact lane orders.

No product mutation is authorized by this report.
