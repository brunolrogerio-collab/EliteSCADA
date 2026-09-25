# Wave 15 — FND-07 Main Coordinator Control Plane

> GitHub live is the sole authority. This file is PREPARED ONLY and does not authorize product mutation.

`CONTROL_BRANCH: coord/w15-fnd07-control`

`MAIN_ORDER_REV: 0021`

`STATE: DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`

`CURRENT_ORDER_ID: FND07-DEV-DETACH-NEUTRAL-V1`

`LATEST_AUDITED_PRODUCT_CHECKPOINT: e3ed5138369c576549cb58a7aff9783792f322d3`

`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`

`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`

`WORK_BRANCH: work/w15-fnd-07-detach-neutral`

`TARGET_BRANCH: wave15/corrections-integration`

`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`

## 0A. Current hold reason

FND-06 is VERIFIED/FROZEN and the final post-FND06 audit rev 0014 is `ACCEPTABLE / FC0A_RELEASE_APPROVED`.

FND-07 is **ACTIVE_CODING** on the exact FC0-A release checkpoint above. The normal ChatGPT DEV may now implement only the bounded detach/neutral-bootstrap slice on its isolated work branch.

The second and third Main audit passes identified no breaking FND-07 contract redefinition. FND-07 remains compositional/compatible, and its active acceptance matrix must explicitly cover **Engineering Lock × detach/switch/neutral-bootstrap** so the existing replacement/recovery exemption and backend Authority rules are preserved without inventing a second credential or leaking protected Engineering content.

Executor policy changed by Product Owner/Main:
- implementation owner: **normal ChatGPT DEV chat**;
- Main owns contract/architecture review;
- scarce sequential CODEX validates the Main-accepted candidate with focused/adversarial tests and exact-head T1;
- material implementation defects return to FND-07 DEV;
- frozen-contract insufficiency returns `FND-07 -> BLOCKED-CONTRACT -> MAIN`.

Cross-lane coordination:
`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`.

## 0B. Activation order — live

On `SIGA`, FND-07 DEV must:
1. re-read this control live;
2. revalidate `work/w15-fnd-07-detach-neutral` still descends from the exact base above;
3. execute only `FND07-DEV-DETACH-NEUTRAL-V1`;
4. preserve frozen lifecycle/Authority/licensing semantics;
5. keep the Engineering Lock × detach/switch/neutral-bootstrap matrix explicit;
6. stop as `BLOCKED-CONTRACT` if a frozen semantic rewrite would be required;
7. do not merge or declare VERIFIED/FROZEN.

## 1. Purpose

FND-07 freezes the secure installation detach/neutral-bootstrap backend transaction required before downstream Installation UX may be released.

Sources:
- Issue #304;
- Issue #305 FND-07;
- C25 Restore-first/System Recovery architecture;
- frozen FND-01 lifecycle/bootstrap;
- frozen FND-02 Authority;
- frozen FND-03 licensing/session/fencing primitives.

## 2. Source audit — prepared findings

Current repository already contains reusable authority:
- `src/Scada.Api/ProjectPackages/SystemRecoveryApplicationService.cs` and endpoints for staged application/System Recovery.
- `SystemRecoveryAuthorityAdmission.cs` for prospective restored-Authority admission.
- atomic Authority-store replacement regressions in `LocalIdentityStoreReplaceTests`.
- current Product Licensing API/service already exposes install/remove primitives.
- FND-03 froze transactional verify/install/replace/remove semantics plus Runtime lease/authority re-evaluation.
- C25 explicitly keeps Application, Authority, Historian/database and Licensing as separate authorities.
- There is no accepted normal populated-install "detach current Application + Authority -> neutral bootstrap" transaction yet.
- Therefore FND-07 should compose existing authorities, not introduce direct database deletion or anonymous recovery.

## 3. Frozen direction to be activated later

1. Detach is an authorized installation-level transaction, not a frontend reset.
2. Before authority removal, stop/fence current project Runtime process effects and reject new writes/commands.
3. Handle unsaved Working state explicitly.
4. Detach current Application lifecycle binding without leaking Project A into Project B.
5. Remove/replace bound local Authority through existing protected atomic mechanisms.
6. Invalidate old Authority-A sessions/tokens.
7. Finish in truthful neutral bootstrap allowing Create / Import / Restore.
8. Do not silently create a Demo project.
9. Machine license is independent: keep is normal; remove/replace are separate deliberate operations using frozen FND-03 semantics.
10. Detach never silently deletes Historian/database state.
11. No populated-install anonymous takeover window is allowed.
12. Audit non-secret transition evidence.

## 3A. Compatibility promise to FC0-A frozen contracts

FND-07 is permitted to activate only after:
- FND-06 VERIFIED/FROZEN; and
- `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01` returns `ACCEPTABLE / FC0A_RELEASE_APPROVED`.

The prepared FND-07 design is **compositional**, not a lifecycle/Authority/licensing redesign.

Must preserve frozen downstream-consumed semantics:
- FND-01 Working/Revisions/Published/Active authority and fail-closed lifecycle;
- FND-02/AUTH-04 capability/scope evaluator and populated-install security invariants;
- FND-03 machine-license trust, install/replace/remove transaction semantics, Runtime lease/authority re-evaluation and shared quota meaning;
- FND-04 Script contract and FND-06 visual authority are unaffected by detach orchestration.

Allowed FND-07 additions include:
- one protected detach/preflight transaction that coordinates existing authorities;
- runtime/process-effect fence before detach;
- old Authority session/token invalidation;
- truthful neutral bootstrap orchestration;
- explicit license keep/remove/replace through existing FND-03 authority;
- additive status/result surfaces for later Installation UX.

