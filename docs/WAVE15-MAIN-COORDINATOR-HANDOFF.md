# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para MAIN COORDINATOR <-> CODEX / DEV / AUD durante a Wave 15.**
>
> Este arquivo é a memória operacional persistente e o **canal primário de ordens** do Main Coordinator. Issues/PR comments podem espelhar decisões/evidências, mas não substituem a `CURRENT ORDER` deste arquivo.
>
> **PROTOCOLO `SIGA`:** CODEX, DEV ou AUD deve reler este arquivo no GitHub live antes de agir e executar somente a ordem mais recente da sua lane.
>
> **GitHub live é a autoridade final.** Se este documento divergir do repositório/PRs/Actions live, o Main reconstrói o estado e corrige este arquivo antes de emitir nova ordem.

**Status date:** 2026-09-17 BRT  
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

Shared Runtime Seat Accounting está integrado e verificado.

- product checkpoint / merge SHA: `a7067ac99f9f88fcd17f740b915d8c4f57c556fc`
- tree: `eed22a377fea2778d3e78143d706e4de0ef9ce38`
- parents:
  - `11a0f32736def27fbbafb8b718abc428bba62056`
  - `6789a989c85e7210c167945665f4ab6c8cef53a0`
- PR #331: MERGED
- post-merge EliteSCADA CI #1546 / run `35269829080` on exact `a7067ac9...`:
  - Backend `105366045111` — **SUCCESS**
  - Web `105366045298` — **SUCCESS**
  - Chromium `105366583742` — **SUCCESS**

O integration HEAD pode estar à frente por commits de coordenação; isso não altera o product checkpoint.

### Foundation

- FND-01 — **VERIFIED/FROZEN**
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **VERIFIED/FROZEN**
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **PHASE A VERIFIED/FROZEN / PHASE B ACTIVE / NOT INTEGRATED**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Mission:** FND-03 Lifecycle/Fencing — CODEX reserve

CODEX makes **no mutation** now. On `SIGA`, reler este arquivo, confirmar `WAIT`, não implementar/commit/PR/CI e aguardar ordem bounded futura.

Exact product contract remains:

- product base `a7067ac99f9f88fcd17f740b915d8c4f57c556fc`
- tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`
- reserved future work branch `work/w15-fnd-03-license-lifecycle-fencing-v1`
- target `wave15/corrections-integration`

Do not start FND-04 or release FC0-A.

---

## 2A. MAIN COORDINATOR -> FND-03 DEV — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**DEV_MODE: IMPLEMENT_PHASE_B**  
**Mission:** FND-03 License Lifecycle/Fencing — Active Runtime Re-evaluation + Durable Demo Recovery v1

### Phase A closure

Phase A is **VERIFIED/FROZEN**.

Exact integrated product checkpoint:

- PR #332 merge SHA: `20b934f23d8798ffb65cca203b62f8b5c3d8f111`
- tree: `4e227fdde1d8475c23852e142c51946c7a2e1859`
- parents:
  - `a3555b3422e0f86ee89d21588e550a33931e71b2`
  - `a07568ea072bf6a095f800dc5443b76b6a6d3a94`
- exact post-merge EliteSCADA CI #1551 / run `35341475101`:
  - Backend `105588126265` — SUCCESS
  - Web `105588126462` — SUCCESS
  - Chromium `105588538108` — SUCCESS

Phase A foundation is now frozen:
- canonical non-mutating candidate verification;
- Runtime `AuthorityRevision`;
- durable transition singleton / migration 023;
- expected-revision capacity binding;
- pending/stale fail-closed lease operations;
- bulk lease fencing with exactly-once Generation mutation.

Do not redesign these contracts in Phase B.

### Exact Phase B authority

- product base: `20b934f23d8798ffb65cca203b62f8b5c3d8f111`
- product tree: `4e227fdde1d8475c23852e142c51946c7a2e1859`
- work branch: `work/w15-fnd-03-runtime-authority-reevaluation-v1`
- target: `wave15/corrections-integration`
- architecture evidence: #301 comment `5722165708`, section 7

Live compare after the product checkpoint shows only coordination/documentation changes in:
- `LAST CHANGE.md`
- `docs/CURRENT-COORDINATOR-HANDOFF.md`
- `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`

Create the Phase B branch from the exact product checkpoint, not from the moving documentation HEAD. Any intervening product/infra delta => `BLOCKED-BASE-DIVERGENCE`.

### Binding architecture

Use the **existing** authority state and canonical license truth only.

- canonical license truth remains `IProductLicenseService` / `IProductRunEntitlementProvider`;
- authority/recovery synchronization remains `IRuntimeSessionLeaseStore.GetAuthorityStateAsync` (or the already-wired registry wrapper over that same store);
- do not instantiate a second lease store, second authority registry, second license parser, second clock or second entitlement evaluator;
- `AuthorityRevision` remains only the global fencing epoch;
- per-lease `Generation` remains CAS/version state;
- Demo anchor is synchronization/recovery metadata only and never enters `.escadapkg`, Application, Authority or project persistence.

### B1 — Active Runtime authority re-evaluation

Primary file:

`src/Scada.Api/Licensing/ProductLicensedRuntimeCoordinator.cs`

Add the frozen operation:

`Task<ProductRuntimeAuthorityReevaluationResult> ReevaluateForAuthorityChangeAsync(DateTimeOffset authorityChangedAtUtc, CancellationToken cancellationToken = default)`

Requirements:

1. serialize with the existing `_activationGate`; it must not race `ActivateCoreAsync` or `ExpireDemoAsync`;
2. derive the active tag count from the current active Runtime and evaluate through the existing canonical entitlement provider;
3. if no Runtime is active:
   - return a deterministic no-active-runtime result;
   - do not construct/dispose a replacement Runtime;
4. if new authority still allows the active Runtime:
   - keep the current inner Runtime instance;
   - update `_activeDecision` and status metadata;
   - preserve the active Runtime identity/revision;
5. if new authority denies the active Runtime:
   - cancel Demo expiry;
   - increment activation generation;
   - swap to a fresh coordinator via the existing `_innerFactory`;
   - dispose the previously active inner;
   - retain a deterministic diagnostic;
   - do not report success before the old Runtime is stopped;
6. if authority becomes Demo and the active tag count is allowed:
   - semantic Demo start is exactly `authorityChangedAtUtc`;
   - if the Demo allowance is already exhausted, stop immediately;
   - otherwise schedule only the remaining duration.

The bounded result record must expose only safe operational state needed by Phase C/tests (for example: whether a Runtime existed, retained/stopped outcome, resulting license state/decision, diagnostic). No license payload/secrets.

### B2 — Durable Demo timer semantics

Still in:

`src/Scada.Api/Licensing/ProductLicensedRuntimeCoordinator.cs`

Correct the timer model so restart/re-evaluation never resets a durable Demo window.

Keep:
- `_demoStartedAtUtc` as semantic start;
- `_demoDuration` as full allowance;
- `_demoStartedTimestamp` as current-process monotonic timestamp.

Add an elapsed-before-current-process component (name may follow code style).

For a seeded Demo anchor:

- `initialElapsed = max(0, TimeProvider.GetUtcNow() - demoStartedAtUtc)`;
- remaining = full duration - initial elapsed - monotonic elapsed in this process;
- clamp remaining at zero;
- `DemoExpiresAtUtc = demoStartedAtUtc + fullDuration`;
- schedule only the remaining duration;
- if remaining <= 0, expire/stop immediately.

For a normal explicit Demo activation with **no durable Demo anchor**, preserve the existing "start now" behavior.

For Demo activation when the existing authority state has a durable `DemoStartedAtUtc`, seed from that anchor instead of minting a fresh window.

### B3 — Persisted Runtime recovery fail-closed

Primary file:

`src/Scada.Api/Persistence/PersistedRuntimeRecoveryService.cs`

Read:
- canonical `IProductLicenseService.CurrentVerification`;
- the same existing `RuntimeAuthorityState` from `IRuntimeSessionLeaseStore`.

Before auto-recovering a persisted Active revision:

1. if `TransitionPending == true`:
   - do not recover Runtime;
   - return a deterministic recovery-denied result/issue;
2. if canonical license is Invalid:
   - remain fail closed; do not bypass the existing product entitlement boundary;
3. if canonical authority is Demo and `DemoStartedAtUtc` is present:
   - allow normal recovery path;
   - `ProductLicensedRuntimeCoordinator` must seed timing from that durable timestamp;
4. if canonical authority is Demo and a persisted Active Runtime is being recovered but no durable Demo anchor exists:
   - **do not auto-recover it**;
   - return a deterministic recovery-denied issue rather than granting a fresh full Demo window;
5. if canonical authority is Valid:
   - normal persisted recovery continues.

Do not create a parallel Script Runtime recovery path. Existing `ServerScriptRuntimeManager` recovery must continue through the normal product runtime coordinator.

A denied recovery must not alter Engineering Lock/protection state.

### B4 — DI wiring

Allowed supporting changes:

- `src/Scada.Api/Licensing/ProductLicensedRuntimeCoordinator.cs` registration helper;
- `src/Scada.Api/Persistence/PersistedRuntimeRecoveryService.cs` constructor dependencies;
- `src/Scada.Api/Program.cs` **only if actually required by DI wiring**.

Use the existing singleton `IRuntimeSessionLeaseStore`; do not instantiate another store.

Registration order is not a reason to create duplicate state: DI resolves the completed service collection at runtime.

### Required Phase B tests

Primary test files:

- `tests/Scada.Drivers.Tests/ProductLicensedRuntimeCoordinatorTests.cs`
- `tests/Scada.Drivers.Tests/PersistedRuntimeRecoveryServiceTests.cs`

Add deterministic coverage for at least:

1. active Valid -> Valid re-evaluation retains the exact same Runtime instance/identity;
2. active authority downgrade that denies current tag count stops Runtime and disposes old inner;
3. active Valid -> Demo allowed transition seeds Demo start from `authorityChangedAtUtc`, not re-evaluation time;
4. delayed re-evaluation schedules only remaining Demo duration;
5. already-expired Demo anchor stops immediately;
6. status `DemoStartedAtUtc`, `DemoExpiresAtUtc`, `DemoRemaining` reflect semantic anchor + monotonic elapsed;
7. explicit Demo activation with no durable anchor still starts a new timer at activation time;
8. activation/recovery under a durable Demo anchor does not reset the semantic start;
9. persisted recovery while authority transition is pending is denied and does not activate Runtime;
10. persisted Demo recovery with durable anchor succeeds through the normal recovery path and preserves remaining time;
11. persisted Demo recovery without durable anchor is denied;
12. persisted Valid recovery remains unchanged;
13. denied recovery does not replace Engineering Lock/protection state;
14. re-evaluation serialized against expiry/activation cannot resurrect an old Runtime generation;
15. no new authority/license/session state appears in Engineering package/persistence models.

Use `FakeTimeProvider` / deterministic time facilities already present in the test project where available. Do not use wall-clock sleeps for the semantic timer acceptance.

A written but unexecuted test is `PENDING`, never PASS.

### Phase B stop boundary

Do **not** implement yet:

- `ProductLicenseLifecycleCoordinator`;
- install/replace/remove orchestration;
- license endpoint mutation cutover;
- EngineeringModify/audit mutation routes;
- restart reconciliation of incomplete lifecycle transition windows W1-W6 beyond the recovery guards above;
- FND-04;
- FC0-A.

Phase C will consume Phase A + Phase B to build the lifecycle mutation orchestrator.

### Commit / return protocol

- prefer bounded logical commits for B1/B2/B3 (+ tests);
- push only to `work/w15-fnd-03-runtime-authority-reevaluation-v1`;
- do not open a PR yet unless Main later authorizes it;
- run focused local tests available in the execution environment;
- do not invent execution evidence: unavailable => `PENDING`;
- do not manually run/rerun GitHub Actions; Main owns CI;
- no merge, no integration mutation, no `main`.

Publish exactly one new top-level #301 handoff beginning:

`FND-03 DEV -> MAIN COORDINATOR — LICENSE LIFECYCLE PHASE B HANDOFF`

Include:
- exact base/head/tree;
- commits;
- exact changed files;
- B1/B2/B3 implementation summary;
- test matrix PASS/FAIL/PENDING;
- any deviation from frozen architecture;
- explicit confirmation Phase C/FND-04/FC0-A were not entered.

Verify the comment exists live, report its numeric ID, then **STOP** for Main review.


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
