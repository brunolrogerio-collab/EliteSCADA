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

`MAIN_ORDER_REV: 0048`

`LAST_MAIN_UPDATE_BRT: 2026-09-25 — FND-05 VERIFIED/FROZEN / FND-07 CODEX ACTIVE`

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

`STATE: ACTIVE / SCRIPT_ENGINEERING_VALIDATION`

`RUNTIME_REQUIREMENT: functional checkout + dotnet + Node/Playwright + GitHub push/PR capability`

This lane is reserved for the **same CODEX execution chat currently finishing INFRA-CI-01A**. Product Owner explicitly chose sequential reuse of that executor instead of starting a second Codex implementation chat.

The FND-04 architecture/plan is unchanged. Only operational sequencing changes.

### CURRENT CODEX EXECUTION ORDER

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`

`ORDER_STATE: ACTIVE_VALIDATION`

`EXECUTOR_MODE: SAME_SEQUENTIAL_CODEX / SCRIPT_ENGINEERING_VALIDATION`

`FND04_STATE: VERIFIED_FROZEN`

`FND06_STATE: VERIFIED_FROZEN`

`AUDIT_RESULT: ACCEPTABLE / FC0A_RELEASE_APPROVED`

`FC0A_RELEASE_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`

`FC0A_RELEASE_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`

`FC0A_BROAD_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`

`EXPECTED_ORDER: DEV-SCRIPT-ENGINEERING-CODEX-VALIDATION-V1`

Instruction:

> FC0-A is released and the six normal-chat DEV/FND implementation lanes are ACTIVE on isolated branches.
>
> The sequential CODEX executor is now **idle validation capacity**.
>
> On every `SIGA`:
> 1. revalidate GitHub live and this control;
> 2. if `ORDER_STATE` is still `WAIT_FOR_MAIN_ACCEPTED_CANDIDATE`, do not mutate any product/test/CI branch;
> 3. do not attach yourself to a DEV lane merely because it has commits or an open PR;
> 4. wait until Main records an exact accepted candidate SHA/tree, lane, validation scope and target order;
> 5. then validate only that Main-accepted candidate using the appropriate dedicated control.
>
> Candidate priority is determined by Main from readiness + risk. FND-05 adversarial HA validation remains mandatory when its candidate reaches Main acceptance. No self-merge/freeze/release authority.

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

`ORDER_ID: FC0A-AUD-ADVISORY-WAIT-0014`

`ORDER_STATE: WAIT / OPTIONAL_ADVISORY_ONLY`

`AUD_MODE: READ_ONLY / NO_RELEASE_AUTHORITY`

Instruction:

> Main Coordinator is the responsible auditor for `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`.
>
> The separate AUD lane is no longer a prerequisite for FC0-A disposition.
>
> Main completed the audit on exact product SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3` with result `CHANGES_REQUIRED`.
>
> On `SIGA`, revalidate live state and report `FC0-A AUD — OPTIONAL ADVISORY WAIT`; do not duplicate the audit unless Main explicitly assigns a bounded advisory question.

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


## MAIN ROUTE — INFRA-CI-01C recurrence after FC0-A merge

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-INFRA-CI-01C-21`

`EXPECTED_ORDER: INFRA-CI-01C-POSTGRES-SCHEMA-RECURRENCE-V1`

Authoritative control:
- branch: `coord/w15-infra-ci-01c-control`
- file: `docs/WAVE15-INFRA-CI-01C-POSTGRES-SCHEMA-RACE-RECURRENCE-CONTROL.md`
- control commit: `8cf15e123823f5aaae2c911f0f113e804c9dad0d`

Exact base/work:
- base SHA: `d975174ae81ff7ed754585097240778a9862d965`
- base tree: `5ac06f47f1bede7a1b0c384c7b1d3c83730015ea`
- work branch: `work/w15-infra-ci-01c-postgres-schema-recurrence`
- target: `wave15/corrections-integration`

Trigger:
- exact post-merge CI `36044280802`;
- failure: PostgreSQL `23505 / pg_namespace_nspname_index`;
- failing test: `PostgreSqlEngineeringSchemaV15CommunicationBindingTests.PostgreSqlRevision_SavePreviewApply_RoundTripsCommunicationBinding`.

Main proved the failing store/helper/Timescale/failing test/concurrency test blobs are unchanged from pre-FC0A frozen product base. Classification is generic infra recurrence, not FC0-A product causality.

On SIGA:
1. read the 01C control fully;
2. checkout/revalidate exact work branch/base;
3. reproduce/diagnose the cross-project fresh-schema race before production mutation;
4. do not use a blind unchanged-SHA rerun as acceptance;
5. do not change test parallelism/sleeps to hide the race;
6. make only the root-cause-bounded generic infra fix + discriminating regression;
7. run focused concurrency + failing test + full local .NET where possible + natural T1;
8. open/update bounded PR;
9. return the exact 01C candidate handoff.

No merge/freeze/FC0A release authority.


## MAIN ROUTE — FC0-A post-merge Help E2E load closeout

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FC0A-HELP-E2E-22`

`EXPECTED_ORDER: FC0A-POSTMERGE-HELP-E2E-LOAD-V1`

Authoritative control:
- branch: `coord/w15-fnd06-control`
- file: `docs/WAVE15-FC0A-POSTMERGE-HELP-E2E-LOAD-CONTROL.md`
- control commit: `fa1c1e36dc1ca9f94ba0acc8e3e0dceaa3d02d9a`

Exact base/work:
- base SHA: `da0e64122f1e4d0293e027f45ef95021cd03c1a1`
- base tree: `a724e565f11121a43b97bf0c59683b414c72f48c`
- work branch: `work/w15-fc0a-postmerge-help-e2e-load`
- target: `wave15/corrections-integration`

Trigger:
- exact broad run `36047274028`;
- Backend/Web/Runtime smoke: SUCCESS;
- Chromium: FAILURE before test execution;
- deterministic parser error: `src/auth/auth.css: Unexpected token (1:0)`;
- load chain begins in FC0-A-added `tests-e2e/contextual-help-routing.spec.ts` importing `AppNavigation.tsx`.

On SIGA:
1. read the new control fully;
2. revalidate exact branch/base;
3. keep correction bounded to the test-load/import boundary;
4. prefer extracting `contextualHelpTopic` to a CSS-free pure module, or an equally small non-duplicative mounted-test correction;
5. do not skip the spec or weaken broad CI;
6. run focused contextual Help Playwright + Web build + relevant Help regression;
7. run natural T1 on exact candidate;
8. open/update bounded PR;
9. return `FC0-A POSTMERGE HELP-E2E CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`.

No merge/freeze/release authority.


## MAIN ROUTE — FC0-A post-merge Canvas source-contract closeout

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CANVAS-CONTRACT-23`

`EXPECTED_ORDER: FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-V1`

Authoritative control:
- branch: `coord/w15-fnd06-control`
- file: `docs/WAVE15-FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-CONTROL.md`
- control commit: `604a55b7a10e2c47408c05c3bf67e48c7663a2cb`

Exact base/work:
- base SHA: `1ab3550e1afb258e38caaa6de3f6547f481bbef7`
- base tree: `701f4591a294885b414284691b1051cf274c9707`
- work branch: `work/w15-fc0a-postmerge-canvas-source-contract`
- target: `wave15/corrections-integration`

Trigger:
- exact broad CI `36049229264 / #1568`;
- same-SHA diagnosed IEC-104 T2 rerun: Backend Test + smoke SUCCESS;
- Chromium full suite: 654 passed / 1 failed;
- only failure: stale source-contract string expectation for `getBuiltinVisualObjectSchema`;
- accepted FC0-A source correctly uses frozen FND-06 `getVisualSchemaForEngineering` compatibility seam.

On SIGA:
1. read the Canvas source-contract control fully;
2. revalidate exact branch/base;
3. change only the stale test contract unless focused evidence proves a contradiction;
4. require `getVisualSchemaForEngineering`, preserve common registry/property-key/no-private-default assertions, and guard against regression to direct builtin-only schema dependency;
5. run focused Canvas source + functional owner tests and Web build;
6. run natural exact-head T1;
7. open/update bounded PR;
8. return `FC0-A POSTMERGE CANVAS CONTRACT CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`.

No product feature scope. No merge/freeze/release authority.


## MAIN ROUTE — DEV-SCRIPT-ENGINEERING candidate validation

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`

`EXPECTED_ORDER: DEV-SCRIPT-ENGINEERING-CODEX-VALIDATION-V1`

Authoritative lane control:
- branch: `coord/w15-parallel-dev-control`
- file: `docs/WAVE15-DEV-SCRIPT-ENGINEERING-CONTROL.md`
- control commit: `aaa8e5f4a8488ea2c485798cf62a148952ca23c9`

Exact candidate:
- PR: `#344`
- branch: `work/w15-dev-script-engineering`
- accepted base: `e3ed5138369c576549cb58a7aff9783792f322d3`
- candidate SHA: `cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`
- candidate tree: `0820a2a00f36bcc13a05659d85cc43ee7f266d24`
- target: `wave15/corrections-integration`
- profiles: `SCRIPT_ENGINEERING, SCRIPT_RUNTIME`

Main review:
- implementation direction accepted;
- live target advance from release base is documentation-only and non-overlapping;
- PR #344 is mergeable;
- first T1 `36063109199` is metadata-invalid only because the PR lacked `VALIDATION_PROFILE`; Main corrected PR metadata without changing source.

On `SIGA`:
1. re-read the dedicated Script Engineering control live;
2. revalidate PR #344 exact head and target;
3. validate the exact Main-accepted candidate; do not attach to another lane;
4. add/strengthen focused tests for mounted event authoring, stale-field clearing, Timer, TAG stable identity/FND-04 negatives, Client Memory identity, Script Assistant compatibility, Python composition and backend tampering;
5. small validation-driven fixes are allowed only if they do not redesign the feature;
6. material product/design defects return `CODEX -> MAIN -> DEV-SCRIPT-ENGINEERING CORRECTION`;
7. run Web/focused evidence and natural exact-head T1;
8. keep the PR updated with exact evidence;
9. return `DEV-SCRIPT-ENGINEERING CODEX -> MAIN COORDINATOR — VALIDATION HANDOFF`.

No self-merge/freeze/T2 authority.


## PLANNED SEQUENTIAL CODEX QUEUE — NOT AN ACTIVE ORDER

This section records Main's current **planned queue only**. It does not supersede the active Script Engineering route above and must not be executed until Main publishes a new binding `MAIN ROUTE`.

Current active route remains:

`ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`

Planned next priorities after the Script validation handoff is resolved:

1. `INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-V1`
   - control: `coord/w15-infra-ci-01d-control:docs/WAVE15-INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-CONTROL.md`
   - prepared control commit: `b23cefeaf78a58ea17eaba8cf9a1566f3095ada4`
   - state: `PREPARED / NO WORK BRANCH YET / NO MUTATION`
   - reason: shared T1 reliability; exact asynchronous observation race proven in the existing IEC-104 fault-injection test.

2. DEV-EDITOR PR `#349`
   - exact Main-accepted candidate: `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`
   - tree: `11a5a01fd805d3e068dc6e22efff7a65077c09e4`
   - T1 `36066874097`: SUCCESS
   - state: `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`.

Why 01D is planned before Editor:
- 01D is test-only and bounded;
- it stabilizes shared .NET evidence that later candidate T1 runs consume;
- its work branch must be created from the current integration HEAD **only when activated**, avoiding stale-base work.

Main may still reassess later priority if a more urgent blocker appears before activation.

No executor may create the 01D work branch or switch away from Script based on this planned queue section.


## MAIN ROUTE STATUS — replacement coordinator queue refresh

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`

`ORDER_STATE: ACTIVE_VALIDATION`

This is still the binding executor mission. No later queue entry in this section supersedes it.

Live accepted/prepared waiting work after replacement-Main review:
- INFRA-CI-01D — `PREPARED / NO_MUTATION`;
- Editor PR #349 — Main-accepted head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- Licensing PR #345 — Main-accepted corrected head `f5d3212b9c114d3ad6e2239460172db0b3d568f8`, T1 `36071779912` SUCCESS;
- FND-05 PR #347 — Main-accepted corrected head `be9cf0f3f02aa1ba49cdb6b589abd5e1e723c845`, T1 `36072325579` SUCCESS, mandatory `CODEX_HA_ADVERSARIAL_GREEN`.

Authority PR #346 and FND-07 PR #348 are not CODEX-ready.

When Script Engineering returns:
1. stop and return its exact validation handoff to Main;
2. do not self-select any waiting mission;
3. Main revalidates integration/candidate heads and publishes one new explicit route;
4. only that new route changes executor mission.

The earlier planning preference for INFRA-CI-01D then Editor is not a standing authorization and may be reassessed against the now-ready Licensing/FND-05 candidates after Script resolves.


## CURRENT SHARED CODEX ROUTE — rev 0040

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-INFRA-CI-01D-26`

`ORDER_STATE: ACTIVE_EXECUTION`

`EXPECTED_ORDER: INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-V1`

Authoritative control:
- branch: `coord/w15-infra-ci-01d-control`;
- file: `docs/WAVE15-INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-CONTROL.md`;
- control commit: `47437359cb33a47faa3db6df5758d4340843e4ea`.

Exact base/work:
- base SHA: `3b1511799a73c3c4fee1c2265005d6724bcaa235`;
- base tree: `bb9b1fb4b9a7ffe47d9eb56eaee6dd8ba0e300fe`;
- work branch: `work/w15-infra-ci-01d-iec104-fault-observation-race`;
- target: `wave15/corrections-integration`.

The Script Engineering route `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25` is complete and superseded by this route after its validated candidate was merged as PR #344.

Execute only 01D. Do not self-select Editor, Authority, Licensing or FND-05 after completion; return to Main for the next explicit route.


## CURRENT SHARED CODEX ROUTE — rev 0041

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-EDITOR-27`

`ORDER_STATE: ACTIVE_VALIDATION`

`EXPECTED_ORDER: DEV-EDITOR-CODEX-VALIDATION-V1`

Authoritative lane control:
- branch: `coord/w15-parallel-dev-control`;
- file: `docs/WAVE15-DEV-EDITOR-CONTROL.md`;
- control commit: `513912326b32683d60b8b65007361ca9b275839c`.

Exact candidate:
- PR #349;
- branch `work/w15-dev-editor-single-canvas`;
- candidate SHA `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- candidate tree `11a5a01fd805d3e068dc6e22efff7a65077c09e4`;
- target `wave15/corrections-integration`;
- current target SHA `66694aac1408218a41d1251459c20465d990cfef`.

