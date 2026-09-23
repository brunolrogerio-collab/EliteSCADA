# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para MAIN COORDINATOR <-> CODEX / DEV / AUD durante a Wave 15.**
>
> Este arquivo é a memória operacional persistente e o **canal primário de ordens** do Main Coordinator. Issues/PR comments podem espelhar decisões/evidências, mas não substituem a `CURRENT ORDER` deste arquivo.
>
> **PROTOCOLO `SIGA`:** CODEX, DEV ou AUD deve reler este arquivo no GitHub live antes de agir e executar somente a ordem mais recente da sua lane.
>
> **GitHub live é a autoridade final.** Se este documento divergir do repositório/PRs/Actions live, o Main reconstrói o estado e corrige este arquivo antes de emitir nova ordem.

**Status date:** 2026-09-22 BRT  
**Wave:** 15 — complete product delivery  
**Integration branch:** `wave15/corrections-integration`  
**Main Coordinator:** único emissor das ordens abaixo

---

## 0. PROTOCOLO PERMANENTE DO MAIN COORDINATOR

### 0.1 Bootstrap / sucessão

Qualquer novo Main Coordinator deve, antes de coordenar:

1. ler integralmente este arquivo no GitHub live;
2. ler/revalidar, conforme relevantes, `PROJECT GOAL.md`, `LAST CHANGE.md`, `README.md`, `docs/README.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`, `docs/ROADMAP.md`, ADRs e handoffs ativos;
3. revalidar integration HEAD/tree, PRs, work branches, issues coordenadoras, Actions, blockers e dependências;
4. ler pelo menos #297, #301, #305 e a evidência do candidate ativo;
5. reconstruir Foundation state, missão ativa, lane owner, exact product base, candidate head/tree, CI, blockers e próximo gate;
6. corrigir qualquer estado stale deste arquivo antes de emitir nova ordem.

GitHub live prevalece sobre memória de chat, resumo, comentário isolado ou SHA histórico.

### 0.2 PRODUCT CHECKPOINT vs COORDINATION HEAD

Sempre separar:

- **PRODUCT CHECKPOINT SHA/TREE** — último código/infra de produto integrado e validado;
- **INTEGRATED CANDIDATE PENDING VERIFICATION** — código integrado aguardando gate pós-merge no exact integrated SHA;
- **COORDINATION HEAD** — HEAD live que pode avançar apenas por documentação de coordenação.

Commits apenas documentais não criam novo product base. Uma `CURRENT ORDER` fixa o **product base**. Ao executar, o agente revalida o integration HEAD live e confirma que qualquer delta desde o product base é somente coordenação/documentação. Delta de produto/infra não coordenado => `STOP / BLOCKED-BASE-DIVERGENCE`.

### 0.3 Autoridade permanente do Main — comunicação e CI

Product Owner autorizou permanentemente o Main Coordinator a:

- atualizar este handoff para publicar/alterar ordens;
- enviar/espelhar ordens a CODEX/DEVs/AUDs em issues/PRs;
- confirmar uma ordem somente depois de `write success + live readback`;
- inspecionar, disparar e rerodar CI/GitHub Actions de validação pré/pós-merge;
- validar exact candidate, merge e integration SHA;
- rerodar job/failed jobs quando houver hipótese concreta ou gate obrigatório.

Guardas: diagnosticar vermelho antes de rerun; preservar SHA; menor rerun suficiente; registrar run/attempt/job; sem loop cego; sem alterar código/teste/workflow para obter verde; sem commit artificial; sem retarget/rebase artificial; sem force push/destructive rebase.

**Autoridade de CI não equivale a autoridade de merge.**

### 0.4 Merge / `main`

- Produto entra na integração por PR revisado e ordem binding.
- `main` permanece protegido: `SIGA`, CI verde, aprovação, freeze ou conclusão de missão não autorizam merge em `main`.
- Merge final em `main` exige autorização explícita do Product Owner quando a governança assim exigir.

### 0.5 Ordem persistida / evidência

O Main só afirma `ordem dada` depois de atualizar a `CURRENT ORDER`, obter write success, fazer readback live e confirmar o conteúdo.

