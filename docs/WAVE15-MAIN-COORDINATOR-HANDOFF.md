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
- FND-04 Script TAG Reference Resolution — **VERIFIED/FROZEN** at `6c810647c9773a19b212d9c33694780141786ac7`
- FND-06 — **ACTIVE / EXECUTABLE PLAN FROZEN / NOT INTEGRATED**
- INFRA-CI-01A — **VERIFIED/FROZEN**
- FC0-A — **BLOCKED only on FND-06**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**ORDER_ID: INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1**  
**CODEX_MODE: BOUNDED_INFRA_CORRECTION**  
**EXECUTOR_IDENTITY: SAME SEQUENTIAL CODEX CHAT/LANE USED IN PRIOR FOUNDATION WORK INCLUDING FND-04/FND-06**  
**Mission:** close the generic shared-PostgreSQL-schema initialization race blocking FND-06 freeze

Exact infra base:
- `624f2eca456310a2c6156538b3616a06e3be075f`
- tree `fb864fb954b0123e69db379cd6b3120349b43600`
- this SHA contains the merged, Main-accepted FND-06 product delta.

Trigger:
- post-merge EliteSCADA CI #1563 / run `35940661531`
- Web SUCCESS
- Backend test FAILURE
- Chromium skipped
- only identified failed test:
  `PostgreSqlVisualDynamicPersistenceTests.RevisionPersistence_PreservesVisualExpressionConditionAndAnalogFill`
- PostgreSQL `23505 / pg_namespace_nspname_index` in `PostgreSqlEngineeringProjectStore.InitializeAsync`.

Main classification:
- `GENERIC_INFRASTRUCTURE_BLOCKER / NOT_FND06_CAUSAL`
- failing store/test are byte-identical to pre-FND06 base;
- same error signature has Wave 14 precedent;
- no blind rerun authorized.

Dedicated executable control:
- branch `coord/w15-infra-ci-01b-control`
- file `docs/WAVE15-INFRA-CI-01B-CONTROL.md`
- control commit `358b067d9af6501b2945f311b5d2cd32cab64efa`
- work branch `work/w15-infra-ci-01b-postgresql-schema-init`.

Required direction:
- explicit completed shared advisory-lock command before shared-schema DDL in inline initializers;
- common key `4993446713136202561`;
- bounded concurrency regression;
- no schema/business/FND-06 semantic change;
- no CI-parallelism/timing workaround;
- no blind retry as substitute for serialization.

FND-06 remains **INTEGRATED / FREEZE BLOCKED BY INFRA-CI-01B**.
FC0-A audit remains PREPARED and blocked until infra correction + exact green broad CI + FND-06 freeze.

No self-merge/freeze authority.
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

**ORDER_STATE: FROZEN / WAIT**  
**ORDER_ID: FND04-DEV-FROZEN-03**  
**DEV_MODE: NO_MUTATION**

FND-04 is **VERIFIED/FROZEN** at exact integration checkpoint:
- merge SHA `6c810647c9773a19b212d9c33694780141786ac7`
- tree `1221ff55963052be4e924dd644efbaa65763f546`
- exact post-merge EliteSCADA CI `35913456486` — SUCCESS.

The normal DEV lane has no active mission. On `SIGA`, revalidate live state, report `FND-04 DEV — FROZEN / WAIT`, and stop unless Main has issued a new Foundation delta.

Downstream lanes may consume the frozen Script TAG reference contract but may not redefine it.
---

## 5. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: FROZEN / WAIT**  
**ORDER_ID: FND04-AUD-FROZEN-0011**  
**AUD_MODE: READ_ONLY / NO ACTIVE REVIEW**

Independent AUD completed exact candidate review on `c89ad92ed38dcacc6d00c4a9b907720f9dbfcf9e` with final classification **ACCEPTABLE**.

PR #336 merged at `6c810647c9773a19b212d9c33694780141786ac7`; exact post-merge CI `35913456486` completed SUCCESS including Web, Backend/test/smoke and Chromium E2E.

No further FND-04 audit is active. On `SIGA`, revalidate live state, report `FND-04 AUD — FROZEN / WAIT`, and stop unless Main issues a new audit order.
---

## 6. FND-04 BINDING CONTRACT — VERIFIED / FROZEN

