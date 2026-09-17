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

Commit documental não cria novo product base.

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

### 0.5 Ordem persistida

O Main só afirma `ordem dada` depois de atualizar a `CURRENT ORDER`, obter write success, fazer readback live e confirmar o conteúdo. Issues/PRs são espelhos opcionais.

### 0.6 `SIGA`

Para CODEX/DEV/AUD: reler este arquivo live e executar somente sua `CURRENT ORDER`; `WAIT` = nenhuma mutação; `STOP` = devolver evidência e parar.

Para Main: `SIGA` = continuar autonomamente o fluxo seguro autorizado, revalidando GitHub live antes de decisão material.

### 0.7 Auditoria / economia de execução

Auditoria arquitetural/contratual e work package pertencem ao Main. CODEX é prioritariamente implementador/corretor bounded; DEV implementa sua lane; AUD revisa/testa candidate indicado. Quando Codex tiver limite, Main reduz redescoberta e entrega pacote fechado.

### 0.8 Lições permanentes

Não repetir: ordem só no chat/issue; afirmar ordem sem readback; confundir coordination HEAD com product checkpoint; delegar auditoria aberta ao Codex; rerun cego; tratar `PR_READY`, CI verde, `INTEGRATED`, `VERIFIED`, `FROZEN` como equivalentes; inferir PASS de acceptance PENDING; liberar downstream sem checkpoint exato.

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
  - Backend build, test and smoke `105366045111` — **SUCCESS**
  - Web build `105366045298` — **SUCCESS**
  - Chromium end-to-end `105366583742` — **SUCCESS**

### COORDINATION HEAD antes desta ordem