Handoffs/evidências de agentes em issues/PRs devem ser tratados como **append-only evidence**: Main não substitui o conteúdo original para registrar review; publica review/correção em novo comentário e altera a ordem canônica neste arquivo.

### 0.6 `SIGA`

Para CODEX/DEV/AUD: reler este arquivo live e executar somente sua `CURRENT ORDER`; `WAIT` = nenhuma mutação; `STOP` = devolver evidência e parar.

Para Main: `SIGA` = continuar autonomamente o fluxo seguro autorizado, revalidando GitHub live antes de decisão material.

### 0.7 Auditoria / economia de execução

Auditoria arquitetural/contratual e work package pertencem ao Main. CODEX é prioritariamente implementador/corretor bounded; DEV implementa sua lane; AUD revisa/testa candidate indicado.

Quando CODEX tiver limite de uso, Main pode usar DEV normal em `ARCH_ONLY` para source mapping/arquitetura/test design sem mutar produto. Main revisa/congela o desenho e pode liberar DEV normal para implementação bounded. CODEX fica reservado para blocker técnico/correção crítica.

`ARCH_ONLY` nunca autoriza produto/testes/branch/PR/merge/CI. A única escrita GitHub permitida é o handoff arquitetural no ledger explicitamente autorizado.

### 0.8 Lições permanentes

Não repetir: ordem só no chat/issue; afirmar ordem sem readback; confundir coordination HEAD com product checkpoint; fixar coordination HEAD como product base; delegar auditoria aberta ao Codex; gastar CODEX em arquitetura que Main+DEV normal podem fechar; ambiguidade de ledger-write em `ARCH_ONLY`; rerun cego; tratar `PR_READY`, CI verde, `INTEGRATED`, `VERIFIED`, `FROZEN` como equivalentes; inferir PASS de acceptance PENDING; sobrescrever evidência histórica de agente em vez de acrescentar review separado.

State machine:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

---

## 1. ESTADO LIVE DA FOUNDATION

### PRODUCT CHECKPOINT atual

FND-03 License Lifecycle/Fencing Phase A e Phase B estão integradas e verificadas.

- product checkpoint / PR #333 merge SHA: `4647dd741551c97306217ac9893d3378b070f43b`
- tree: `d7eb7d3f57269e71ed5984c82e701a059be56bfb`
- reviewed Phase B candidate: `29c5911318c06f6d07578dd4b97b908f66e3c773`
- candidate tree: `8e890abab8de005ab4f8e09899e9a208ef3f8073`
- exact PR CI #1554 / run `35663835807`: Backend/Web/Chromium **SUCCESS**
- exact post-merge CI #1555 / run `35665138086` on `4647dd741...`:
  - Web `106549082646` — **SUCCESS**
  - Backend `106549082897` — **SUCCESS**
  - Chromium `106549531824` — **SUCCESS**

Antes desta ordem, o coordination HEAD era `8debd70b7c0e0e432b7deca29f5b31070a73e837`; o compare desde o product checkpoint mostrava quatro commits à frente e alterações somente em `LAST CHANGE.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md` e `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`. O commit documental desta própria ordem pode avançar novamente o coordination HEAD sem criar novo product base.

### Foundation

- FND-01 — **VERIFIED/FROZEN**
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **VERIFIED/FROZEN**
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **PHASE A+B BASELINE VERIFIED / BOUNDED PHASE A DEFECT AMENDMENT AUTHORIZED / PHASE C ACTIVE / NOT INTEGRATED**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**ORDER_ID: FND03-PHASE-C-IDEMPOTENT-REMOVE-CLOSE-04**  
**CODEX_MODE: BOUNDED_PRODUCT_CORRECTION**  
**Mission:** close the already-Demo remove loophole + strengthen acceptance #7 boundary proof

### Exact reviewed state

Main independently reviewed the exact Phase C candidate and CI:

- exact product base: `4647dd741551c97306217ac9893d3378b070f43b`
- work branch: `work/w15-fnd-03-license-lifecycle-orchestrator-v1`
- reviewed head before this correction: `40f0001f969930f227861ef2f11d79e3bd9f2931`
- reviewed tree: `b2f4c83690731723d225c47d371740de2f6c265c`
- PR #334: OPEN / mergeable / target `wave15/corrections-integration`
- exact natural CI #1557 / run `35810903479` on `40f0001f...`:
  - Web `107022021198` — SUCCESS
  - Backend `107022021316` — SUCCESS
  - Chromium `107022392473` — SUCCESS
