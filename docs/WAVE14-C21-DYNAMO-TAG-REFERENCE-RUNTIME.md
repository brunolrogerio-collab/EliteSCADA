# W14-C21 — Dynamo TagReference Runtime Parameter Convergence

**Date:** 2026-09-05 BRT  
**State:** PRODUCT CORRECTION ACTIVE / C11 BLOCKER  
**Exact base:** `9cbbd8465e75b34d69199df6c865cc2233868c5b`  
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

Before implementation, verify the existing intended authoring convention that connects a Dynamo parameter definition to a child binding.

C11 currently marks child bindings with metadata identifying the parameter. This is evidence from the DEMO builder, not automatic authority for the generic product contract.

C21 must inspect existing Engineering types, validators, authoring UI and Wave09 tests before adopting or changing that convention. If no canonical convention exists, C21 must define one generically and prove it through normal Engineering/package persistence rather than adding a C11-only interpretation.

## 5. Validation

Required generic evidence includes:

1. `TagReference` parameter round-trip through Engineering/package persistence;
2. Active wire null/absence convergence;
3. malformed scalar + TAG-reference mixtures rejected;
4. one Dynamo definition used by two instances with distinct TAG references;
5. independent effective child bindings for both instances;
6. independent live subscriptions and rendered state/value changes;
7. existing Wave09 Dynamo composition tests remain green;
8. existing `EquipmentPath` behavior remains green;
9. normal Web build and affected browser lifecycle remain green.

After exact C21 bytes are accepted and merged into integration, C11 must consume that integrated fix and resume its two-pump HMI proof. C11 must additionally prove P01-only and P02-only operation independently, not merely count two rendered Dynamo containers.

## 6. Hard boundaries

- no EEE-, P01- or P02-specific product behavior;
- no duplicate hand-authored pump symbols as a workaround;
- no private C11 renderer/subscription path;
- no weakening of malformed-shape validation;
- no frontend fabrication of process state;
- no direct Runtime/package mutation outside normal product lifecycle;
- PR target is integration only;
- PR #212 remains DRAFT and unauthorized for merge to `main`.
