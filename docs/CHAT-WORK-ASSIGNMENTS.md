# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — fourth static audit hardening completed on Wave 07 after repository review found top-level malformed-view Preview gaps, stale current-schema Script tests, missing canonical `VisualObject` Script reference catalog, dropped prospective errors on existing Scripts and v10 visual-identity projection mismatches; exact current integration head is `e376b37d2772906dd667afa199a6d8882abd43ae`; all workers remain stopped; Wave 08 is not active.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, and current MustReadSpecific documents. Then verify real branch/head/CI and execute only the current authorized assignment.

## Current product gate

`VISUAL-RUNTIME-WAVE-07` is **FOURTH STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED / NOT MERGE-READY**.

- Wave 06: MERGED through PR #83; final CI #487 SUCCESS
- Wave 07 Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- Integration branch: `integration/visual-runtime-wave-07`
- Exact current integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`
- Integration PR: NOT OPEN
- CI mode: CONSTRAINED
- Owner-reported remaining allowance: approximately 19 included minutes until explicitly superseded by a reset report
- Latest Actions run: #488; no Wave 07 Actions triggered
- Workers: stopped
- Wave 08: NOT ACTIVE

Integrated worker heads:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

Coordinator hardening/convergence covers:
- truthful `Default` versus explicit `Engineering Base` semantics;
- typed `VisualObjectPropertySchema` authority;
- stable nested visual object IDs and transitional Schema v11;
- canonical binding semantics (`Key` = destination visual property, `Target` = source reference);
- frontend typed visual element projection;
- C#/TypeScript common property catalog parity;
- renderer-independent `core.*` built-in object schemas;
- canonical `assetRef = null | { assetId }` authority;
- schema-guided transitional string-property codecs;
- official frontend Engineering -> Runtime visual adapter;
- backend Preview enforcement for built-in schemas/properties/binding capabilities;
- exact-current-instance Python visual provider composition;
- prototype-safe and bounded Python bridge structured values;
- explicit Python Script override set/clear path through dispatcher, provider composition and actual Pyodide Worker module;
- fail-closed malformed/null visual Engineering Preview paths;
- signed-Int32 TypeScript/C# integer parity;
- stale Wave 06 policy snapshot reconciled to Wave 07 structured-value limits;
- top-level Screen/Popup malformed JSON fail-closed handling;
- canonical `VisualObject` Script dependency and object-scoped event resolution using stable `definitionId/objectId` identity;
- package-derived recursive nested visual object reference resolution;
- malformed Script/reference collections blocked during Preview rather than deferred to Apply;
- prospective validation issues on already-saved Scripts surfaced as blocking `script-model` issues;
- legacy v10 Screen/Popup child identity projection aligned with Apply/key-preservation semantics;
- prospective Screen/Popup/Dynamo definition identity precedence aligned with the real import handlers.

Earlier deterministic test corrections:
- `cd2753f64a8191df3d2861871bb53077b74cc7a2` — corrected stale AssetReference source-contract expectation;
- `c3f9cc15a6715bf6434b1553878ba7c6121e0783` — aligned browser registry acceptance to identity-only asset references;
- `63878f6fe28a0a9ac101d622628f8b95658899a7` — aligned Engineering projection tests to `createDefaultValues()` and identity-only asset shape.

Third static audit hardening ended at:
- `d184fdd5b65f2ce0c0e6ca28cd092644be080555` — Python clear, malformed nested visual Preview, Int32 parity and Wave-06 policy-test correction.

Fourth static audit hardening ends at:
- `e376b37d2772906dd667afa199a6d8882abd43ae` — top-level malformed-view handling, current-schema test correction, canonical nested visual Script references, prospective existing-Script integrity and v10 identity projection reconciliation.

### Temporary no-Actions rule

Until the owner explicitly reports reset:
1. do not open Wave 07 PR;
2. do not manually dispatch/rerun Actions;
3. do not merge Wave 07 into `main`;
4. do not activate Wave 08 workers;
5. do not implement Wave 08 graphical functionality;
6. preserve all final CI requirements.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `VISUAL-RUNTIME-WAVE-07 / WAVE-08 READINESS`  
**Status:** `WAIT_FOR_OWNER_CI_RESET — FOURTH STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED`  
**IntegrationBranch:** `integration/visual-runtime-wave-07`  
**CurrentIntegrationHead:** `e376b37d2772906dd667afa199a6d8882abd43ae`

**CurrentTask:** no further speculative product-code work. On `siga`, re-read current `main` and verify real branch/PR/CI state. Resume implementation only if the owner explicitly reports Actions reset or repository review demonstrates another concrete correctness blocker that can safely be addressed without CI.

**MustReadSpecific:**
- `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`
- `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/VISUAL-ASSETS-AND-IMAGES.md`
- canonical Engineering contracts/import-export/validation/views/scripts
- existing C# `VisualScripting` foundation
- integrated TypeScript `web/scada-web/src/visual-runtime/**`
- Client Visual Python dispatcher/provider/Worker tests when reviewing visual scripting authority.

**Canonical convergence settled:**
1. stable nested visual object identity is established through transitional Schema v11;
2. visual binding target semantics are explicit without adding a redundant field;
3. frontend Screen/Popup element projection is typed;
4. C# and TypeScript runtime source semantics distinguish registry Default from Engineering Base;
5. common visual property catalog and built-in `core.*` schema sets are aligned structurally;
6. `assetRef` identity/authority semantics are aligned as `null | { assetId }`;
7. schema-guided transition codecs isolate current string persistence;
8. official Engineering -> Runtime projection seam exists;
9. backend Preview validates built-in visual schema/property/binding authority;
10. Python visual Script overrides have explicit set **and clear** semantics without null sentinel;
11. malformed nested and top-level visual Engineering data remains on the Preview/ImportIssue path instead of throwing through later validation layers;
12. integer visual properties use the same signed-Int32 domain in TS and C#;
13. canonical Script Engineering can resolve nested visual objects by stable `definitionId/objectId` for dependencies and visual events;
14. prospective validation surfaces existing Scripts invalidated by incoming Engineering changes rather than silently dropping those issues;
15. legacy v10 Screen/Popup identity projection preserves existing child IDs by key when Apply would preserve them and rejects removed referenced children;
16. deterministic test drift discovered by static audits has been corrected on the integration head.

**Remaining readiness blockers before Wave 08 activation:**
- canonical JSON-native typed visual property persistence/migration is not yet settled; `VisualElementEngineeringDto.Properties` remains `Dictionary<string,string>` and the codec is transitional only;
- Wave 07 exact-final-head CI/merge remains mandatory.

**Lower-priority convergence items attached to typed-persistence/readiness work:** transitional numeric text is not guaranteed byte-identical between C# and JavaScript for every exponent representation; canonical `Tag` versus `Property` binding source discrimination should be retained before the graphical binding engine consumes it; older non-visual Engineering collections retain broader null/empty-ID hardening debt that is not current Wave 07 authorization.

Stable `assetRef` identity is sufficient to define the future Image/import slice. The first-class project asset entity, binary import/storage/serving and renderer remain Wave 08 implementation work.

**Repository-wide debt identified by audit, not current-scope authorization:** frontend lockfile/version reproducibility; .NET SDK/compiler pinning; branch protection for `main`; production CORS hardening; earlier typecheck for `tests-e2e`; cheaper separation of unit/source contract tests from full Playwright.

**AllowedScope while waiting:** official documentation/state verification; review/correction of newly demonstrated concrete correctness blockers only. After explicit Actions reset, coordinator may reconcile the branch, finalize typed visual persistence/migration, open the integration PR strategically and run the required exact-head validation.

**ForbiddenScope:** Canvas; graphical editor; zoom/pan/grid/snap; selection/drag/resize/rotation UI; Property Inspector UI; Object Palette UI; image importer/storage/renderer; Screen/Popup/Dynamo graphical authoring; production animation/tween engine; Wave 09/10 functionality; speculative refactors without CI.

**ValidationMatrix:** DEFERRED. Final exact Wave 07 head still requires Web + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + visual/Python acceptance before merge.

**StopCondition:** REACHED again after fourth static audit hardening. Remain stopped until explicit Actions reset unless another new concrete safe blocker is demonstrated.

---

# DEV 1 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `ed8d9af027173e6d265ff382dbc9c115bcd2e284`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

---

# DEV 2 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `d6c1e997178e0ce525233079effd442f59743386`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

---

# DEV 3 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

## Coordinator note

Do not create work merely to keep workers busy. Wave 08 becomes executable only after Wave 07 final validation/merge and the readiness conditions in `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md` are satisfied.
