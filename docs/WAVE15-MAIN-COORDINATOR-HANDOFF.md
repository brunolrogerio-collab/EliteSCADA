# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para MAIN COORDINATOR <-> CODEX / DEV / AUD durante a Wave 15.**
>
> Este arquivo é a memória operacional persistente e o **canal primário de ordens** do Main Coordinator. Issues/PR comments podem espelhar decisões/evidências, mas não substituem a `CURRENT ORDER` deste arquivo.
>
> **PROTOCOLO `SIGA`:** CODEX, DEV ou AUD deve reler este arquivo no GitHub live antes de agir e executar somente a ordem mais recente da sua lane.
>
> **GitHub live é a autoridade final.** Se este documento divergir do repositório/PRs/Actions live, o Main reconstrói o estado e corrige este arquivo antes de emitir nova ordem.

**Status date:** 2026-09-23 BRT  
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

FND-03 está agora integralmente **VERIFIED / FROZEN**.

- exact integrated product checkpoint / PR #334 merge SHA: `a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`
- tree: `e48c8b9918f4d3a5ae4dee1df6211393c95b6513`
- reviewed Phase C candidate parent: `5ddb9065efa24b52c81e59fdfe3aa3b0b9e9d1c4`
- candidate tree: `e2fd7012b5d1fbc3b5d6ee8cf020d1d62fd66f12`
- candidate CI #1558 / run `35813975645`: Backend/Web/Chromium SUCCESS
- exact post-merge CI #1559 / run `35815261288`, attempt 2: SUCCESS
  - Web `107058812138` — SUCCESS
  - Backend `107058810300` — SUCCESS
  - Chromium `107059153655` — SUCCESS / 624 passed

Attempt 1 of #1559 had one isolated PostgreSQL advisory-lock test failure outside the FND-03 delta. Main diagnosed it, used the permitted single failed-backend-job rerun, and the exact same integrated SHA completed attempt 2 fully green. No product/workflow mutation was made to obtain the green gate.

The FND-04 work branch was created directly from this exact verified product checkpoint:
`work/w15-fnd-04-script-tag-reference-resolution`.

Coordination/documentation commits after this checkpoint do not create a new product base.
### Foundation

- FND-01 — **VERIFIED/FROZEN**
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **VERIFIED/FROZEN**
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **VERIFIED/FROZEN**
- FND-03 global — **VERIFIED/FROZEN**
- FND-04 Script TAG Reference Resolution — **ACTIVE / EXECUTABLE PLAN FROZEN / NOT INTEGRATED**
- FND-06 — **NOT STARTED**
- INFRA-CI-01 — **AUDITED / IMPLEMENTATION PENDING**
- FC0-A — **BLOCKED on FND-04 + FND-06 + INFRA-CI-01**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**ORDER_ID: INFRA-CI-01A-REVIEW-CLOSE-02**  
**CODEX_MODE: BOUNDED_CORRECTION**  
**Mission:** close Main review defects in PR #335 without widening the six-file INFRA-CI-01A scope

### Reviewed candidate

- PR #335
- head `2069f4cb1ea398da615286001102fa44cbc35e67`
- tree `f2e563c06d97810dc560a0c2963be725c9f083d6`
- target `wave15/corrections-integration`
- six-file allowlist respected
- natural T1 run #2 / `35859380179` — SUCCESS
  - Classify `107175542297` — SUCCESS
  - Common sanity `107175593790` — SUCCESS
  - Focused .NET `107175593514` — SUCCESS
  - T1 gate `107175891532` — SUCCESS
  - Web/Chromium intentionally skipped for the candidate's effective profile

Main does **not** approve integration yet. Independent review found risk-floor defects that can let under-declared changes receive insufficient T1 evidence.

### Defect 1 — actual FND-04 server-runtime paths are not inferred

Current router does not infer any profile for these real, currently-authorized FND-04 paths:

- `src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs`
- `src/Scada.Api/Runtime/ServerScriptRunner.py`
- `src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs`

A PR changing those files could declare a cheap unrelated profile and suppress `SCRIPT_RUNTIME`, violating the non-bypassable inference-floor contract.

Fix the path rules so these real Server Script runtime surfaces infer `SCRIPT_RUNTIME`.

### Defect 2 — actual FND-04 web authoring paths do not infer SCRIPT_ENGINEERING

Current router classifies real paths such as:

- `web/scada-web/src/engineering/scripts/scriptEngineeringTypes.ts`
- `web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.logic.ts`
- `web/scada-web/src/engineering/scripts/scriptAssistantModel.ts`
- `web/scada-web/src/engineering/scripts/scriptAssistantReferenceValidation.ts`

as generic `UI_EDITOR` only.

These are the real Script Engineering authoring surfaces. They must infer `SCRIPT_ENGINEERING` (additional `UI_EDITOR` is acceptable where appropriate).

The existing tests used fictional representative paths, so they did not prove the real FND-04 surface.

### Defect 3 — manual dispatch only examines the last commit

On `workflow_dispatch`, the workflow currently uses:

`git diff --name-only "${GITHUB_SHA}^" "$GITHUB_SHA"`

That is not a safe risk floor for a multi-commit feature branch: earlier risk-bearing commits can disappear from inference.

For manual dispatch on a feature branch, compare the selected head against the merge-base with `wave15/corrections-integration` (or an equivalently explicit target-base computation). The effective changed-file set must cover the whole branch delta, not only the final commit.

Apply the same base logic to manual-dispatch `git diff --check`.

### Defect 4 — profile-owned backend evidence is incomplete for UX/runtime profiles

The profile declaration itself must imply sufficient owning evidence even when path inference is imperfect.

At minimum:

- `AUTHORITY_UX` must request focused Authority/backend evidence in addition to Web/browser evidence;
- `LICENSING_UX` must request owning licensing/backend evidence in addition to Web/browser evidence;
- `ELITEGO_RUNTIME` must request server Runtime/session/reconnect backend evidence in addition to client/browser evidence.

Using existing test projects is acceptable; do not add product tests or broaden outside the six-file allowlist.

### Required deterministic correction tests

Extend `tests/ci/test_wave15_profile_router.py` with exact real-path proofs:

1. each of the three `src/Scada.Api/Runtime/...` FND-04 files above infers `SCRIPT_RUNTIME`;
2. each of the four `web/scada-web/src/engineering/scripts/...` files above infers `SCRIPT_ENGINEERING`;
3. a cheap declared profile cannot suppress those inferred profiles;
4. `AUTHORITY_UX` produces backend + Web/browser evidence;
5. `LICENSING_UX` produces backend + Web/browser evidence;
6. `ELITEGO_RUNTIME` produces backend + Web/browser evidence.

Keep all prior router tests green.

### Scope remains unchanged

Only the original six files are authorized:

- `.github/workflows/wave15-pr.yml`
- `.github/workflows/dotnet-ci.yml` — trigger-only delta; no job/assertion rewrite
- `scripts/ci/wave15_profile_router.py`
- `tests/ci/test_wave15_profile_router.py`
- `docs/CI-USAGE-POLICY.md`
- `docs/CI-VALIDATION-POLICY.md`

No product source, FND-04 code/test, Playwright config/test, package/lockfile, specialized workflow or branch-protection change.

### Validation

Before handoff:

- all router unit tests PASS;
- synthetic exact FND-04 path examples PASS;
- manual-dispatch branch-delta logic reviewed/validated without using a one-commit shortcut;
- `git diff --check` PASS;
- push correction to PR #335;
- allow natural T1 CI on the new exact head;
- no merge.

Return exactly:

`CODEX -> MAIN COORDINATOR — INFRA-CI-01A REVIEW-CLOSE HANDOFF`

with old head -> new head/tree, exact six-file diff, exact real-path inference matrix, profile-to-evidence matrix, dispatch-base proof, tests, natural run/jobs, and explicit non-actions.

FND-04 DEV remains independently ACTIVE. Do not touch its branch.
---

## 2A. MAIN COORDINATOR -> FND-03 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**DEV_MODE: FND03_FROZEN / NO ACTIVE MISSION**

FND-03 is **VERIFIED/FROZEN** at exact checkpoint `a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`.

On `SIGA`, re-read this file and GitHub live; if no new Main order exists, report `FND-03 DEV — FROZEN / WAIT` and stop.
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

**ORDER_STATE: BLOCKED_ENV / WATCH_ONLY**  
**ORDER_ID: FND04-DEV-ENV-HOLD-02**  
**Exact product base:** `a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`  
**Base tree:** `e48c8b9918f4d3a5ae4dee1df6211393c95b6513`  
**Work branch:** `work/w15-fnd-04-script-tag-reference-resolution`

Main accepts the normal DEV chat's environment blocker: connector-only access cannot execute the mandatory local RED/GREEN evidence. The branch remains untouched at the exact base.

The normal DEV chat must not create a competing implementation. It remains available for live revalidation and later review.

### Delegated executable lane

A dedicated **FND-04 CODEX EXECUTOR** is now ACTIVE under the same frozen plan:

- control branch/file: `coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md`
- control commit: `e871d51238881824e57778cf884b246ba8163bec`
- executor order: `FND04-CODEX-TAGREF-V1-01`
- source plan: `FND04-TAGREF-V1 / section 3B`
- exact same base/branch/allowlists
- RED-1/RED-2/RED-3 must execute before production
- local dotnet/Node/Playwright GREEN evidence required
- no scope widening, no self-merge, no freeze authority.

This is an execution substitution only; architecture and DEV ownership semantics are unchanged.
---

## 5. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: WAIT_CANDIDATE**  
**ORDER_ID: FND04-AUD-WAIT-CANDIDATE-0003**  
**Default mode:** `READ_ONLY_REVIEW`

DEV is active, but AUD must not inspect a moving candidate as if immutable. Wait until Main supplies exact DEV candidate SHA/tree, then execute the adversarial matrix from the dedicated control plane.
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
