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
**ORDER_ID: FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1**  
**CODEX_MODE: BOUNDED_SERVER_SCRIPT_RECOVERY**  
**EXECUTOR_IDENTITY: SAME SEQUENTIAL CODEX CHAT/LANE USED IN PRIOR FOUNDATION WORK**

Audit result:
`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — CHANGES_REQUIRED`

Active correction:
- W15-P1-01 Server Script bounded recovery;
- exact base SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- base tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- work branch `work/w15-fc0a-p101-server-script-recovery`;
- target `wave15/corrections-integration`;
- validation profile `SCRIPT_RUNTIME`;
- control `coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`;
- control commit `b8858d08a8516588db4be40d2da48ea266fe793e`;
- routing control rev 0026 / `a088c884d90bd0c2d86b844f74332306a80b7a7c`.

P1-06 Engineering fallback remains queued and must not be mixed into P1-01.

No merge/freeze authority.
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

## 5. FC0-A POST-FND06 AUDIT — MAIN COORDINATOR RESULT

**AUDIT_OWNER: MAIN COORDINATOR**  
**STATE: COMPLETED / CHANGES_REQUIRED**

Exact audited checkpoint:
- SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- final broad `35953557122` — SUCCESS.

Confirmed blockers:
- W15-P1-01 Server Script permanent throttle latch / missing bounded recovery;
- W15-P1-06 synthetic Engineering fallback identity/status.

Contract disposition:
- FND-05 = additive / compatible;
- FND-07 = compositional / compatible;
- no current breaking FND-05/FND-07 contract requirement.