Shared shell/router changes needed to expose neutral bootstrap are integration hotspots, not permission to redefine lifecycle/Authority/licensing semantics.

If implementation requires changing the meaning of a frozen FND-01/FND-02/FND-03 contract consumed by FC0-A DEVs, stop:
`FND-07 -> BLOCKED-CONTRACT -> MAIN`.

No such breaking change is currently identified by the prepared source audit; final approval belongs to the mandatory post-FND06 audit.

## 3B. Delivery / validation ownership

FND-07 implementation is owned by the normal-chat DEV lane after activation.

DEV deliverable:
- coherent protected detach/neutral-bootstrap implementation on the exact Main-provided FC0-A base;
- compile/cheap focused sanity where practical;
- exact head/tree + changed-file map;
- explicit orchestration/fencing assumptions;
- acceptance rows not executed locally marked `PENDING_FOR_CODEX`.

Main reviews lifecycle/Authority/licensing/Engineering-Lock composition before spending CODEX time.

Only after `MAIN_ACCEPTED_FOR_CODEX` does the sequential CODEX run the focused/adversarial matrix and exact-head T1.

Material product/design defects return to FND-07 DEV. Small validation-driven corrections may be made by CODEX if they do not redesign the contract.

## 4. Prepared first implementation slice

`ORDER_ID: FND07-DEV-DETACH-NEUTRAL-V1`

`ORDER_STATE: ACTIVE_CODING / AUTHORIZED`

`EXECUTOR_MODE: NORMAL_CHAT_DEV / BOUNDED_FOUNDATION_IMPLEMENTATION`

At activation Main must write exact product SHA/tree and create an isolated work branch.

First slice should implement the backend transaction/state machine and protected API required for:
- preflight/preview;
- explicit consequence acknowledgement;
- runtime/process-effect fence;
- application detach;
- Authority detach/replace transition;
- session invalidation;
- neutral-bootstrap completion;
- license keep/remove/replace orchestration only through already-frozen licensing authority.

Full end-user Installation UX remains downstream DEV-INSTALLATION-UX.

## 5. Mandatory RED/acceptance matrix at activation

At minimum prove:
1. old base has no supported normal detach transaction;
2. unauthorized principal cannot detach;
3. unsaved Working state is surfaced before mutation;
4. Runtime/Drivers/Server Script/process writes are fenced before Application/Authority removal;
5. new Project-A writes fail once detach begins;
6. Project-A Working/Published/Active cannot become Project-B authority;
7. Authority-A sessions/tokens are invalid after transition;
8. neutral bootstrap exposes supported Create/Import/Restore paths;
9. no hidden Demo project is seeded;
10. keep-license path preserves valid machine license;
11. deliberate remove enters Demo/no-license, not invalid-installed-license;
12. valid replacement commits only after verification;
13. invalid/wrong-machine replacement preserves prior valid license;
14. Runtime lease/capacity state is re-evaluated/fenced through frozen FND-03 mechanisms;
15. Historian/database state is retained by default and not silently erased;
16. Authority backup remains separate from `.escadapkg`;
17. license remains outside Application/Authority artifacts;
18. A->B->A repeated switching is deterministic;
19. Audit contains safe metadata and no credentials/signing material;
20. Engineering Lock × detach/switch/neutral-bootstrap is explicitly reconciled with the existing authorized replacement/recovery exemption: backend Authority remains authoritative, protected Engineering does not leak, no second credential/bypass is invented, and locked/unlocked paths are deterministic;
21. natural Wave 15 T1 + focused System Recovery/Authority/Licensing tests green.

## 6. Forbidden

No anonymous populated-install restore, direct DB hacks, silent Historian deletion, license-in-package, credentials-in-package, stale process effects, permissive invalid-license fallback, lifecycle/Authority weakening, or self-merge/freeze.

## 7. Activation dependency

FND-01/FND-02/FND-03/FND-06 prerequisites are frozen. The mandatory post-FND06 audit rev 0014 returned `ACCEPTABLE / FC0A_RELEASE_APPROVED`. This order is now ACTIVE on the exact base recorded above.

After FC0-A release, FND-07 DEV may implement in parallel with the other prepared DEV lanes on its own isolated branch. There is no fixed four-DEV concurrency cap. Main controls shared-hotspot collisions and validation/integration order.

CODEX does not need to be free for FND-07 **coding**. It is required later for focused/adversarial validation and exact-head T1 before Main integration approval.


## 8. Main review — deterministic fresh-install fixture correction

Exact reviewed candidate:
- PR `#348` (draft);
- head `ae11e42e8ad5e39b1e2c5a0068f81e4ec31653c6`;
- T1 `36066422907`.

T1 classification:
- classifier: SUCCESS;
- Common sanity: SUCCESS;
- Web semantic build: SUCCESS;
- focused .NET: SUCCESS;
- focused Chromium: FAILURE;
- exact failure: `tests-e2e/local-auth.spec.ts:89`, old assertion that the fresh server's exported Engineering package contains Demo TAGs.

Main classification:

`EXPECTED_W15_PRODUCT_SEMANTIC / STALE_DEMO-DEPENDENT_E2E_FIXTURE / DEV_CORRECTION_REQUIRED`

Reason:
- the prepared first-project fresh-install contract explicitly requires a genuinely fresh installation with **no hidden/synthetic Demo Project and no preconfigured TAG/Screen/Script/Data Source/Working state**;
- FND-07's neutral/bootstrap direction likewise requires no hidden Demo after detach;
- therefore restoring Demo product behavior merely to satisfy the old test is forbidden.

The red is still deterministic and must be corrected before Main contract acceptance. Do not rerun unchanged SHA as acceptance.

### CURRENT CORRECTION ORDER

