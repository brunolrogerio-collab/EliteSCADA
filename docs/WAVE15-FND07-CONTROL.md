# Wave 15 — FND-07 Main Coordinator Control Plane

> GitHub live is the sole authority. This file is PREPARED ONLY and does not authorize product mutation.

`CONTROL_BRANCH: coord/w15-fnd07-control`

`MAIN_ORDER_REV: 0009`

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
