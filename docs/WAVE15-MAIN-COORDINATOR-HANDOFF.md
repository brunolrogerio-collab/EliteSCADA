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
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **PHASE A VERIFIED/FROZEN / PHASE B VERIFIED/FROZEN / PHASE C ACTIVE / NOT INTEGRATED**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**ORDER_ID: FND03-PHASE-C-LIFECYCLE-ORCH-01**  
**CODEX_MODE: IMPLEMENT_BOUNDED**  
**Mission:** FND-03 License Lifecycle/Fencing Phase C — lifecycle mutation orchestrator + restart reconciliation + licensing endpoint authorization/audit cutover

### Exact base / branch / target

- exact product base: `4647dd741551c97306217ac9893d3378b070f43b`
- product tree: `d7eb7d3f57269e71ed5984c82e701a059be56bfb`
- work branch: `work/w15-fnd-03-license-lifecycle-orchestrator-v1`
- branch was created by Main directly from the exact product base above
- target: `wave15/corrections-integration`
- parent/product ledger: #301
- dependency/checkpoint ledger: #305
- installation consumer contract: #304
- binding architecture: #301 comment `5722165708`
- frozen implementation evidence: PR #332 Phase A + PR #333 Phase B

On every `SIGA`, CODEX must first re-read this file live and compare the current integration HEAD against the exact product base. Coordination/documentation-only delta is allowed; any uncoordinated product/infra delta means `STOP / BLOCKED-BASE-DIVERGENCE`.

### Frozen prerequisites — consume, do not redesign

Phase A is frozen:
- canonical `VerifyCandidate` seam on `IProductLicenseService`;
- `RuntimeAuthorityState` / transition APIs;
- `AuthorityRevision` fencing epoch;
- pending/revision enforcement in admission/validate/heartbeat/terminate;
- PostgreSQL migration/state primitives and in-memory mirror.

Phase B is frozen:
- `ReevaluateForAuthorityChangeAsync`;
- allowed Runtime retention / denied Runtime stop;
- durable Demo authority-change anchor;
- remaining-duration Demo semantics;
- persisted Runtime recovery denial while transition is pending;
- persisted Demo recovery only with durable anchor.

Do not reopen these contracts unless a deterministic Phase C test proves a real defect. If that happens, STOP and return `BLOCKED-FROZEN-CONTRACT` evidence before modifying the frozen contract.

### Authorized Phase C scope

1. **Lifecycle orchestrator**
   - add one API-host `ProductLicenseLifecycleCoordinator` (or naming-equivalent single authority);
   - implement bounded install/replace and remove operations;
   - invalid/tampered/wrong-machine/expired/malformed candidate is a true no-op before transition;
   - after Begin transition, canonical file mutation remains outside DB transactions;
   - file mutation failure aborts the pending transition without revision bump/fence/Runtime disruption;
   - successful authority change commits the next revision, re-evaluates local Runtime, fences all pre-change remote leases, then completes pending state before success returns.

2. **Crash/restart reconciliation**
   - implement the binding W1-W6 conservative reconciliation from #301 comment `5722165708`;
   - canonical `CurrentVerification` remains license truth;
   - if exact post-file authority-change timestamp was not persisted, use the durable transition start as the conservative no-later-than anchor required by the frozen design;
   - repeated reconciliation must be idempotent;
   - pending must remain fail-closed if reconciliation cannot complete.

3. **Startup ordering**
   - register the lifecycle service/reconciler through normal DI;
   - execute pending lifecycle reconciliation only after Runtime Session Lease store initialization and before persisted Engineering Runtime recovery, so a persisted Runtime cannot recover under unresolved authority state;
   - do not create a second startup authority path.

4. **Licensing mutation route cutover**
   - `GET /api/licensing/status` and `GET /api/licensing/request` retain the current read boundary;
   - install/replace/remove must use `ApiAuthorizationService.CheckWorkspace(..., SecurityCapability.EngineeringModify)`;
   - machine-license mutation must not depend on Application Engineering Lock;
   - routes call only the lifecycle orchestrator, not `FileProductLicenseService` directly.

5. **Audit**
   - reuse `ApiAuditService` / `AuditEvent`;
   - add stable bounded actions for product-license install/replace/remove;
   - audit allowed, denied and failed operations with safe metadata only: operation/result code, previous/new LicenseState, previous/new AuthorityRevision, authorityChangedAtUtc, fenced lease count, local Runtime outcome;
   - never audit raw license code/ESLIC payload, signing material, credentials/tokens or arbitrary exception text that could echo them.

6. **Deterministic proof**
   - cover acceptance items 3-6, 8-10, 13-18 in section 3 below;
   - include fault windows: pending committed before file I/O; file failure/abort; crash after file commit before revision; crash after revision before Runtime re-evaluation; Runtime re-evaluation failure; crash before/after bulk fence; repeat reconciliation; concurrent admission/heartbeat/terminate vs transition;
   - preserve existing ESLIC2 + Shared Seat Accounting regressions and `.escadapkg` exclusion.

### Forbidden scope

- no License Generator UI;
- no full #304 Application/Authority detach UX;
- no Authority A->B switching implementation;
- no Historian switching/reset;
- no EliteGO UI;
- no cross-machine HA election/fencing/convergence;
- no FND-04;
- no broad ESLIC1 cleanup;
- no workflow weakening;
- no direct write to `wave15/corrections-integration` or `main`;
- no merge.

### Delivery / PR gate

CODEX may implement, commit, run focused tests and open a PR from the assigned branch to `wave15/corrections-integration`. Natural PR CI is allowed.

Return exactly:

`CODEX -> MAIN COORDINATOR — FND-03 LIFECYCLE PHASE C HANDOFF`

with:
- exact base SHA/tree;
- exact head SHA/tree;
- changed files and scope statement;
- acceptance matrix `PASS | FAIL | PENDING`;
- focused tests actually executed;
- PR number/state/base/head;
- exact CI run/jobs if available;
- explicit non-actions;
- blockers/risks.

Do not merge. Main independently reviews the candidate and CI before any integration order.

FND-03 DEV remains WAIT to avoid dual implementation.
FND-04 DEV/AUD remain WAIT.
FC0-A remains BLOCKED.
---

## 2A. MAIN COORDINATOR -> FND-03 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**DEV_MODE: WAIT_CODEX_PHASE_C**  
**Mission:** FND-03 License Lifecycle/Fencing — Phase C assigned exclusively to CODEX

Phase A and Phase B remain **VERIFIED/FROZEN** at product checkpoint:

- PR #333 merge SHA: `4647dd741551c97306217ac9893d3378b070f43b`
- merge tree: `d7eb7d3f57269e71ed5984c82e701a059be56bfb`
- exact post-merge CI #1555 / run `35665138086` — Web/Backend/Chromium SUCCESS

The active Phase C work package is owned by CODEX under `FND03-PHASE-C-LIFECYCLE-ORCH-01`.

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