`ORDER_ID: FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: ae11e42e8ad5e39b1e2c5a0068f81e4ec31653c6`

Required bounded correction:

1. update the local-auth/fresh-first-project E2E contract so the **product-visible pre-project state** proves no hidden Demo/preconfigured Engineering content;
2. remove the old dependency on capturing an in-memory Demo package before first-project creation;
3. do not expect a Demo `tagValueChanged` stream before a project/runtime with TAGs exists;
4. preserve proof that initial Administrator bootstrap/session and first-project creation work normally;
5. preserve proof that the first persisted project is genuinely empty of customer/demo Engineering entities;
6. if later dependent E2E requires a populated baseline, seed/restore a **test-owned fixture explicitly after the no-Demo/first-project assertions**, through canonical supported APIs, so test infrastructure is not mistaken for normal product bootstrap;
7. any realtime assertion that depends on TAG traffic must run only after that explicit test-owned population/activation step;
8. add/retain a regression for post-detach neutral bootstrap showing no hidden Demo/Application A content;
9. keep production FND-07 no-Demo/neutral semantics unchanged unless a separate focused product defect is proven.

Do not broaden this correction into Installation UX.

After correction:
- run the focused local-auth/fresh-install case;
- run FND-07 focused .NET;
- run natural exact-head T1;
- return updated exact SHA/tree and evidence.

Required return prefix remains:

`FND-07 DEV -> MAIN COORDINATOR — CANDIDATE HANDOFF`

No CODEX routing yet. No merge/freeze.


## 9. Replacement Main follow-up — fresh-install fixture progressed, one stale role assertion remains

Exact corrected candidate:
- head `af7bf1ae51539975b3b3e8b40ea36472249ade2e`;
- tree `0947177b314dbcbaaebb3ae8ba65eb2fa3498c49`;
- delta from prior reviewed head: one test-only commit in `web/scada-web/tests-e2e/local-auth.spec.ts`;
- natural T1 `36072685058`: FAILURE only in focused Chromium.

Accepted progress:
- clean pre-project export now proves no hidden Demo/preconfigured Engineering content;
- Demo realtime dependency is removed;
- first Administrator + first project + genuinely-empty-project assertions remain.

Remaining deterministic stale fixture expectation:
- `security-roles` returns exactly one clean bootstrap `developer` role;
- the test still expects historical `developer + operator`;
- the same clean run reports `workspace.securityRoleCount == 1`;
- canonical package assertions separately keep zero project `securityRoles` and two Authority policy reference role IDs.

### CURRENT CORRECTION ORDER — rev 0006

`ORDER_ID: FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: af7bf1ae51539975b3b3e8b40ea36472249ade2e`

Required bounded correction:
1. assert truthful clean bootstrap Engineering security-role state;
2. preserve explicit bootstrap developer coverage;
3. preserve Authority reference assertions as a separate authority;
4. do not seed Demo/operator state to obtain green;
5. return a new exact candidate and natural T1.

Do not rerun `36072685058` unchanged. No CODEX, merge or freeze authority exists yet.


## 10. Fresh-install Authority reference follow-up — rev 0007

Exact head:
- `3f90cc6d62d1ea9df0101e228cd28332d422fa08`;
- tree `845cb62238246f4d525d8e78e8f662e2d196b9e7`;
- T1 `36079596118`: FAILURE only in focused Chromium.

The clean bootstrap Engineering role assertion is now correct. Remaining stale fixture assertion:
- `authorityPolicyReference.roleIds` expects historical cardinality 2;
- clean bootstrap returns exactly one role ID `46000000-0000-0000-0000-000000000002`, consistent with the single developer bootstrap role.

`ORDER_ID: FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: 3f90cc6d62d1ea9df0101e228cd28332d422fa08`

Required bounded delta:
1. remove the historical cardinality-2 expectation;
2. assert the Authority reference contains the actual bootstrap developer identity;
3. preserve empty project `securityRoles`;
4. do not seed operator/Demo state;
5. return a new candidate and natural T1.

No product mutation, CODEX route, merge or freeze is authorized by this failure.


## 11. Downstream populated E2E fixture gap — rev 0008

Exact corrected candidate:
- head `72c22e0ea751222e9929af2fb86305f0da95204c`;
- tree `a318df054072aaffe5f7aae8efe0cdf7ab2eebea`;
- natural T1 `36084614406`: FAILURE only in focused Chromium.

The clean fresh-install/local-auth scenario progressed beyond the prior role and Authority-reference assertions. Remaining failure is in downstream `runtime.spec.ts`, which still expects the historical shared Demo baseline (`ONLINE · 7 TAGs`, `Demo.P01.Frequency`, `demo.overview`, etc.).

Classification:
`DOWNSTREAM_DEMO_DEPENDENT_E2E_BASELINE / TEST_HARNESS_FIXTURE_GAP`.

The product fresh-install semantic remains correct.

`ORDER_ID: FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: 72c22e0ea751222e9929af2fb86305f0da95204c`

Required bounded correction:
1. keep all clean fresh-install assertions before any populated setup;
2. after those assertions, provision an explicit **test-owned** populated baseline for downstream Chromium specs;
3. use supported/canonical APIs only;
4. do not capture/restore product-seeded Demo state;
5. if downstream security/runtime specs need developer/operator Authority setup, provision it explicitly through supported Authority test/setup paths;
6. keep Project `securityRoles` separate from Authority roles;
7. do not mutate production bootstrap semantics;
8. run a new natural exact-head T1 and return the candidate.

No CODEX, merge or freeze authority yet.


## 12. Explicit downstream populated fixture candidate — T1 pending