Authoritative audit report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`
commit `463d357f8a9c11f774f5da59480e4a17a19569f5`.

Audit control rev 0011 / `ddfc2997ea2d5c594c158a0d16c52277688c46ee`.
Separate AUD lane is optional advisory only.

FC0-A remains HOLD until P1-01 + P1-06 are corrected and affected audit rows pass again.
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
- active order: `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V2`
- control commit: `358b067d9af6501b2945f311b5d2cd32cab64efa`

Post-FND06 FC0-A audit:
- rev `0004`
- control commit `bbe9c4a63aef310c45f7f8d2dab17ccb9ccf5bac`
- evidence matrix rev `0002` / commit `435e1ee639e520644751c14fab6baca2e5ada38c`
- state: PREPARED / blocked on INFRA-CI-01B + FND-06 freeze.


## INFRA-CI-01B V2 scope amendment

Intermediate candidate `97c665c8e4d62268336dfdef400f992c2f9d43cf` has natural T1 `35943407678` SUCCESS, but full local .NET/PostgreSQL regression reproduced `23505`.

Additional shared-schema creators were confirmed:
- `PostgreSqlAuthorityPolicyStore`: initialization creates `elitescada` without the shared DDL lock;
- `PostgreSqlAuthorityLifecycleStore`: shared lock + DDL remain in one SQL batch;
- `PostgreSqlRuntimeSessionLeaseStore`: shared lock + DDL remain in one SQL batch.

This is the same generic DDL serialization invariant. Main expanded the bounded infra order to:
`INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V2`

Control commit:
`be7a2d875c24e07d023162e64c65acb0aebe9672`

Authority/RuntimeSession files may be touched **only** for shared-schema initialization sequencing; all policy, epoch, session, quota, licensing, admission and fencing semantics remain forbidden to change.

Candidate `97c665c8...` is intermediate only, not merge-ready.


## Broad CI #1564 causality split

Exact integration `eb4563cf0060449b479c4335ef30a19ed65e35ab`, run `35944510920`:
- Web SUCCESS;
- Backend SUCCESS;
- Chromium FAILURE, 636/637.

INFRA-CI-01B PostgreSQL correction is Main-accepted and its owning backend gate is green.
The sole remaining failure is FND-06 test fixture leakage.

Current controls:
- FND-06 rev 0010 / `b7b3ab914464a2da87d7b2175eb95a24d8a7db9b`
- shared CODEX route FND-04 rev 0021 / `982b09f40d1e97835883011fdfbefce7709bceb8`
- INFRA-CI-01B rev 0004 / `a3d81308b460fb04524014f40802031574415b3a`
- post-FND06 audit rev 0006 / `e5323dcbeff2933762de12cefd8e1b737ed1f52d`.

No FC0-A release until test-only correction merges, a fresh broad run is fully green, FND-06 freezes, and the independent audit passes.


## Pre-audit finding — W15-P1-01 Server Script recovery

While preparing the mandatory post-FND06 FC0-A audit, Main revalidated the current Server Script runtime against the final Wave 14 backlog contract.

Current source still shows:
- `ScriptRuntimeExecutionCoordinator.ProcessNextAsync` returns `Throttled` while diagnostics `IsThrottled` is true;
- the only coordinator exit is explicit `ResetThrottle()`;
- repository search found no production automatic Server Script caller of `ResetThrottle()`;
- existing tests prove timeout -> throttled behavior, not bounded cooldown/half-open/probe recovery.

Wave 14/W15-P1-01 explicitly required bounded automatic recovery and rejected a permanent silent throttle latch.

Preliminary disposition:
`W15-P1-01 = PRELIMINARY BLOCKED_FOUNDATION`

This finding is independent of FND-06 and does not prevent FND-06 from becoming VERIFIED/FROZEN if exact broad CI #1565 is green. It **does** prevent immediate FC0-A release if the independent audit confirms it.

Audit evidence:
- `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-EVIDENCE.md`
- preliminary matrix commit `3483faf3aab4940791807b07b4453865629d9077`
- audit control rev 0008 / commit `18a0fcda2b354779cdf0f1ba4d829a838714b3d2`.

DEV-SCRIPT-ENGINEERING may not absorb this runtime/Foundation correction silently.


## Pre-audit finding — W15-P1-06 truthful Engineering fallback

Exact source inspected at the final FND-06 product checkpoint `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`:

`web/scada-web/src/engineering/EngineeringApp.tsx`

Observed:
- `snapshot` initializes as null while loading;
- failed load sets/keeps `snapshot=null` and renders an error/retry state;
- the sidebar project chip nevertheless renders `snapshot?.workspace.projectName ?? snapshot?.workspace.projectKey ?? 'Demo Project'`;
- therefore an absent/unloaded public model can still be displayed as `Demo Project`;
- no-model WorkspaceBar fallbacks can also present `unsaved` / `clean` despite no authoritative Working model.

This matches the Wave14 A6 / W15-P1-06 class that required truthful loading/unavailable/error identity rather than a fictitious Working project.

Preliminary disposition:
`W15-P1-06 = PRELIMINARY BLOCKED_PRODUCT / SHARED_ENGINEERING_SHELL`

This is not causal to FND-06 and does not block its freeze if broad #1565 is green. If independent audit confirms it, FC0-A remains blocked until a bounded shared-shell correction is integrated/revalidated.

Audit control rev 0009:
`888473cbf27e003d659ec3dd87de2f0f89240474`

Evidence matrix:
`a5260ca309742894ee48e8cce17d9175d67cda08`.


## Audit blocker correction preparation

Coordination-only preparation exists for the two preliminary blockers. It does **not** authorize product mutation:

`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`

prep commit:
`5904924faf7dcc13ec42c495ff54fe6ef82ad005`

Prepared, inactive orders:
- `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`
- `FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

Activation is forbidden until independent audit confirms the corresponding finding on the exact frozen post-FND06 checkpoint.

P1-01 plan preserves FND-04 Script TAG semantics, sandbox isolation, bounded queue/coalescing and Active revision safety while replacing a permanent latch only if confirmed.

P1-06 plan preserves lifecycle/Authority/FND-06 contracts while removing fictitious no-snapshot project/status state and distinguishing transport rejection from actual HTTP response only if confirmed.


## Post-FND06 audit — second-pass deep review

Main completed a distinct second-pass audit on the frozen baseline:
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`.

Report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-SECOND-PASS.md`

report commit:
`43c266949236af377ba859c9b8f09fa2d3a3a31a`