Prior Script/01D routes are complete. Execute only Editor validation. Do not self-select Authority, Licensing or FND-05 afterward; return to Main for the next explicit route.


## CURRENT SHARED CODEX ROUTE — rev 0042

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FND05-HA-28`

`ORDER_STATE: ACTIVE_VALIDATION`

`EXPECTED_ORDER: FND05-CODEX-HA-ADVERSARIAL-V2-02`

Authoritative lane control:
- branch: `coord/w15-fnd05-control`;
- file: `docs/WAVE15-FND05-CONTROL.md`;
- control commit: `ddfd84ab4f2522c255fa9a155f9ec14dfbbc8734`.

Exact candidate:
- PR #347;
- branch `work/w15-fnd05-ha-authority`;
- candidate SHA `be9cf0f3f02aa1ba49cdb6b589abd5e1e723c845`;
- candidate tree `c9e94e84c078928ad690217484171fafa6390b46`;
- current target SHA `cfaafa4b29e1462bf9d304af995cc8638578a3c2`.

Mandatory outcome is `CODEX_HA_ADVERSARIAL_GREEN` or a material defect returned to Main. Execute only FND-05. Do not self-select Authority/Licensing afterward.


## CURRENT SHARED CODEX ROUTE — rev 0043

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FND05-POSTMERGE-29`

`ORDER_STATE: ACTIVE_VALIDATION`

`EXPECTED_ORDER: FND05-CODEX-POSTMERGE-VALIDATION-V2-03`

Authoritative lane control:
- branch: `coord/w15-fnd05-control`;
- file: `docs/WAVE15-FND05-CONTROL.md`;
- control commit: `346d5f64cb317355ecb3b4589a1efb45954339a2`.

Exact integration head:
- SHA `b2874a00c7f7b35ca8223defd7e3b6bbdd89ecf8`;
- tree `c942dc46692a1f2350bcf478b952db00cfa756ec`.

FND-05 is merged but not yet VERIFIED/FROZEN. Execute post-merge validation only. Return to Main after handoff; do not self-select Authority or Licensing.


## CURRENT SHARED CODEX ROUTE — rev 0044

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FND07-30`

`ORDER_STATE: ACTIVE_VALIDATION`

`EXPECTED_ORDER: FND07-CODEX-EXACT-HEAD-T1-VALIDATION-04`

Authoritative lane control:
- branch: `coord/w15-fnd07-control`;
- file: `docs/WAVE15-FND07-CONTROL.md`;
- control commit: `d86926adad90712e679607c90437533591505db2`.

Exact candidate:
- PR #348;
- branch `work/w15-fnd-07-detach-neutral`;
- SHA `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- tree `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`.

Primary mission:
- obtain exact-head Wave 15 T1 on this SHA;
- then perform FND-07 adversarial/focused validation;
- no source mutation initially;
- bounded test-only fixes allowed only if validation exposes test-harness defects;
- material product defect returns to Main/FND-07 DEV.

FND-05 is now VERIFIED/FROZEN and this route supersedes its post-merge route.

Do not self-select Authority or Licensing after FND-07.


## CURRENT SHARED CODEX ROUTE — rev 0045

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FND07-30`

`ORDER_STATE: ACTIVE_VALIDATION / BOUNDED_TEST_FIX`

`EXPECTED_ORDER: FND07-CODEX-EXACT-HEAD-T1-VALIDATION-04`

Authoritative updated lane control:
- branch: `coord/w15-fnd07-control`;
- file: `docs/WAVE15-FND07-CONTROL.md`;
- control commit: `2a9b44ce9c20ef620b9c8c2088babd31917df0e4`.

The exact-head T1 `36088026405` on `a8fcfe8c855a692670e9c74a95f4450caf612b2f` failed only because the explicit downstream test fixture was saved but not published/activated.

CODEX remains on FND-07 and is authorized for the bounded test-only lifecycle completion defined in rev 0014. No product mutation. Do not switch to Authority or Licensing yet.


## CURRENT SHARED CODEX ROUTE — rev 0046

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-DEFECT-RETURN-31`

`ORDER_STATE: PAUSED / MATERIAL_DEFECT_RETURNED_TO_DEV / CODEX_USAGE_BLOCKED`

The prior bounded test-fix authorization is revoked.

Main independently confirmed a material FND-07 product defect:
`AUTHORITY_HYDRATION_BEFORE_ENGINEERING_CHECKOUT_REQUIRED`.

Binding DEV order:
- branch: `coord/w15-fnd07-control`;
- file: `docs/WAVE15-FND07-CONTROL.md`;
- order: `FND07-DEV-AUTHORITY-BEFORE-ENGINEERING-RESTART-05`;
- state: `DEV_CORRECTION / AUTHORIZED`.

CODEX also hit its current usage limit during investigation. It must not continue local/test-only mutation when capacity returns unless Main publishes a new route after the corrected DEV candidate is reviewed.

No Authority or Licensing reroute is active while FND-07 material correction is unresolved.


## CURRENT SHARED CODEX ROUTE — rev 0047

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-DEEP-AUDIT-HOLD-32`

`ORDER_STATE: PAUSED / FND07_DEEP_AUDIT_BLOCKERS_OPEN`

Main cumulative audit of PR #348 found open BLOCKER/MAJOR lifecycle defects. Binding FND-07 DEV order:
`FND07-DEV-DEEP-AUDIT-BLOCKERS-06`.

CODEX must remain idle for FND-07 until Main re-audits the next DEV candidate and explicitly publishes a new route.

Do not self-select Authority or Licensing.


## CURRENT SHARED CODEX ROUTE — rev 0048

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FND07-RECONCILED-33`

`ORDER_STATE: ACTIVE_VALIDATION`

`EXPECTED_ORDER: FND07-CODEX-RECONCILED-ADVERSARIAL-09`

Authoritative control:
- branch: `coord/w15-fnd07-control`;
- file: `docs/WAVE15-FND07-CONTROL.md`;
- exact candidate: `2bacddbc558bba0dd1a316d98b9895fbb847683c`;
- tree: `06ddb27f8f50c495653c1b8e57a2142769ca29e0`;
- PR #348.

Main has completed cumulative deep audit and cross-lane reconciliation.

CODEX must perform final adversarial validation on the exact reconciled candidate. Do not mutate source initially. Test-only correction is permitted only for a proven harness/test defect; any product/architecture defect returns to Main.

Natural T1 `36093062819` is already active on the exact SHA.

After handoff, return to Main. Do not self-select Authority or Licensing.


## CURRENT SHARED CODEX ROUTE — rev 0049

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-CAPACITY-HOLD-34`

`ORDER_STATE: PAUSED / CODEX_UNAVAILABLE / MAIN_TEST_AUDIT_HANDOFF`

The prior rev 0048 ACTIVE_VALIDATION route is superseded.

Current FND-07 exact candidate:
- PR #348;
- branch `work/w15-fnd-07-detach-neutral`;
- head `3ecc4a78080685b0556402d50190e09236d6d8fa`;
- exact-head T1 `36094394912`: FAILURE only in focused Chromium; classifier/Common/Web/focused .NET all SUCCESS.

Main test audit already closed several legacy/harness defects and one hidden production endpoint defect. The remaining live blocker is:
- explicit test-owned fixture publishes revision successfully;
- `POST /api/engineering/persistence/e2e-wave03/published/activate` now reaches the real handler and returns HTTP 422;
- root cause of `Activated=false` is not yet diagnosed at coordinator handoff.

CODEX is currently unavailable to the Product Owner and must remain idle. When capacity returns, do not resume rev 0048 or self-select Authority/Licensing. The next Main Coordinator must first revalidate PR #348 live, diagnose the exact-head 422, and publish a new explicit route if CODEX is still needed.
## CURRENT SHARED CODEX ROUTE — rev 0050

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-FIXTURE-CORRECTION-HOLD-35`

`ORDER_STATE: PAUSED / CODEX_UNAVAILABLE / FND07_DEV_FIXTURE_CORRECTION_ACTIVE`

The exact-head FND-07 activation 422 has now been diagnosed by Main.

Current classification:
`FND07_TEST_FIXTURE_SOURCE_INCOMPATIBILITY / BUILTIN_SIMULATION_NOT_ACTIVE_RUNTIME_SOURCE`.

Binding FND-07 DEV order:
- control branch: `coord/w15-fnd07-control`;
- file: `docs/WAVE15-FND07-CONTROL.md`;
- control rev: 0023;
- control commit: `e7964661e06ce70f3cca20659a3bd7d05a79065b`;
- order: `FND07-DEV-ACTIVE-RUNTIME-FIXTURE-SOURCE-11`;
- state: `DEV_CORRECTION / AUTHORIZED / TEST_HARNESS_ONLY`.

CODEX remains unavailable and must stay idle.
Do not resume rev 0048 or any older FND-07 route.
Do not self-select Authority or Licensing.

When CODEX capacity returns, Main must first revalidate the corrected FND-07 exact head and then publish a new explicit route. No implicit continuation is authorized.
## CURRENT SHARED CODEX ROUTE — rev 0051

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-AVAILABLE-HOLD-FND07-36`

`ORDER_STATE: AVAILABLE_MODERATE / HOLD_WAIT_FND07_CORRECTED_HEAD / NO_ACTIVE_MISSION`

Product Owner reports that CODEX capacity is available again at a moderate level.

This availability does **not** reactivate rev 0048, rev 0049 or rev 0050 automatically.

Live revalidation at this revision:
- FND-07 PR #348 remains at exact head `3ecc4a78080685b0556402d50190e09236d6d8fa`;
- no corrected head has yet been delivered for order `FND07-DEV-ACTIVE-RUNTIME-FIXTURE-SOURCE-11`;
- therefore no CODEX execution is currently authorized.

Resource policy while capacity is moderate:
1. preserve CODEX for validation rather than duplicate implementation work already assigned to FND-07 DEV;
2. wait for the corrected FND-07 exact head;
3. Main reviews that head first;
4. only then Main publishes a new explicit CODEX route for focused/adversarial validation and exact-head evidence;
5. no self-selection of Authority or Licensing while FND-07 closeout remains the priority.

Current state is availability without mission, not an execution route.
## CURRENT SHARED CODEX ROUTE — rev 0052

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FND07-LOCK-CORRECTION-37`

`ORDER_STATE: ACTIVE_CORRECTION_AND_VALIDATION`

`EXPECTED_ORDER: FND07-CODEX-ENGINEERING-LOCK-AUTHORITY-CORRECTION-12`

Authoritative lane control:
- branch: `coord/w15-fnd07-control`;
- file: `docs/WAVE15-FND07-CONTROL.md`;
- control rev: 0025;
- control commit: `0511b86c387fce496b57b03cc532c66f665342ef`.

Exact starting candidate:
- PR #348;
- branch `work/w15-fnd-07-detach-neutral`;
- SHA `95fd68468c7ce26b94e8c60a513625e917f4c4a2`;
- tree `beca8b125258e812680573558a1c382acffe87ff`;
- T1 `36136242327`: green except focused Chromium.

Mission:
- correct the bounded generic Engineering Lock partial-package Authority-reference defect;
- add focused regression coverage;
- execute focused activation/browser evidence;
- obtain new exact-head T1;
- complete final FND-07 adversarial validation.

Do not bypass Authority validation.
Do not self-select Authority UX or Licensing UX afterward.
Return the complete durable handoff to PR #348 and Main.
## CURRENT SHARED CODEX ROUTE — rev 0053

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-ACCEPTED-HOLD-38`

`ORDER_STATE: COMPLETED / MAIN_ACCEPTED / HOLD_FOR_MERGE`

FND-07 final CODEX validation is accepted by Main.

Accepted exact head:
- `ebec9e346e03068a565669d03ba018d2b9eb025f`;
- tree `58a1bd60f42fe36af7c17f8c8692e32ef53cbe61`;
- exact-head T1 `36189002703`: SUCCESS.

Main acceptance control:
- FND-07 rev 0026;
- commit `e24ce7098c905f95947dedd22592d92d8f20b28c`.

CODEX must not mutate FND-07 further and must not self-select Authority UX or Licensing UX. Shared resource is held until Main completes the protected merge and publishes the next explicit route.
## CURRENT SHARED CODEX ROUTE — rev 0054

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-POSTMERGE-HOLD-39`

`ORDER_STATE: HOLD / FND07_INTEGRATED_PENDING_POSTMERGE_CI`

PR #348 has been merged.

Integrated FND-07 SHA:
`1b186c48ba5d2e3012be2c58f0efc36170101fe9`.

Exact integrated CI:
- EliteSCADA CI `36191702355`;
- run #1576;
- currently queued.

CODEX must remain idle until Main accepts the post-merge CI and records FND-07 VERIFIED/FROZEN or explicitly returns a material failure.

Do not self-select Authority UX or Licensing UX yet.
## CURRENT SHARED CODEX ROUTE — rev 0055

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-POSTMERGE-LOCAL-AUTHORITY-40`

`ORDER_STATE: ACTIVE_POSTMERGE_CORRECTION`

`EXPECTED_ORDER: FND07-CODEX-POSTMERGE-LOCAL-AUTHORITY-GATE-15`

Authoritative FND-07 control:
- branch `coord/w15-fnd07-control`;
- file `docs/WAVE15-FND07-CONTROL.md`;
- rev 0028;
- control commit `b6b5e8a1b6a54cc5b3f161e08cd137de04d810e4`.

Correction branch:
`work/w15-fnd07-postmerge-local-authority-gate`

Exact base:
`1b186c48ba5d2e3012be2c58f0efc36170101fe9`.

Mission:
- fix the post-merge DI/composition failure when local Authority is disabled;
- condition local-Authority detach recovery/endpoints on actual local-Authority capability;
- preserve external/canonical Authority policy bootstrap;
- preserve all enabled-local-Authority FND-07 semantics;
- test both modes and obtain exact-head CI evidence.

Do not self-select Authority UX or Licensing UX afterward. Return durable handoff to Main.
## CURRENT SHARED CODEX ROUTE — rev 0056

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-CORRECTION-ACCEPTED-41`

`ORDER_STATE: COMPLETED / MAIN_ACCEPTED / HOLD_FOR_CORRECTION_MERGE`

Accepted correction:
- PR #352;
- head `50a6fa477b166dd115b7d6b8152fd7d53f7e7d6a`;
- T1 `36193282035`: SUCCESS.

Main acceptance control:
- FND-07 rev 0029;
- commit `169aa0a7c975edd1268d56aad27b64dd1f39777e`.

