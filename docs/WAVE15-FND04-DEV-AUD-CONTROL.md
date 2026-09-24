# Wave 15 — FND-04 DEV / AUD Control Plane

> **LIVE COORDINATION FILE — MAIN COORDINATOR AUTHORITY**
>
> This file is the operational control plane for two normal ChatGPT execution lanes:
>
> - `FND-04 DEV` — implementation owner;
> - `FND-04 AUD` — independent audit/test owner.
>
> Repository: `brunolrogerio-collab/EliteSCADA`
>
> Control branch: `coord/w15-fnd04-dev-aud-control`
>
> Product integration branch: `wave15/corrections-integration`
>
> GitHub live is the final authority. Every agent must re-read this file live on every user message `SIGA` before acting.

---

## 1. Authority model

The Main Coordinator owns:

- activation/deactivation of both lanes;
- exact base SHA issuance;
- scope boundaries;
- sequencing between DEV and AUD;
- acceptance/rejection of handoffs;
- merge authorization;
- VERIFIED/FROZEN promotion;
- FC0-A / downstream release decisions.

`FND-04 DEV` and `FND-04 AUD` are execution agents only. They may not redefine the Foundation contract, silently broaden scope, self-merge, self-freeze, release downstream work, or mutate `main` / `wave15/corrections-integration` directly.

If this file conflicts with old chat memory, old handoffs or stale prompts, this file plus GitHub live state wins.

---

## 2. Global state

`MAIN_ORDER_REV: 0020`

`LAST_MAIN_UPDATE_BRT: 2026-09-23 — FND-04 FROZEN / SEQUENTIAL CODEX REASSIGNED TO FND-06`

`GLOBAL_GATE: FND04_VERIFIED_FROZEN`

Current situation:

- FND-04 exact accepted candidate: `c89ad92ed38dcacc6d00c4a9b907720f9dbfcf9e` / tree `7fa948de25e4f120566113dc8d14a0a696342a32`.
- Independent AUD classification on that exact candidate: **ACCEPTABLE**.
- PR #336 merged normally with expected-head protection.
- Exact integration merge SHA: `6c810647c9773a19b212d9c33694780141786ac7`.
- Exact merge tree: `1221ff55963052be4e924dd644efbaa65763f546`.
- Natural exact post-merge `EliteSCADA CI` run `35913456486` / #1562: **SUCCESS**.
  - Web build `107358858133` — SUCCESS.
  - Backend build/test/smoke `107358858405` — SUCCESS.
  - Chromium end-to-end `107359503423` — SUCCESS.
- FND-04 is now **VERIFIED / FROZEN** at exact product checkpoint `6c810647c9773a19b212d9c33694780141786ac7`.
- The readable Script TAG reference contract is frozen for downstream consumption.
- FND-04 AUD remains **FROZEN / WAIT / NO_MUTATION**.
- The **same sequential CODEX executor/chat that executed prior Foundation work including FND-04 is now reassigned to FND-06**. FND-04 being frozen does **not** mean that CODEX is idle.
- Any later change to this shared contract requires a new Main/Foundation delta; downstream lanes may not redefine it.

Live integration divergence from the product base remains acknowledged only for verified INFRA-CI-01A + coordination documentation. Any other unacknowledged product delta remains `BLOCKED-BASE-DIVERGENCE`.
---

## 3. Frozen FND-04 product objective

FND-04 must freeze the shared Script TAG reference-resolution contract before `DEV-SCRIPT-ENGINEERING` is released.

Required behavior:

1. Normal developer-visible Python uses a canonical human-readable TAG reference, normally full visible path, for example `EEE.Process.LevelPct`.
2. Stable `TagId` / Guid remains the authoritative internal identity.
3. `tag_read` and `tag_write` use the same resolver semantics.
4. Runtime/backend proves that the visible reference still corresponds to the expected stable TagId before read/write.
5. Rename/move/path reuse must never silently retarget a script to another TAG.
6. Missing, ambiguous, stale or identity-drift references fail closed with deterministic machine-readable states.
7. The visible-reference <-> expected-TagId binding is persisted/versioned where required for safe round-trip.
8. Save/load/export/import/package behavior preserves the relevant identity binding.
9. Legacy GUID/TagId source has an explicit, tested compatibility/migration policy.
10. Canonical Authority remains applied; no second authorization pipeline is allowed.
11. No second TAG registry/resolver authority is allowed.
12. DEV-SCRIPT-ENGINEERING must be able to consume the frozen resolver contract without redesigning Foundation.

Canonical resolver states must be equivalent to:

- `found`
- `notFound`
- `ambiguous`
- `stale`
- `identityDrift`

Main may refine names during activation, but semantics may not be weakened.

---

## 3A. Main pre-activation source audit — exact integrated SHA `a3eb86f8...`

Main has already mapped the current Script/TAG authority surface so activation does not need another open-ended discovery cycle.

Observed live facts on the provisional next base:

- `ScriptEngineeringDependency` currently persists `Kind + StableReference`; normal TAG `StableReference` is GUID/TagId-first.
- `ScriptEngineeringReferenceResolver` already owns Script dependency catalog/resolution. For TAGs it carries both `EntityId` and `EntityPath`, but canonical normalization still resolves TAG dependency references as GUIDs.
- `ServerScriptRunner.py` currently exposes `read_tag` / `write_tag` using stable TAG ID strings and reports errors in GUID-first terms.
- `IsolatedPythonScriptHandlerExecutor.ResolveAllowedTags` parses TAG dependency `StableReference` directly as `Guid`; runtime values and write requests are keyed by TagId.
- `InMemoryTagRegistry` is already the canonical TAG registry and supports both `TryGet(Guid)` and `TryGetByPath(string)`; path ownership is unique while `TagDefinition.Id` is stable identity.
- `TagAccessAuthorization` already evaluates canonical Authority using both stable `ResourceId = TagId` and current `TagPath`. FND-04 must consume this path and must not create another authorization pipeline.

Prepared minimal contract direction for activation:

1. Canonical new authoring/source form uses the human-visible TAG path in Python, while persisting an explicit expected stable TagId binding beside that visible reference.
2. Runtime read and write use one shared resolver authority; no separate read resolver and write resolver.
3. Resolver success requires the current visible reference to resolve to exactly the persisted expected TagId.
4. Current path owner missing -> deterministic `notFound`/stale failure; multiple candidates -> `ambiguous`; current path owned by another TagId -> `identityDrift`; all fail closed.
5. Rename/move never silently retargets old source. Engineering may later offer an explicit rebind/update workflow, but runtime correctness remains fail-closed until that explicit update occurs.
6. Legacy GUID/TagId source remains accepted through an explicit compatibility path; new canonical authoring must not remain GUID-first.
7. Persisted binding must survive Script save/load and engineering package export/import round-trip.
8. Python sandbox receives only the already-resolved declared dependency map. It must not get direct TAG-registry or Authority access.
9. Read/write ultimately continue against stable TagId and the existing Runtime/Authority write boundary.
10. No second TAG registry, resolver authority, source parser authority or capability evaluator may be introduced.

Prepared primary source surface:

- `src/Scada.Engineering/Scripts/ScriptEngineeringContracts.cs`
- `src/Scada.Engineering/Scripts/ScriptEngineeringReferenceResolution.cs`
- Script persistence/import-export adapters only where required for the new versioned binding;
- `src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs`
- `src/Scada.Api/Runtime/ServerScriptRunner.py`
- minimum runtime host bridge needed to resolve one binding to stable TagId;
- focused Core/Drivers/PostgreSQL tests.

This is a prepared scope, not active authorization. If exact post-merge CI changes the product base or exposes a causal FND-03 defect, Main must revalidate this map before activation.


---

## 3B. EXECUTABLE IMPLEMENTATION PLAN — FND04-TAGREF-V1

**PLAN_STATE: ACTIVE / FROZEN_FOR_EXECUTION**  
**PLAN_ID: FND04-TAGREF-V1**  
**Provisional exact base:** a3eb86f8e1022675f84f0a76129a64d8e9d5faa6  
**Provisional tree:** e48c8b9918f4d3a5ae4dee1df6211393c95b6513  
**Implementation branch after activation:** work/w15-fnd-04-script-tag-reference-resolution  
**Target:** wave15/corrections-integration

GLOBAL_GATE is now FND04_ACTIVE. This plan is the binding executable scope for DEV order FND04-DEV-TAGREF-V1-01.

### 3B.1 Closed architectural choice

Use an additive typed binding on the existing Script dependency. Do not create a second registry, reference database or UI-only source of truth.

For TAG / ServerMemoryTag dependencies, add an optional v1 binding equivalent to:

~~~text
ScriptTagReferenceBinding
  Version = 1
  Reference = canonical human-visible TAG reference/path
  Expected = TagValueReference(TagId, optional structured selector)
~~~

Binding rules:

- existing StableReference remains the expected stable TagId for internal identity and compatibility;
- TagBinding.Reference is the developer-visible source reference;
- TagBinding.Expected.TagId must equal the TagId encoded by StableReference;
- TagBinding.Version must be exactly 1 for the new canonical form;
- a plain path without expected identity is never authoritative;
- existing GUID-only TAG dependencies with no binding remain an explicit legacy compatibility form;
- new authoring must not generate GUID-first source;
- ClientMemoryTag is outside this delta.

No new database table or separate sidecar store is allowed. The binding travels with the existing Script definition and existing save/load/export/import/package paths.

### 3B.2 Resolver states and deterministic classification

The contract exposes exactly:

- found
- notFound
- ambiguous
- stale
- identityDrift

Classification:

1. found: visible reference resolves to exactly one current TAG and resolved TagId equals expected TagId.
2. identityDrift: visible reference resolves, but its current TagId differs from expected TagId. Old-path reuse by another TAG is this state.
3. stale: expected TagId still exists, but its current canonical path differs from the persisted visible reference. Rename/move without explicit rebind is this state.
4. notFound: visible reference resolves to no TAG and expected TagId is also absent.
5. ambiguous: prospective/import validation sees more than one canonical candidate for the same visible reference. Runtime registry normally prevents this, but validation must classify it rather than select one.

identityDrift outranks stale when the old visible path is already owned by another TagId.

Diagnostics include state/code, visible reference, expected TagId, resolved TagId when present, and current canonical path of the expected TagId when useful.

### 3B.2A Canonical TAG path and source-binding equality rule

This rule is **FROZEN FOR FND-04 CORRECTION** and deliberately separates registry resolution from Script declaration membership.

#### A. Persisted binding -> canonical TAG registry

1. `TagBinding.Reference` stores the human-visible TAG path spelling selected at authoring time.
2. When Engineering/runtime proves that binding against the current TAG model, path equality follows the existing canonical TAG registry semantics: .NET `StringComparer.OrdinalIgnoreCase` / `StringComparison.OrdinalIgnoreCase`.
3. Therefore a case-only change in the current `TagDefinition.Path` is not a rename/move for FND-04 and remains `found` if the resolved stable TagId equals `Expected.TagId`.
4. `stale` requires that the persisted binding reference no longer resolves under the canonical registry semantics while the expected TagId still exists at a non-equivalent path.
5. `identityDrift` outranks stale when the persisted binding reference is currently owned by a different TagId.
6. Prospective/import ambiguity uses the same backend canonical path semantics.
7. The canonical TAG registry remains the only authority for path equivalence. Do not reimplement `OrdinalIgnoreCase` in JavaScript, Python, another table, cache, locale fold or Unicode helper.

#### B. Python source argument -> persisted declared binding

1. Matching a Python `read_tag` / `write_tag` argument to a Script dependency is **declaration membership**, not canonical TAG path resolution.
2. The source-visible token must match the persisted `TagBinding.Reference` exactly after the already-established outer `trim` behavior.
3. This matching is intentionally case-sensitive and does not perform locale case folding, Unicode normalization, separator rewriting, aliases or registry lookup.
4. Script Assistant emits the exact persisted binding spelling, so normal generated source is deterministic.
5. A source token with different casing/spelling is undeclared even if the backend registry would consider that path case-equivalent. It fails closed before process read/write.
6. Client Visual and Server Script must use the same exact declaration-membership rule.
7. Once declaration membership is established, runtime must still prove the persisted binding reference through the canonical registry/protected read path and compare the returned stable identity with `Expected.TagId` before read/write.
8. Stable TagId remains the only write identity.

This separation prevents a second TAG-path comparer authority while keeping registry/binding semantics consistent with the canonical registry.

### 3B.3 One read/write semantic

Client Visual Python:

1. source supplies the visible reference;
2. host finds the matching declared Script TAG binding;
3. existing protected read-by-path surface resolves the current TAG;
4. host compares returned stable ID with expected TagId;
5. read returns only after found;
6. write uses the same proof, then calls the existing protected write-by-TagId surface;
7. path is never used as write identity.

Server Script Python:

1. source uses the visible reference;
2. executor maps the declared binding to expected TagId before sandbox execution;
3. Active Runtime TAG identity/path is checked against the binding;
4. sandbox receives only the bounded declared reference/value map;
5. actual read/write remains through existing stable TagId host methods.

The Python sandbox receives no TAG registry, Driver, database or Authority object.

### 3B.4 Frozen authorities that must remain unchanged

Preserve:

- TagDefinition.Id / Guid as TAG identity authority;
- canonical TAG registry;
- TagValueReference stable identity/selector semantics;
- existing protected read-by-path and write-by-TagId backend surfaces;
- TagAccessAuthorization and capability evaluation;
- Runtime Session / ViewOnly / Interactive restrictions;
- Server Script Active-revision gate, sandbox, timeout, queue/coalescing and cancellation.

A display path must never become system identity authority.

### 3B.5 Closed production file allowlist

DEV may modify production code only in this allowlist:

1. src/Scada.Engineering/Scripts/ScriptEngineeringContracts.cs
2. src/Scada.Engineering/Scripts/ScriptEngineeringReferenceResolution.cs
3. src/Scada.Engineering/Scripts/ScriptEngineeringValidation.cs
4. src/Scada.Engineering/Scripts/ScriptEngineeringAdapters.cs
5. src/Scada.Engineering/VisualScripting/PythonScriptingContracts.cs
6. src/Scada.Engineering/ImportExport/Handlers/ScriptEngineeringHandler.cs
7. src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs
8. src/Scada.Api/Runtime/ServerScriptRunner.py
9. src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs — conditional, only for minimum Active TAG path/ID lookup required by the resolver proof; no lifecycle/recovery/ownership change
10. web/scada-web/src/engineering/scripts/scriptEngineeringTypes.ts
11. web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.logic.ts
12. web/scada-web/src/engineering/scripts/scriptAssistantModel.ts
13. web/scada-web/src/engineering/scripts/scriptAssistantReferenceValidation.ts
14. web/scada-web/src/python-runtime/createClientVisualPythonCapabilityProvider.ts
15. web/scada-web/src/python-runtime/clientVisualEventDispatcher.ts
16. web/scada-web/src/python-runtime/engineeringPythonPreview.ts — conditional only if required for binding-consistent preview