DEV advanced the bounded test-harness correction to:
- head `da1f00a2db4dbd1125fa342aa5a41c5ec57edbdb`;
- tree `a53271a20a9b5b2e7a2f51d20dcce766f9bb62d8`;
- delta from prior head: test-only `local-auth.spec.ts`.

Main source review confirms the intended sequencing:
1. clean fresh-install/first-project truth is asserted first;
2. only afterwards the test explicitly creates the downstream operator role through the Authority API;
3. only afterwards it applies/saves a populated Engineering fixture through supported APIs;
4. the fixture is explicitly marked test setup and is not product bootstrap behavior;
5. no production file changed.

Natural T1 `36085732961` is currently running.

`ORDER_STATE: DEV_CANDIDATE / T1_PENDING / DEV_WAIT`

DEV must not add further mutation while this exact candidate is under Main review/T1 unless Main returns a defect.


## 13. Authority-vs-Engineering role-count fixture correction — rev 0010

Exact candidate:
- head `da1f00a2db4dbd1125fa342aa5a41c5ec57edbdb`;
- tree `a53271a20a9b5b2e7a2f51d20dcce766f9bb62d8`;
- T1 `36085732961`: focused Chromium failure only.

The explicit downstream fixture is accepted directionally. Remaining failure:
- Authority has developer + operator after explicit test setup;
- Engineering Workspace correctly still reports one Engineering security role;
- test incorrectly expected Workspace count 2.

`ORDER_ID: FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: da1f00a2db4dbd1125fa342aa5a41c5ec57edbdb`

Required minimal correction:
- assert Authority developer+operator separately;
- keep Engineering Workspace securityRoleCount = 1;
- keep project package securityRoles empty;
- preserve Authority policy reference separation;
- no production mutation;
- run new natural exact-head T1.


## 14. ONLY CURRENT ACTION — exact-head T1 dispatch, NO CODE CHANGES — rev 0011

This section **supersedes every earlier FND-07 correction instruction as the current action**.

Current exact candidate is already corrected:

- PR: `#348`
- branch: `work/w15-fnd-07-detach-neutral`
- head: `a8fcfe8c855a692670e9c74a95f4450caf612b2f`
- tree: `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`
- delta from prior reviewed head: exactly **1 line** in `web/scada-web/tests-e2e/local-auth.spec.ts`
- source correction: populated Engineering Workspace `securityRoleCount` expectation changed from stale `2` to truthful `1`
- Main source review: **ACCEPTED**

### DO NOT DO

DEV must **NOT**:
- edit code;
- amend or create another commit;
- change product behavior;
- rebase merely to start CI;
- change PR metadata;
- rerun the old failed run `36085732961`;
- merge, route CODEX, or declare VERIFIED/FROZEN.

### DO THIS NOW

The automatic `pull_request/synchronize` T1 did not appear for exact head `a8fcfe8c...`.

Dispatch the existing Wave 15 T1 workflow explicitly against the exact candidate branch:

```bash
gh workflow run wave15-pr.yml \
  --repo brunolrogerio-collab/EliteSCADA \
  --ref work/w15-fnd-07-detach-neutral \
  -f validation_profile='INSTALLATION, AUTHORITY_CORE, SESSION_LICENSING, SCRIPT_RUNTIME, RUNTIME_RENDERER'
```

Then obtain the newly-created run and verify that its `head_sha` is exactly:

`a8fcfe8c855a692670e9c74a95f4450caf612b2f`

If the run head is not this SHA, stop and report the mismatch. Do not mutate source.

### REQUIRED RETURN

Return only after the exact-head workflow is visible, using:

`FND-07 DEV -> MAIN COORDINATOR — EXACT-HEAD T1 HANDOFF`

Include:
- exact candidate SHA/tree;
- workflow run ID;
- event type;
- workflow head SHA;
- per-job status/conclusion;
- final T1 result if already complete.

If T1 is still running, return `T1_RUNNING`; do **not** make another source change.

`ORDER_ID: FND07-DEV-EXACT-HEAD-T1-02`

`ORDER_STATE: TEST_ONLY / DISPATCH_T1 / NO_SOURCE_MUTATION`

This is the single current FND-07 DEV action.


## 15. CURRENT STATE — environment blocker confirmed / WAIT_MAIN — rev 0012

This section supersedes rev 0011 as the current FND-07 DEV instruction.

Exact candidate remains unchanged:
- PR `#348`
- branch `work/w15-fnd-07-detach-neutral`
- head `a8fcfe8c855a692670e9c74a95f4450caf612b2f`
- tree `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`
- source review: ACCEPTED
- source mutation after rev 0011: none

Environment finding:
- DEV connector cannot dispatch `workflow_dispatch`;
- DEV runtime has no `gh`;
- Main attempted non-source GitHub event triggers while preserving the exact candidate:
  - close/reopen PR #348 on the same head;
  - temporary ref pulse parent -> exact head;
- neither produced a new Wave 15 T1 run for `a8fcfe8c...`.

Therefore the blocker is coordination/tooling, not DEV implementation.

`ORDER_ID: FND07-DEV-WAIT-MAIN-T1-03`

`ORDER_STATE: WAIT_MAIN / NO_SOURCE_MUTATION`

### DEV ACTION

Do nothing.

DEV must not:
- edit source/test code;
- create commits;
- change PR metadata;
- rebase;
- rerun the old `da1f00a2...` workflow;
- attempt alternate CI workarounds;
- merge/freeze.

Main owns resolution of exact-head T1 execution from this point.

Required return on `SIGA` while this order remains current:

`FND-07 DEV -> MAIN COORDINATOR — WAIT_MAIN / EXACT-HEAD T1 ENVIRONMENT BLOCKED`

No additional action is expected from the FND-07 DEV until Main publishes a new order.


## 16. Main route transfer — CODEX exact-head T1 / FND-07 validation — rev 0013

