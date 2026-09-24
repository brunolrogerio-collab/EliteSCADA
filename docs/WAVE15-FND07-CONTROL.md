# Wave 15 — FND-07 Main Coordinator Control Plane

> GitHub live is the sole authority. This file is PREPARED ONLY and does not authorize product mutation.

`CONTROL_BRANCH: coord/w15-fnd07-control`

`MAIN_ORDER_REV: 0002`

`STATE: PREPARED / NOT ACTIVE / HOLD_ON_P1-01_AND_P1-06_FC0A_GATE`

`PREPARED_ORDER_ID: FND07-CODEX-DETACH-NEUTRAL-V1`

`LATEST_AUDITED_PRODUCT_CHECKPOINT: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`ACTIVATION_BASE_RULE: revalidate latest wave15/corrections-integration product checkpoint before activation`

## 0A. Current hold reason

FND-06 is VERIFIED/FROZEN and the post-FND06 audit is complete with `CHANGES_REQUIRED`.

FND-07 remains **PREPARED / NOT ACTIVE** because FC0-A is held on:
- W15-P1-01 Server Script bounded recovery;
- W15-P1-06 truthful Engineering no-model/loading/error fallback.

The second and third Main audit passes identified no breaking FND-07 contract redefinition. FND-07 remains compositional/compatible, but before activation its acceptance matrix must explicitly cover **Engineering Lock × detach/switch/neutral-bootstrap** so the existing replacement/recovery exemption and backend Authority rules are preserved without inventing a second credential or leaking protected Engineering content.

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

## 4. Prepared first implementation slice

`ORDER_ID: FND07-CODEX-DETACH-NEUTRAL-V1`

`ORDER_STATE: WAIT / NOT AUTHORIZED`

`EXECUTOR_MODE: BOUNDED_FOUNDATION_IMPLEMENTATION`

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
20. natural Wave 15 T1 + focused System Recovery/Authority/Licensing tests green.

## 6. Forbidden

No anonymous populated-install restore, direct DB hacks, silent Historian deletion, license-in-package, credentials-in-package, stale process effects, permissive invalid-license fallback, lifecycle/Authority weakening, or self-merge/freeze.

## 7. Activation dependency

FND-01/FND-02/FND-03 prerequisites are frozen, but this order remains PREPARED until FND-06 is VERIFIED/FROZEN and the mandatory post-FND06 FC0-A Foundation Closure Audit passes.

Intended sequencing:
- FND-06 -> FC0-A release;
- FND-05/FND-07 can then progress while FC0-A downstream feature lanes execute, subject to Main capacity and exact-base revalidation.