CODEX must not mutate the corrective branch further and must not self-select another lane until Main completes integration and post-merge CI.
## CURRENT SHARED CODEX ROUTE — rev 0057

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-FINAL-POSTMERGE-HOLD-42`

`ORDER_STATE: HOLD / FINAL_POSTMERGE_CI_PENDING`

FND-07 corrected integration:
`e49155e4a17acb6cd35683500f4ef211eb8f6e56`.

Final CI:
`36194762603` / run #1577.

CODEX remains idle until Main classifies this exact integrated CI and either freezes FND-07 or publishes another explicit blocker route.
## CURRENT SHARED CODEX ROUTE — rev 0058

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-CI-FIRST-PROJECT-43`

`ORDER_STATE: ACTIVE_CI_HARNESS_CORRECTION`

`EXPECTED_ORDER: FND07-CODEX-CI-FIRST-PROJECT-SMOKE-18`

Authoritative FND-07 control:
- branch `coord/w15-fnd07-control`;
- file `docs/WAVE15-FND07-CONTROL.md`;
- rev 0031;
- control commit `7a3b7ffabf946dc3e0817ab86747b519199df3ba`.

Correction branch:
`work/w15-fnd07-postmerge-ci-first-project-smoke`

Exact base:
`e49155e4a17acb6cd35683500f4ef211eb8f6e56`.

Mission:
- update only the stale full-CI smoke from generic first save to canonical `projects/first`;
- preserve product binding enforcement;
- adapt necessary response/assertion handling;
- improve API-log diagnostics on smoke failure;
- run validation and return durable evidence.

Do not modify production code and do not self-select Authority UX or Licensing UX afterward.
## CURRENT SHARED CODEX ROUTE — rev 0059

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-CI-PARITY-BATTERY-44`

`ORDER_STATE: ACTIVE_CI_HARNESS_CORRECTION + LOCAL_FULL_CI_PARITY`

`EXPECTED_ORDER: FND07-CODEX-CI-FIRST-PROJECT-SMOKE-18`

Authoritative amendment:
- FND-07 control rev 0032;
- commit `25877bfb5c1154d63c21f001f2190c9b28dc6bc6`.

In addition to the first-project smoke correction, CODEX must now run a persistent local battery equivalent to the complete `EliteSCADA CI`:

- backend restore/build/full tests;
- complete Runtime smoke;
- frontend build;
- complete Chromium E2E.

Use one persistent environment and reuse .NET restore, npm dependencies, installed Chromium and database services where safe. Reset data/state between test passes, not the whole toolchain.

Purpose:
find sequential CI failures locally before spending another GitHub Actions run.

Do not stop after fixing the first known smoke error. Continue until the local full-CI parity battery is green or a genuine out-of-scope product blocker is proven.

No production mutation is authorized. Do not self-select another lane afterward.
## CURRENT SHARED CODEX ROUTE — rev 0060

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-CONTAINERIZED-CI-PARITY-45`

`ORDER_STATE: ACTIVE_CI_HARNESS_CORRECTION + REUSABLE_CONTAINERIZED_PARITY`

`EXPECTED_ORDER: FND07-CODEX-CI-FIRST-PROJECT-SMOKE-18`

Authoritative amendment:
- FND-07 rev 0033;
- commit `3a0e47c8e005286f80cf0c385c2f0cd2927ce3d5`.

CODEX should turn the current persistent local CI battery into a reusable repository-owned container/Compose harness for future CI investigations.

Design rule:
do not create a second CI truth. Extract/share executable scripts so the local harness and GitHub Actions exercise the same commands whenever practical.

The harness must make future investigation cheap:
- dependencies/services start once;
- test databases reset independently;
- backend/full smoke/web/e2e can be rerun individually;
- full `all` pass is available;
- logs are easy to inspect;
- no production source changes.

Continue the active first-project smoke correction and use this harness to drive the full local parity battery to green.
## CURRENT SHARED CODEX ROUTE — rev 0061

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-AUTONOMOUS-CI-HARNESS-46`

`ORDER_STATE: ACTIVE / CI_INFRA_CREATIVE_AUTONOMY`

`EXPECTED_ORDER: FND07-CODEX-CI-FIRST-PROJECT-SMOKE-18`

Authoritative amendment:
- FND-07 rev 0034;
- commit `26d7cbecbdf5c31c1f0543b2784baacc9830fff0`.

CODEX now owns implementation decisions for the reusable local CI-parity harness within the CI/test-infrastructure boundary.

Main specifies the outcome:
- faithful universal-CI parity;
- cheap repeated investigation;
- persistent/cached dependencies where safe;
- deterministic clean-state reset;
- backend + Runtime smoke + web + full Chromium;
- useful failure logs;
- final independent hosted-CI confirmation.

CODEX may freely redesign the harness implementation, files, containers, caches and orchestration if a better solution is discovered.

Do not ask Main for preferences on routine harness limitations. Diagnose and solve them autonomously, then document the result.

Return to Main only if the needed fix crosses into product semantics/security/licensing/HA/runtime contracts, requires weakening a gate, or cannot reproduce a material CI dependency credibly.

No production mutation authority is granted.
## CURRENT SHARED CODEX ROUTE — rev 0062

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-LINUX-E2E-PARITY-47`

`ORDER_STATE: ACTIVE / LINUX_CONTAINERIZED_E2E_PARITY`

`EXPECTED_ORDER: FND07-CODEX-CI-FIRST-PROJECT-SMOKE-18`

Authoritative FND-07 control:
- rev 0035;
- commit `d7f804d9da516b01f7ce7e162060be8db6da3855`.

Current candidate:
`68b22226a230cf75a40a228f5fce7cc07f226855`.

Backend/full tests, Runtime smoke and Web build are locally green. Windows browser execution is classified as an environment parity gap, not product failure.

CODEX must now execute Playwright 1.62.1 in a Linux container/runtime materially aligned with hosted CI, retain failure artifacts, autonomously correct test/harness-only defects, and continue until:
- full Linux Chromium suite is green;
- one clean full local parity `all` pass is green.

Only then use hosted CI for independent confirmation.

Routine Linux/container/tooling limitations remain CODEX-owned. No production mutation authority is granted.
## CURRENT SHARED CODEX ROUTE — rev 0063

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-DEMO-RECOVERY-48`

`ORDER_STATE: ACTIVE / BOUNDED_PRODUCT_CORRECTION + LINUX_FULL_CI_PARITY`

`EXPECTED_ORDER: FND07-CODEX-DEMO-RECOVERY-ANCHOR-19`

Authoritative FND-07 control:
- rev 0036;
- commit `89d4f8a6b1e81ed34f7bdbbc80b8020e1c8171ef`.

Current branch/head:
- `work/w15-fnd07-postmerge-ci-first-project-smoke`;
- published head `68b22226a230cf75a40a228f5fce7cc07f226855`.

Main classification:
1. first successful explicit Demo activation fails to persist the durable session anchor required by restart recovery;
2. expected fail-closed product-authority recovery denials are incorrectly escalated into fatal host startup.

CODEX is explicitly authorized to make the narrow product correction defined in rev 0036.

Do not seed the E2E database/anchor directly and do not use fake licensing transitions or bypass recovery.

Required result:
- durable Demo session semantics correct across explicit Run, reactivation, process restart and expiry;
- expected authority/entitlement denials keep Runtime stopped while host remains usable;
- genuine corruption remains fatal;
- full Linux local parity battery green;
- then hosted CI confirmation.

Harness implementation autonomy remains broad. Product changes remain bounded to the rev 0036 contract.
## CURRENT SHARED CODEX ROUTE — rev 0064

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-DEMO-ATOMICITY-49`

`ORDER_STATE: ACTIVE / PR353_REQUEST_CHANGES / DURABILITY_ATOMICITY`

`EXPECTED_ORDER: FND07-CODEX-DEMO-RECOVERY-ATOMICITY-20`

Authoritative control:
- FND-07 rev 0037;
- commit `5d4228fefd00c2729b4827f1f5397eabf47603f1`.

PR #353 head `2b857f55cd85a46b0458aa16ac093412a7bf6889` is green in local Linux parity, hosted CI and T1, but Main found one merge blocker:

the Engineering Active revision is durably recorded before the new/replacement Demo anchor is durably established.

CODEX must eliminate the crash/failure window so a new durable Active Runtime can never exist without the Demo-session anchor required for restart recovery.

CODEX has bounded design autonomy over the persistence/activation mechanism needed to satisfy this invariant. Do not broaden into unrelated product architecture.

Required before return:
- failure-injection regression proof;
- focused tests green;
- clean Linux full parity green;
- exact-head hosted CI + T1 green.

Do not merge and do not self-select another lane.
## CURRENT SHARED CODEX ROUTE — rev 0065

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-ATOMICITY-ACCEPTED-50`

`ORDER_STATE: COMPLETED / MAIN_ACCEPTED / HOLD_FOR_MERGE`

Accepted exact head:
`a0152678243e1418905994f9440e5e668aaa3c86`.

Exact gates:
- T1 `36278444005`: SUCCESS;
- EliteSCADA CI `36278465853`: SUCCESS;
- clean Linux parity: SUCCESS.

Main atomicity acceptance:
- FND-07 rev 0038;
- commit `090158f8ae2b5f9259fa9d5c68d0e419e6b348b1`.

CODEX must remain idle and must not self-select another lane until Main completes protected merge and post-merge verification.
## CURRENT SHARED CODEX ROUTE — rev 0066

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-FINAL-INTEGRATED-HOLD-51`

`ORDER_STATE: HOLD / FND07_POSTMERGE_CI_PENDING`

FND-07 final corrective PR #353 is merged.

Integrated SHA:
`3819715ba3a015a182c97b2a4ebcb4de447da717`.

Post-merge EliteSCADA CI:
`36279534882` / run #1582 — queued.

CODEX remains idle. Do not self-select another lane until Main classifies this exact integrated run and publishes the next explicit route.
## CURRENT SHARED CODEX ROUTE — rev 0067

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FND07-CLOSED-FEATURE-RECONCILE-HOLD-52`

`ORDER_STATE: HOLD / NO_ACTIVE_MISSION`

FND-07 is now:
`VERIFIED / FROZEN`
at integrated SHA:
`3819715ba3a015a182c97b2a4ebcb4de447da717`.

Final integrated CI:
`36279534882` / #1582 — SUCCESS.

The shared CODEX resource is currently reported temporarily unavailable and has no active mission.

Authority UX PR #346 and Licensing UX PR #345 are being returned to their own DEV lanes for mechanical reconciliation onto the new integration baseline. CODEX must not auto-resume any older route.

When CODEX availability returns, Main will publish a new explicit sequential adversarial-validation route against a freshly reconciled exact head.
## CURRENT SHARED CODEX ROUTE — rev 0068

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-UNAVAILABLE-RECONCILED-QUEUE-53`

`ORDER_STATE: HOLD / CODEX_TEMPORARILY_UNAVAILABLE / NO_ACTIVE_MISSION`

Frozen integration baseline:
`3819715ba3a015a182c97b2a4ebcb4de447da717`.

Ready reconciled candidates:

1. Authority UX PR #346
   - head `e3c646b5f0e6fb06b509f44b0ed866ec61fd1db6`;
   - T1 `36280754990`: SUCCESS;
   - Main accepted for adversarial CODEX validation.

2. Licensing UX PR #345
   - head `3d73ccd8f683be94f741fdb431df0e1525e6c0e9`;
   - T1 `36280752132`: SUCCESS;
   - Main accepted for adversarial CODEX validation.

Planned priority after CODEX availability returns:
Authority #346 first, Licensing #345 second.

No old route may auto-resume. Main must publish a new explicit ACTIVE route before CODEX acts.
## CURRENT SHARED CODEX ROUTE — rev 0069

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-AUTHORITY-SCARCE-54`

`ORDER_STATE: ACTIVE / AUTHORITY_UX_ADVERSARIAL / SCARCE_BUDGET`

`EXPECTED_ORDER: DEV-AUTHORITY-UX-CODEX-ADVERSARIAL-SCARCE-07`

Exact candidate:
- PR #346;
- head `e3c646b5f0e6fb06b509f44b0ed866ec61fd1db6`;
- T1 `36280754990`: SUCCESS.

Authoritative Authority control:
- rev 0009;
- commit `919c1f8bf33716ddd92c8b9eaaaf129a94637f5e`.

CODEX must prioritize targeted adversarial Authority evidence and reuse existing harness/dependencies. Avoid redundant full-battery execution unless a material cross-cutting correction makes it necessary.

Licensing UX #345 remains WAIT and must not be touched until Main publishes the next explicit route.

Return durable handoff to PR #346 and Main.
## CURRENT SHARED CODEX ROUTE — rev 0070

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-AUTHORITY-CONCURRENCY-FEEDBACK-55`

`ORDER_STATE: ACTIVE / AUTHORITY_BOUNDED_CORRECTION / SCARCE_BUDGET`

`EXPECTED_ORDER: DEV-AUTHORITY-UX-CODEX-CONCURRENCY-FEEDBACK-08`

Authoritative Authority control:
- rev 0010;
- commit `a425166f0fe531c68144dfb45ffdcfcec8ee28f2`.

The adversarial Authority pass found no authorization bypass, but exposed one bounded UX truth mismatch:
stale-version validation returns `400 + AUTHORITY_POLICY_CONCURRENCY_CONFLICT`, while the UI only shows localized conflict guidance on HTTP 409.

CODEX is authorized to correct this in Authority UX/tests only, using the semantic error code while retaining existing 409 handling.

Keep execution cheap:
- targeted tests;
- new exact-head T1 after the commit;
- no full Linux parity unless a focused failure warrants it.

Licensing #345 remains WAIT.
## CURRENT SHARED CODEX ROUTE — rev 0071

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-AUTHORITY-ACCEPTED-HOLD-56`

`ORDER_STATE: COMPLETED / AUTHORITY_MAIN_ACCEPTED / HOLD_FOR_MERGE`

Authority UX accepted exact head:
`f82a5662234e42a73b77f15fbdfd730872cc5cc1`.

Exact-head T1:
`36288094878` / #112 — SUCCESS.

Main acceptance control:
- Authority rev 0011;
- commit `6670f027624245c3a24e1b58cbc2c50e274318f3`.

CODEX must remain idle. Do not self-select Licensing #345 until Main completes Authority integration/post-merge verification and publishes a new explicit route.
## CURRENT SHARED CODEX ROUTE — rev 0072

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-AUTHORITY-POSTMERGE-HOLD-57`

`ORDER_STATE: HOLD / AUTHORITY_POSTMERGE_CI_PENDING`

