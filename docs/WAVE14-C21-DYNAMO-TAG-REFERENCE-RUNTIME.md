# W14-C21 — Dynamo TagReference Runtime Parameter Convergence

**Date:** 2026-09-05 BRT  
**State:** PRODUCT CORRECTION ACCEPTED / READY FOR INTEGRATION  
**Exact base:** `9cbbd8465e75b34d69199df6c865cc2233868c5b`  
**Accepted product SHA:** `6d0d71bc91b08114f4c3d3238b56e4ca225b76bd`  
**Target:** `wave14/corrections-integration`

## 1. Trigger

C21 is a generic product correction exposed by the canonical C11 EEE DEMO while exercising a normal reusable Dynamo through the real lifecycle:

`Engineering JSON Preview -> Apply -> Save -> Publish -> Activate -> Active HMI Runtime`

C11 is paused at the affected Dynamo proof. No DEMO-specific workaround is authorized.

## 2. Confirmed defect — Active wire null convergence

A valid Dynamo instance with a `TagReference` parameter is authored without a scalar `value` field and with a stable `tagReference` only.

The package passes Engineering Preview/Apply. After persistence and activation, `/api/runtime/application` materializes the absent scalar field as `value: null`.

The Runtime Dynamo composer currently treats any defined `value`, including canonical JSON null, as a scalar payload and rejects the parameter:

`VISUAL_RUNTIME_DYNAMO_PARAMETER_SHAPE_INVALID: Dynamo parameter 'running' of kind TagReference cannot carry a scalar value.`

Observed sequence on C11:

- authored candidate: `TagReference` parameter has no `value` member;
- Active package: same parameter has `value: null` plus the valid stable `tagReference`;
- Runtime composition fails closed for both pump instances;
- Analog Fill and preceding lifecycle evidence remain functional, isolating the failure to Dynamo parameter composition.

Relevant C11 Wave11 runs:

- #313: first exposed invalid C11 authoring with explicit `value: null`;
- #314: reproduced the same Runtime rejection after C11 removed the `value` field from authoring, proving the null is introduced by canonical persistence/wire projection rather than the DEMO builder.

## 3. Required complete correction

C21 must fix the complete generic `TagReference` Dynamo Runtime contract, not merely weaken one null check.

### 3.1 Shape convergence

- canonical absent scalar data represented as JSON `null` must be semantically equivalent to absence for a `TagReference` parameter;
- a genuine non-null scalar payload together with `TagReference` must continue to fail closed;
- malformed, missing and wrong-kind parameter values must continue to fail closed;
- backend/frontend persisted wire representations must converge deterministically.

### 3.2 Effective child bindings

Per-instance `TagReference` parameters must affect the effective bindings of the composed Dynamo instance.

A reusable Dynamo definition bound through a public TAG-reference parameter must support two instances referencing different TAGs without modifying or duplicating the definition.

The Runtime must not silently fall back to definition-time TAG references when an instance supplies a valid `TagReference` parameter.

### 3.3 Live subscriptions

Runtime live-value subscription collection must subscribe to the effective composed instance references, not only the raw Dynamo definition bindings.

Two independent instances must therefore receive and render their own TAG samples.

### 3.4 Identity and existing semantics

Preserve:

- stable Dynamo definition identity;
- stable instance identity;
- stable composed child runtime identity;
- existing valid `EquipmentPath` substitution behavior;
- normal Screen/Popup composition;
- Active-revision authority;
- authorization/licensing/lifecycle boundaries.

## 4. Contract discovery gate

The discovery gate established that the intended public contract already exists and does not need replacement:

- Dynamo instance values expose first-class `TagReference` parameter data;
- child bindings opt into a Dynamo parameter through `metadata.dynamoParameter`;
- `dynamoRuntimeBindingProjection.ts` projects the per-instance TAG reference into the effective child binding;
- `runtimeDynamoVisualProjection.ts` performs definition normalization, instance normalization, composition, binding projection and instance-scoped child identity;
- the normal Active Runtime expands Dynamos before live-value subscription collection.

Therefore C21 does not introduce a new EEE-specific or alternate Dynamo contract. The defect is at the existing API/browser wire seam: definition defaults already converge JSON `null` to absent scalar data, while instance parameter values did not.

## 5. Implemented correction

`normalizeDynamoParameterValue()` now treats canonical JSON `value: null` as an absent scalar value while preserving the `TagReference` and selector.

A genuine non-null scalar value remains present. Consequently the existing Runtime compositor continues to reject mixed scalar + `TagReference` shapes with `VISUAL_RUNTIME_DYNAMO_PARAMETER_SHAPE_INVALID`.

Generic C21 evidence was added to the Wave11 chain. It uses:

- one ordinary `builtin.memory.server` Source;
- two generic Boolean TAGs;
- one reusable Dynamo definition;
- two instances of that definition, each parameterized with a different TAG reference;
- a child binding using the public `metadata.dynamoParameter` contract;
- normal Engineering Preview/Apply, Save, Publish and Activate;
- `/api/runtime/application` evidence that the Active wire contains `value: null`;
- live TAG writes proving instance A and instance B render independently in both directions.

The same test also proves stable instance-scoped child identities and continued fail-closed behavior for a genuine non-null scalar mixed with a `TagReference`.

## 6. Accepted validation evidence

Accepted product SHA:

`6d0d71bc91b08114f4c3d3238b56e4ca225b76bd`

Exact-SHA CI evidence, all **SUCCESS**:

- EliteSCADA CI #1387 — run `33947947184`;
- Wave 11 Active HMI Runtime #315 — run `33947947199`;
- Preview Licensing CI #337 — run `33947947182`;
- L3 Seven-Driver Lab #293 — run `33947947188`;
- Interop Lab Smoke #214 — run `33947947253`.

The validation-only PR used to trigger the main-targeted workflows is #268. It is evidence only, must be closed without merge, and is not an integration path.

C21 acceptance means the generic product blocker exposed by C11 is resolved at the accepted product SHA. After C21 is merged only into `wave14/corrections-integration`, C11 must consume that integrated correction and resume its HMI proof. C11 must still prove P01-only and P02-only behavior independently rather than merely count two rendered Dynamo containers.

## 7. Hard boundaries

- no EEE-, P01- or P02-specific product behavior;
- no duplicate hand-authored pump symbols as a workaround;
- no private C11 renderer/subscription path;
- no weakening of malformed-shape validation;
- no frontend fabrication of process state;
- no direct Runtime/package mutation outside normal product lifecycle;
- PR target is integration only;
- PR #212 remains DRAFT and unauthorized for merge to `main`.
