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
**ORDER_ID: INFRA-CI-01A-W15-T1-01**  
**CODEX_MODE: BOUNDED_AUTONOMOUS_INFRA**  
**Mission:** implement Wave 15 profile-aware T1 PR CI in an isolated reviewable PR while FND-04 DEV works independently

### Exact base / branch

- coordination/integration base at assignment: `084d48f833415797f62ec525d0192def1a380592`
- product checkpoint carried by that commit: `a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`
- branch: `work/w15-infra-ci-01-profile-orchestration`
- target: `wave15/corrections-integration`
- issue/ledger: #305
- FND-04 work branch is separate and MUST NOT be touched.

The current live `.github/workflows/dotnet-ci.yml` now targets pull requests to `wave15/corrections-integration`, so ordinary Wave 15 PRs pay the full universal Backend + Web + 624-test Chromium gate. Main's timing audit in #305 comment `5789766944` shows ~13.5–14.5 minutes wall-clock, dominated by serial Chromium.

### Required implementation

Implement **INFRA-CI-01A/B** as the smallest auditable T1 orchestration change.

#### 1. Add a deterministic profile router

Create:

- `scripts/ci/wave15_profile_router.py`
- `tests/ci/test_wave15_profile_router.py`

Use Python standard library only.

The router must:

- parse a required PR-body declaration `VALIDATION_PROFILE: ...`;
- accept an explicit manual-dispatch override;
- support the versioned vocabulary:
  - `FOUNDATION_LIFECYCLE`
  - `FOUNDATION_TIMING`
  - `AUTHORITY_CORE`
  - `SESSION_LICENSING`
  - `SCRIPT_RUNTIME`
  - `RUNTIME_RENDERER`
  - `UI_EDITOR`
  - `SCRIPT_ENGINEERING`
  - `AUTHORITY_UX`
  - `LICENSING_UX`
  - `ELITEGO_RUNTIME`
  - `INSTALLATION`
  - `HA_DISTRIBUTED`
  - `DOCS_I18N_HELP`
  - `EEE_PACKAGE`
  - `DRIVER_PROTOCOL`;
- infer a conservative minimum profile set from changed paths;
- calculate effective profiles as **declared/override UNION inferred**;
- never allow a declared cheap profile to suppress an inferred risk profile;
- reject unknown profile names;
- reject a PR with missing declaration unless its change is provably limited to coordination-only documentation explicitly exempted by policy;
- expose stable machine-readable outputs for conditional jobs and a human-readable summary.

At minimum the inference must conservatively recognize Security/Authority, Licensing/session, Script runtime/engineering, Runtime/renderer, visual/editor, Installation, HA/distributed, Driver/DriverHost/Gateway/common communication, EEE/package and docs/i18n/help surfaces.

#### 2. Add Wave 15 T1 workflow

Create:

`.github/workflows/wave15-pr.yml`

Required trigger:

- `pull_request` targeting `wave15/corrections-integration`;
- `workflow_dispatch` with optional profile override;
- read-only contents permission;
- concurrency with cancel-in-progress for superseded PR/branch heads.

Required job shape:

1. `classify`
   - checkout with enough history for changed-file classification;
   - resolve PR base/head or dispatch comparison deterministically;
   - execute the router;
   - publish effective profiles and job booleans.

2. `common-sanity`
   - always for non-exempt product/infra PRs;
   - `git diff --check`;
   - router/profile self-tests;
   - no PostgreSQL/browser/Driver lab.

3. profile-driven focused jobs only when required:
   - Web semantic/type/build evidence for Web/editor/script-authoring profiles;
   - focused .NET tests for owning Foundation/Authority/Licensing/Script Runtime/Installation/HA/package profiles;
   - focused Chromium only for profiles that materially require browser behavior;
   - Driver-impact classification must be explicit and must not silently claim Seven-Driver/Interop PASS.

4. final `t1-gate`
   - uses `if: always()`;
   - fails if classification or any required conditional job failed/cancelled;
   - succeeds when all required T1 evidence is green;
   - summary names exact effective profiles and explicitly lists specialized/heavy gates as `REQUIRED NOW`, `UNCHANGED/TRUSTED`, or `DEFERRED TO T2/T3`.

Do not duplicate product tests merely for routing. Reuse existing commands/specs.

#### 3. Stop paying the universal full CI on every Wave 15 PR

Modify only the trigger topology in:

`.github/workflows/dotnet-ci.yml`

Required result:

- keep `workflow_dispatch`;
- keep broad `push` validation on `wave15/corrections-integration` for integrated T2-style checkpoints;
- keep universal PR validation for `main`;
- remove `wave15/corrections-integration` from the universal `pull_request` branch list after `wave15-pr.yml` owns T1.

Do not weaken, delete, shard or otherwise rewrite the universal Backend/Web/Chromium assertions in this order.

Browser sharding of the broad checkpoint gate is a separate follow-up after T1 routing is proven; the immediate efficiency gain comes from not executing all 624 Chromium tests for unrelated leaf PRs.