audit control rev 0012:
`62a731cc4291b751e9cb2e91e440fcf0d5ffb3bd`

release-prep refinement:
`c78895b218608b511e61b90206189d2b641a1e71`

Second-pass conclusion:
`NO NEW PRE-FC0A BLOCKER IDENTIFIED`.

The two confirmed release blockers remain:
- W15-P1-01 Server Script bounded recovery;
- W15-P1-06 truthful Engineering no-model/loading/error state.

New/refined downstream findings:
- DEV-EDITOR must consume FND-06 compatibility for known-legacy marquee/geometry/z-order/multi-object authoring paths that still use strict built-in lookup;
- DEV-SCRIPT must make Script Assistant consume the FND-06 compatibility seam for known-legacy property discovery;
- Script Engineering already has cursor-aware insertion and timer/tagChanged authoring, so those are regression/refinement scope, not greenfield;
- Python API Help still lacks a formal signature/parameter/return/example contract;
- DEV-LICENSING must surface requested vs granted Runtime class, explicit ViewOnly request and Interactive-quota fallback/reason UX; backend Authority/capacity contract is already sound;
- one minor Runtime API message still says `viewer` where canonical public vocabulary is `viewOnly`;
- W15-P2-01 Trends still lacks explicit realtime/reconnect/last-request/last-success/freshness observability;
- W15-P2-02 shared Popup/live-value path correctly handles numeric zero/quality but still lacks explicit freshness-age/reason telemetry.

Contract result strengthened:
- FND-05 remains ADDITIVE / COMPATIBLE;
- FND-07 remains COMPOSITIONAL / COMPATIBLE;
- production host uses `EngineeringWorkspace(seedDemo:false)`, so a truthful neutral no-Demo workspace is already representable;
- existing Authority detach/attach/switch primitives further reduce FND-07 contract risk.

No DEV/FND-05/FND-07 release occurs until P1-01 and P1-06 close and Main reruns the affected release rows.


## Main Coordinator takeover — third post-FND06 audit pass (2026-09-24)

Live takeover revalidated against GitHub after coordinator-chat rotation.

- frozen product checkpoint remains `560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- post-FND06 FC0-A audit remains `CHANGES_REQUIRED`;
- third Main pass outcome: `NO NEW PRE-FC0A BLOCKER IDENTIFIED`;
- authoritative third-pass report: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-THIRD-PASS.md`, commit `b144de748337045bf447a5142d825fa0beac1ff1`;
- audit control rev 0013: `0d27fe59d154c161418c996a4ea2df6fe59066e6`;
- confirmed release blockers remain only W15-P1-01 and W15-P1-06;
- active order remains `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1` on `work/w15-fc0a-p101-server-script-recovery`;
- work branch revalidated identical to exact base: 0 ahead / 0 behind / no changed files / no PR;
- P1-06 remains queued separately; it must not be mixed into P1-01;
- DEV-EDITOR, DEV-SCRIPT-ENGINEERING, DEV-AUTHORITY-UX, DEV-LICENSING-UX, FND-05 and FND-07 remain HOLD.

Third-pass refinements are downstream/pre-activation only: P2-03 shell responsiveness remains open; P2-04 account accessibility is substantially implemented but needs focused keyboard regression; P2-05/P2-06 remain Engineering layout-density residuals; P2-07 is partially implemented and should not rebuild the existing Dynamo insertion preview; Help routing is surface-level rather than Engineering-section-contextual; FND-07 gained an explicit Engineering Lock × detach/switch/neutral-bootstrap acceptance guard. FND-05 remains additive/compatible; FND-07 remains compositional/compatible.

FND control refreshes:
- FND-05 hold/activation metadata: `0c69dd307880b6afcd89f352cf76fef183087410`;
- FND-07 hold metadata + Lock/detach acceptance guard: `d0aba9e375a8330b7720b32f4defcdabf0ec12ae`.

No product code, product branch or release state was changed by this audit pass.


## Main decision — consolidated FC0-A correction package (2026-09-24)