Explicitly not expected to change:

- src/Scada.Core/Tags/TagValueReference.cs
- canonical TAG registry interface/implementation
- src/Scada.Security/Authorization/TagAccessAuthorization.cs
- web/scada-web/src/runtime/tagInspectorApi.ts
- web/scada-web/src/runtime/runtimeTagWriteApi.ts
- web/scada-web/src/python-runtime/clientVisualPythonWorker.ts
- web/scada-web/src/python-runtime/pythonRuntimeContracts.ts
- .github/workflows/**

If an explicitly non-expected production file becomes required, stop under BLOCKED-CONTRACT instead of widening scope.

### 3B.6 Closed test allowlist

Focused test edits are limited to:

- tests/Scada.Core.Tests/ScriptEngineeringReferenceResolverTests.cs
- tests/Scada.Core.Tests/CanonicalScriptEngineeringTests.cs
- tests/Scada.Core.Tests/CanonicalScriptCompatibilityValidationTests.cs
- tests/Scada.Persistence.PostgreSql.Tests/PostgreSqlCanonicalScriptPersistenceTests.cs
- tests/Scada.Persistence.PostgreSql.Tests/ScriptEngineeringDependencyIntegrityTests.cs
- tests/Scada.Drivers.Tests/ServerScriptRuntimeAutomationIntegrationTests.cs
- web/scada-web/tests-e2e/script-assistant-model.spec.ts
- web/scada-web/tests-e2e/script-assistant-reference-validation.spec.ts
- web/scada-web/tests-e2e/script-engineering-workspace-roundtrip.spec.ts
- web/scada-web/tests-e2e/python-tag-write-capability.spec.ts
- web/scada-web/tests-e2e/python-runtime-host.spec.ts

One new focused file is allowed if useful:

- web/scada-web/tests-e2e/script-tag-reference-resolution.spec.ts

No broad fixture rewrite or unrelated test cleanup is authorized.

### 3B.7 Mandatory RED proof before production correction

From the exact activated base, create/run a small behavior-only RED slice before changing production behavior.

RED-1 — authoring readability:
- normal Script Assistant TAG snippet must contain canonical visible path;
- Guid literal must be absent.
- Old base must fail because canonicalReference is GUID-first.

RED-2 — Client Visual read/write convergence:
- use visible reference Plant.Process.LevelPct;
- injected reader returns stable ID A;
- writer spy must receive A, not the path.
- Old base must fail because provider forwards the input string directly to the writer.

RED-3 — Server Script readable reference:
- source uses read_tag("Simulation.ProcessState") / write_tag("Simulation.ProcessState", ...);
- declaration still names the existing stable dependency TagId so this RED test compiles against the old model;
- expected declared TAG must be read/written.
- Old base must fail because Server Script values/writes are keyed by GUID strings.

A RED test that passes on the old base is non-discriminating and must be corrected before implementation.

Record exact command, failing test names and failure reason.

### 3B.8 Mandatory GREEN matrix

All items below must be deterministic PASS:

1. canonical generated source uses visible path and no Guid literal;
2. v1 Reference + Expected TagId + Version survives normalize/clone/save/load/package round-trip;
3. persisted/PostgreSQL Working round-trip preserves the binding;
4. readable tag_read resolves found to expected TagId;
5. readable tag_write uses the same found proof and writes by expected TagId;
6. read and write expose the same resolution state/evidence;
7. rename/move of expected TagId causes stale and no retarget;
8. old path reused by TagId B while binding expects A causes identityDrift and B remains untouched;
9. missing path + missing expected ID causes notFound;
10. duplicate prospective canonical path causes ambiguous and no arbitrary choice;
11. structured TagValueReference selector remains bound to expected TagId;
12. legacy GUID-only dependency/source compatibility is explicit and tested;
13. undeclared readable source reference fails closed before any write;
14. ServerMemory binding cannot elevate a non-ServerMemory TAG;
15. allowed read/write continues through existing protected backend surfaces and denied write remains denied;
16. representative source binds at least two readable TAG paths to two distinct expected TagIds, reads both, compares values and performs a bounded conditional action without GUID literals;
17. no second TAG registry, second authorization evaluator or divergent read/write resolver exists;
18. focused Core + persistence + Drivers + Web tests are green, git diff --check is green, and natural exact-head EliteSCADA CI is green.

### 3B.9 Compatibility rules

- no mass rewrite of existing projects/scripts;
- legacy GUID-only form remains tested;
- new authoring uses path + expected stable identity;
- no automatic rename refactor in FND-04;
- explicit rebind/source-update UX belongs downstream;
- unchanged new-format source may become stale after rename; this is intentional fail-closed behavior;
- no new top-level Engineering package schema migration is expected;
- the binding carries its own Version = 1;
- if a top-level package schema bump or database migration proves necessary, stop for Main disposition.

### 3B.10 Out of scope

FND-04 does not implement:

- Monaco autocomplete/search UX;
- Project Object Browser redesign;
- cursor-placement polish;
- automatic source refactor on TAG rename/move;
- broad Script Engineering help/recipes;
- Server Script recovery/throttle redesign;
- HA ownership/fencing;
- FND-06 renderer;
- FND-07 Installation;
- FND-03 licensing changes;
- TAG registry redesign;
- Authority/capability redesign;
- Driver changes;
- workflow/CI architecture changes.

### 3B.11 Hard blocker criteria

Return exactly:

FND-04 DEV -> MAIN COORDINATOR — BLOCKED-CONTRACT

and stop if any is true:

1. integration gains an unacknowledged product/infra delta after assigned base;
2. visible path must become authoritative identity;
3. second TAG registry/resolver authority/cache-of-truth/auth evaluator is required;
4. read and write cannot share one semantic without reopening a frozen contract;
5. TagValueReference, TAG registry semantics or Security capability semantics must change;
6. protected read/write endpoints must be weakened or bypassed;
7. direct Driver/database access would enter Python;
8. sandbox allowlist/escape boundary must broaden;
9. new database migration/table is required;
10. top-level Engineering schema must change rather than additive binding;
11. FND-03, FND-05, FND-06 or FND-07 product change becomes necessary;
12. broad Script Engineering UI ownership is required;
13. any explicitly non-expected production file becomes necessary;
14. RED discrimination cannot be established against the exact base;
15. GREEN requires weakening/removing a security, package, runtime or sandbox assertion.

Environment-only inability to execute required tests returns:

FND-04 DEV -> MAIN COORDINATOR — BLOCKED-ENV

with exact missing dependency/tool. Unexecuted evidence remains PENDING.

### 3B.12 Reviewable implementation sequence

1. RED tests only.
2. typed binding + resolver state machine + validation/adapters.
3. Server Script readable-ref bridge.
4. Client Visual shared read/write binding + minimal readable snippet generation.
5. persistence/compatibility/adversarial GREEN tests.
6. git diff --check + focused validation.
7. open exactly one PR to wave15/corrections-integration.
8. natural exact-head CI.
9. DEV handoff; no self-merge.

No commit may mix unrelated cleanup.

### 3B.13 Final DEV acceptance table

DEV handoff reports PASS | FAIL | PENDING for:

1. v1 binding contract
2. readable source
3. stable TagId authority
4. shared read/write semantic
5. found/read
6. found/write
7. stale rename/move
8. identityDrift path reuse
9. notFound
10. ambiguous
11. selector identity safety
12. undeclared ref fail-closed
13. legacy GUID compatibility
14. save/load/package round-trip
15. persisted/PostgreSQL round-trip
16. ServerMemory restriction
17. Authority/security preservation
18. multi-TAG readable script
19. no second registry/resolver/auth authority
20. exact-head CI

Any FAIL or required PENDING blocks PR_READY.

### 3B.14 AUD attack matrix

After an immutable DEV candidate, AUD independently attacks:

- path case/canonicalization;
- rename then old-path reuse;
- expected-ID missing vs path missing;
- duplicate prospective path ambiguity;
- binding version invalid/missing;
- StableReference vs Expected.TagId mismatch;
- undeclared source literal;
- read/write divergence;
- write-after-resolution must not retarget another TagId;
- ServerMemory kind confusion;
- legacy GUID compatibility;
- selector identity drift;
- package round-trip;
- denied Authority write;
- accidental direct registry/Driver access;
- accidental second resolver/auth path.

AUD remains READ_ONLY unless Main later explicitly sets AUD_MODE: WRITE_TESTS.

---

## 4. FND-04 DEV lane

### Identity

`LANE: FND-04 DEV`

`STATE: BLOCKED_ENV / WATCH_ONLY`

Reserved implementation branch after activation:

`work/w15-fnd-04-script-tag-reference-resolution`

Target:

`wave15/corrections-integration`

### Ownership

DEV owns production implementation of the shared resolver contract and only the minimum production/persistence/API changes needed to satisfy the active work package.

DEV must not:

- redesign Server Script sandbox/runtime ownership beyond the active order;
- modify unrelated Editor/UX feature surfaces;
- create a second resolver or second TAG registry;
- bypass Authority;
- use mutable display path as authoritative identity;
- directly write to integration or main;
- merge its own PR;
- declare FND-04 VERIFIED/FROZEN.

### CURRENT DEV ORDER

`ORDER_ID: FND04-DEV-ENV-HOLD-02`

`ORDER_STATE: BLOCKED_ENV`

`DEV_MODE: WATCH_ONLY / NO COMPETING IMPLEMENTATION`

`EXACT_BASE_SHA: a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

`EXACT_BASE_TREE: e48c8b9918f4d3a5ae4dee1df6211393c95b6513`

`WORK_BRANCH: work/w15-fnd-04-script-tag-reference-resolution`

Instruction:

> Main accepts the reported environment blocker. Do not create production/test commits from this normal chat while the FND-04 CODEX EXECUTOR order below is ACTIVE. On `SIGA`, re-read this file, revalidate live branch/head and report status only. You may review the eventual immutable Codex handoff/candidate when Main asks, but you are not a second implementation team.

The original implementation order `FND04-DEV-TAGREF-V1-01` remains the binding implementation contract for the delegated Codex executor; it is not cancelled semantically, only reassigned operationally because this chat cannot execute mandatory RED/GREEN evidence.

### DEV mandatory return format

Normal implementation handoff must begin exactly:

`FND-04 DEV -> MAIN COORDINATOR — IMPLEMENTATION HANDOFF`

Blocked shared-contract return:

`FND-04 DEV -> MAIN COORDINATOR — BLOCKED-CONTRACT`

Environment-only blocker:

`FND-04 DEV -> MAIN COORDINATOR — BLOCKED-ENV`

Every DEV handoff must include:

- exact base SHA/tree;
- exact head SHA/tree;
- branch and PR;
- changed files/symbols;
- resolver public contract and states;
- persistence/binding contract;
- read/write integration points;
- rename/move/path-reuse behavior;
- legacy GUID policy;
- Authority preservation evidence;
- tests run and exact results;
- CI run/job IDs when available;
- `PASS | FAIL | PENDING` acceptance matrix;
- residual risks and deferred items;
- explicit confirmation of no second resolver/TAG registry/Authority pipeline.


---

## 4A. FND-04 CODEX EXECUTOR lane

### Identity

`LANE: FND-04 CODEX EXECUTOR`

`STATE: ACTIVE`

`RUNTIME_REQUIREMENT: functional checkout + dotnet + Node/Playwright + GitHub push/PR capability`

This lane is reserved for the **same CODEX execution chat currently finishing INFRA-CI-01A**. Product Owner explicitly chose sequential reuse of that executor instead of starting a second Codex implementation chat.

The FND-04 architecture/plan is unchanged. Only operational sequencing changes.

### CURRENT CODEX EXECUTION ORDER

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-INFRA-CI-01B-13`

`ORDER_STATE: ACTIVE_ROUTE`

`EXECUTOR_MODE: SAME_SEQUENTIAL_CODEX / REASSIGNED_TO_INFRA_CI_01B`

`FND04_STATE: VERIFIED_FROZEN`

`FND06_STATE: INTEGRATED / FREEZE_BLOCKED_GENERIC_INFRA`

`NEXT_CONTROL_BRANCH: coord/w15-infra-ci-01b-control`

`NEXT_CONTROL_FILE: docs/WAVE15-INFRA-CI-01B-CONTROL.md`

`EXPECTED_ORDER: INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V2`

Instruction:

> The shared sequential CODEX lane is not idle.
> FND-04 remains frozen and FND-06 product work is merged.
> Main has assigned this same CODEX lane to the generic PostgreSQL post-merge blocker `INFRA-CI-01B`.
>
> On every `SIGA`:
> 1. revalidate GitHub live;
> 2. read `coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`;
> 3. execute its latest ACTIVE order;
> 4. return with the INFRA-CI-01B mandatory handoff prefix.
>
> Do not report FND-04 WAIT or FND-06 WAIT while this routing order is current.

### CODEX mandatory return

Normal final handoff:

`FND-04 CODEX EXECUTOR -> MAIN COORDINATOR — FINAL CANDIDATE HANDOFF`

Contract blocker:

`FND-04 CODEX EXECUTOR -> MAIN COORDINATOR — BLOCKED-CONTRACT`

Environment blocker:

`FND-04 CODEX EXECUTOR -> MAIN COORDINATOR — BLOCKED-ENV`

---

## 5. FND-04 AUD lane

### Identity

`LANE: FND-04 AUD`

`STATE: WAIT_CORRECTED_CANDIDATE`

Default mode:

`READ_ONLY_REVIEW`

Optional test-only branch, created only after explicit Main order:

`audit/w15-fnd-04-script-tag-reference-resolution-v1`

### Ownership

AUD owns independent adversarial verification of the DEV candidate. AUD is not a second implementation team.

AUD must independently test/review at minimum:

1. normal human-readable source generation;
2. one resolver semantics for both read and write;
3. expected TagId validation;
4. rename behavior;
5. move behavior;
6. old-path reuse by a different TagId;
7. missing reference;
8. ambiguous reference;
9. stale binding;
10. identity drift;
11. save/load round-trip;
12. export/import/package round-trip where applicable;
13. legacy GUID/TagId compatibility/migration;
14. Authority still enforced on resolved read/write;
15. no second resolver, TAG registry or authorization pipeline;
16. representative multi-TAG readable script scenario.

AUD may not change production code unless Main explicitly sets:

`AUD_MODE: WRITE_TESTS`

If `WRITE_TESTS` is authorized, AUD may add only bounded adversarial/regression tests on its isolated audit branch from the exact DEV candidate SHA named by Main. Production-code fixes remain DEV ownership unless Main explicitly reassigns one.

AUD never merges its own work and never writes directly to DEV branch, integration or main.

### CURRENT AUD ORDER

`ORDER_ID: FND04-AUD-FROZEN-0011`

`ORDER_STATE: WAIT`

`AUD_MODE: FND04_FROZEN / READ_ONLY`

`FROZEN_PRODUCT_SHA: 6c810647c9773a19b212d9c33694780141786ac7`

`POST_MERGE_CI_RUN: 35913456486 / SUCCESS`

Instruction:

> FND-04 is VERIFIED/FROZEN after your ACCEPTABLE review and exact post-merge CI. On `SIGA`, revalidate GitHub live and report `FND-04 AUD — FROZEN / WAIT` unless Main has issued a new Foundation-delta audit order. Do not mutate or re-audit the frozen candidate speculatively.

### AUD mandatory return format

Normal audit handoff must begin exactly:

`FND-04 AUD -> MAIN COORDINATOR — AUDIT HANDOFF`

Critical defect found:

`FND-04 AUD -> MAIN COORDINATOR — REJECT CANDIDATE`

Shared-contract blocker:

`FND-04 AUD -> MAIN COORDINATOR — BLOCKED-CONTRACT`

Every AUD handoff must include:

- exact reviewed base/head/tree;
- PR and exact diff scope;
- independent acceptance matrix `PASS | FAIL | PENDING`;
- negative/adversarial evidence;
- exact tests/CI examined or run;
- defects with file/symbol/test evidence;
- scope leakage findings;
- concurrency/persistence/package concerns where applicable;
- final classification: `ACCEPTABLE`, `CHANGES_REQUIRED`, or `BLOCKED-CONTRACT`.

AUD classification is advisory evidence. Main makes the final integration/freeze decision.

---

## 6. SIGA protocol — binding for both chats

After the user sends the initial lane prompt once, later user messages may contain only:

`SIGA`

On every `SIGA`, the agent must:

1. re-read `docs/WAVE15-FND04-DEV-AUD-CONTROL.md` from branch `coord/w15-fnd04-dev-aud-control` live on GitHub;
2. revalidate `wave15/corrections-integration` HEAD;
3. identify its own lane and the latest `CURRENT ... ORDER`;
4. compare `MAIN_ORDER_REV` with the last revision it executed;
5. execute only the currently authorized order for its lane;
6. never reuse an old exact SHA after Main has advanced the order;
7. stop and report if the base moved by product/infra delta not acknowledged by Main;
8. diagnose red CI before any rerun;
9. return evidence to Main using the mandatory handoff prefix;
10. never infer that `SIGA` authorizes merge, main mutation, scope expansion or another lane's work.

If the order is `WAIT`, `SIGA` means revalidate and wait; it does not authorize speculative work.

### Sequential CODEX routing override

The CODEX executor is a **sequential shared lane across Foundation missions**. When its CURRENT CODEX order routes to another Foundation control plane, that routing order overrides the local FND-04 frozen state for executor activity. The executor must follow the routed control rather than waiting on FND-04.

---

## 7. Coordination / handoff ledger

Primary coordination ledger:

- Issue `#305` — Wave 15 execution/dependency orchestration.

FND-04 contract source:

- `#305` binding FND-04 delta and the live Wave 15 coordinator handoff.

**Handoff routing rule:**
- DEV/CODEX/AUD normal handoffs should be posted to Issue `#305` when the lane has GitHub-comment capability;
- PR-specific supporting evidence may additionally be posted to PR `#336`;
- if an agent runtime cannot post comments, it returns the full handoff in its own chat and stops; Main Coordinator records the evidence in `#305`;
- the Product Owner must not be used as a required message courier.

Agents must not edit this control file. Only Main Coordinator updates this file/order board.

---

## 8. Main Coordinator sequencing model

Planned normal sequence after FND-03 closes sufficiently for activation:

`MAIN activates DEV -> DEV candidate/PR -> MAIN preliminary review -> MAIN activates AUD against exact candidate -> AUD adversarial handoff -> MAIN sends DEV corrections if needed -> AUD rechecks bounded corrections -> MAIN validates exact-head CI -> MAIN authorizes integration -> post-merge CI -> MAIN VERIFIED/FROZEN decision`

Possible controlled parallelism:

- AUD may prepare a read-only test matrix while DEV implements, but may not judge an uncommitted moving target.
- AUD test-writing is allowed only from a specific immutable DEV candidate SHA after explicit Main authorization.
- DEV remains single owner of production-code corrections unless Main says otherwise.

This prevents two normal chats from creating competing Foundation architectures while still using them in parallel efficiently.

---

## 9. Acceptance target for FND-04 freeze

Main will not mark FND-04 `VERIFIED/FROZEN` merely because a PR exists or CI is green.

At minimum Main must have evidence that:

- source is human-readable without GUID-first normal representation;
- shared resolver semantics are single-owner and versioned enough for downstream;
- stable identity is enforced;
- rename/move does not silently retarget;
- path reuse by a different TagId fails closed;
- missing/ambiguous/stale/identityDrift behavior is deterministic;
- persistence/package round-trip preserves required binding;
- legacy source policy is explicit/tested;
- Authority is preserved;
- representative multi-TAG script behavior is proven;
- relevant exact-head CI/tests are green;
- exact integration SHA and post-merge validation are recorded;
- no unresolved `BLOCKED-CONTRACT` remains.

Only then may Main freeze FND-04 and decide whether `DEV-SCRIPT-ENGINEERING` / FC0-A dependencies can be released.

---

## 10. Initial prompt — FND-04 DEV chat

Use this once to initialize the DEV chat:

```text
Você é o FND-04 DEV do EliteSCADA.

GitHub live é a única autoridade sobre o estado do projeto. Você é um agente executor subordinado ao Main Coordinator e não decide sozinho arquitetura Foundation, integração, merge ou freeze.

Repositório: brunolrogerio-collab/EliteSCADA

Seu control plane obrigatório é:
- branch: coord/w15-fnd04-dev-aud-control
- arquivo: docs/WAVE15-FND04-DEV-AUD-CONTROL.md

Leia esse arquivo integralmente agora. Em seguida revalide o HEAD live de wave15/corrections-integration.

A partir desta inicialização, quando eu disser apenas "SIGA", você deve reler o control plane live, localizar a seção FND-04 DEV / CURRENT DEV ORDER e executar somente a ordem mais recente do Main Coordinator.

Nunca use uma ordem antiga por memória. Nunca escreva em main ou wave15/corrections-integration diretamente. Nunca faça merge ou declare VERIFIED/FROZEN sem ordem explícita do Main. Se o order state for WAIT, apenas revalide e aguarde.

Seu retorno deve seguir exatamente os prefixes e requisitos definidos no control plane.
```


---

## 10A. FND-04 CODEX bootstrap note — SAME EXECUTOR AFTER INFRA

Do **not** start a separate Codex executor while `FND04-CODEX-WAIT-INFRA-02` is current.

The Product Owner chose to reuse the CODEX chat that is finishing INFRA-CI-01A. When Main closes that dependency, the canonical Main handoff plus this control plane will be updated to an ACTIVE FND-04 CODEX order. The executor then re-reads GitHub live and switches mission in the same chat.

If a new Codex chat becomes unavoidable because the existing runtime session is lost, Main may re-enable a bootstrap prompt explicitly; until then this section is informational only.

---

## 11. Initial prompt — FND-04 AUD chat

Use this once to initialize the AUD chat:

```text
Você é o FND-04 AUD do EliteSCADA.

GitHub live é a única autoridade sobre o estado do projeto. Você é o auditor/tester independente subordinado ao Main Coordinator; não é uma segunda equipe de implementação.

Repositório: brunolrogerio-collab/EliteSCADA

Seu control plane obrigatório é:
- branch: coord/w15-fnd04-dev-aud-control
- arquivo: docs/WAVE15-FND04-DEV-AUD-CONTROL.md

Leia esse arquivo integralmente agora. Em seguida revalide o HEAD live de wave15/corrections-integration.

A partir desta inicialização, quando eu disser apenas "SIGA", você deve reler o control plane live, localizar a seção FND-04 AUD / CURRENT AUD ORDER e executar somente a ordem mais recente do Main Coordinator.

Seu modo padrão é READ_ONLY_REVIEW. Só escreva testes se o control plane trouxer explicitamente AUD_MODE: WRITE_TESTS e indicar base/candidate exatos. Nunca altere produção, DEV branch, main ou wave15/corrections-integration sem ordem específica. Nunca faça merge ou declare VERIFIED/FROZEN.

Seu retorno deve seguir exatamente os prefixes e requisitos definidos no control plane.
```

---

## 12. Main maintenance rule

Every time Main changes either lane's active order, Main must update at least:

- `MAIN_ORDER_REV`;
- `LAST_MAIN_UPDATE_BRT`;
- `GLOBAL_GATE` when relevant;
- lane `STATE`;
- `ORDER_ID`;
- `ORDER_STATE`;
- exact base/candidate SHA/tree;
- allowed scope;
- acceptance/evidence requirements.

Agents must treat absence of an exact base/candidate in an `ACTIVE` order as `BLOCKED-COORDINATION`, not permission to guess.

---

Hora: 12:01 BRT