- integration HEAD before this order: `de2ac0f1a179d2a20f6acc9351643eff6694d02f`; compare from product base is documentation-only.

The candidate is **not approved for integration** because Main found one deterministic lifecycle defect and one proof gap after the green CI.

### Defect A — repeated remove while already Demo resets authority/Demo window

Binding architecture #301 comment `5722165708` explicitly states:

> If CurrentVerification is already Demo because the machine license file is absent, remove may be treated as idempotent no-op with no revision bump.

Current candidate `ProductLicenseLifecycleCoordinator.RemoveAsync` unconditionally calls `ChangeAsync("remove", ...)`. Therefore, when the machine is already Demo/no-license, repeated DELETE can:
- Begin a new authority transition;
- increment `AuthorityRevision`;
- write a fresh `DemoStartedAtUtc`;
- re-evaluate Runtime and fence leases even though license authority did not change.

That creates an avoidable Demo-window reset path and contradicts the binding architecture. Green CI #1557 does not cover this case.

### Authorized product correction

Only `src/Scada.Api/Licensing/ProductLicenseLifecycleCoordinator.cs` may change in production.

Required semantics:

1. `RemoveAsync` must inspect canonical `CurrentVerification`.
2. If current state is **Demo because the license is absent** and Runtime authority state is coherent/non-pending:
   - return a successful idempotent result;
   - stable reason code such as `already-demo`;
   - no `BeginAuthorityTransitionAsync`;
   - no license-file mutation;
   - no authority revision bump;
   - no Runtime re-evaluation;
   - no remote lease fence;
   - preserve existing `AuthorityChangedAtUtc` / `DemoStartedAtUtc` exactly.
3. If current state is Demo but `TransitionPending == true`, remain fail-closed:
   - do not return idempotent success;
   - do not clear pending;
   - throw/return the existing deterministic transition-incomplete path so startup/lifecycle reconciliation remains authoritative.
4. If current state is **Invalid**, remove remains a real authority change: delete invalid installed file, advance revision, enter Demo, re-evaluate/fence.
5. Valid -> remove semantics remain unchanged.

Do not alter the frozen Phase A/B contracts for this correction.

### Mandatory deterministic tests

Update `tests/Scada.Drivers.Tests/ProductLicenseLifecycleCoordinatorTests.cs`:

- perform a real Valid -> Demo removal at T0;
- call remove again while already Demo at T1 > T0;
- assert second operation succeeds idempotently;
- assert `AuthorityRevision` unchanged;
- assert `DemoStartedAtUtc` remains exactly T0, not T1;
- assert no extra Runtime re-evaluation and no extra fencing/lease epoch transition.

#### Independent Main test-audit guard

The existing fixture's `FixedTimeProvider` always returns the same `Now`; reusing it for both removals would make a broken implementation capable of passing the Demo-anchor assertion accidentally. Therefore the ORDER-04 regression is accepted only if it proves real clock separation.

Required shape:

1. Use a mutable/steppable `TimeProvider` (or two otherwise provably distinct timestamps) with exact `T1 > T0`; do **not** use the existing constant `FixedTimeProvider` unchanged for this regression.
2. Start from `LicenseState.Valid`; execute the first remove at T0 and capture the complete post-first-remove authority state before advancing time.
3. Assert after the first remove:
   - `AuthorityRevision == R+1`;
   - `DemoStartedAtUtc == T0`;
   - `AuthorityChangedAtUtc == T0`;
   - `TransitionPending == false`.