FND-07 DEV remains in WAIT_MAIN and must not mutate source.

Exact candidate:
- PR #348;
- branch `work/w15-fnd-07-detach-neutral`;
- head `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- tree `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`;
- source review: ACCEPTED;
- remaining blocker: no Wave 15 T1 exists for this exact head.

`ORDER_ID: FND07-CODEX-EXACT-HEAD-T1-VALIDATION-04`

`ORDER_STATE: CODEX_VALIDATION / ACTIVE_SHARED_ROUTE`

CODEX must:
1. revalidate PR #348 exact head and target;
2. do not change source initially;
3. dispatch/run the existing `wave15-pr.yml` against exact branch/head using the lane's declared profiles:
   `INSTALLATION, AUTHORITY_CORE, SESSION_LICENSING, SCRIPT_RUNTIME, RUNTIME_RENDERER`;
4. verify the workflow head SHA is exactly `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
5. if T1 is green, run the required adversarial/focused FND-07 validation on fresh-install, detach, neutral bootstrap, no hidden Demo, Runtime truth and no-leak behavior;
6. if validation finds only bounded test-harness defects, CODEX may fix test-only issues and rerun exact-head T1;
7. any material product/architecture defect returns to Main -> FND-07 DEV;
8. no self-merge/freeze.

Return:
`FND-07 CODEX -> MAIN COORDINATOR — EXACT-HEAD VALIDATION HANDOFF`.


## 17. CODEX exact-head T1 diagnosis — populate fixture must publish + activate — rev 0014

Exact candidate tested:
- PR #348;
- head `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- tree `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`;
- exact workflow-dispatch T1: `36088026405`;
- workflow head SHA: exact match;
- classifier: SUCCESS;
- Common sanity: SUCCESS;
- Web semantic build: SUCCESS;
- focused .NET: SUCCESS;
- focused Chromium: FAILURE;
- final T1 gate: FAILURE.

Deterministic Chromium failure:
- `tests-e2e/runtime.spec.ts` times out waiting for `ONLINE · 7 TAGs`;
- the preceding local-auth fixture has already imported the 7-TAG Engineering package and saved a revision;
- it does **not** publish that saved revision nor activate the published revision;
- therefore the fixture populates Working/Engineering state but does not make it the Active Runtime authority.

Canonical lifecycle API already provides:
- `POST /api/engineering/persistence/{projectKey}/revisions/{revision}/publish`;
- `POST /api/engineering/persistence/{projectKey}/published/activate`.

Classification:
`TEST_HARNESS_LIFECYCLE_GAP / ACTIVE_NOT_UPDATED / PRODUCT_SEMANTIC_CORRECT`.

`ORDER_ID: FND07-CODEX-EXACT-HEAD-T1-VALIDATION-04`

`ORDER_STATE: CODEX_BOUNDED_TEST_FIX / ACTIVE_SHARED_ROUTE`

CODEX is authorized to make a bounded **test-only** correction on PR #348:
1. preserve all clean fresh-install assertions before fixture provisioning;
2. after fixture import + save, capture the returned saved `revision`;
3. publish that exact saved revision through the supported lifecycle endpoint;
4. activate the published revision through the supported lifecycle endpoint;
5. assert the publish and activate calls succeed;
6. optionally assert runtime consistency/lifecycle reports the same active revision before downstream Runtime specs;
7. do not change production/bootstrap/runtime authority semantics;
8. do not introduce hidden Demo startup behavior;
9. rerun exact-head Wave 15 T1 after the test-only commit;
10. continue focused/adversarial FND-07 validation only after exact-head T1 is green.

If this lifecycle-complete fixture still fails, return the exact failure to Main before further mutation.

Current integration target advanced only by the test-only Editor closeout PR #351:
- integration SHA `9895a01a662851505198b965fae2335e55fba6fa`;
- tree `9f1238feafb9a2d19c6539bb99e51455cd8ccd39`.
This target advance does not alter FND-07 product semantics.

FND-07 DEV remains `WAIT_MAIN / NO_SOURCE_MUTATION`.


## 18. CURRENT ORDER — material startup-order defect confirmed — rev 0015

This section supersedes rev 0014 as the only current FND-07 order.

Main independently confirmed the CODEX finding against exact candidate:
- PR #348;
- head `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- tree `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`.

### Confirmed product defect

Startup currently performs Engineering persistence bootstrap before canonical Authority policy hydration.

Concrete code path:
1. `Program.cs` calls `InitializeEngineeringPersistenceAsync()`;
2. persisted Working bootstrap may call `EngineeringWorkspaceCheckoutService.CheckoutAsync(...)`;
3. checkout calls live `EngineeringExchangeService.Apply(...)`;
4. live apply validates `AuthorityPolicyReference` against `IAuthorityPolicyEngineeringRegistryView.AuthoritySnapshot()`;
5. `PostgreSqlAuthorityPolicyStore` begins with an empty in-memory snapshot and only loads persisted policy in `InitializeAsync()`;
6. that initialization is currently reached later through `AuthorityPolicyBootstrapService.EnsureInitializedAsync()`.

Result:
a valid persisted Engineering project may fail restart checkout against an empty in-memory Authority snapshot before persisted Authority is loaded.

Classification:
`FND07_PRODUCT_DEFECT / AUTHORITY_HYDRATION_BEFORE_ENGINEERING_CHECKOUT_REQUIRED`

The earlier test-only classification in rev 0014 is revoked.

