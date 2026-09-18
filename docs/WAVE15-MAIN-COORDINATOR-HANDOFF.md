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
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **PHASE A VERIFIED/FROZEN / PHASE B CI-BUILD CORRECTION ACTIVE / NOT INTEGRATED**
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
**DEV_MODE: IMPLEMENT_PHASE_B_TEST_FIXTURE_CORRECTION**  
**Mission:** FND-03 Lifecycle/Fencing Phase B — fix the exact Demo recovery test fixture only

### Exact candidate / PR

- product base: `20b934f23d8798ffb65cca203b62f8b5c3d8f111`
- PR: #333
- branch: `work/w15-fnd-03-runtime-authority-reevaluation-v1`
- current head: `5ebf533132b217085ff74db8f26ddb16cf95da88`
- tree: `3946849ebf7ff60a018fbf42fd7b779eb18ddfab`
- target: `wave15/corrections-integration`
- natural CI: EliteSCADA CI #1553 / run `35394388702`

The prior CS0649 correction is accepted:
- exactly one commit from `abb1e497...`;
- exactly one test file changed;
- zero production/workflow changes;
- Backend build now succeeds.

### CI #1553 diagnosis

- Web `105759791089` — SUCCESS.
- Backend `105759791284` — Build SUCCESS, Test FAILURE.
- Chromium `105760270078` — SKIPPED because Backend failed.
- Scada.Drivers.Tests: **669/670 PASS**.

Sole failure:

`PersistedRuntimeRecoveryServiceTests.Recovery_DemoWithAuthorityAnchor_UsesNormalPathAndPreservesRemainingWindow`

at line 279:

`Assert.True(result.Recovered)`

Main independently traced the failure through the canonical Runtime implementation.

The test currently builds:

`CreateSimplePackage(0)`

which contains:
- zero TAGs;
- zero DataSources;
- therefore zero active Runtime sources.

`EngineeringRuntimeCoordinator.BuildCandidate` intentionally rejects such a package with:

`RUNTIME_NO_ACTIVE_SOURCES`

So the test fixture cannot produce a successful persisted Runtime recovery even when the Phase B Demo-anchor behavior is correct.

This is a **test fixture defect**, not evidence of a Phase B production defect.

### Exact correction — ONE TEST FILE ONLY

Authorized file:

`tests/Scada.Drivers.Tests/PersistedRuntimeRecoveryServiceTests.cs`

Required:

1. keep the failing acceptance test semantically about **Demo persisted recovery with durable authority anchor**;
2. replace the zero-source package in that test with the smallest deterministic Runtime-valid package;
3. use **Server Memory**, not a network/protocol driver:
   - one TAG;
   - Source bound to a local memory DataSource;
   - DataSource driver `InternalMemoryRuntimePlanner.ServerMemoryDriverKey`;
   - no sockets, no external server, no wall-clock dependency;
4. one TAG is well below the Demo tag ceiling and must not alter the entitlement meaning of the test;
5. preserve all existing assertions proving:
   - `result.Recovered == true`;
   - active revision restored;
   - `DemoStartedAtUtc == durable anchor`;
   - `DemoExpiresAtUtc == anchor + LicensingPolicy.DemoMaxContinuousRun`;
   - only remaining Demo time is scheduled;
   - Engineering Lock is restored only after successful recovery.

A local helper in this same test file is allowed if needed. Follow the repository's existing Server Memory fixture pattern:
- TAG `Source: "memory.server"`;
- DataSource key `"memory.server"`;
- driver `InternalMemoryRuntimePlanner.ServerMemoryDriverKey`.

Do not alter production to make a zero-source Runtime activatable. `RUNTIME_NO_ACTIVE_SOURCES` is valid existing product behavior.

### Guards

- exactly one file may change versus `5ebf533132b217085ff74db8f26ddb16cf95da88`;
- zero production changes;
- zero workflow changes;
- preserve prior CS0649 correction;
- no test deletion/skip/assertion weakening;
- no rebase/retarget;
- PR #333 remains the same PR;
- no Phase C/FND-04/FC0-A;
- no merge;
- no `main`.

### Validation

1. push one bounded test-fixture correction commit to the existing branch;
2. let PR #333 trigger a **new natural CI** on the new exact head;
3. do not rerun #1553 unchanged;
4. tests remain PENDING until Main validates the new exact-head run.

### Return

Publish exactly one new top-level #301 comment beginning:

`FND-03 DEV -> MAIN COORDINATOR — LICENSE LIFECYCLE PHASE B TEST-FIXTURE CORRECTION HANDOFF`

Include:
- old head `5ebf5331...` -> new head/tree;
- exact single changed file;
- exact Runtime-valid Server Memory fixture used;
- confirmation all prior Demo-anchor/timer/Engineering-Lock assertions remain;
- confirmation zero production/workflow changes;
- PR #333 unchanged;
- new natural CI run ID if exposed;
- tests PENDING until Main review;
- confirmation no Phase C entered.

Verify the comment live, report numeric ID, then **STOP**.

Main owns new-head review, exact-head CI diagnosis, merge decision and post-merge gate.

CODEX remains WAIT.
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