#### 4. Update policy docs

Modify only:

- `docs/CI-USAGE-POLICY.md`
- `docs/CI-VALIDATION-POLICY.md`

Record:
- Wave 15 T0/T1/T2/T3/T4 responsibilities;
- exact `VALIDATION_PROFILE` vocabulary;
- path inference is a floor, never authority;
- Main may escalate;
- universal `dotnet-ci.yml` remains broad integrated/main acceptance;
- `wave15-pr.yml` is the Wave 15 leaf-PR T1 gate;
- specialized heavy workflows remain risk-sensitive/manual/checkpoint gates;
- no test/coverage reduction.

### Exact allowlist

Only these files may change:

- `.github/workflows/wave15-pr.yml` — new
- `.github/workflows/dotnet-ci.yml` — trigger-only delta
- `scripts/ci/wave15_profile_router.py` — new
- `tests/ci/test_wave15_profile_router.py` — new
- `docs/CI-USAGE-POLICY.md`
- `docs/CI-VALIDATION-POLICY.md`

No product source, FND-04 file, existing specialized workflow, Playwright config/test, package file or lockfile change is authorized.

### Mandatory deterministic tests

The router test suite must cover at least:

1. docs-only example does not require backend/browser/Driver jobs;
2. Web/editor example requires Web evidence but not Driver;
3. Script Engineering-only example requires script/Web evidence;
4. Script Runtime example escalates to owning runtime/.NET evidence;
5. Security/Authority example requires Authority evidence;
6. Licensing/session example requires Licensing evidence;
7. Driver-impact example always infers `DRIVER_PROTOCOL` despite a cheaper declared profile;
8. HA example cannot be suppressed by `UI_EDITOR`;
9. unknown profile fails;
10. missing required declaration fails;
11. declared + inferred profiles are unioned deterministically;
12. manual override can only add/escalate, never remove inferred risk.

Also validate representative current FND-04 paths infer `SCRIPT_ENGINEERING` and/or `SCRIPT_RUNTIME` as appropriate.

### Required proof before handoff

- `python3 -m unittest tests/ci/test_wave15_profile_router.py` PASS;
- router synthetic CLI examples PASS;
- workflow YAML/structure validation available in environment PASS;
- `git diff --check` PASS;
- exact changed-file allowlist proof;
- open exactly one PR to `wave15/corrections-integration`;
- observe whatever natural Actions the PR actually produces and report exact run/job IDs;
- do not manufacture a run with empty commits;
- do not merge.

If the newly added T1 workflow cannot self-execute on its introduction PR because of GitHub workflow-event semantics, report that fact accurately and provide the strongest static/synthetic evidence; Main will decide the integration/bootstrap proof. Do not weaken the design to force a green badge.

### Bounded autonomy

CODEX may iterate autonomously:

`implement -> test -> commit/push -> inspect PR/Actions -> causally correct within allowlist`

without a new Main micro-order.

Stop with:

`CODEX -> MAIN COORDINATOR — BLOCKED-AUTONOMY-BOUNDARY`

if completion requires:
- changing product code/tests;
- modifying an existing specialized workflow;
- reducing/removing test coverage;
- broad global Playwright parallelism against shared state;
- branch-protection/admin settings;
- secrets/credentials;
- a new CI architecture outside the T1 router/orchestrator contract.

### Final handoff

Return exactly:

`CODEX -> MAIN COORDINATOR — INFRA-CI-01A T1 FINAL CANDIDATE HANDOFF`

with:
- exact base/head/tree;
- PR;
- six-file-or-smaller exact diff;
- profile vocabulary and inference table;
- synthetic test matrix/results;
- natural Actions evidence;
- current expected T1 cost shape vs universal ~14-minute gate;
- specialized gates not executed and why;
- explicit non-actions;
- acceptance `PASS | FAIL | PENDING`.

No merge authority.
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

**ORDER_STATE: ACTIVE**  
**ORDER_ID: FND04-DEV-TAGREF-V1-01**  
**Exact product base:** `a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`  
**Base tree:** `e48c8b9918f4d3a5ae4dee1df6211393c95b6513`  
**Work branch:** `work/w15-fnd-04-script-tag-reference-resolution`  
**Target:** `wave15/corrections-integration`  
**Validation profile:** `SCRIPT_ENGINEERING, SCRIPT_RUNTIME`

The dedicated control plane is authoritative for implementation details:

- branch: `coord/w15-fnd04-dev-aud-control`
- file: `docs/WAVE15-FND04-DEV-AUD-CONTROL.md`
- Main control commit at activation: `cea43de5141824057822657d35ed2fb32b3d03f4`
- plan: `FND04-TAGREF-V1`, section 3B.

DEV has bounded autonomy inside that closed plan to iterate RED -> implementation -> focused tests -> push -> natural CI, without Main micro-orders. No scope/contract widening or self-merge is authorized.
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
