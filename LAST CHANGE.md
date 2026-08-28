# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE-07 FOURTH STATIC AUDIT COMPLETE / CI AVAILABLE / PRE-FINALIZATION / NOT MERGE-READY**  
**CI budget mode:** **NORMAL**

## Mandatory resume reading

Before any action read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`, `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`, plus current source/tests.

GitHub branch/PR/head/CI is operational truth.

## Wave 06 — merged baseline

- PR #83: MERGED
- final product head: `d665dc13b0922938a15252d9775ef6604e41bff4`
- final CI #487: fully green
- main merge: `cc79713434c1d7b5988158b843b137eaf488d923`

## Actions availability restored

The previous no-Actions freeze is lifted.

On 2026-08-28 the owner reported a GitHub configuration change expected to provide approximately **1000 additional Actions minutes**. Repository tooling cannot independently read the billing balance, so the exact minute total remains owner/account evidence.

A controlled availability probe was performed by rerunning only the historical Wave 06 `Web build` job from run #488:

- run: #488 / ID `33194817294`
- attempt: 2
- job: `Web build`
- job ID: `98990802140`
- hosted runner allocated successfully
- Checkout: SUCCESS
- Node setup: SUCCESS
- dependency install: SUCCESS
- `npm run build`: SUCCESS
- complete job: SUCCESS

This proves Actions execution is available again. It does **not** validate Wave 07 because the probe ran against historical head `cc79713434c1d7b5988158b843b137eaf488d923`.

The overall historical run #488 may still display failure because only Web build was intentionally rerun; the old Backend failure-without-runner was not rerun and Chromium remained skipped.

## Wave 07 — exact current state

Wave 07 remains **NOT MERGED** and **NOT MERGE-READY**, but it is no longer blocked by CI budget.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- integration branch: `integration/visual-runtime-wave-07`
- exact current integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`
- integration PR: not open yet
- exact integration head has not yet received final integrated CI

Worker deliveries remain integrated and workers remain stopped:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

## Wave 07 implemented/converged foundation

The integration branch contains:

- typed Visual Property Registry and renderer-independent Runtime Visual Instance;
- stable visual definition/object identity, lifecycle and per-client isolation;
- deterministic `Animation > Script > Binding/Expression > Engineering Base > Default` precedence;
- runtime-readable/runtime-writable/animatable/binding authority checks;
- canonical `assetRef = null | { assetId }` with path/URL rejection;
- transitional Engineering Schema v11 with stable nested visual IDs and v1-v10 compatibility;
- canonical binding semantics (`Key` = destination visual property, `Target` = source reference);
- typed frontend visual Engineering projection;
- C#/TypeScript common property catalog and `core.*` built-in schema parity;
- schema-guided transitional string-property codecs;
- official frontend Engineering -> Runtime adapter;
- backend Preview enforcement for built-in schemas/properties/binding capabilities;
- Client Visual Python visual-property read/write/explicit-clear on the exact current Runtime Visual Instance;
- bounded/prototype-safe structured Python bridge;
- canonical Script Engineering resolution of nested visual objects for dependencies and object-scoped events;
- prospective existing-Script validation against incoming Engineering changes;
- legacy v10 visual identity projection aligned with Apply semantics.

## Static audit history

Earlier audits corrected deterministic AssetReference/API-test drift through `63878f6fe28a0a9ac101d622628f8b95658899a7`.

Third audit head `d184fdd5b65f2ce0c0e6ca28cd092644be080555` additionally corrected:

- actual Python visual-property clear path;
- malformed nested visual JSON fail-closed behavior;
- TS signed-Int32 parity;
- stale Wave 06 Python policy snapshot that would have failed Chromium.

Fourth audit head `e376b37d2772906dd667afa199a6d8882abd43ae` additionally corrected:

- top-level malformed Screen/Popup fail-closed paths;
- stale current-schema Script assertions after Schema v11;
- canonical `VisualObject` Script reference catalog and nested package resolver;
- malformed Script/reference collections blocked in Preview;
- dropped prospective errors on already-saved Scripts;
- v10 Screen/Popup child identity preservation/removal semantics;
- prospective Screen/Popup/Dynamo definition identity precedence matching Apply.

All fourth-audit code still needs actual build/test execution on the final candidate head.

## Remaining readiness work before final Wave 07 CI

### Canonical typed visual persistence

`VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>`. Schema v11 is transitional identity/convergence, not final JSON-native typed visual persistence.

Before Wave 08 activation, settle and validate the typed representation/migration so the graphical editor consumes canonical Engineering rather than an editor-private/stringly model.

### Reproducibility blockers worth settling before expensive final validation

Repository audit also identified:

- frontend dependencies floating on `latest` with no committed `package-lock.json` and CI using `npm install` rather than `npm ci`;
- no `global.json` while .NET language/compiler selection is floating;
- `tests-e2e` outside normal frontend typecheck.

Resolve deliberately if needed for trustworthy exact-head validation rather than using CI capacity as an excuse for unrelated refactors.

### Lower-priority convergence debt

- transitional C# and JavaScript numeric serializers can spell some exponent values differently;
- canonical `Tag` versus `Property` binding source discrimination should be retained before the graphical binding engine resolves sources itself;
- older non-visual Engineering collections have broader null/empty-ID hardening debt;
- production CORS and `main` branch protection remain repository/product hardening items.

## Current coordinator state

**READY_FOR_WAVE07_FINALIZATION — CI AVAILABLE.**

Do not activate Wave 08 yet. Do not open the Wave 07 PR merely to test Actions availability; that availability is already proven.

Next coordinator execution should:

1. re-read current `main` and verify exact branch state;
2. reconcile `integration/visual-runtime-wave-07` with current documentation-only `main` history;
3. settle canonical typed visual property persistence/migration and required compatibility coverage;
4. settle the minimum reproducibility blockers needed for trustworthy CI;
5. prepare the exact final candidate head;
6. open one Wave 07 integration PR;
7. run Web + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + Wave-specific visual/Python acceptance;
8. fix root causes only and rerun changed exact heads as needed;
9. merge Wave 07 only fully green;
10. activate Wave 08 only after its Definition of Ready is satisfied.
