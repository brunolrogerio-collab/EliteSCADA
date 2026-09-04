# W14-C17 — Convergence Fix Handoff — New Data Source identity race

**State:** **CHANGES REQUIRED / DEV AUTHORIZED FOR THIS BOUNDED FIX ONLY**  
**Correction branch:** `wave14/c17-convergence-datasource-new-race`  
**Exact correction base:** `607a60d0e930fc7080e09c0689c306c040c4ace6`  
**Historical accepted C17 candidate:** `6db4fb33f06159f108ca17ceca23a35ee158b228`  
**Historical C17 intake PR:** `#249`  
**Integration target:** `wave14/corrections-integration` / DRAFT PR `#212`  
**C18:** **HOLD** until C12–C17 converge  
**C11:** **IMPLEMENTATION LOCKED**

GitHub is official memory. Revalidate live refs before changing code.

## 1. Why this correction exists

C17 was previously accepted because its isolated exact-head validation and its first integration composition passed. Later C16 composition changed the Wave11 sequence and exposed a latent race in the same normal Data Source authoring path used by C17.

Last combined-green product authority:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Wave11 #265 / run `33838725796` — SUCCESS.

C16 composition product-code SHA:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Wave11 #266 / run `33869678407` — FAILURE.

Both PR runs used the same `main` base:

`edbdf446ea657713bdc487be91bf10bfcd03c684`

Therefore `main` drift is excluded.

## 2. Failure evidence

Wave11 fails in:

`web/scada-web/tests-wave11/c17-memory-lifecycle.spec.ts`

The later TAG creation cannot find Source key:

`memory.server.c17`

The retained Playwright trace proves canonical Working state actually lost that Source before the selector lookup.

Sequence captured in the artifact:

1. Built-in Simulation exists with id `40000000-0000-0000-0000-000000000001` and `metadata.system=true`.
2. User enters normal `Nova Data Source` flow and immediately chooses Server Memory.
3. The new `Server Memory C17` candidate unexpectedly contains that same id and system metadata, replacing Built-in Simulation on Apply.
4. User again enters normal `Nova Data Source` flow and immediately chooses Client Memory.
5. `Client Memory C17` again inherits id `40000000-...0001`, replacing Server Memory C17.
6. TAG authoring then cannot find `memory.server.c17` because it no longer exists.

This is not a slow selector and not the previous IEC-104 flake.

## 3. Root cause

Relevant frontend:

`web/scada-web/src/engineering/DataSourceCatalogEditor.tsx`

Current new-mode transition:

- button handler `choose(NEW_DATA_SOURCE_IDENTITY)` changes `selectedIdentity` immediately;
- `isNew` therefore becomes true on the next render;
- fresh `draft = newDataSourceDraft()` is installed only later by a `useEffect` observing `selectedIdentity`;
- until that effect runs, `draft` can still be the previously selected persisted Source.

If type selection happens in that window, `changeType()` calls:

`switchDataSourceType(draft, type)`

`switchDataSourceType()` in `DataSourceCatalogEditor.logic.ts` intentionally spreads the supplied Source so editing an existing Source preserves entity-owned fields. In the race window, however, the supplied Source is stale while the editor is already in **new** mode. That preserves prior `id`, `metadata` and any other carried fields in what should be a fresh entity.

`newDataSourceDraft()` itself is correct: it does not assign an id.

Backend `DataSourceEngineeringHandler` is also behaving according to the submitted stable identity: an incoming explicit id resolves the existing Source and Upsert updates it. Do not weaken that backend contract to compensate for the frontend race.

## 4. Required bounded correction

Implement the smallest generic correction that guarantees entering **New Data Source** is atomic from the user's perspective.

Required invariants:

1. As soon as new-mode is selected, all interactive controls must operate on a fresh new-source draft, never the prior persisted Source.
2. A new draft must not inherit prior canonical `id`.
3. A new draft must not inherit system or other entity `metadata` accidentally.
4. Prior settings / secret references must not leak unless they are defaults explicitly supplied by the newly selected Data Source type.
5. Existing Source editing must continue preserving its stable identity and intended metadata.
6. Solution must be generic/catalog-driven; no `builtin.memory.server`, `builtin.memory.client`, fixed GUID, C17 or DEMO special case.
7. Do not add sleeps to Wave11 as the product fix.
8. Do not bypass normal Engineering UI with hidden package mutation.
9. Do not relax backend stable-identity semantics.

A valid implementation could make the new-mode selection and draft reset synchronous/atomic in the event transition, or otherwise structurally prevent controls from using a stale draft. DEV owns the implementation choice, but the invariants above are mandatory.

## 5. Required regression coverage

Add deterministic test coverage that does not rely on scheduler luck.

At minimum prove:

- start with an existing Source carrying a stable id and metadata;
- trigger `New Data Source`;
- immediately choose a different Data Source type, without waiting for an artificial timeout;
- inspect/submit the resulting candidate;
- prove the new entity does **not** carry the old id or metadata;
- prove existing Source remains present and unchanged;
- repeat creation so two consecutive new Sources receive independent identities after Apply;
- preserve the real C17 Server Memory + Client Memory Engineering UI lifecycle.

Prefer a focused frontend/state regression test plus the existing Wave11 full lifecycle. Do not replace the existing E2E with only a unit test.

## 6. Validation requirements

Before coordinator acceptance, report exact corrected candidate SHA and exact workflow run IDs.

Required candidate validation:

- EliteSCADA CI;
- Wave 11 Active HMI Runtime;
- Preview Licensing CI;
- L3 Seven-Driver Lab;
- Interop Lab Smoke.

Because this correction starts from the exact red combined product `607a60d0...`, Wave11 must demonstrate the previously failing C16 -> C17 sequence now passes without weakening assertions.

After candidate review, Coordinator will integrate the correction into `wave14/corrections-integration` preserving history, then require all five combined gates green on the new exact integration product-code SHA.

C12–C17 are not declared converged until that combined validation is green.

## 7. Delivery format

DEV delivery must include:

- branch `wave14/c17-convergence-datasource-new-race`;
- base `607a60d0e930fc7080e09c0689c306c040c4ace6`;
- corrected candidate SHA;
- changed files;
- concise root-cause/fix explanation;
- regression tests added/changed;
- five exact workflow run IDs/results;
- any known limitation.

Do not merge to `main`. Do not merge directly into integration without Coordinator review.

## 8. Coordination records

Issue #211:

- C18 HOLD / initial blocker diagnosis: comment `5541091621`;
- root-cause ownership decision: comment `5541152530`.

Historical C17 PR #249:

- post-integration CHANGES REQUIRED: comment `5541149386`.

## 9. Current marker

**C17 CONVERGENCE FIX — DEV AUTHORIZED ON BOUNDED BRANCH / NOT ACCEPTED**

C18 remains HOLD.

C11 remains IMPLEMENTATION LOCKED.