`ORDER_ID: FND07-DEV-AUTHORITY-BEFORE-ENGINEERING-RESTART-05`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: a8fcfe8c855a692670e9c74a95f4450caf612b2f`

### Required correction

1. guarantee durable canonical Authority policy state is initialized/hydrated before any persisted Engineering checkout/apply that validates `AuthorityPolicyReference`;
2. do **not** weaken, skip or special-case `AuthorityPolicyReferenceValidator`;
3. preserve Engineering package reference-only Authority semantics;
4. preserve fail-closed behavior for true Authority-reference mismatch;
5. preserve fresh empty-installation bootstrap;
6. preserve deliberately-detached / neutral installation behavior;
7. preserve legacy pre-AUTH-03 migration support;
8. if Authority migration needs Engineering catalog/schema availability, split durable Engineering schema/store initialization from Working checkout/runtime recovery so Authority can hydrate/migrate before package apply;
9. do not restore hidden Demo state;
10. add deterministic regression coverage for:
   - restart with persisted project + matching persisted Authority reference -> server startup/Working checkout succeeds;
   - persisted project + mismatched Authority reference -> fail closed;
   - fresh empty installation -> still valid;
   - detached/neutral installation -> still valid;
11. run focused FND-07/.NET evidence and natural exact-head Wave 15 T1 on the corrected candidate;
12. return exact SHA/tree and evidence.

Required return prefix:
`FND-07 DEV -> MAIN COORDINATOR — PRODUCT CORRECTION HANDOFF`

No merge/freeze. CODEX is paused until a new DEV candidate exists and Main publishes a fresh explicit route.


## 19. CURRENT ORDER — candidate ed552418 rejected for storage-init recursion — rev 0016

This section supersedes rev 0015 as the only current FND-07 DEV instruction.

DEV produced:
- head `ed552418a95d8b7b086eb81ed42ac12d29efaefa`;
- tree `5329588c4e6bfeabd524d190a48ff0cfca95a8d0`;
- delta: `Program.cs`, `EngineeringPersistenceApi.cs`, focused checkout tests.

Main accepts the architectural direction:
- Engineering durable storage/schema initialization separated from Working checkout;
- Authority hydration placed before persisted Engineering checkout;
- immutable Authority reference validation preserved.

### Blocking implementation defect

The new method:

`InitializeEngineeringPersistenceStorageAsync(...)`

currently calls:

`await app.InitializeEngineeringPersistenceStorageAsync(cancellationToken);`

This is direct self-recursion and will not initialize the persistence service. It must be replaced by initialization of the actual persistence service/storage object, preserving idempotent startup behavior.

### Regression gap

The new matching/mismatched Authority checkout tests are useful but do not yet prove the required startup sequencing contract.

`ORDER_ID: FND07-DEV-AUTHORITY-BEFORE-ENGINEERING-RESTART-05`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: ed552418a95d8b7b086eb81ed42ac12d29efaefa`

Required bounded correction:
1. eliminate storage-init self-recursion;
2. initialize actual Engineering persistence/schema exactly once before Authority hydration;
3. preserve binding-store initialization needed by installation lifecycle;
4. keep canonical Authority hydration before persisted Working checkout/apply;
5. keep `AuthorityPolicyReferenceValidator` unchanged and fail-closed;
6. retain matching/mismatch checkout tests;
7. add deterministic regression coverage for the startup-order seam, including:
   - matching persisted Authority + persisted project restart succeeds;
   - mismatch fails closed;
   - fresh empty install remains valid;
   - detached/neutral install remains valid;
8. run focused .NET evidence and natural exact-head Wave 15 T1;
9. return:
   `FND-07 DEV -> MAIN COORDINATOR — PRODUCT CORRECTION HANDOFF`
   with exact SHA/tree/run IDs.

Do not rebase solely because integration advanced to `9895a01a662851505198b965fae2335e55fba6fa`. Main will reconcile target after candidate acceptance.

No merge/freeze/CODEX route yet.


## 20. MAIN DEEP-AUDIT GATE — mandatory before CODEX/merge — rev 0017

This gate is mandatory for the **next FND-07 DEV handoff and every replacement candidate until Main explicitly closes the audit**.

Reason:
the lane accumulated multiple fast correction cycles and several gaps were found only by later validation. Main will therefore perform a full cumulative audit rather than reviewing only the final incremental patch.

`MAIN_DEEP_AUDIT_REQUIRED: YES`

`CODEX_ROUTE_BEFORE_DEEP_AUDIT: FORBIDDEN`

`MERGE_BEFORE_DEEP_AUDIT: FORBIDDEN`

`T1_GREEN_ALONE_IS_NOT_ACCEPTANCE: TRUE`

### Audit scope

Main must review the full cumulative FND-07 implementation, not only the latest commit:

1. **Cumulative diff**
   - inspect the entire PR #348 change set from the authoritative Wave 15 base through the final candidate;
   - identify accidental complexity, duplicated initialization, dead paths, stale compatibility code and changes introduced during iterative fixes.

2. **Startup dependency/order graph**
   - Engineering storage/schema initialization;
   - installation binding initialization;
   - Authority lifecycle recovery;
   - canonical Authority policy hydration/migration;
   - Working checkout;
   - persisted Runtime recovery;
   - detach/attach recovery;
   - licensing/runtime-session/audit initialization interactions.

3. **Durable restart matrix**
   - truly fresh installation;
   - initial Administrator before first Project;
   - first Project persisted;
   - persisted Project + matching Authority;
   - persisted Project + mismatched Authority;
   - Authority deliberately detached;
   - installation neutral;
   - detach/attach transition in progress;
   - legacy/pre-AUTH-03 migration;
   - multiple persisted projects / configured Working/Runtime selection;
   - repeated process restart/idempotent initialization.

4. **Authority boundary**
   - Authority remains canonical/server-side;
   - Engineering packages remain Authority-reference-only;
   - stable role IDs are preserved;
   - no client/project role duplication;
   - true mismatch remains fail-closed;
   - no validation bypass or special-case that converts invalid state into success.