Frozen downstream contract:
- human-readable Python-visible TAG reference for normal authoring;
- stable `TagId`/Guid remains authoritative identity;
- persisted versioned visible-reference <-> expected-TagId binding;
- canonical backend path proof uses the existing TAG registry semantics;
- Script source token membership is exact after trim;
- read/write fail closed on missing/ambiguous/stale/identityDrift and never silently retarget;
- legacy GUID-only dependencies remain explicitly compatible;
- Authority/security paths remain canonical;
- no second TAG registry/resolver/comparer/auth authority is permitted.

Exact frozen checkpoint:
`6c810647c9773a19b212d9c33694780141786ac7` / tree `1221ff55963052be4e924dd644efbaa65763f546`.

Any change requires a new Main/Foundation delta.
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

## FND-04 post-merge status

- PR #336: MERGED
- exact merge/product checkpoint: `6c810647c9773a19b212d9c33694780141786ac7`
- merge tree: `1221ff55963052be4e924dd644efbaa65763f546`
- independent AUD: `ACCEPTABLE`
- exact post-merge EliteSCADA CI `35913456486` / #1562: **SUCCESS**
  - Web: SUCCESS
  - Backend build/test/smoke: SUCCESS
  - Chromium end-to-end: SUCCESS
- FND-04: **VERIFIED/FROZEN**
- FND-04 control commit: `0453428521b28e951aa8e9d01742ee58b09f6690`
- downstream may consume the frozen FND-04 contract but may not redefine it.

## FND-06 active foundation

- state: **ACTIVE / NOT INTEGRATED**
- exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
- work branch: `work/w15-fnd-06-visual-stability-foundation`
- control branch: `coord/w15-fnd06-control`
- control file: `docs/WAVE15-FND06-CONTROL.md`
- control commit: `1a1488388fd67bf89380879b07437e1460170f18`
- order: `FND06-CODEX-VISUAL-STABILITY-V2`
- FC0-A remains blocked on FND-06 **and the mandatory post-FND06 Foundation Closure Audit**.
- prepared downstream release plan: `docs/WAVE15-FC0-A-RELEASE-PREP.md` on the FND-06 control branch.



## FND-06 status classification correction

- control revision: `0003`
- active order: `FND06-CODEX-VISUAL-STABILITY-V2`
- valid Wave 15 T1 profile: `UI_EDITOR, RUNTIME_RENDERER`
- `tank | value | dynamo | status` are the mandatory known persisted legacy identifiers for the FND-06 compatibility boundary
- bare `status` is evidenced in the exact product-base seed and is **not** a current `core.*` built-in
- no guessed alias/migration to `instrument.status`, `core.valueDisplay`, or another canonical type is authorized
- truly unknown types remain fail-closed/contained
- control commit: `1a1488388fd67bf89380879b07437e1460170f18`


## FC0-A mandatory post-FND06 audit gate

FND-06 freeze is necessary but is **not sufficient** to release FC0-A.

After FND-06 exact post-merge CI is green and Main declares it VERIFIED/FROZEN, activate:

`FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

Control:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-CONTROL.md`

The audit must robustly reconcile:
- Wave 15 premises/roadmap;
- `WAVE15-CORRECTION-BACKLOG-FINAL.md`;
- final Wave 14 diagnostics/comments, especially #286 `5628159172`, `5628311338`, `5628760255`, `5634503355`;
- all frozen Foundation contracts and exact integrated evidence;
- remaining product gaps/residuals;
- compatibility of prepared FND-05/FND-07 with contracts consumed by the four FC0-A DEVs.

Release rule:
- if audit = `ACCEPTABLE / FC0A_RELEASE_APPROVED`, Main may activate the four FC0-A DEVs **plus FND-05 and FND-07 in parallel** from the exact audited checkpoint;
- if FND-05/FND-07 require breaking a frozen consumed contract, result = `BLOCKED-CONTRACT`; Foundation delta occurs before DEV release.

FND-05 compatibility control: `coord/w15-fnd05-control` rev 0002 / `b995435594f9031a33df5674a0207016405a41d7`.
FND-07 compatibility control: `coord/w15-fnd07-control` rev 0002 / `503a89d2985db64c1d6666e40d1de06251027687`.


## PR #337 contract-risk snapshot for post-FND06 FC0-A gate

