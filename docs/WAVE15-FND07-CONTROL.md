# Wave 15 — FND-07 Main Coordinator Control Plane

> GitHub live is the sole authority. This file is PREPARED ONLY and does not authorize product mutation.

`CONTROL_BRANCH: coord/w15-fnd07-control`

`MAIN_ORDER_REV: 0004`

`STATE: ACTIVE_CODING / DEV_IMPLEMENTATION_AUTHORIZED`

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