5. **Engineering / Runtime lifecycle**
   - Working != Published != Active;
   - import/save does not silently activate Runtime;
   - publish/activate transitions remain explicit;
   - first Project is genuinely empty except canonical built-ins;
   - no hidden Demo restoration or bootstrap fixture leakage.

6. **Detach / neutral semantics**
   - no project/Authority leakage after detach;
   - restart while neutral remains neutral;
   - transition recovery is deterministic and fail-closed;
   - no accidental auto-attach or inferred authority.

7. **Persistence and failure handling**
   - schema/store initialization is idempotent;
   - no recursion/re-entrant initialization;
   - no initialization ordering race;
   - partial startup failure cannot leave contradictory durable markers;
   - retry/restart behavior is deterministic.

8. **Tests**
   - verify tests actually exercise production startup order, not just isolated services;
   - inspect negative-path assertions, not only happy paths;
   - require deterministic evidence for matching/mismatched Authority, fresh install, neutral/detached and restart;
   - ensure E2E fixture setup is explicitly test-owned and lifecycle-complete;
   - distinguish harness failures from product failures with causal evidence.

9. **Cross-lane compatibility**
   - rebase/target reconciliation is Main-owned;
   - compare against the then-current `wave15/corrections-integration`;
   - verify no conflict with frozen FND-05, integrated Script/Editor, Authority UX or Licensing contracts.

10. **Review output**
   Main must produce an explicit finding set:
   - `BLOCKER`;
   - `MAJOR`;
   - `MINOR`;
   - `NO_FINDING`;
   for each audit domain above.

Only when all BLOCKER/MAJOR findings are closed may Main publish a fresh CODEX route.

### Current DEV order remains unchanged

The current implementation order remains:

`FND07-DEV-AUTHORITY-BEFORE-ENGINEERING-RESTART-05`

DEV should continue only that bounded correction and return the required Product Correction Handoff.

The deep audit is a **Main responsibility after the handoff**. DEV must not broaden its implementation to preemptively satisfy speculative audit findings.


## 21. CURRENT ORDER — cumulative deep audit BLOCKERS — rev 0018

This section supersedes prior current-action sections for FND-07 DEV.

Audited candidate:
- PR #348;
- head `f2f1fed37552133e30567faa0c944d605b048125`;
- tree `74d2186d69e77c8252f0ec8740794e3485072e5f`;
- cumulative lane: 14 commits / 21 files.

`DEEP_AUDIT_RESULT: REJECTED / BLOCKERS_OPEN`

### BLOCKER 1 — FND07-AUD-ATTACH-RESTART-01

`AttachInProgress` recovery currently happens after `InitializeEngineeringPersistenceAsync()`.

A crash after `BeginAttachAsync` but before the first durable project revision can leave an empty catalog + selected journal project key. Working bootstrap may throw before `RecoverInterruptedAttachAsync` can abort the journal.

Required:
- recover installation attach/detach journal before persisted Working checkout can fail;
- test pre-save crash -> Neutral recovery;
- test post-save crash -> Attached recovery followed by Working restore.

### BLOCKER 2 — FND07-AUD-DETACH-AUTHORITY-02

`RecoverInterruptedDetachAsync` completes Application cleanup/binding Neutral but does not detach Authority.

A crash after Application BeginDetach and before Authority detach can therefore restart into:
- Application Neutral;
- Authority still Present.

Required:
- interrupted installation detach must idempotently converge Authority to DeliberatelyDetached before the installation journal completes;
- test restart before/during/after Authority detach.

### MAJOR 3 — FND07-AUD-LICENSE-JOURNAL-03

Detach license intent is not durable. Normal detach completes Application + Authority + binding before license Remove/Replace, so a license failure can be reported as detach failure after detach already completed.

Required:
- make license handling restart-safe and result semantics truthful;
- preserve frozen Licensing authority;
- add Keep/Remove/Replace failure/restart tests.

### MAJOR 4 — FND07-AUD-TEST-GAPS-04

Required coverage is missing for:
- `AttachInProgress`;
- `DetachInProgress`;
- installation detach recovery;
- System Recovery using the installation binding journal;
- complete startup orchestration rather than only manually ordered helper calls.

### MAIN-owned cross-lane blocker — FND07-AUD-CROSSLANE-05

FND-07 overlaps current integration in:
- `ProductLicensedRuntimeCoordinator.cs`;
- `Program.cs`;
- `ScadaRuntimeFacade.cs`.

Final reconciliation must preserve both:
- frozen FND-05 HA industrial-effect authority;
- FND-07 installation Neutral/Detach fence.

DEV must not rebase merely to resolve this. Main owns reconciliation after functional findings close.

### MINOR — FND07-AUD-E2E-FIXTURE-06

The large inline E2E populated baseline should later be extracted to a named test fixture/helper. This does not block the current product correction.