- `wave15/corrections-integration@9e3f6770be19523c82b82ae8f5003407287bf324`
- delta `a7067ac9... -> 9e3f6770...`: apenas `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
- portanto o exact product base para o próximo slice permanece `a7067ac9...`.

### Foundation

- FND-01 — **VERIFIED/FROZEN**
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **VERIFIED/FROZEN**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**Mission:** FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing v1

### Exact authority

- authorized product base: `a7067ac99f9f88fcd17f740b915d8c4f57c556fc`
- product tree: `eed22a377fea2778d3e78143d706e4de0ef9ce38`
- current integration seed HEAD at authorization: `9e3f6770be19523c82b82ae8f5003407287bf324` — docs-only delta from product checkpoint
- work branch: `work/w15-fnd-03-license-lifecycle-fencing-v1`
- target: `wave15/corrections-integration`

Create/use the work branch from the live integration HEAD above. Treat `a7067ac9...` as the exact product contract being extended.

### Main audit — current source truth

Main already audited the exact product checkpoint. Do not spend a Codex round repeating an open-ended audit.

Current canonical facts:

1. `FileProductLicenseService` is the single installed-license authority. `CurrentVerification` reads the machine-local license file; missing file means Demo; invalid installed file means fail-closed Invalid.
2. `InstallLicense` validates candidate **before** writing a temp file and atomic-ish overwrite. `RemoveLicense` deletes the installed file. Candidate verification remains private to that service.
3. `IProductLicenseService` currently exposes `CurrentVerification`, `InstallLicense`, `RemoveLicense`, machine fingerprint/request and Runtime tag entitlement evaluation; there is no public candidate-verify primitive.
4. `ProductLicensingApi` exposes status/request/install/remove. Today all routes use `RequireWorkspaceEngineeringRead`; mutation must be corrected to canonical `EngineeringModify` authorization rather than EngineeringView-only access.
5. `ProductLicensedRuntimeCoordinator` evaluates product entitlement on explicit Runtime activation and Demo expiry, but there is **no** license-change re-evaluation path for an already-active Runtime.
6. Runtime session admission reads current licensing for new admission, but an already-active logical lease is validated only against lease subject/client/runtime identity; there is **no license-authority change fence**.
7. `IRuntimeSessionLeaseStore` supports per-lease terminate only; there is no bulk authoritative fence for a license mutation.
8. #304 requires: keep license across project switch; deliberate remove -> Demo (not Invalid); valid replace only after verification; invalid/tampered/wrong-machine replacement preserves current valid license; active Runtime must be fenced/re-evaluated without temporary unlicensed or expanded-entitlement execution.

### Binding architecture for this slice

Use the existing `IProductLicenseService` / `FileProductLicenseService` as the **only** license verifier/store. Do not create another license registry, trust anchor, signature path or quota authority.

Implement the smallest host-owned lifecycle/orchestration boundary needed to make license mutation safe. Exact class names are implementation-defined, but the invariants below are binding.

#### A. Candidate verification without mutation

Expose a server-owned candidate verification primitive that reuses the exact canonical verifier and returns typed/non-secret result sufficient for UI/downstream lifecycle.

It must not install, remove or alter the current license.

#### B. Transactional replace/install

A valid candidate may replace/install only after full schema/signature/key/machine/expiry verification.

Invalid/tampered/wrong-machine/expired candidate:

- must not alter the installed valid license;
- must not fence healthy Runtime/sessions merely because an invalid candidate was submitted;
- returns deterministic failure reason without raw secret/license leakage.

#### C. Deliberate remove

Successful deliberate remove produces the canonical no-file **Demo** state, distinct from Invalid installed license.

#### D. Runtime/session fencing on successful authority change

For any successful installed-license authority change that changes/removes the authoritative license:

- all currently active **remote Runtime logical leases** must be fenced/invalidated deterministically before the lifecycle operation is considered complete;
- stale REST/WSS clients must not retain command/write authority after the successful operation returns;
- clients re-admit under the new license/Demo quotas;
- no selective hidden Web/EliteGO pool is introduced.

A simple fail-closed policy of fencing all remote logical leases on successful authority change is acceptable and preferred over inventing a complex seat-preservation policy in this slice.

#### E. Active local Runtime entitlement re-evaluation

After a successful license authority change, the active product Runtime must be re-evaluated against the **new** canonical authority:

- if the current Runtime is not allowed by the new tag/product entitlement, stop/fence it deterministically before the lifecycle call completes;
- if transition is to Demo and the current Runtime remains allowed, Demo continuous-runtime timing begins from the successful authority transition, not from an old unrelated activation timestamp;
- if a valid replacement continues to allow the active Runtime, status must reflect the new license authority without requiring host reinstall;
- no temporary unlicensed/expanded-entitlement execution window is allowed.

Implementation must coordinate license mutation, session fencing and active Runtime re-evaluation under one host-owned lifecycle gate/transactional sequence. Do not rely on timing luck between independent HTTP requests.

#### F. Authorization / audit

- read-only licensing status/request/candidate inspection may use the established readable authority as appropriate;
- install/replace/remove are mutations and must require canonical `SecurityCapability.EngineeringModify` at minimum, not EngineeringView-only authorization;
- preserve Engineering Lock behavior where the existing product mutation model requires it;
- record audit/system evidence for successful/denied mutation without logging raw license codes, private/signing material or credentials.

### Required deterministic acceptance

At minimum prove all as `PASS | FAIL | PENDING`:

1. candidate verify valid ESLIC2 succeeds without changing installed state;
2. candidate verify tampered/wrong-key/wrong-machine/expired/malformed fails without mutation;
3. valid install from Demo becomes authoritative;
4. valid A -> valid B replacement commits B only after B verifies;
5. invalid replacement while A is valid leaves A byte-for-byte/effectively authoritative and does not disrupt Runtime/leases;
6. deliberate remove enters Demo, not Invalid;
7. keep-license/project switch boundary remains independent: no package/Authority operation silently removes machine license;
8. successful install/replace/remove fences all pre-change remote logical leases; old session IDs fail afterward;
9. concurrent admission vs license downgrade/remove cannot leave a post-return stale lease or quota expansion window;
10. after successful change, new admissions use only new ESLIC2/Demo effective totals;
11. active Runtime exceeding new tag entitlement is stopped/fenced deterministically;
12. active Runtime allowed after valid replacement continues with status reflecting new authority;
13. active Runtime transitioning to Demo starts a fresh bounded Demo allowance from authority-change time;
14. install/replace/remove endpoint denies a principal with EngineeringView but without EngineeringModify;
15. mutation audit contains operation/result/actor-safe metadata but no raw license code/signing material;
16. ESLIC2 signature/hardware/expiry/schema and Shared Seat Accounting regressions remain green;
17. `.escadapkg` remains free of license/key/session state;
18. exact-head CI green.

### Scope boundaries

Do **not** implement in this slice:

- License Generator UI;
- full Installation detach/switch UX from #304;
- Authority A -> B transition;
- Historian cleanup/switching;
- EliteGO UI;
- HA election/fencing;
- FND-04;
- main merge.

Do not broadly remove ESLIC1 parser compatibility as cleanup. Product Owner has already ruled that ESLIC1 receives no inferred remote session quota; this slice must not create legacy commercial semantics.

### Handoff

Open a bounded PR targeting `wave15/corrections-integration` and return beginning exactly:

`CODEX -> MAIN COORDINATOR — FND-03 LICENSE LIFECYCLE FENCING HANDOFF`

Include:

- exact base/head/tree and PR;
- files/symbols changed;
- lifecycle transaction/gate mechanism;
- candidate verify contract;
- install/replace/remove semantics;
- Runtime re-evaluation/fencing path;
- session bulk-fence mechanism and concurrency proof;
- authorization/audit evidence;
- acceptance matrix `PASS | FAIL | PENDING`;
- local tests and exact Actions run/job IDs;
- schema/migration impact;
- residual risks and explicit non-actions;
- confirmation that no second licensing/session/quota/Authority authority was created.

Do not self-merge or self-freeze FND-03. Then **STOP** for Main review.

---

## 3. MAIN COORDINATOR -> FND-04 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 DEV

Do not implement FND-04 yet. On every `SIGA`, reler este arquivo live. Só iniciar quando Main mudar esta seção para `ACTIVE` com exact product base SHA/tree, branch, scope e acceptance.

Reserved branch: `work/w15-fnd-04-script-tag-reference-resolution`  
Target: `wave15/corrections-integration`.

FND-04 DEV é o único owner de produção desse contrato; sem self-merge/self-freeze.

---

## 4. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 AUD  
**Default mode:** `READ_ONLY_REVIEW`

Não auditar candidate especulativo e não escrever testes enquanto `WAIT`. Quando ativado, Main fornecerá exact DEV candidate SHA/tree e `AUD_MODE`. Somente `AUD_MODE: WRITE_TESTS` autoriza testes em branch isolada. AUD nunca modifica produção do DEV, integration ou `main`, e nunca mergeia/congela.

---

## 5. FND-04 BINDING CONTRACT — READY, NOT ACTIVE

Quando ativado, FND-04 deve garantir:

- referência TAG Python-visible humana/canônica, normalmente full path;
- `TagId`/Guid como identidade estável autoritativa;
- um único resolver compartilhado `tag_read`/`tag_write`;
- binding persistido/versionável `visible reference <-> expected TagId`;
- rename/move/path reuse sem silent retarget;
- `missing/ambiguous/stale/identityDrift` fail closed;
- legacy GUID/TagId explícito e testado;
- Authority preservada;
- nenhum segundo Tag registry/resolver/authorization pipeline;
- contrato consumível downstream sem redesign Foundation.

---

## 6. FND-03 REMAINING AFTER CURRENT LIFECYCLE SLICE

Após o lifecycle/fencing ser integrado/verificado, Main deve reavaliar #301 para determinar se FND-03 pode fechar ou se ainda exige um último bounded closeout. Itens esperados para revisão final:

- observability/rejection reasons e counters finais;
- client/admission heartbeat/reuse residuals;
- integração contratual suficiente para #304 sem implementar seu UX;
- regressões negativas/concurrency finais.

FND-03 só vira `VERIFIED/FROZEN` global quando todos os critérios de #301 estiverem comprovados no exact integration checkpoint.

---

## 7. PERMANENT GUARDS

- GitHub live é autoridade.
- `main` não recebe merge sem autorização protegida aplicável.
- Produto entra na integration por PR revisado; sem feature write direto.
- Main pode atualizar documentação de coordenação e operar CI conforme seção 0.
- Red CI: diagnosticar antes de rerun.
- Evidência no exact candidate/merge SHA.
- Required unexecuted test = `PENDING`.
- No force push/destructive rebase/evidence deletion.
- Runtime Session Class/licensing é teto restritivo; Authority é capability authority.
- `CommandExecute` separado de `ProcessValueWrite`.
- Web Runtime + EliteGO compartilham lease/quota authority.
- Nenhum segredo/chave/session/topology state em `.escadapkg`.
- Stable IDs outrank mutable names/paths.
- No downstream silent redesign of frozen contracts.
- Uma lane não escolhe sua próxima missão.

---

## 8. LEDGERS / RETORNO

Canal primário: este arquivo.

Ledgers de evidência:

- #297 — Wave 15 global
- #301 — FND-03/licensing
- #305 — dependency/checkpoints
- #304 — Installation/lifecycle consumer requirements
- PR conversation — candidate-local evidence

Agents executam a ordem, retornam evidência e param quando a ordem diz `STOP`/`WAIT`. Main promove estados e escreve a próxima ordem.

`Hora: HH:MM` em `America/Sao_Paulo`.