Authority UX PR #346 is merged.

Integrated SHA:
`27412e4fbe47dbbb6573229364ee8319369ae076`.

Post-merge EliteSCADA CI:
`36288961537` / #1583 — queued.

Because CODEX budget is reduced, keep it idle until Main classifies this exact integrated run.

Licensing #345 remains WAIT and must not be touched until Main publishes a new explicit route.
## CURRENT SHARED CODEX ROUTE — rev 0073

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-LICENSING-RECONCILE-HOLD-58`

`ORDER_STATE: HOLD / NO_ACTIVE_MISSION / SCARCE_BUDGET_PRESERVED`

Authority UX is now integrated and verified at:
`27412e4fbe47dbbb6573229364ee8319369ae076`.

Licensing UX PR #345 became stale only because Authority was merged after its prior reconciliation.

Main verified zero file overlap between the Authority merge and the 13 Licensing-owned files.

Licensing DEV is now authorized to reconcile onto the new baseline under:
`DEV-LICENSING-UX-POST-AUTHORITY-RECONCILE-09`.

CODEX remains idle to preserve reduced usage budget.

Do not auto-resume any prior route. Main will publish a fresh explicit Licensing adversarial route only after a new exact reconciled head + T1 are available.
## CURRENT SHARED CODEX ROUTE — rev 0074

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-LICENSING-SCARCE-59`

`ORDER_STATE: ACTIVE / LICENSING_UX_ADVERSARIAL / SCARCE_BUDGET`

`EXPECTED_ORDER: DEV-LICENSING-UX-CODEX-ADVERSARIAL-SCARCE-10`

Exact candidate:
- PR #345;
- head `3d166c0b45eef34ef878e710b5db28c3ea2fa93f`;
- base `27412e4fbe47dbbb6573229364ee8319369ae076`;
- T1 `36289854846` / #113: SUCCESS.

Authoritative Licensing control:
- rev 0010;
- commit `80c5c16bd0adb60824f9c9215ef8a8fb0044ca51`.

CODEX must prioritize targeted adversarial Licensing/session truth evidence and reuse existing harness/dependencies.

Avoid redundant full-battery execution unless a material cross-cutting correction makes it necessary.

Authority UX is integrated/verified and must not be touched.
FND-05/FND-07 remain frozen.

Return durable handoff to PR #345 and Main. No other CODEX route is active.
## CURRENT SHARED CODEX ROUTE — rev 0075

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-LICENSING-ACCEPTED-HOLD-60`

`ORDER_STATE: COMPLETED / LICENSING_MAIN_ACCEPTED / HOLD_FOR_MERGE`

Licensing UX accepted exact head:
`3d166c0b45eef34ef878e710b5db28c3ea2fa93f`.

Exact-head T1:
`36289854846` / #113 — SUCCESS.

CODEX adversarial evidence:
- focused Chromium 6/6 PASS;
- focused Drivers 36/36 PASS;
- no candidate-causal defect;
- no source mutation.

Main acceptance control:
- Licensing rev 0011;
- commit `eab694e8bbc6e099656d69d2eebbc60bda0ccabf`.

CODEX must remain idle. Do not self-select any new lane until Main completes Licensing integration/post-merge verification and publishes a new explicit route.
## CURRENT SHARED CODEX ROUTE — rev 0076

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-LICENSING-POSTMERGE-HOLD-61`

`ORDER_STATE: HOLD / LICENSING_POSTMERGE_CI_PENDING`

Licensing UX PR #345 is merged.

Integrated SHA:
`1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Post-merge EliteSCADA CI:
`36290910850` / #1584 — queued.

Because CODEX budget is reduced, keep it idle until Main classifies this exact integrated run.

No other CODEX route is active.
## CURRENT SHARED CODEX ROUTE — rev 0077

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-INTEGRATED-T2-HANDOFF-HOLD-62`

`ORDER_STATE: HOLD / NO_ACTIVE_MISSION / AWAITING_MAIN_T2_DECISION`

Latest integration:
`1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Latest broad CI:
`36290910850` / #1584 — SUCCESS.

Feature status:
- Script Engineering: integrated, still formally `INTEGRATED_PENDING_T2`;
- Editor: integrated, still formally `INTEGRATED_PENDING_T2`;
- Authority UX: integrated / verified complete;
- Licensing UX: integrated / verified complete.

Foundations:
- FND-05 VERIFIED/FROZEN;
- FND-07 VERIFIED/FROZEN.

Do not auto-resume any prior CODEX route.

Next Main must define/execute/accept the broader integrated feature T2 on the exact current integration head before activating the prepared fresh-install first-project preview.

CODEX usage budget is reduced; preserve it until Main publishes an explicit T2 mission.



## CURRENT SHARED CODEX ROUTE — rev 0078

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-TO-FIRST-PROJECT-BLACKBOX-63`

`ORDER_STATE: ACTIVE / FIRST_PROJECT_BLACKBOX_PREVIEW / SCARCE_BUDGET`

Main has formally accepted the four-feature broader integrated T2 and activated the first-project partial preview.

Binding evidence:
- exact preview product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`;
- exact product tree: `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`;
- integrated T2: `W15-FOUR-FEATURE-INTEGRATED-T2-01 -> PASS / ACCEPTED`;
- T2 evidence: EliteSCADA CI #1584 / run `36290910850` — SUCCESS;
- central board acceptance commit: `f018438c0a651aff5fd3776989f54ea9e9d9e0bf`;
- authoritative preview control activation commit: `5cf92000ad3bf182b7aa26c43bd44b7c415f1f0b`.

On next CODEX execution, first read live:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`

Execute only:
`W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`.

During the user-like exploratory journey:
- use a genuinely clean fresh-install environment;
- use normal user-visible product interfaces and product-visible Help only;
- do not inspect repository source, control-plane documents for implementation discovery, database state, internal APIs, implementation logs or test fixtures;
- do not mutate product/source/configuration to correct findings;
- do not use shell/state shortcuts to bypass the product journey.

Mission:
> You have just installed EliteSCADA. Explore the product using normal user-visible interfaces and create a small SCADA application from scratch that reaches a functional Runtime.

If the journey completes or becomes product-blocked, only then may diagnostic source/log/API inspection begin to correlate findings. Product correction still requires a separate Main order.

Persist detailed findings in the dedicated preview evidence surface defined by the preview control and keep them embargoed from the Product Owner until the independent human journey is complete. Return only the gate status and durable evidence location to Main before the embargo lifts.

Do not activate the Human Preview yourself. Do not resume any older CODEX route. Do not self-select another mission after this gate.