Product Owner requested that all known FC0-A findings be corrected now where safely possible, with one larger CODEX delivery before the next Main review.

Active order:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V2`
- control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`
- control commit: `0ab4a2f9226a1f3710aa516dff9534d880023218`
- branch: `work/w15-fc0a-consolidated-corrections`
- exact base: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- exact base tree: `674019fbbc21001a2d68deb853c2c0b293e0a5cb`
- one consolidated PR only after the package is complete and exact-head validation is green.

The former P1-01-only order/branch is superseded unused; it was identical to base with no PR at supersession.

The package includes the two mandatory blockers plus confirmed shared/downstream residuals that can be safely brought forward: shell responsiveness, account keyboard regression, Engineering scroll composition, Engineering Lock density, Trends/live-value freshness observability, contextual Help routing, canonical viewOnly wording, frozen FND-06 compatibility consumption in Editor/Script Assistant, structured Script API Help/representative recipe, Runtime Session/Licensing requested/granted UX, and Template/Equipment/Library inspection.

Evidence-bounded/speculative items and future Foundations remain outside the package. Frozen FND-01/02/03/04/06/08 semantics may not be redefined.

Sequential CODEX route updated to rev 0027 / commit `93d79a773d7571677a9f43e6f3a80ad21aa25612`.
No intermediate Main review is required; CODEX returns one final candidate handoff unless a real contract/base/environment blocker prevents safe completion.


## Main decision — six prepared parallel DEV chats / CODEX as sequential validator (2026-09-24)

Product Owner clarified that the previous `normally no more than four active coding DEVs` rule was a historical operational throttle for a different context and is **not** a permanent Wave 15 concurrency limit.

Prepared post-FC0A implementation chats:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05 DEV;
- FND-07 DEV.

Canonical cross-lane coordination:
- branch: `coord/w15-parallel-dev-control`
- file: `docs/WAVE15-PARALLEL-DEV-CONTROL.md`
- prepared control commit: `92f72080a778ede54b85e382a5496cfadb62fb81`

All six are **PREPARED / BLOCKED** until Main records `FC0A_RELEASE_APPROVED` on an exact integrated SHA/tree. No work branch is created before that exact activation base is known.

Execution pipeline:
`normal DEV chat implements code -> Main reviews -> same DEV corrects material defects -> Main accepts exact candidate for CODEX -> sequential scarce CODEX writes/extends tests, runs focused/adversarial local validation and exact-head T1 -> Main finalizes integration`.

CODEX may make small validation-driven corrections that do not redesign the feature. Material product/design defects return to the owning DEV. Frozen-contract insufficiency returns to Main.

Feature DEV results stay in **separate PRs**; they are not combined into a raw monolithic implementation package. After individually accepted/T1-green merges, Main runs broader integrated T2 on the exact integration head.

FND-05/FND-07 also move to normal-chat DEV implementation:
- FND-05 prepared order: `FND05-DEV-HA-AUTHORITY-V1`; dedicated control rev 0004 / commit `f3f685cff16ebfef53db4aa69b061d7bac773829`.
- FND-07 prepared order: `FND07-DEV-DETACH-NEUTRAL-V1`; dedicated control rev 0003 / commit `ae92f5f1ec55464eea7f231c178d0a05dc04df13`.

Foundation validation remains stricter:
`DEV -> Main contract review -> CODEX adversarial/focused validation -> exact-head T1 -> Main merge -> post-merge validation -> VERIFIED/FROZEN`.

FND-05 additionally requires `CODEX_HA_ADVERSARIAL_GREEN`.

Release preparation was updated:
- `coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`
- commit `349ed55e74b237c77dec64971a7f8dac3ee97c7e`.

ROADMAP current coordination update:
- commit `08b11987b78adee134de77906fd57b2af199e5fb`.

Current FC0-A product work remains PR #340 / V2 IN PROGRESS. This coordination preparation does not activate any downstream lane and does not alter the current CODEX mission.

Bootstrap texts for each lane will be generated on Product Owner request; the bootstrap is only onboarding convenience and GitHub live controls remain authoritative.