`ORDER_ID: FND07-DEV-DEEP-AUDIT-BLOCKERS-06`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: f2f1fed37552133e30567faa0c944d605b048125`

Required DEV return:

`FND-07 DEV -> MAIN COORDINATOR — DEEP AUDIT CORRECTION HANDOFF`

Include exact SHA/tree, changed files, focused tests, transition/restart matrix results and natural exact-head T1 if available.

No CODEX route, merge or freeze until Main re-audits and closes all BLOCKER/MAJOR findings.


## 22. HANDOFF RECEIVED / MAIN DEEP AUDIT IN PROGRESS — rev 0019

Main received the DEV handoff for exact candidate:

- PR #348;
- head `0189b7f79bf9772c1cf7ece8f4774ba9ea207f6b`;
- tree `bd6971f73348ec7b4f4682d18cabe36f6ef32a47`;
- deep-audit correction delta from `f2f1fed37552133e30567faa0c944d605b048125`: 7 commits / 7 files;
- focused .NET not executed by DEV due environment;
- no natural exact-head T1 exists yet.

Main mirrored the chat-only DEV handoff to PR #348 because the DEV did not persist it there.

`ORDER_ID: FND07-MAIN-DEEP-AUDIT-REVIEW-07`

`ORDER_STATE: MAIN_DEEP_AUDIT / DEV_WAIT / NO_SOURCE_MUTATION`

DEV must make no additional source/test mutation until Main returns the audit disposition.

### Durable handoff rule

From this revision onward, a FND-07 handoff is **not considered delivered** if it exists only in the executor chat.

Required handoff transport:
1. post the complete handoff directly to PR #348; or
2. if PR commenting is unavailable, write the handoff into this dedicated control and explicitly report the tooling blocker.

The Product Owner must not be used as an inter-agent messenger.

Main now owns:
- independent verification of all claimed blocker/major closures;
- cumulative PR audit;
- cross-lane reconciliation against current integration;
- decision whether the candidate may return to CODEX.

No CODEX route, merge or freeze is authorized while this audit is open.


## 23. DEEP AUDIT DISPOSITION — product findings source-closed / Main reconciliation pending — rev 0020

Exact candidate under review:
- PR #348;
- head `0189b7f79bf9772c1cf7ece8f4774ba9ea207f6b`;
- tree `bd6971f73348ec7b4f4682d18cabe36f6ef32a47`;
- DEV correction delta from audited `f2f1fed3...`: 7 commits / 7 files.

Main deep re-audit disposition:
- `FND07-AUD-ATTACH-RESTART-01 -> SOURCE_REVIEW_CLOSED`;
- `FND07-AUD-DETACH-AUTHORITY-02 -> SOURCE_REVIEW_CLOSED`;
- `FND07-AUD-LICENSE-JOURNAL-03 -> ARCHITECTURAL_REVIEW_CLOSED`;
- `FND07-AUD-TEST-GAPS-04 -> COVERAGE_REVIEW_CLOSED / EXECUTION_PENDING`;
- `FND07-AUD-CROSSLANE-05 -> OPEN / MAIN_OWNED`;
- `FND07-AUD-E2E-FIXTURE-06 -> MINOR / NON_BLOCKING`.

Why Licensing finding is closed:
the frozen FND-07 contract explicitly treats machine licensing as an independent FND-03 authority. The candidate resolves deliberate Keep/Remove/Replace before the destructive installation journal, so license rejection/failure cannot falsely report after Application/Authority detach has already completed. Once the installation journal begins, restart recovery owns convergence.

Remaining mandatory gates:
1. reconcile FND-07 with current integration `9895a01a662851505198b965fae2335e55fba6fa`;
2. preserve both frozen FND-05 HA effect authority and FND-07 Neutral/Detach effect fence;
3. execute focused .NET / lifecycle matrix on the reconciled exact candidate;
4. execute exact-head Wave 15 T1;
5. perform final CODEX adversarial validation before merge/freeze.

`ORDER_ID: FND07-MAIN-RECONCILIATION-AND-VALIDATION-08`

`ORDER_STATE: MAIN_OWNED / DEV_WAIT / NO_SOURCE_MUTATION`

FND-07 DEV must now wait. It must not rebase, resolve conflicts, mutate source, or start another correction unless Main returns a new finding.

Future handoffs must be GitHub-durable under rev 0019 protocol.


## 24. MAIN RECONCILIATION COMPLETE / FINAL VALIDATION — rev 0021

Main reconciled the audited FND-07 candidate with current integration.

Exact reconciled candidate:
- head `2bacddbc558bba0dd1a316d98b9895fbb847683c`;
- tree `06ddb27f8f50c495653c1b8e57a2142769ca29e0`;
- first parent: `9895a01a662851505198b965fae2335e55fba6fa`;
- second parent: `0189b7f79bf9772c1cf7ece8f4774ba9ea207f6b`;
- PR #348 is mergeable.

Cross-lane reconciliation preserves:
- FND-05 HA industrial-effect authority;
- FND-07 Neutral/Detach process-effect fence;
- both HA and Installation endpoints/startup services.

Additional Main reconciliation finding:
`FND07-AUD-NEUTRAL-RUNTIME-METADATA-07`
was closed in the merge candidate. Neutral now hides Operational Events, Client Memory, Drivers and Server Memory identity, with a focused regression test.

Natural exact-head Wave 15 T1:
- run `36093062819`;
- exact head `2bacddbc558bba0dd1a316d98b9895fbb847683c`;
- state: ACTIVE / queued-or-running.

`ORDER_ID: FND07-CODEX-RECONCILED-ADVERSARIAL-09`

`ORDER_STATE: CODEX_FINAL_VALIDATION / ACTIVE_SHARED_ROUTE / DEV_WAIT`

FND-07 DEV remains WAIT and must not mutate source.

CODEX final validation must cover:
1. exact reconciled SHA/tree;
2. both HA and installation process-effect fences;
3. fresh install / first Administrator / first Project / no Demo;
4. startup order: storage -> Authority -> installation journal -> Working;
5. AttachInProgress pre-save/post-save restart;
6. DetachInProgress Authority convergence;
7. Keep/Remove/Replace FND-03 semantics;
8. Neutral metadata/process-effect no-leak behavior;
9. System Recovery attach journal;
10. Working != Published != Active;
11. exact-head T1 result;
12. no regression to frozen FND-05 HA boundary.

No merge/freeze unless Main receives green exact-head T1 and accepts the final CODEX handoff.