4. Admit/capture a post-first-remove lease at revision `R+1` or otherwise instrument the store so the second call can prove no fence/epoch mutation.
5. Advance the clock to a distinct T1, preferably by at least one minute, and execute the second remove.
6. Assert after the second remove, by direct equality against the captured state:
   - `AuthorityRevision == stateAfterFirst.AuthorityRevision`;
   - `DemoStartedAtUtc == stateAfterFirst.DemoStartedAtUtc == T0`;
   - `AuthorityChangedAtUtc == stateAfterFirst.AuthorityChangedAtUtc == T0`;
   - `TransitionPending == false`;
   - the second result reports the same previous/current revision and `ReasonCode == "already-demo"` (or the exact stable equivalent chosen by implementation);
   - `FencedLeaseCount == 0`;
   - no additional Runtime reevaluation call occurred;
   - no additional `RemoveLicense` file-mutation call occurred;
   - any post-first-remove lease remains valid under the unchanged revision.
7. The test must fail against the currently reviewed head `40f0001f...` semantics; a test that would also pass the old unconditional `ChangeAsync("remove", ...)` implementation is not sufficient evidence.

Also prove:
- already-Demo + pending transition does not bypass fail-closed pending state and preserves the exact pending transition metadata;
- Invalid -> remove still performs a real transition to Demo with revision advance and fresh Demo anchor.

### Proof gap B — acceptance #7 guard misses method-group references

Current test `ProductLicenseMutationBoundaryTests` searches only patterns equivalent to `.InstallLicense(...)` / `.RemoveLicense(...)`.

The canonical lifecycle uses `licensing.RemoveLicense` as an `Action` method-group without `()`, so the current guard does **not** actually detect the one authorized RemoveLicense reference it claims to protect.

Strengthen only `tests/Scada.Drivers.Tests/ProductLicenseMutationBoundaryTests.cs` so it detects member references to both:
- `.InstallLicense`
- `.RemoveLicense`

whether invoked directly or passed as method groups. The exact allowlist must contain only the canonical lifecycle coordinator references. A new production reference anywhere else must fail the test.

### Allowed files

Exactly:
- `src/Scada.Api/Licensing/ProductLicenseLifecycleCoordinator.cs`
- `tests/Scada.Drivers.Tests/ProductLicenseLifecycleCoordinatorTests.cs`
- `tests/Scada.Drivers.Tests/ProductLicenseMutationBoundaryTests.cs`

No web change is needed; C04 stabilization at `40f0001f...` is accepted and remains untouched.
No workflow/config/package-lock/docs change.
No merge.

### Required validation

Before handoff:
- focused lifecycle tests including the three mandatory remove cases;
- strengthened acceptance #7 boundary test;
- relevant Phase C focused .NET suite;
- `git diff --check`;
- push to the same branch / PR #334;
- allow a **new natural CI on the new exact head**;
- do not rerun #1557 unchanged.

Return exactly:

`CODEX -> MAIN COORDINATOR — FND-03 PHASE C IDEMPOTENT-REMOVE CLOSE HANDOFF`

with:
- old head `40f0001f...` -> new exact head/tree;
- exact 3-file-or-smaller diff;
- deterministic proof of no Demo-anchor/revision reset on repeated remove;
- pending-state fail-closed proof;
- Invalid -> Demo proof;
- strengthened #7 result;
- focused tests;
- PR #334 state/base/head;
- new natural CI run/jobs/status;
- explicit non-actions.

Until Main verifies the new candidate:
- Phase C remains ACTIVE / NOT VERIFIED / NOT INTEGRATED;
- FND-03 global remains ACTIVE / NOT FROZEN;
- FND-03 DEV WAIT;
- FND-04 DEV/AUD WAIT;
- FC0-A BLOCKED.
---

## 2A. MAIN COORDINATOR -> FND-03 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**DEV_MODE: WAIT_CODEX_PHASE_C**  
**Mission:** FND-03 License Lifecycle/Fencing — Phase C assigned exclusively to CODEX

Phase A and Phase B remain **VERIFIED/FROZEN** at product checkpoint:

- PR #333 merge SHA: `4647dd741551c97306217ac9893d3378b070f43b`
- merge tree: `d7eb7d3f57269e71ed5984c82e701a059be56bfb`
- exact post-merge CI #1555 / run `35665138086` — Web/Backend/Chromium SUCCESS

The active Phase C work package is owned by CODEX under `FND03-PHASE-C-LIFECYCLE-ORCH-02`.

While this order is WAIT:

- make no code/test/branch/PR changes;
- do not implement or review Phase C unless Main later assigns a bounded correction/review;
- do not rerun CI;
- do not merge anything;
- do not start FND-04 / FC0-A;
- on `SIGA`, reread this file, confirm `WAIT_CODEX_PHASE_C`, and stop.

FND-04 DEV/AUD remain WAIT.
FC0-A remains BLOCKED.
---

## 3. FND-03 ACCEPTANCE BINDING

Implementation, when later authorized, must prove `PASS | FAIL | PENDING` for:

1. valid ESLIC2 candidate verify succeeds without installed-state mutation;
2. tampered/wrong-key/wrong-machine/expired/malformed verify fails without mutation;
3. valid install from Demo becomes authoritative;
4. valid A -> B replacement commits B only after B verifies;
5. invalid replacement preserves A and does not disrupt Runtime/leases;
6. deliberate remove enters Demo, not Invalid;
7. project/package/Authority operations never silently remove machine license;
8. successful install/replace/remove fences all pre-change remote leases; old IDs fail;
9. concurrent admission vs downgrade/remove cannot leave usable stale post-return lease/window;
10. new admissions use only new ESLIC2/Demo totals;
11. active Runtime exceeding new tag entitlement stops/fences deterministically;
12. allowed active Runtime continues with status reflecting new authority;
13. transition to Demo starts fresh bounded allowance from authority-change time;
14. mutation denies EngineeringView-only principal lacking EngineeringModify;
15. audit safe metadata, no raw license/signing material;
16. ESLIC2 + Shared Seat Accounting regressions remain green;
17. `.escadapkg` remains free of license/key/session state;
18. exact-head CI green.

Scope exclusions: License Generator UI; full #304 detach/switch UX; Authority A->B; Historian switching; EliteGO UI; cross-machine HA election/fencing; FND-04; main merge; broad ESLIC1 cleanup.

---

## 4. MAIN COORDINATOR -> FND-04 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
Reserved branch: `work/w15-fnd-04-script-tag-reference-resolution`  
Target: `wave15/corrections-integration`.

No implementation until Main activates with exact product base/scope/acceptance.

---

## 5. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Default mode:** `READ_ONLY_REVIEW`

No speculative audit/test write. When activated, Main supplies exact DEV candidate and `AUD_MODE`.

---

## 6. FND-04 BINDING CONTRACT — READY, NOT ACTIVE

When activated: human/canonical Python-visible TAG reference (normally full path); `TagId`/Guid stable authority; one shared `tag_read`/`tag_write` resolver; persisted/versionable visible-reference <-> expected-TagId binding; rename/move/path-reuse without silent retarget; missing/ambiguous/stale/identityDrift fail closed; legacy GUID/TagId explicit/tested; Authority preserved; no second Tag registry/resolver/auth pipeline.

---

## 7. FND-03 REMAINING AFTER LIFECYCLE

After lifecycle/fencing integrated+verified, Main re-evaluates #301 for final closeout: observability/rejection reasons/counters, admission heartbeat/reuse residuals, sufficient #304 integration contract, remaining negative/concurrency proof. FND-03 global freezes only when all #301 criteria are proven on exact integrated checkpoint.

---

## 8. PERMANENT GUARDS

- GitHub live authority.
- no direct feature write to integration; product via reviewed PR.
- `main` protected.
- red CI diagnosed before rerun.
- exact-SHA evidence.
- required unexecuted test = `PENDING`.
- no force push/destructive rebase/evidence deletion.
- Runtime Session Class/licensing is restrictive ceiling; Authority is capability authority.
- `CommandExecute` != `ProcessValueWrite`.
- Web Runtime + EliteGO share lease/quota authority.
- no secret/key/session/topology state in `.escadapkg`.
- stable IDs outrank mutable names/paths.
- no downstream redesign of frozen Foundation contracts.
- a lane never chooses its next mission.

---

## 9. LEDGERS / RETORNO

Primary channel: this file.

Ledgers: #297 Wave 15; #301 FND-03/licensing; #305 dependency/checkpoints; #304 Installation consumer contract; PR conversation for local evidence.

Agents execute current order, return evidence and stop on `STOP`/`WAIT`. Main promotes states and writes next order.

`Hora: HH:MM` in `America/Sao_Paulo`.