Post-FND06 audit control:
- audit: `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`
- audit rev: `0002`
- latest audit-control commit: `9e75fd836d03dc34619f72ba5056e3aa874cfdbb`

PR #337 pre-freeze evidence:
- candidate `923543705378016090e7067b35954795a9591a57`
- tree `5657cee7169a4e77370d416add4efcf07184d7c0`
- natural T1 `35931139983` SUCCESS
- no Security/Authority/Licensing/Script/lifecycle contract change in its 9-file visual/editor/runtime delta
- FND-06 still HOLD pending mounted A7 closeout; this is an evidence gate, not a known contract break.

Current FC0-A contract-risk classification:
- DEV-EDITOR: `LOW / GUARDED` — must consume frozen FND-06 compatibility/renderer authority; mounted A7 proof still pending.
- DEV-SCRIPT-ENGINEERING: `NONE IDENTIFIED / GUARDED` — FND-04 remains untouched; FND-05 may only fence execution authority around it.
- DEV-AUTHORITY-UX: `NONE IDENTIFIED / GUARDED` — FND-07 must compose FND-02/AUTH-04, not redefine it.
- DEV-LICENSING-UX: `LOW BUT MATERIAL RESIDUAL` — FND-05 redundancy entitlement/readiness must remain additive/backward-compatible to frozen FND-03 semantics.

FND-05 hard guard:
- control rev `0003`
- commit `85502eaaa3a27fc2c050396b3f40c3e08943ae37`
- existing FND-03 license validity/meaning, Interactive/ViewOnly/session quota semantics, machine binding and install/replace/remove behavior may not be reinterpreted by HA.
- if HA needs such a breaking change -> `BLOCKED-CONTRACT` before FC0-A DEV release.

FC0-A release prep snapshot commit:
`c0e4bd69c89c47efc7dc936a6ac9e61cbbbd8f96`.

This is pre-freeze risk assessment only; final release still requires exact FND-06 freeze + independent post-FND06 audit PASS.


## FND-06 merged checkpoint pending freeze

PR #337 is merged.

- candidate: `2257f8f99b5e6deac80d64ed2cc0c43aa8dab1cc`
- candidate tree: `2ebb839a788bb4fad249877689c25ac1b18f6d74`
- merge SHA: `624f2eca456310a2c6156538b3616a06e3be075f`
- merge tree: `fb864fb954b0123e69db379cd6b3120349b43600`
- candidate T1 `35939646387`: SUCCESS
- post-merge broad CI `35940661531` / #1563: PENDING/IN PROGRESS at this record
- FND-06 state: **INTEGRATED / POST-MERGE CI PENDING / NOT YET VERIFIED-FROZEN**
- CODEX: `FND06-CODEX-WAIT-POSTMERGE-V4 / NO_MUTATION`
- FND-06 control rev `0006`, commit `8d1f415bc16b556e8133e6a1da1ab89881e7f189`

Post-FND06 audit remains blocking and is preloaded with this exact checkpoint:
- `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`
- audit rev `0003`
- audit-control commit `a9ed4003acf8c71db035bc854a75f87cf97273fb`
- state `PREPARED / WAIT_FND06_POST_MERGE_CI_GREEN`

No FC0-A DEV, FND-05 or FND-07 release until FND-06 freezes and the independent audit returns `ACCEPTABLE / FC0A_RELEASE_APPROVED`.


## INFRA-CI-01B post-FND06 blocker

FND-06 PR #337 is merged at `624f2eca456310a2c6156538b3616a06e3be075f`, but broad post-merge CI `35940661531` failed on a generic PostgreSQL shared-schema initialization race.

- FND-06 visual/mounted evidence remains Main-accepted.
- FND-06 is not VERIFIED/FROZEN until infrastructure is corrected and exact broad integration CI is green.
- active control: `coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`
- active order: `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`
- control commit: `358b067d9af6501b2945f311b5d2cd32cab64efa`

Post-FND06 FC0-A audit:
- rev `0004`
- control commit `bbe9c4a63aef310c45f7f8d2dab17ccb9ccf5bac`
- evidence matrix rev `0002` / commit `435e1ee639e520644751c14fab6baca2e5ada38c`
- state: PREPARED / blocked on INFRA-CI-01B + FND-06 freeze.
