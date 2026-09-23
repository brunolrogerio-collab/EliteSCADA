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
**ORDER_ID: FND03-PHASE-C-AUTONOMOUS-CLOSE-05**  
**CODEX_MODE: BOUNDED_AUTONOMOUS_CLOSE_LOOP**  
**Mission:** finish FND-03 Phase C to an exact-head green candidate with fewer Main round-trips

### Starting point

- exact product base: `4647dd741551c97306217ac9893d3378b070f43b`
- work branch: `work/w15-fnd-03-license-lifecycle-orchestrator-v1`
- current reviewed work head at order issuance: `40f0001f969930f227861ef2f11d79e3bd9f2931`
- PR #334 -> `wave15/corrections-integration`
- integration advances since product base remain coordination/documentation-only
- ORDER-04 remove-idempotency defect and RED proof remain binding
- C04 stabilization already accepted and must not be weakened

### Delegated autonomy

CODEX no longer needs a new Main order for each small corrective iteration inside this Phase C candidate.

CODEX may independently repeat this loop until it reaches a clean exact-head handoff:

`diagnose -> edit -> focused tests -> commit/push -> inspect natural CI -> diagnose again`

Within that loop CODEX may:

1. implement the ORDER-04 already-Demo idempotent-remove correction;
2. add, strengthen or refactor deterministic Phase C tests needed to prove the existing acceptance matrix;
3. fix a newly exposed defect **without waiting for Main** when all of the following are true:
   - the defect is directly causal to Phase C behavior already authorized in #301;
   - the correction does not redefine a frozen public/shared contract;
   - no new database schema/migration, entitlement model, authorization model, Runtime Session contract or HA/Authority architecture is required;
   - the correction stays within the existing Phase C functional boundary;
4. modify existing Phase C production files already touched by PR #334 when directly necessary to close such a causal defect;
5. add or modify relevant tests under `tests/Scada.Drivers.Tests/`;
6. keep the accepted C04 fixture correction and, only if a fresh CI proves another deterministic fixture defect in that same test, correct that test without waiting for Main, preserving all semantic assertions;
7. inspect PR CI and workflow-job evidence directly;
8. push multiple bounded corrective commits to the same branch/PR;
9. update the PR body/comments with accurate current evidence;
10. stop only after producing an exact-head candidate with all Phase C acceptance items non-PENDING and natural CI green, or after hitting a hard stop below.

### ORDER-04 requirements remain mandatory

The repeated-remove regression must use a mutable/steppable clock with real `T1 > T0`.

It must prove on the corrected code:
- first Valid -> Demo remove at T0 establishes revision R+1 and Demo anchor T0;
- second remove at distinct T1 is idempotent;
- exact `AuthorityRevision` preserved;
- exact `DemoStartedAtUtc` preserved at T0;
- exact `AuthorityChangedAtUtc` preserved at T0;
- no second file mutation;
- no second Runtime reevaluation;
- no fence/epoch change;
- a lease admitted after the first remove remains valid;
- pending transition remains fail-closed;
- Invalid -> remove remains a real authority change.

The same regression must be demonstrably RED against old head `40f0001f...`; the existing Main audit in #301 comment `5788352467` is acceptable RED evidence if the implemented test shape matches it.

The acceptance #7 guard must detect both invocation and method-group references to `InstallLicense` / `RemoveLicense`.

### CI autonomy

For each new head, prefer the natural PR CI.

If CI fails:

- if failure is in changed Phase C code/tests or has a direct causal path to them, CODEX may diagnose, correct, push, and let a new CI run without asking Main;
- if failure is an unchanged unrelated test and evidence supports nondeterministic infrastructure/fixture behavior, CODEX may rerun the **single failed job once** without asking Main;
- if that one rerun fails again, do not loop reruns; diagnose and either fix a proven fixture defect within the allowed Phase C/test boundary or STOP with evidence;
- never weaken/skip tests, increase timeouts merely to mask failure, or change workflow gates to obtain green.

### Files / scope freedom

The prior exact 3-file limit is relaxed.

CODEX may edit:
- any production file already changed by PR #334 **only when directly causal to closing Phase C acceptance**;
- relevant `tests/Scada.Drivers.Tests/**`;
- the already accepted `web/scada-web/tests-e2e/c04-tag-source-browser.spec.ts` only for a proven same-test fixture defect.

CODEX must not modify:
- unrelated product areas;
- `.github/workflows/**`;
- licensing schema/signing/trust model beyond the already frozen design;
- Runtime Session public contract or persistence schema/migrations beyond the already authorized migration 024;
- HA, FND-04, FND-05+, #304 UX, License Generator UI, EliteGO, Historian switching;
- canonical coordinator documents;
- `main` or `wave15/corrections-integration` directly.

### Hard-stop conditions — Main required

STOP and return evidence if any correction would require:

- a new migration/schema beyond existing authorized 024;
- changing a frozen shared/public contract;
- reopening Phase A/B semantics outside a defect directly necessary for Phase C correctness;
- modifying authorization/capability semantics rather than consuming `EngineeringModify`;
- weakening fencing/fail-closed behavior;
- changing workflow gates;
- touching another FND lane;
- resolving a product decision not already fixed by #301 architecture.

Use prefix:

`CODEX -> MAIN COORDINATOR — BLOCKED-AUTONOMY-BOUNDARY`

### Merge boundary

CODEX still has **no merge authority**.

Even after exact-head CI is fully green, CODEX must not merge PR #334 or write directly to integration/main.

When the candidate is complete, return exactly:

`CODEX -> MAIN COORDINATOR — FND-03 PHASE C FINAL CANDIDATE HANDOFF`

including:
- exact base/head/tree;
- complete changed-file list;
- final acceptance matrix with no silent PENDING;
- ORDER-04 RED/GREEN evidence;
- focused test evidence;
- exact natural CI run/jobs;
- PR state/base/head;
- any rerun used and why;
- explicit non-actions.

Main then performs one final independent integration review rather than micromanaging intermediate iterations.

Until that final review:
- FND-03 remains ACTIVE / NOT FROZEN;
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