## CURRENT SHARED CODEX ROUTE — rev 0079

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FIRST-PROJECT-ENV-HARNESS-64`

`ORDER_STATE: ACTIVE / ENVIRONMENT_HARNESS_ONLY / PRE_BLACKBOX`

The Product Owner has changed the Preview topology before either exploratory journey produced findings.

Do not begin the black-box product journey yet.

Authoritative preview control:
- branch: `coord/w15-fresh-install-preview-control`;
- file: `docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`;
- rev 0003 amendment commit: `9f9d750cc28b42214bf46974dec9f7107ba5b666`.

Dedicated environment branch already created by Main directly from the accepted product checkpoint:
`preview/w15-first-project-env-harness`

Exact product base:
`1f14a57491805a5d976bc9d0bf51393cf1b3ebcd` / tree `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`.

### Mission

Prepare one repository-owned, infrastructure-only fresh-install Preview harness capable of producing two isolated environments from the same product bytes:

A. CODEX local container environment;
B. fresh GitHub Codespace environment for the Product Owner.

You may use source/config/control knowledge while building and validating the harness. This is **PRE-AUDIT ENVIRONMENT PREPARATION**, not the black-box journey.

Prefer reuse/convergence with existing assets:
- `ci/local/Dockerfile.linux-e2e`;
- `ci/local/docker-compose.yml`;
- `scripts/ci/*`;
- current `.devcontainer/devcontainer.json`;
- historical `docs/CODESPACES-PREVIEW-RUNBOOK.md` only as infrastructure reference.

Do not resurrect stale Wave 14 Demo/bootstrap behavior.

Required outcome:
- container/Compose path starts normal EliteSCADA API + Web against an isolated disposable PostgreSQL/TimescaleDB state;
- fresh install has no imported project, EEE, hidden Demo Engineering project or test-owned Working state;
- startup automation may start dependencies/API/Web but must not create the user's first Administrator/project/application for them;
- deterministic clean reset;
- browser endpoint suitable for user-like exploration;
- separate Codespaces path from the same harness with Web 5173 Private, API 5080 + DB 5432 internal;
- no production semantic/auth/licensing/runtime/HA/security bypass;
- harness-only files unless a genuine product blocker is proven.

Validate both modes enough to prove they boot cleanly and reproducibly. Do not perform the actual first-project exploration while validating environment readiness.

Return to Main with:
- exact harness branch/head/tree;
- file list and rationale;
- local container clean-boot evidence;
- Codespaces/devcontainer readiness evidence that can be established repository-side;
- exact reset/start commands or automation entrypoints;
- confirmation that the branch differs from product base only by environment/harness infrastructure;
- any genuine blocker that prevents a truthful fresh install.

After Main accepts the harness it will release both exploratory gates for parallel independent execution. Do not self-start them before that acceptance.


## CURRENT SHARED CODEX ROUTE — rev 0080

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FIRST-PROJECT-ENV-HARNESS-W14-CODESPACE-LIFECYCLE-65`

`ORDER_STATE: ACTIVE / ENVIRONMENT_HARNESS_ONLY / W14_CODESPACE_LIFECYCLE_REQUIRED`

This amendment is binding and supersedes rev 0079 where more specific.

The Product Owner identified a proven Wave 14 Codespaces lifecycle/reopen problem that MUST be consumed before designing the Wave 15 human Preview environment.

### Mandatory historical source set — read before changing the harness

Primary historical Preview branch:
`preview/codespaces-test-preview`

Read these exact repository-controlled artifacts from that branch:
1. `docs/CODESPACES-PREVIEW-RUNBOOK.md` — operational handoff/runbook, introduced at commit `0ab6e80c1c47a78b0bd33b07424d906b5f847faa`, later policy-aligned at `a08171ebe62ce20427a22aaf028b764a9c114184`;
2. `.devcontainer/devcontainer.json` — final preserved Preview topology;
3. `.devcontainer/docker-compose.yml`;
4. `scripts/preview/launch-test-preview.sh`;
5. `.vscode/tasks.json`.

Historical GitHub evidence that explains WHY the lifecycle design matters:
- issue #208 comment `5510874911`: first automatic-launch correction; real Codespace had forwarded 5173 but no serving Web process; startup was moved to repository-controlled automatic launch instead of requiring a terminal;
- issue #208 comment `5511449185`: CRITICAL lifecycle finding — CI could validate the configured `postAttachCommand` string and launcher behavior but did **not** reproduce real Codespaces lifecycle/process-lifetime behavior after the command returned; fresh Codespace still produced 502. This is the specific historical warning to preserve;
- issue #208 comment `5512754904`: durable operational runbook and recovery levels A/B/C/D;
- issue #286 comment `5629189357`: later direct-Codespace handoff explicitly requires capturing browser/5173/5080/process/log state BEFORE restart/reopen, proving restart/reopen can erase diagnostic evidence and must be treated as an environment lifecycle event.

### Historical configuration facts to understand, not blindly cargo-cult

The preserved Wave 14 Preview used:
- Compose-backed devcontainer named `EliteSCADA Test Preview`;
- app service kept alive by `command: sleep infinity`;
- `shutdownAction: stopCompose`;
- disposable machine identity prepared during `initializeCommand` and mounted read-only at `/etc/machine-id`;
- TimescaleDB service with `restart: unless-stopped`;
- only port 5173 forwarded/opened; API 5080 ignored for auto-forwarding; database private;
- `postCreateCommand` only for dependency installation;
- automatic launcher `bash scripts/preview/launch-test-preview.sh` through devcontainer lifecycle;
- manual VS Code task `Launch Test Preview` as explicit recovery/restart path;
- launcher is idempotent around stale PID files/processes and can restart API/Web without requiring a fresh Codespace.

Do NOT import the old Wave 11 Demo/bootstrap semantics into Wave 15. The value of these artifacts is the **Codespaces lifecycle, process supervision, restart/reopen, machine identity, port exposure and deterministic recovery design**.

### Product Owner-specific reopen requirement

The Wave 15 harness must explicitly cover the scenario the Product Owner remembers from Wave 14:
- Codespace is initially opened and EliteSCADA starts successfully;
- the Codespace later stops/suspends after its normal inactivity/continuous-use lifecycle;
- the same Codespace is opened/resumed again;
- EliteSCADA API and Web MUST become available again automatically without requiring hidden terminal intervention or recreating the Codespace;
- forwarded 5173 MUST NOT remain as a misleading proxy-only 502 state;
- the solution must work from repository-controlled devcontainer/lifecycle automation.

Do not assume that merely declaring `postAttachCommand` proves this. Wave 14 explicitly demonstrated that CI string validation was insufficient. Choose the appropriate devcontainer lifecycle hooks/process model after reading the historical artifacts, then prove the actual stop/start/reattach behavior as closely as the available environment permits. If `postStartCommand`, `postAttachCommand`, both, or another repository-owned supervisor is needed, decide from lifecycle semantics and validate it rather than copying one hook blindly.

### Required evidence in CODEX handoff

In addition to rev 0079 requirements, return:
- exact historical Wave 14 references actually consumed;
- explanation of the old first-open/502/resume failure mode;
- chosen Wave 15 lifecycle hook/process-supervision design and why;
- proof that initial creation works;
- proof that launcher restart is idempotent;
- proof or faithful simulation of Codespace stop/suspend -> resume/reopen -> automatic API/Web recovery;
- proof that 5173 becomes healthy again while 5080/5432 remain private/internal;
- explicit statement that no first-project/user journey was pre-seeded while solving environment lifecycle.

Do not begin either exploratory audit until Main accepts this lifecycle proof and releases ENV_A/ENV_B READY.


## CURRENT SHARED CODEX ROUTE — rev 0081

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FIRST-PROJECT-ENV-HARNESS-PUBLIC-WEB-66`

`ORDER_STATE: ACTIVE / ENVIRONMENT_HARNESS_ONLY / HUMAN_CODESPACE_5173_PUBLIC`

This amendment is binding and supersedes rev 0080 where more specific.

Product Owner requirement for Environment B (Human Preview Codespace):
- Web port `5173` must be PUBLIC automatically;
- API `5080` and database `5432` remain internal/private;
- Public visibility must be restored automatically after Codespace stop/suspend -> resume/reopen;
- do not require the Product Owner to open the Ports panel and manually change visibility each session.

Read the authoritative Preview control rev 0004 first:
- branch: `coord/w15-fresh-install-preview-control`;
- file: `docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`;
- commit: `b4d84d646be9d5b20567394290cac066b0b87cb0`.

Important implementation constraint:
GitHub Codespaces can revert a forwarded port to private when the port is removed/re-added or the Codespace restarts. Therefore a one-time manual/public setting is insufficient.

Implement repository-controlled visibility re-assertion for 5173 using supported GitHub Codespaces mechanisms (for example `gh codespace ports visibility 5173:public -c <current-codespace>` where available), deriving the current Codespace identity dynamically and verifying success. Do not hardcode a Codespace name.

The harness readiness proof must include:
1. fresh Codespace -> automatic product startup -> 5173 healthy and PUBLIC;
2. direct ordinary-browser access to the public 5173 URL reaches EliteSCADA;
3. stop/suspend -> resume/reopen -> product auto-recovers and 5173 becomes PUBLIC again without Product Owner manual action;
4. 5080/5432 remain non-public;
5. no product auth/licensing weakening and no first-project state pre-seeding.

Note: public forwarded 5173 removes GitHub's port-level authentication for anyone who knows the URL. EliteSCADA's own login/auth/authorization must remain intact.


## CURRENT SHARED CODEX ROUTE — rev 0082

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FIRST-PROJECT-ENV-HARNESS-RESUMABLE-LOCAL-AUDIT-67`

`ORDER_STATE: ACTIVE / ENVIRONMENT_HARNESS_ONLY / LOCAL_AUDIT_START_PAUSE_RESUME_REQUIRED`

This amendment is binding and supersedes rev 0081 where more specific.

Product Owner requirement for Environment A (CODEX local audit): the audit must be resumable across CODEX usage-window limits and PC shutdown/restart.

Read authoritative Preview control rev 0005 first:
- branch: `coord/w15-fresh-install-preview-control`;
- file: `docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`;
- commit: `6a6ce2226df69b6c976d125f80749d65b31b6802`.

Implement one repository-controlled lifecycle surface with at least:
`start | pause | resume | status | reset`.

Preferred shape:
`scripts/preview/local-audit.sh start|pause|resume|status|reset`
(or a demonstrably better equivalent).

Binding semantics:
- `start`: create a brand-new clean audit session only when no resumable session exists;
- `pause`: checkpoint + stop runtime/compute while preserving audit DB/project/evidence/provenance;
- `resume`: continue the same paused audit session, never silently reset/reseed;
- `status`: report session ID, lifecycle state, exact product/harness SHA, service health and resumability;
- `reset`: explicit destructive cleanup only; never implicit during pause/resume.

Persistent state must survive container/process lifetime via named Docker volume and/or host-mounted ignored storage. Preserve database/project state, audit evidence and exact provenance. Do not rely only on writable container layers.

Resume must validate exact saved product/harness identity and fail clearly on mismatch rather than migrating product state automatically.

Required proof before ENV_A can be accepted READY:
1. fresh start is actually fresh/no-project;
2. create distinguishable ordinary product-visible state;
3. pause stops services without deleting that state;
4. restart host/container runtime as faithfully as available and resume same session/state;
5. second pause/resume cycle works idempotently;
6. status truthfully distinguishes RUNNING / PAUSED_RESUMABLE;
7. reset is explicitly destructive and next start returns to true fresh-install state;
8. no product semantic/auth/licensing/runtime change was made for resumability.

Keep black-box integrity: do not use the checkpoint mechanism to inject source-derived navigation hints or repair product state. Detailed findings remain embargoed across pauses.

Do not start the actual first-project exploratory audit until Main accepts this lifecycle proof and marks ENV_A READY.


## CURRENT SHARED CODEX ROUTE — rev 0083

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-READINESS-PROOF-68`

`ORDER_STATE: ACTIVE / READINESS_PROOF_ONLY / BLACKBOX_HOLD`

Main independently reviewed exact harness candidate `1df4dae293bcca59ee3191faf889058fecc973ee` / tree `6efbf4b6fccd7750231c51d5f638d556707fdaec` and accepts it only for readiness validation.

Authoritative preview-control acceptance:
- branch: `coord/w15-fresh-install-preview-control`;
- file: `docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`;
- rev 0006 commit: `d142afce04954fe65986b2a0c74a944f6e7e6d5d`.

Do NOT begin the actual black-box first-project audit.

### Execute ENV_A lifecycle proof now

From exact harness candidate, run the repository-owned local audit lifecycle using Main acceptance SHA:
`scripts/preview/local-audit.ps1 start -AcceptedHarnessSha 1df4dae293bcca59ee3191faf889058fecc973ee`.

Then complete the readiness sequence from rev 0006:
1. clean start / prove no harness-seeded Administrator/project/application;
2. create only the smallest supported product-owned persistence marker needed to prove continuity;
3. first `pause` with coarse checkpoint;
4. stop/restart local Docker/host runtime as faithfully as possible;
5. `resume` and prove same session/marker survived;
6. second `pause -> resume` cycle and prove idempotence;
7. prove `status` truth for RUNNING and PAUSED_RESUMABLE plus exact provenance;
8. explicit `reset`;
9. prove final `status = NOT_STARTED`, no dedicated preview containers/volumes remain, and no probe state survives.

This pre-audit probe may use normal supported product/public contracts strictly to create/read the minimal persistence marker. Do not use database row mutation, hidden fixture, EEE/Demo import, exploratory UX evaluation, or product correction.

Return a `CODEX -> MAIN` handoff with:
- exact branch/head/tree;
- commands actually executed;
- session ID and exact product/harness provenance;
- evidence for both pause/resume cycles;
- evidence for post-reset fresh state;
- explicit statement that the actual black-box audit did NOT start;
- any blocker/limitation in faithfully simulating host shutdown/reboot.

ENV_B remains WAIT_REAL_CODESPACE_PROOF. Do not self-start Human Preview.


## CURRENT SHARED CODEX ROUTE — rev 0084

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-HARNESS-FIX-69`

`ORDER_STATE: ACTIVE / HARNESS_FIX_ONLY / BLACKBOX_HOLD`

Main reviewed CODEX readiness-proof handoff #305 comment `5855982610`.

The exact accepted harness `1df4dae293bcca59ee3191faf889058fecc973ee` successfully booted fresh EliteSCADA infrastructure but failed in the PowerShell lifecycle wrapper after health became green:
`Add-Transition` attempted to assign missing optional property `lastTransitionMessage` on the session `PSCustomObject`.

CODEX correctly stopped, did not bypass the wrapper, ran explicit authorized reset, and returned `NOT_STARTED` with no dedicated preview containers/volumes.

Authoritative correction order:
- Preview control rev 0007;
- commit `162e2ed82e8dbf26c73f8fabcb1489f02d4ede40`.

### Mission

On `preview/w15-first-project-env-harness`, make the narrowest infrastructure-only correction so transition/session metadata is schema-safe across both newly created and JSON-reloaded PowerShell objects.

Required behavior:
- missing optional transition fields may be added safely before update;
- existing fields update normally;
- repeated transitions remain idempotent;
- exact product-base, clean-worktree, accepted-harness-SHA, version-mismatch and destructive-reset guards remain intact.

Do NOT change product source/tests/workflows/identity/licensing/Authority/lifecycle/project semantics.

### Proof before handoff

Use blank-first-run infrastructure state only and prove:
1. parse/static checks;
2. `start` -> truthful RUNNING + persisted transition metadata;
3. `status` -> RUNNING;
4. `pause -Checkpoint ...` -> PAUSED_RESUMABLE with checkpoint/message persisted;
5. `resume` after session JSON reload -> healthy RUNNING/RESUMED;
6. repeated transition metadata updates do not throw;
7. explicit `reset` -> NOT_STARTED and no dedicated containers/volumes;
8. compare from `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd` remains harness-only.

Publish one replacement exact harness commit/tree and return `CODEX -> MAIN` with commands/evidence and final clean/reset state.

Do NOT create the positive continuity marker yet. Do NOT start the actual black-box audit. ENV_B remains WAIT_REAL_CODESPACE_PROOF.


## CURRENT SHARED CODEX ROUTE — rev 0085

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-FINAL-CONTINUITY-PROOF-70`

`ORDER_STATE: ACTIVE / FINAL_READINESS_PROOF_ONLY / BLACKBOX_HOLD`

Main accepts exact replacement harness:
- SHA `ec050e9bfda121805b1165860a4aeda0eb2582e8`;
- tree `0d6221540c3678a8b042c51082f7a2ed0a466fa2`;
- product base `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Authoritative preview control:
- rev 0009;
- commit `d3f35cc6ea09d3be032c1337aa3b020992d01e4d`.

Do NOT begin the black-box first-project audit.

### Final ENV_A readiness mission

Run one disposable pre-audit session on exact accepted harness and prove product-state continuity across a **real Docker Desktop/daemon restart**.

Required sequence:
1. start with `-AcceptedHarnessSha ec050e9bfda121805b1165860a4aeda0eb2582e8`;
2. confirm fresh state / no harness-seeded Administrator/project/application;
3. create the smallest supported product-owned persistence marker, preferably only the first Local Administrator identity;
4. verify marker via normal supported product/public behavior;
5. pause -> PAUSED_RESUMABLE;
6. fully stop and restart Docker Desktop/engine using a supported host mechanism; do not substitute only Compose stop/start;
7. after Docker is healthy, status + resume;
8. prove same session ID + marker survived without reseed/migration;
9. second ordinary pause/resume cycle;
10. verify status/provenance;
11. reset;
12. prove NOT_STARTED, no dedicated preview containers/volumes, and no marker survives the fresh boundary.

If host policy/tooling prevents a real Docker daemon restart, return exactly `HOST_DAEMON_RESTART_PROOF_BLOCKED` with the limitation. Do not fake equivalent proof.

Use public/supported product contracts only for the minimal marker. No exploratory UX evaluation, direct DB mutation, project fixture, EEE/Demo import, product correction or black-box journey.

Return `CODEX -> MAIN COORDINATOR` with:
- exact branch/head/tree;
- session ID;
- exact marker type (do not expose secrets);
- command/method used to stop/start Docker Desktop/engine;
- proof before/after daemon restart;
- second pause/resume proof;
- reset/fresh proof;
- explicit statement that black-box exploration did not start.

ENV_B is now pinned to this same exact shared harness candidate but remains under MAIN ownership and WAIT_REAL_CODESPACE_PROOF.


## CURRENT SHARED CODEX ROUTE — rev 0086

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-DEPENDENCY-BOUNDARY-FIX-71`

`ORDER_STATE: ACTIVE / HARNESS_FIX_ONLY / BLACKBOX_HOLD`

Main reviewed #305 comment `5856199560`.

The final continuity proof materially passed the Product Owner's shutdown/resume requirement on exact harness `ec050e9bfda121805b1165860a4aeda0eb2582e8`:
- minimal Local Administrator created through UI;
- clean pause;
- real Docker Desktop restart via `docker desktop restart --timeout 180`;
- Docker session/engine restarted;
- same audit session resumed;
- same post-bootstrap no-project product state survived;
- second pause/resume passed;
- reset returned NOT_STARTED with no dedicated preview resources.

The remaining blocker is harness-only: reset also destroyed dependency volumes, so the subsequent fresh start ran runtime `npm install` and failed on external registry certificate validation (`UNABLE_TO_VERIFY_LEAF_SIGNATURE`).

Authoritative correction contract:
- Preview control rev 0011;
- commit `44023a9cf9e29c38d9b0e62f72198fa89e242018`.

### Mission

On `preview/w15-first-project-env-harness`, make the narrowest robust infrastructure-only change that separates immutable dependency/tool preparation from destructive product/audit reset.

Preferred direction:
- add explicit `prepare` semantics and/or a provenance-bound prebuilt dependency image/cache;
- network/npm/NuGet resolution occurs at preparation/build time;
- `reset` removes DB/runtime/audit session state but preserves dependency/tool preparation state;
- repeated `reset -> start` must not need new npm/NuGet downloads;
- dependency reuse must be tied to exact lock/project/harness provenance and must never silently accept stale packages.

Forbidden:
- disabling TLS/certificate verification;
- `strict-ssl=false`;
- `NODE_TLS_REJECT_UNAUTHORIZED=0`;
- importing unknown machine-local trust anchors automatically;
- floating dependency versions;
- changing product source/tests/workflows/auth/licensing/Authority/lifecycle semantics.

If one-time preparation cannot reach registries under valid TLS, return `ENVIRONMENT_PREP_BLOCKED_TLS`; do not hide the environment problem in product startup.

### Required replacement proof

Return one exact replacement harness SHA/tree after proving:
1. infrastructure-only diff;
2. valid dependency preparation/provenance;
3. clean product start -> first Administrator surface;
4. minimal Administrator marker via UI;
5. pause;
6. real Docker Desktop/engine restart;
7. resume same marker/session;
8. reset destroys product state but not immutable dependency preparation;
9. second clean start reaches first-run/no-project state with no new npm/NuGet network download;
10. if practical, repeat that start while external package network is unavailable after preparation;
11. final reset -> NOT_STARTED with no dedicated product-state containers/volumes;
12. black-box Stage 1 did not start.

Do not start ENV_A Stage 1 or Stage 2. ENV_B remains under Main ownership.


## CURRENT SHARED CODEX ROUTE — rev 0087

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-FINAL-EXACT-SHA-READINESS-PROOF-72`

`ORDER_STATE: ACTIVE / FINAL_READINESS_PROOF_ONLY / BLACKBOX_HOLD`

Main independently reviewed and accepts exact replacement harness for final readiness proof:
- branch: `preview/w15-first-project-env-harness`;
- SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Authoritative Preview control:
- rev 0012;
- commit `08471efc2716fd5065bddb279a20d9f48c89e8b8`.

Preparation/provenance design and the explicit trusted Windows root opt-in for preparation-only TLS inspection are accepted as bounded environment infrastructure. TLS verification must remain enabled. Do not change trust globally or carry the selected root into product start/resume.

### Execute final ENV_A proof now

Use the already prepared exact candidate state if `status` confirms:
`NOT_STARTED / PREPARATION=READY`.

Then execute:
1. start exact `bb451fa...` using Main acceptance SHA;
2. verify true fresh first-run UI, no seeded project/import/Demo;
3. create only minimal Local Administrator through UI;
4. verify no-project post-bootstrap state;
5. pause -> PAUSED_RESUMABLE;
6. real Docker Desktop/engine restart;
7. status + resume after engine recovery;
8. prove same session + same Administrator/no-project state;
9. second ordinary pause/resume;
10. reset;
11. prove product/audit resources removed but `PREPARATION=READY` retained;
12. second clean start on same exact accepted SHA;
13. prove no npm/NuGet install/restore/download occurs during second start and fresh first-run UI appears;
14. where practical, prove package-network access is unnecessary for that second start without changing product networking semantics;
15. final reset -> `NOT_STARTED / PREPARATION=READY`, no product/audit resources.

Do not create a project. Do not begin Stage 1 black-box. Do not begin Stage 2.

Return `CODEX -> MAIN COORDINATOR — ENV_A FINAL READINESS HANDOFF` with:
- exact head/tree/product base;
- preparation dependency key and provenance status;
- session IDs used;
- exact Docker Desktop restart method and before/after engine evidence;
- evidence of same product-owned marker after resume;
- evidence reset preserved preparation but destroyed product state;
- evidence second start used no package network/install/restore;
- final status/resources;
- explicit statement Stage 1/Stage 2 did not start;
- any blocker classified by layer.

ENV_B remains Main-owned and WAIT_REAL_CODESPACE_PROOF.


## CURRENT SHARED CODEX ROUTE — rev 0088

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-READY-HOLD-FOR-ENV-B-73`

`ORDER_STATE: HOLD / ENV_A_READY / WAIT_ENV_B_READY / NO_ACTIVE_EXPLORATORY_MISSION`

Main reviewed and accepted the final ENV_A readiness proof on exact harness:
- SHA `bb451fa6e07982ac12384895f6097d5833761d16`;
- tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Authoritative Preview control:
- rev 0013;
- commit `af6c911623e93ca868176c7b5597b04a9e303f0f`.

Disposition:
`ENV_A = READY / CLEAN / RESUMABLE`.

Evidence accepted:
- provenance-bound preparation READY;
- first-run UI truthful;
- minimal Local Administrator via UI;
- real Docker Desktop restart;
- same-session state continuity;
- second pause/resume;
- reset preserved PREPARATION=READY while deleting product/audit state;
- second clean start used no npm/NuGet install/restore/network bootstrap;
- final reset -> NOT_STARTED / PREPARATION=READY;
- Stage 1/Stage 2 did not start.

Do NOT begin Stage 1 yet.
Do NOT begin Stage 2.
Do NOT create a project.
Do NOT resume any older mission.

Reason for HOLD:
The Product Owner requested independent parallel audit topology. Main is now completing ENV_B real Codespace readiness so both environments can begin cleanly without cross-contamination.

Next CODEX action will be issued only after Main records ENV_B READY and explicitly releases the black-box gate.


## CURRENT SHARED CODEX ROUTE — rev 0089

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-FIRST-PROJECT-BLACKBOX-STAGE1-74`

`ORDER_STATE: ACTIVE / BLACKBOX_STAGE1 / EMBARGO_ACTIVE`

Main has accepted both independent Preview environments as READY.

Authoritative Preview control:
- rev 0020;
- commit `965ceef9753bac16d488c064de5b9bcb1ed55333`.

ENV_A exact authority:
- harness branch: `preview/w15-first-project-env-harness`;
- harness SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- harness tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`;
- readiness: `READY / CLEAN / RESUMABLE`;
- expected initial infrastructure state: `NOT_STARTED / PREPARATION=READY`.

Embargoed evidence branch:
`preview/w15-first-project-codex-evidence`.

### Execute Stage 1 now

Start exactly one new clean audit session using the accepted harness SHA, then perform the black-box product journey.

Binding user mission:
> You have just installed EliteSCADA. Explore the product using normal user-visible interfaces and create a small SCADA application from scratch that reaches a functional Runtime.

During the exploratory journey:
- use only normal user-visible product UI and product-visible Help/manual surfaces;
- do not read product source/control documents to discover implementation or navigation;
- do not inspect database state;
- do not use internal API shortcuts to bypass UI;
- do not use implementation logs as navigation guidance;
- do not use test fixtures/imports/hidden Demo state;
- do not patch product/configuration;
- do not correct code;
- do not use shell/state mutation to advance product state.

Allowed shell/harness operations are infrastructure-only:
- `local-audit.ps1 start -AcceptedHarnessSha bb451fa6e07982ac12384895f6097d5833761d16` once;
- `status` for environment health/state;
- `pause -Checkpoint <coarse user-visible stopping point>` when CODEX time/host interruption requires suspension;
- `resume` to continue the same audit session later.

Do not `reset` an active Stage 1 session without a new Main order unless the environment is conclusively invalid and continuation would destroy evidence; in that case stop and return `INVALID_ENVIRONMENT / RESET_REQUIRED`.

If the user-visible journey completes, return coarse status `COMPLETE` to Main. If product-visible behavior blocks progress, return `BLOCKED_BY_PRODUCT`. If infrastructure blocks progress, return `BLOCKED_BY_ENVIRONMENT`.

Do not expose detailed findings to the Product Owner-facing ledger/control while the Human Preview is active.

After COMPLETE/BLOCKED, diagnostic correlation may inspect source/log/API/tests and persist detailed findings to the embargoed evidence branch. Product mutation remains forbidden.

### Required coarse handoff to Main while embargo active

Return only:
- exact harness SHA/tree;
- audit session ID;
- coarse gate state;
- whether pause/resume was used;
- whether a functional Runtime was reached;
- embargoed evidence branch/path reference;
- no spoiler detail.

Human Preview is active independently. Do not wait for Product Owner observations and do not consume them.

Stage 2 remains PREPARED / NOT ACTIVE.


## CURRENT SHARED CODEX ROUTE — rev 0090

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-STAGE1-ATTEMPT1-SEAL-RECONCILE-75`

`ORDER_STATE: ACTIVE / INFRA_RECONCILIATION_ONLY / NO_PRODUCT_INTERACTION / EMBARGO_ACTIVE`

Main reviewed your coarse handoff and the embargoed evidence branch.

Authoritative Preview control:
- rev 0021;
- commit `95a632ba8fb2ef3db8caaff760e94be9953f4ef6`.

Disposition of current black-box attempt:
`STAGE1_ATTEMPT1 = INCONCLUSIVE / SEALED`.

Do not continue product exploration. Do not perform further diagnostic correlation. Do not create/modify project state.

### Reconcile only runtime harness identity

Existing audit session:
`6f18039a-5e67-49bd-a566-2050cf775aee`.

Accepted harness identity:
- SHA `bb451fa6e07982ac12384895f6097d5833761d16`;
- tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- expected dependency key `0d074f1bd0f1702a5313651791dd7ce6aa1996821f312ca45a7d6761f9b0794e`.

Perform exactly:
1. ensure current detailed evidence is pushed to `preview/w15-first-project-codex-evidence`;
2. from now on, do not checkout the evidence branch in the runtime harness worktree; use a separate Git worktree or connector-backed write path for evidence;
3. inspect runtime worktree `git status --short`, current branch, HEAD and tree;
4. if it is not exact accepted harness SHA/tree, restore the runtime worktree to exact `bb451fa...` cleanly, without touching Docker containers/volumes/database/session manifest;
5. do NOT run `prepare`, `start`, `resume`, or `reset` during this reconciliation;
6. run only:
   `scripts/preview/local-audit.ps1 status`;
7. if and only if status returns same session with `RUNNING` and `PREPARATION=READY` on expected dependency key, run:
   `scripts/preview/local-audit.ps1 pause -Checkpoint "Stage1 attempt 1 sealed by Main after audit-state reconciliation"`;
8. verify final coarse state `PAUSED_RESUMABLE / PREPARATION=READY`;
9. if any mismatch remains, stop immediately and return the exact coarse infrastructure mismatch without destructive action.

Do not expose embargoed findings in the Product Owner-facing ledger.

### Required handoff

Return:
`CODEX -> MAIN COORDINATOR — STAGE1 ATTEMPT1 SEALED RECONCILIATION`

Include only:
- runtime worktree branch/HEAD/tree before reconciliation;
- whether it differed from exact harness;
- status after exact-harness restoration;
- dependency key;
- same audit session ID confirmation;
- final lifecycle state (expected PAUSED_RESUMABLE);
- evidence branch HEAD;
- no detailed product findings.

After this handoff, remain:
`HOLD / STAGE1_ATTEMPT1_SEALED / WAIT_MAIN_RETRY_DECISION`.

Do not begin Stage 2.
Do not begin a Stage 1 retry in the same CODEX context.


## CURRENT SHARED CODEX ROUTE — rev 0091

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-AUTONOMOUS-ENV-A-INFRA-RECOVERY-76`

`ORDER_STATE: ACTIVE / AUTONOMOUS_INFRA_HARNESS_RECOVERY / NO_PRODUCT_FIX / EMBARGO_ACTIVE`

Product Owner explicitly authorized you to keep making useful progress without stopping for Main approval on every small infrastructure decision.

Authoritative Preview control:
- rev 0022;
- commit `40125a09fd4444c64ca874c59dcb4269055940c1`.

Current facts:
- accepted harness/product identity remains `bb451fa6e07982ac12384895f6097d5833761d16` / tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- sealed attempt-1 session: `6f18039a-5e67-49bd-a566-2050cf775aee`;
- runtime worktree is already exact/clean;
- session reports RUNNING;
- preparation reports REQUIRED because expected provenance manifest is absent;
- attempt 1 is SEALED / INCONCLUSIVE and must not resume as black-box exploration.

### Mission

Act autonomously as ENV_A infrastructure/audit-harness maintainer.

Do not return to Main for routine implementation decisions. Investigate, preserve, repair, test, iterate and push a replacement harness candidate if needed.

Priorities:
1. preserve attempt-1 state/evidence durably;
2. safely pause/quiesce the attempt-1 runtime;
3. determine the missing-preparation-manifest root cause;
4. fix the harness so evidence work, worktree operations and lifecycle events cannot silently invalidate preparation;
5. add regression coverage for the discovered failure;
6. validate a clean replacement candidate comprehensively;
7. leave ENV_A deterministic for a future fresh CODEX context and/or Stage 2.

### You are authorized to

- inspect Docker/container/volume/image labels and metadata;
- inspect ignored Preview artifacts, preparation manifests, dependency markers and session manifests;
- inspect harness source/config/logs/tests;
- use source/API/log knowledge for infrastructure diagnosis;
- create separate Git worktrees for evidence;
- change/commit/push **Preview harness infrastructure only**;
- add infrastructure regression tests/checks;
- add repair/snapshot/preparation lifecycle commands if useful;
- reconstruct preparation metadata only from cryptographically/label-verified immutable prepared assets;
- run explicit preparation after attempt-1 is safely preserved/quiesced;
- use the already accepted explicit trusted-root preparation mechanism if package network is needed;
- create and destroy disposable validation sessions;
- restart Docker Desktop for validation;
- perform as many safe harness iterations as necessary.

### Preserve attempt 1 first

Before destructive cleanup of attempt-1 runtime state, capture and verify:
- session manifest;
- exact harness/product identity;
- Docker container/volume/network metadata;
- a durable database/product-state snapshot or equivalent restorable backup;
- embargoed evidence branch references.

Try the normal harness `pause` first. It does not require PREPARATION=READY. If it fails solely because of infrastructure, you may use the least-invasive Docker quiesce necessary, documenting it.

After durable preservation, you may archive/reset attempt-1 runtime if that becomes necessary for harness repair/validation. Never reuse it as a new black-box attempt.

### Forbidden

- no EliteSCADA product source modification;
- no product test changes to hide a defect;
- no auth/licensing/Authority weakening;
- no direct DB-row repair to force success;
- no seeded project/Demo/EEE state;
- no Product Owner observation consumption;
- no detailed finding disclosure to Product Owner-facing surfaces;
- no new independent Stage1 black-box attempt in this already-contaminated CODEX context.

If you discover a probable product defect, preserve it in embargoed evidence and keep working on independent infrastructure tasks. Do not fix the product.

### Completion target

Return only after either:
A. a fully validated replacement harness candidate is pushed, with root cause + regression proof + lifecycle validation; or
B. a genuine external blocker prevents safe further progress.

The preferred final handoff should include:
- exact branch/head/tree/product base;
- attempt-1 preservation location and final sealed state;
- missing-manifest root cause;
- exact harness changes;
- regression coverage;
- preparation/provenance validation;
- start/pause/resume/Docker-restart/reset validation;
- proof evidence branch operations no longer invalidate runtime preparation identity;
- final clean/disposable environment state;
- explicit non-actions on product code.

Human Preview continues independently. Stage 2 remains PREPARED / NOT ACTIVE.


## CURRENT SHARED CODEX ROUTE — rev 0092

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-REPAIR-FINAL-LIFECYCLE-77`

`ORDER_STATE: ACTIVE / AUTONOMOUS_FINAL_LIFECYCLE_VALIDATION / NO_PRODUCT_FIX / EMBARGO_ACTIVE`

Main reviewed and accepts the repair candidate for final lifecycle validation:
- branch `preview/w15-first-project-env-harness-repair`;
- SHA `50a4451aa122f7f9fd0af98173c184f6623a147b`;
- tree `5efeafb08725a90ce0c3df689b8d613f165bb569`;
- product base `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Authoritative Preview control:
- rev 0023;
- commit `5c0e57c6336e75a68769f35251cca245963b4801`.

Accepted root cause:
old dependency provenance hashed working-tree bytes, so Windows LF/CRLF materialization could change the dependency key on the same exact committed Git tree. Repair derives identity from committed Git blob IDs instead. Embedded Bash text is also normalized to LF before PowerShell->Bash transport.

Attempt 1 remains sealed/inconclusive and preserved. Do not resume it as black-box exploration.

### Continue autonomously — do not return for micro-decisions

Run the complete final lifecycle proof on the repair branch. You are already authorized to iterate on harness-only fixes/regressions if any step fails.

Required final proof:
1. dependency identity + Bash line-ending regression PASS;
2. status NOT_STARTED / PREPARATION=READY on exact candidate;
3. disposable start reaches healthy true fresh first-run product surface without starting a new audit;
4. repeated status keeps the same dependency key/READY state;
5. pause -> PAUSED_RESUMABLE/READY;
6. resume same disposable session;
7. real Docker Desktop/engine restart;
8. status + resume after engine recovery;
9. separate evidence worktree commit while runtime worktree stays fixed; runtime status must remain same PREPARATION=READY/key;
10. reset -> NOT_STARTED/READY, no product-state resources;
11. second fresh disposable start without package prepare/install/restore;
12. final reset -> NOT_STARTED/READY;
13. PowerShell/static/diff/worktree checks pass;
14. compare from product base remains harness-only.

No project creation is required. Do not start another independent Stage1 attempt in this context.

If small harness defects appear, fix/test/commit them on the repair branch and continue. Return only after a fully validated final candidate is pushed or a genuine external blocker/preservation risk remains.

Do not fast-forward the canonical harness branch yourself; Main will promote after final review.

Human Preview continues independently. Product Owner observations remain off-limits. Detailed product findings remain embargoed. Stage2 remains PREPARED / NOT ACTIVE.


## CURRENT SHARED CODEX ROUTE — rev 0093

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-ENV-A-REPAIR-FINAL-LIFECYCLE-77A`

`ORDER_STATE: ACTIVE / AUTONOMOUS_FINAL_LIFECYCLE_VALIDATION / HUMAN_PREVIEW_COMPLETE / EMBARGO_LIFTED`

This revision supersedes rev 0092 only for Human Preview / embargo status. The active infrastructure mission and exact repair candidate remain unchanged.

Human Preview ended:
`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = BLOCKED_BY_PRODUCT`.

Canonical human evidence:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-HUMAN-PREVIEW-FINDINGS.md`
commit `e413b3c85224cbf3f62bad6896738ec7ebe7810c`.

The findings embargo is now LIFTED. After you complete the current ENV_A harness final lifecycle validation, you may consume the Human Preview findings and the sealed attempt-1 CODEX observations for cross-audit/Stage2 work when Main activates Stage2.

Do not interrupt or shortcut the current harness validation because of the newly available product findings.

Current repair candidate remains:
- branch `preview/w15-first-project-env-harness-repair`;
- SHA `50a4451aa122f7f9fd0af98173c184f6623a147b`;
- tree `5efeafb08725a90ce0c3df689b8d613f165bb569`.

Continue the complete lifecycle proof exactly as ordered in rev 0092.

New/confirmed product correction owners now available for later directed verification:
- #354 fresh first-project Demo Runtime leakage / authority mismatch;
- #355 Data Source Type selector blocks TAG creation;
- #356 Templates create/edit authoring gap;
- #303 Editor Properties/text/fill usability;
- #308 Library/Dynamo visual preview.

Stage2 contract now includes V2-14..V2-18 via commit `e96f1045a644a70e3f49c6aa0cdd1ebb88eab0da`.

Do NOT start Stage2 until Main accepts the final repaired harness and explicitly activates it.

Product-source modification remains forbidden in the current infrastructure mission.


## CURRENT SHARED CODEX ROUTE — rev 0094

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-VS-LOCAL-OPERATOR-BOOTSTRAP-78`

`ORDER_STATE: ACTIVE / INFRASTRUCTURE_AND_DOCS_ONLY / PRODUCT_FIX_FORBIDDEN`

Main accepts the repaired ENV_A harness and has promoted canonical harness branch:
`preview/w15-first-project-env-harness@50a4451aa122f7f9fd0af98173c184f6623a147b`
/tree `5efeafb08725a90ce0c3df689b8d613f165bb569`.

Authoritative Preview control:
- rev 0028;
- commit `510291282cbdceca9bbdab806dd7f5b4524f55e9`.

Dedicated implementation branch:
`preview/w15-vs-local-runner`
created from exact accepted harness SHA `50a4451...`.

Dedicated issue:
#360 — `W15-LOCAL-OPS — operador local containerizado + bootstrap para IA do Visual Studio`.

### Mission

Create a robust one-command-surface local EliteSCADA operator intended for Product Owner + native Visual Studio AI use.

Preferred entry point:
`scripts/preview/elite-local.ps1`.

Required commands:
- `prepare`;
- `start`;
- `status`;
- `pause`;
- `resume`;
- `stop`;
- `restart`;
- `diagnose`;
- `reset` with explicit destructive confirmation.

Use the already accepted dependency identity/preparation/lifecycle infrastructure instead of duplicating fragile Compose behavior.

### User semantics

The Product Owner should be able to tell Visual Studio AI in normal Portuguese:
- "Rode o EliteSCADA localmente";
- "Pause, vou desligar o PC";
- "Retome o mesmo ambiente";
- "Pare sem apagar meu projeto";
- "Reinicie o EliteSCADA sem apagar nada";
- "Qual o status e a URL?";
- "Colete um diagnóstico para comparar com o Codespace";
- "Quero fresh install" — destructive confirmation required.

The script must make these mappings unambiguous.

### Required bootstrap

Create:
`docs/VISUAL-STUDIO-AI-LOCAL-ELITESCADA-BOOTSTRAP.md`.

It must be concise enough to paste/read directly in the native Visual Studio AI and must instruct it to:
- use the operator script instead of improvising Docker commands;
- run `status` before lifecycle actions;
- preserve state by default;
- use `restart` for service restart, never `reset`;
- never run reset without explicit Product Owner request/confirmation;
- report the local Web URL clearly;
- use `diagnose` on failure and report evidence;
- not alter product code without a later explicit correction mission;
- not seed Demo/EEE/project state;
- not weaken TLS/auth/licensing/Authority;
- not infer Codespace-only failures are generic product defects without local A/B evidence.

Include ready-to-use example prompts for the Product Owner.

### Diagnose contract

`diagnose` must be non-mutating and useful for local-vs-Codespace comparison, reporting/capturing at minimum:
- Git branch/HEAD/tree;
- dependency preparation key/state;
- local workbench/session identity/lifecycle;
- DB/API/Web health;
- ports/URLs;
- recent startup/API/Web logs;
- timestamp;
- no secrets.

Keep room for later latency/jitter diagnostics under #307/#359, but do not add product-specific remote workarounds.

### Safety/ergonomics

- Windows/PowerShell + Docker Desktop first-class;
- repo-root-relative paths only;
- detect Docker Desktop/engine availability;
- commands idempotent where possible;
- preserve DB/product state on pause/stop/restart;
- `reset` is the only destructive product-state operation and must fail without explicit confirmation;
- preparation remains reusable/provenance-bound;
- default Web URL normally `http://localhost:5173`;
- optional JSON status output / IDE tasks are encouraged if clean.

### Acceptance proof

Before returning:
1. prepare -> READY;
2. start -> DB/API/Web healthy;
3. status exact + useful;
4. preserve one disposable state marker across pause/resume;
5. preserve it across stop/start or stop/resume;
6. preserve it across restart;
7. preserve it across Docker Desktop restart + resume;
8. diagnose works and leaks no secret;
9. reset refuses without explicit destructive confirmation;
10. explicit reset -> fresh product state while PREPARATION remains READY;
11. next start performs no package reinstall/restore;
12. bootstrap matches actual command syntax and semantics;
13. PowerShell parse/static checks + relevant lifecycle regressions PASS;
14. branch diff from product base is infrastructure/docs only.

### Creative freedom

You have freedom to simplify/refactor the local operator and reuse existing modules. Prefer one obvious user-facing command surface over exposing audit-internal complexity.

Do not stop for ordinary implementation choices. Iterate until a validated branch/head/tree is ready or a genuine external blocker remains.

### Forbidden

- no EliteSCADA product source modification;
- no product test weakening;
- no auth/licensing/Authority weakening;
- no direct DB-row shortcut;
- no seeded Demo/EEE/project pass condition;
- no product correction for #354/#355/#359/UX2 in this mission.

Return:
`CODEX -> MAIN COORDINATOR — VS LOCAL OPERATOR + VISUAL STUDIO AI BOOTSTRAP`
with exact branch/head/tree, implemented commands, validation evidence, bootstrap path and explicit product non-actions.


## CURRENT SHARED CODEX ROUTE — rev 0095

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-VS-LOCAL-OPERATOR-BOOTSTRAP-78A`

`ORDER_STATE: ACTIVE / SAME_MISSION / BOOTSTRAP_COPY_REQUIRED_IN_HANDOFF`

This revision does not change the implementation mission from rev 0094.

Additional Product Owner requirement:

When the Visual Studio AI bootstrap is complete, CODEX must deliver **two forms** of it:

1. canonical repository file:
   `docs/VISUAL-STUDIO-AI-LOCAL-ELITESCADA-BOOTSTRAP.md`;
2. a **verbatim full copy of the final bootstrap text in the CODEX -> MAIN handoff**, so Main can paste/re-send it directly to the Product Owner in ChatGPT without requiring GitHub navigation.

The handoff must therefore include a clearly delimited section:

`BEGIN VISUAL STUDIO AI BOOTSTRAP COPY`

<complete final bootstrap text>

`END VISUAL STUDIO AI BOOTSTRAP COPY`

The copy must match the committed file exactly at the returned final branch HEAD.

If the bootstrap is long, do not summarize it in the handoff; include the complete text.

Also report:
- bootstrap file blob/path identity where practical;
- exact final branch HEAD/tree;
- confirmation that the handoff copy and repository file are byte-equivalent as UTF-8 text aside from platform newline normalization, if any.

All rev 0094 safety, validation and product non-action rules remain binding.


## CURRENT SHARED CODEX ROUTE — rev 0096

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-VS-LOCAL-OPERATOR-BOOTSTRAP-78B`

`ORDER_STATE: ACTIVE / SAME_MISSION / LOCAL_OPERATIONS_EVIDENCE_REQUIRED`

This revision keeps rev 0094/0095 implementation scope and adds a mandatory canonical evidence artifact.

Create and commit:
`docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md`.

Purpose:
preserve the real evidence, failure modes, lifecycle behavior and design lessons discovered while running EliteSCADA locally so they can be reused by:
- the next fresh Preview;
- #307/#359 local-vs-remote diagnostics;
- Windows packaging #205/#207;
- future Linux packaging;
- installed-service lifecycle #361.

Required evidence sections:
1. environment/provenance;
2. confirmed local lifecycle behavior for prepare/start/status/pause/resume/stop/restart/reset;
3. actual problems encountered, including provenance-manifest loss, LF/CRLF identity instability, PowerShell->Bash CRLF, evidence/runtime worktree contamination risk, Docker restart/resume, port/resource/startup/readiness issues;
4. confirmed root causes/corrections/regressions;
5. persistence semantics and destructive boundaries;
6. readiness/health contract;
7. diagnose/redaction contract;
8. implications for the next Preview;
9. implications for Windows Service/Linux systemd installers;
10. open questions/future tests.

Use explicit evidence labels:
`CONFIRMED | OBSERVED | HYPOTHESIS | NOT_TESTED`.

Do not include secrets/credentials/private keys/tokens/license secrets.

Installed-service design owner now exists:
#361 — `INSTALL-OPS — lifecycle operacional como Windows Service e Linux systemd`.

Important boundary:
#360 may prototype/reuse lifecycle semantics, but must **not** implement production Windows Service/systemd architecture in this mission. Record evidence and implications only.

The final CODEX handoff must include:
- exact evidence file path/blob/commit;
- a concise list of the most important confirmed local-operation lessons;
- the full verbatim Visual Studio AI bootstrap copy required by rev 0095;
- exact branch/head/tree;
- explicit product non-actions.

All previous safety/validation requirements remain binding.


## CURRENT SHARED CODEX ROUTE — rev 0097

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-VS-LOCAL-OPERATOR-BOOTSTRAP-78C`

`ORDER_STATE: WAIT_PENDING_MAIN_REVIEW / HANDOFF_RECEIVED / DO_NOT_CONTINUE_OR_MERGE`

CODEX has returned the #360 implementation/evidence handoff.

Current exact candidate:
- PR #362 — `feat(preview): add safe local workbench operator`;
- branch `preview/w15-vs-local-runner`;
- exact HEAD `13cb1fcaa3091085515e4d84a3d3e894db541f5f`;
- base `preview/w15-first-project-env-harness@50a4451aa122f7f9fd0af98173c184f6623a147b`;
- PR currently OPEN / mergeable / NOT MERGED.

Changed paths are bounded to:
- `docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md`;
- `docs/LOCAL-FIRST-PROJECT-PREVIEW-HARNESS.md`;
- `docs/VISUAL-STUDIO-AI-LOCAL-ELITESCADA-BOOTSTRAP.md`;
- `scripts/preview/elite-local.ps1`;
- `scripts/preview/local-audit.ps1`;
- `scripts/preview/test-local-operator.ps1`.

CODEX reports static/regression checks PASS, but the complete product lifecycle proof on exact HEAD `13cb1f...` is still NOT_TESTED because Main has not accepted this changed harness SHA for a new ENV_A session.

Do not continue implementation, prepare, start, reset, merge or rewrite #362 until the next Main Coordinator independently reviews the exact PR diff/head and issues the next order.

Mandatory next Main action:
1. revalidate PR #362 live exact head/base;
2. inspect all six changed files and compare against accepted harness `50a4451...`;
3. confirm scope remains Preview infrastructure/docs only and reset/lifecycle safety is correct;
4. if acceptable, explicitly authorize exact candidate SHA for lifecycle validation without product mutation;
5. require full lifecycle proof before considering merge/promotion.

The Visual Studio bootstrap full text was delivered in Issue #305 comment `5858902468`.

Local-operations evidence handoff is Issue #305 comment `5859122865`.

Container-native strategic architecture is separately recorded under #363 / ADR-010. Do not widen #360 into OCI production implementation.


## CURRENT SHARED CODEX ROUTE — rev 0098

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-VS-LOCAL-OPERATOR-CORRECTION-79`

`ORDER_STATE: ACTIVE_CORRECTION / EXACT_HEAD_13CB1F_REJECTED_FOR_LIFECYCLE / NO_PREPARE_START_RESET_MERGE`

Main independently reviewed PR #362 at exact HEAD:
`13cb1fcaa3091085515e4d84a3d3e894db541f5f`
against accepted harness:
`50a4451aa122f7f9fd0af98173c184f6623a147b`.

Main review disposition:
`CHANGES_REQUIRED / PREVIEW_INFRA_ONLY / LIFECYCLE_NOT_AUTHORIZED`.

Scope review:
- exact compare is 3 commits ahead / 0 behind from the accepted harness;
- exactly six paths changed;
- all six are Preview infrastructure/docs/operator-test paths;
- no EliteSCADA product source, product tests, workflow, licensing, Authority, Runtime/Driver contract, or ADR-010 implementation is present.

Blocking defect on exact HEAD `13cb1f...`:
- `docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md` was added after the operator implementation commit;
- `scripts/preview/local-audit.ps1:Get-HarnessIdentity` does not include that exact documentation path in its allowed Preview file set;
- therefore normal identity-gated operations such as `prepare`, new `start`, `resume`, and `restart` classify the PR's own evidence file as a product-scope change and fail before lifecycle execution;
- this means the full lifecycle proof cannot validly run on `13cb1f...` as delivered.

Documentation truthfulness correction:
- the evidence file currently calls `4ffb4bef69c3ca69528793e58eee7aa2fecb9c6a` the "current local-operator delivery", while live PR HEAD is `13cb1f...`;
- preserve `4ffb4bef...` only as the operator implementation commit if useful, and phrase branch-tip/current-candidate identity so it cannot become self-referential/stale.

Required narrow correction:
1. make the canonical evidence document an explicitly permitted Preview documentation path for strict harness identity checks; do not broaden this to arbitrary `docs/**`;
2. add/strengthen regression coverage so the full exact #362 changed-path set is accepted by the Preview scope guard while a representative product-scope path is still rejected;
3. correct the stale/misleading `4ffb4bef...` wording in `docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md`;
4. rerun PowerShell parser/operator safety tests, dependency-identity LF/CRLF regression, documentation whitespace/`git diff --check`, and any focused scope-guard regression;
5. return exact new branch HEAD/tree and changed-file list.

Hard boundaries:
- no product source/test/workflow mutation;
- no ADR-010/#363 implementation;
- no `prepare`, product `start`, `resume`, `restart`, destructive `reset`, or lifecycle proof in this correction order;
- no merge;
- do not rewrite/delete existing evidence history.

After the corrected exact HEAD returns, Main will revalidate the diff and only then may issue an exact-SHA lifecycle authorization.

Required return:
`CODEX -> MAIN COORDINATOR — #362 SCOPE-GUARD CORRECTION HANDOFF`.


## CURRENT SHARED CODEX ROUTE — rev 0099

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-P0-DEMO-RUNTIME-AUTHORITY-CORRECTION-80`

`ORDER_STATE: ACTIVE_PRODUCT_CORRECTION / #362_PARKED_NO_LIFECYCLE_NO_MERGE / #354_FIRST`

Product Owner has reprioritized Wave 15 to **CORRECTION-NOW**. User-visible and authority defects confirmed by Human Preview/CODEX cross-audit must be corrected now; Editor is urgent but is only one lane inside the broader correction program.

### #362 park / exact live disposition

PR #362 live branch advanced after rev0098 to:
`preview/w15-vs-local-runner@6d0f4c6f80e7bb1a53b4d95b6a215a9cc6ba1e05`.

Main revalidated that:
- it remains Preview infrastructure/docs only;
- the original evidence-file allowlist defect is corrected;
- however the new commit is **not** a narrow rev0098 handoff: it adds a separate local development profile, `launch` semantics, new Compose overlay and broader lifecycle behavior;
- no explicit rev0098-compliant CODEX correction handoff was posted;
- no lifecycle proof exists on `6d0f4c6...`.

Therefore:
`#362 = PARKED / NO LIFECYCLE / NO MERGE / PRESERVE BRANCH`.

Do not rewrite/delete it. Main will return later for independent full review of the expanded local-development operator.

### Active product correction — #354

Owner issue:
#354 — `W15-PREVIEW-P0 — fresh first project leaks Demo Runtime content / Engineering authority mismatch`.

Dedicated branch:
`work/w15-p0-demo-runtime-authority-correction`

Exact base:
`wave15/corrections-integration@00d17e716b877e4cc00e25ea093f53f0da485c24`.

Accepted product bytes at that base remain:
`1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Human/CODEX convergent evidence:
- Human first-project Runtime displayed unexpected Demo-like tank/pump/frequency/current content that could not be reconciled from Engineering.
- Independent CODEX Stage 1 observed visible `Demo · Estação Elevatória` while Engineering showed another project identity and zero TAGs/Data Sources.

Classification:
`P0/P1 GENERIC PRODUCT AUTHORITY DEFECT / FIRST-PROJECT TRUST BLOCKER`.

### Mission

Identify and correct the exact generic product authority/recovery/fallback path that allows hidden Demo/EEE Runtime content to become visible under Neutral/fresh/first-project state.

Required proof before handoff:
1. trace exact Working/Published/Active/Runtime source-of-truth path that produced the stale/hidden Demo content;
2. preserve all valid Demo-mode licensing/session behavior while preventing Demo application content from becoming an implicit fresh-project fallback;
3. true fresh/no-project state shows no unexplained Demo Runtime;
4. normal first-project creation through supported product lifecycle does not inherit unrelated Demo visuals/state;
5. Working/Published/Active/Runtime project + revision identities agree intentionally;
6. restart/recovery/resume does not resurrect unrelated Demo content;
7. Neutral/detach/bootstrap remains free of stale operational visual/project projection;
8. cross-project activation/recovery remains fail-closed;
9. regression covers no-project bootstrap, first-project bootstrap, persisted restart/recovery and explicit Demo application behavior if applicable;
10. full affected focused tests + relevant broad product gates pass.

### Diagnostic freedom

CODEX may inspect source/tests/history and run deterministic local tests. This is a correction mission, not black-box Preview.

Use existing canonical authority/lifecycle contracts. Do not solve by:
- hiding Demo visuals in frontend;
- hard-coding the first project;
- deleting valid Demo licensing/session semantics;
- weakening persisted Runtime recovery;
- mutating database rows as a product fix;
- seeding a different fixture;
- changing unrelated Editor/UX/Container/DB architecture.

### Scope boundary

Modify only files required for #354 root-cause correction and regressions.

If root cause crosses a frozen shared authority contract, stop before broad redesign and return:
`BLOCKED_CONTRACT / exact contract + evidence`.

Do not include #355, #359, Editor UX, TAG Gateway, Templates, container-first or DB-topology implementation in this branch.

### Required return

`CODEX -> MAIN COORDINATOR — #354 DEMO RUNTIME AUTHORITY CORRECTION HANDOFF`

Include:
- exact branch/head/tree;
- exact root cause;
- changed files;
- regression matrix;
- focused/broad test evidence;
- explicit preserved Demo semantics;
- remaining uncertainty;
- no merge.

After #354 candidate returns, Main will independently review before integration.

## CURRENT SHARED CODEX ROUTE — rev 0100

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-P0-DEMO-RUNTIME-AUTHORITY-E3-81`

`ORDER_STATE: ACTIVE_E3_VALIDATION / EXACT_CANDIDATE_LOCKED / NO_PRODUCT_MUTATION / NO_MERGE`

Main independently reviewed #354 candidate:
- PR #372;
- exact branch `work/w15-p0-demo-runtime-authority-correction`;
- exact HEAD `c84dee88268ecf786737cf3ec9364d0113d5f028`;
- exact tree `d7219cbbc27d39bdebb54193d6a71b9d3f65db2f`;
- exact base `wave15/corrections-integration@00d17e716b877e4cc00e25ea093f53f0da485c24`;
- T1 exact-SHA run `36370397556` SUCCESS.

Main disposition:
`MAIN_CODE_REVIEW_PASS / E2_ACCEPTED / WAIT_E3_MOUNTED_AUTHORITY_VALIDATION`.

### Mission

Validate the exact #354 candidate as a real mounted local product without changing product source/tests/contracts.

Required E3 environment:
- real PostgreSQL;
- real API;
- real Web;
- browser using normal mounted product UI;
- persistent state across an actual application/service restart.

Required journey:
1. start from genuinely fresh product state;
2. complete supported first-run Administrator/identity flow;
3. before any project is Active, open Runtime and prove neutral state with no hidden Demo/tank/pump application content;
4. create the first project through normal supported product flow but do not Activate it; Runtime must remain neutral;
5. Publish/Activate that project explicitly; Runtime project/revision identity must match the selected Active revision;
6. restart the mounted application/services while preserving the product DB/state; Runtime must recover the same persisted Active project/revision;
7. create or edit a different Working project/revision without activating it; Runtime must stay on the previous Active authority;
8. verify Runtime reads/effects are not sourced from a non-Active Working/Published project;
9. verify no hidden Demo application content appears at any neutral/first-project stage;
10. determine whether a currently supported explicit Demo application artifact/path exists. If yes, prove it works only after explicit normal publish/activate. If no, report N/A with concrete product/code evidence. Do not recreate legacy fallback merely to satisfy this point.

Capture:
- exact candidate SHA/tree;
- exact environment/start commands;
- DB/API/Web health;
- browser-visible states/screenshots where useful;
- Runtime project/revision before activation, after activation and after restart;
- persistence/recovery evidence;
- negative Demo-content evidence;
- any errors/log correlations.

Hard boundaries:
- no product source/test/workflow mutation;
- no branch commit;
- no merge;
- no write to `main` or `wave15/corrections-integration`;
- no timeout workaround;
- no fixture seeding that bypasses normal first-project authority;
- no use of the parked #362 expanded lifecycle as accepted evidence unless Main separately authorizes it.

If E3 cannot be run in this environment, return:
`ENV_CAPABILITY_GAP / E3 / <exact reason> / <recommended executor>`.

Required return:
`CODEX -> MAIN COORDINATOR — #354 E3 AUTHORITY VALIDATION HANDOFF`.

After this E3 handoff, stop and wait. Do not proceed to #371 on your own.


## CURRENT SHARED CODEX ROUTE — rev 0101

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-WAIT-RECOMPOSED-EDITOR-E3-82`

`ORDER_STATE: WAIT_EXACT_CANDIDATE / #354_INTEGRATED / NO_PRODUCT_MUTATION / NO_MERGE`

#354 E3 was accepted and PR #372 was merged.

Integrated authority baseline:
`wave15/corrections-integration@33e514eb3f5cf8f984779c0091069387741e7296`

`C-AUTHORITY-01 = FROZEN_FOR_CONSUMERS`.

Do **not** continue to #371 using stale candidate `2e288bbf...`.

#371 must first be recomposed by DEV-EDITOR-CORE onto the exact integration baseline above and rerun E2/T1.

Until Main posts a new exact candidate HEAD/tree and explicit E3 order:
- wait;
- do not modify #371;
- do not merge;
- do not start #370;
- do not return to #362.

When the user says SIGA while this WAIT state remains, revalidate live control + #303/#371 and report WAIT if no recomposed candidate is authorized.


## CURRENT SHARED CODEX ROUTE — rev 0102

`ORDER_ID: ROUTE-SEQUENTIAL-CODEX-COMBINED-EDITOR-DATA-E3-83`

`ORDER_STATE: ACTIVE_E3_VALIDATION / TWO_EXACT_CANDIDATES / LOCAL_COMPOSITION_ONLY / NO_PRODUCT_MUTATION / NO_MERGE`

Main revalidated both recomposed candidates.

Shared base:
`wave15/corrections-integration@33e514eb3f5cf8f984779c0091069387741e7296`

Candidate C / #371:
- branch `work/w15-editor-first-user-correction`
- HEAD `38506f1ea8d281287949b3fb23f26dbd319ffa43`
- tree `f4843811a53b4dc91ca301d3594f0a85b574bb35`
- T1 `36374037238` SUCCESS
- PR mergeable=true.

Candidate B / #370:
- branch `work/w15-p1-datasource-authoring-correction`
- HEAD `bc39f5fe64f59e73b1f0e0e413196cb917af50b4`
- tree `eae78480d565aa5020df58ae79fa9b53910a5168`
- T1 `36374028034` SUCCESS
- PR mergeable=true.

Main independently confirmed zero changed-file overlap between #371 and #370.

### Mission

Validate both candidates together in one exact local composition.

Do not push a combined product branch and do not mutate either candidate branch.

1. fetch/revalidate exact base + both exact heads;
2. locally compose #371 and #370 with the exact shared base, preserving both candidates byte-for-byte where there is no overlap;
3. if any merge conflict or unexpected additional delta exists, STOP and return `COMPOSITION_BLOCKED / <evidence>`;
4. compute and record:
   - local combined commit SHA if one is created;
   - mandatory exact `COMBINED_TREE_SHA`;
   - diff path set against `33e514...`;
5. prove the combined path set is exactly the union of the reviewed #371 + #370 diffs;
6. run one real mounted E3 with PostgreSQL + API + Web + browser on that exact combined tree.

### Required E3 journey

Use a genuinely fresh mounted product state.

Authority sanity:
- before Active, Runtime is neutral;
- no hidden Demo/tank/pump fallback.

Editor #371:
- normal Engineering UI;
- Screen: add/select rectangle, change fill/stroke/width, immediate canonical WYSIWYG;
- Text: edit literal + rename Key while stable Id remains unchanged;
- Canvas/Outliner/Properties remain synchronized;
- Undo/Redo;
- Save/Reopen;
- repeat applicable first-user path on Popup;
- local disclosure controls/Engineering Lock/HMI config layout remain usable.

Data #370:
- create Data Source through normal UI;
- Type/Driver catalog is visible/selectable;
- configure/apply Data Source;
- create representative TAG;
- bind/use the TAG through normal product authoring;
- Save/Reopen;
- restart mounted API/Web while preserving DB;
- Publish/Activate;
- Runtime resolves the intended Active project/revision and representative TAG path/value/quality as applicable.

Composition:
- after restart and after activation, re-open Editor/Data Source surfaces and prove persistence;
- no Working-only change replaces Active Runtime authority;
- no frontend/private API shortcut may substitute for discovery/configuration steps required by the user-facing journey.

The exact injected catalog 503 -> Reload regression is already E2-covered. It may be rechecked if convenient, but the mandatory E3 gate is the complete normal mounted lifecycle above.

### Boundaries

- no product source/test/workflow mutation;
- no push of a combined product branch;
- no write to either DEV branch;
- no merge;
- no write to main or wave15/corrections-integration;
- no #367/#368/#369 expansion;
- no #307/#359 timing redesign;
- no Codespace-specific workaround;
- no second renderer;
- no Design-mode process write.

### Required return

`CODEX -> MAIN COORDINATOR — #371 + #370 COMBINED E3 HANDOFF`

Include:
- exact base;
- both exact input heads/trees;
- exact COMBINED_TREE_SHA;
- local composition method;
- proof path-set == union of #371 + #370;
- mounted environment;
- Editor Screen/Popup evidence;
- Data Source -> TAG -> Runtime evidence;
- restart/persistence evidence;
- authority sanity;
- findings;
- explicit non-actions.

Final disposition must be one of:
- `COMBINED_E3_PASS`
- `COMBINED_E3_FAIL / <finding>`
- `COMPOSITION_BLOCKED / <reason>`
- `ENV_CAPABILITY_GAP / E3 / <reason>`

After return, STOP. Main alone decides integration.
