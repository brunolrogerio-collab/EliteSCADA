# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para MAIN COORDINATOR <-> CODEX / DEV / AUD durante a Wave 15.**
>
> Este arquivo é a memória operacional persistente e o **canal primário de ordens** do Main Coordinator. Issues/PR comments podem espelhar decisões e evidências, mas não substituem a `CURRENT ORDER` deste arquivo.
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
5. reconstruir explicitamente Foundation state, missão ativa, lane owner, exact product base, candidate head/tree, CI, blockers e próximo gate;
6. corrigir qualquer estado stale deste arquivo antes de emitir nova ordem.

GitHub live prevalece sobre memória de chat, resumo, comentário isolado ou SHA histórico.

### 0.2 PRODUCT CHECKPOINT vs COORDINATION HEAD

Sempre separar:

- **PRODUCT CHECKPOINT SHA/TREE** — último código/infra de produto integrado e validado;
- **COORDINATION HEAD** — HEAD live da integração, que pode avançar apenas por documentação de coordenação.

Commit documental não cria automaticamente novo product base. Ao autorizar implementação, declarar exact product base; se houver delta de produto/infra na integração, reavaliar a base.

### 0.3 Autoridade permanente do Main — comunicação e CI

Product Owner autorizou permanentemente o Main Coordinator a:

- atualizar este handoff para publicar/alterar ordens;
- enviar/espelhar ordens a CODEX/DEVs/AUDs em issues/PRs;
- confirmar uma ordem somente depois de `write success + live readback`;
- inspecionar, disparar e rerodar CI/GitHub Actions de validação pré-merge e pós-merge;
- validar exact candidate SHA, exact merge SHA e exact integration SHA;
- rerodar job/failed jobs quando houver hipótese concreta ou gate obrigatório.

Guardas de CI:

- diagnosticar vermelho antes de rerun;
- preservar exact SHA/ref;
- usar o menor rerun suficiente;
- registrar run/attempt/job e resultado;
- nenhum loop cego de rerun;
- não alterar código/teste/workflow para obter verde;
- não criar commit artificial;
- não retarget/rebase artificialmente;
- sem force push/destructive rebase.

**Autoridade de CI não equivale a autoridade de merge.** CI verde nunca autoriza merge por si só.

### 0.4 Merge / `main`

- Integração de produto deve seguir PR revisado e ordem binding.
- `main` permanece protegido: `SIGA`, CI verde, aprovação, freeze ou conclusão de missão **não** autorizam merge em `main`.
- Merge final em `main` exige autorização explícita do Product Owner quando a governança assim exigir.

### 0.5 Uma ordem só existe depois de persistida

O Main só pode afirmar `ordem dada` depois de:

1. atualizar a `CURRENT ORDER` da lane neste arquivo;
2. obter sucesso da escrita;
3. fazer readback live e confirmar o conteúdo;
4. opcionalmente espelhar em issue/PR.

### 0.6 `SIGA`

Para CODEX/DEV/AUD: reler este arquivo live e executar somente sua `CURRENT ORDER`; `WAIT` = nenhuma mutação; `STOP` = devolver evidência e parar.

Para Main: `SIGA` do Product Owner = continuar autonomamente o fluxo seguro já autorizado, revalidando GitHub live antes de decisão material.

### 0.7 Auditoria / economia de execução

A auditoria arquitetural/contratual e a definição do work package pertencem ao Main. CODEX é prioritariamente implementador/corretor bounded; DEV implementa sua lane; AUD revisa/testa candidate indicado. Quando Codex tiver limite de uso, o Main reduz redescoberta e entrega pacote fechado.

### 0.8 Lições permanentes

Não repetir:

- ordem apenas no chat/issue com handoff stale;
- afirmar ordem sem readback;
- confundir coordination HEAD com product checkpoint;
- delegar auditoria arquitetural aberta ao Codex;
- rerun cego;
- tratar `PR_READY`, CI verde, `INTEGRATED`, `VERIFIED` e `FROZEN` como equivalentes;
- transformar acceptance obrigatório `PENDING` em `PASS` por inferência;
- liberar downstream sem checkpoint exato.

State machine:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

---

## 1. ESTADO LIVE DA FOUNDATION

### Product checkpoint atual

- `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- tree `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`
- contém PR #330 / Runtime Admission;
- CI pós-merge #1543: Backend/Web/Chromium PASS.

### Coordination HEAD antes desta ordem

- `wave15/corrections-integration@973010698d6a883eb31124cf6654a7f73f380c71`
- comparação `6f02b9e3... -> 97301069...`: 9 commits, somente:
  - `LAST CHANGE.md`
  - `docs/CURRENT-COORDINATOR-HANDOFF.md`
  - `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
  - `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
- não existe delta de produto/infra concorrente desde o authorized product base do PR #331.

### Foundation

- FND-01 — **VERIFIED/FROZEN**
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **PR_READY / APPROVED FOR INTEGRATION**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**Mission:** FND-03 Shared Runtime Seat Accounting — integrate approved PR #331

### Approved exact candidate

- PR: `#331` — `FND-03: enforce shared runtime seat accounting`
- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- target: `wave15/corrections-integration`
- authorized product base / merge-base: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- approved candidate HEAD: `6789a989c85e7210c167945665f4ab6c8cef53a0`
- approved candidate tree: `bd6d79490d7fc0630737fb834fa6b8fc95b7b8d9`
- PR live at Main review: OPEN / `mergeable=true` / `mergeable_state=clean` / not draft / not merged
- review threads: none.

### Acceptance-close evidence

Delta from previously reviewed production candidate:

`09f81e97369089def481ceb25629779a5aba8aff -> 6789a989c85e7210c167945665f4ab6c8cef53a0`

is exactly one file:

`tests/Scada.Drivers.Tests/DistributedRuntimeFoundationTests.cs`

with +59 test lines and **zero production-file changes**.

Former PENDING criterion is now PASS through:

- `SeatCapacity_WebAndEliteGoLogicalClientsShareTheSameClassPools`
- `SeatCapacity_HighConcurrencyMixedWebAndEliteGoIdentities_NeverOversubscribesOrDuplicatesLeases`

The second test runs 64 distinct mixed `web-*` / `elitego-*` identities against 2 Interactive + 2 ViewOnly and proves exact class totals, exhaustion, bounded active leases and uniqueness.

### Exact-head CI #1545

Run `35267768938` on `6789a989...`:

- Backend build, test and smoke `105359117658` — **SUCCESS**
- Web build `105359118013` — **SUCCESS**
- Chromium end-to-end `105359631809` — **SUCCESS**

### Main decision

**PR #331 is approved for integration of the bounded Shared Runtime Seat Accounting slice.**

### ORDER CODEX-331-MERGE-04

CODEX must:

1. revalidate immediately before merge that PR #331 still points to exact head `6789a989c85e7210c167945665f4ab6c8cef53a0`, remains open/clean/mergeable, and target remains `wave15/corrections-integration`;
2. merge **only PR #331** through the normal PR route into `wave15/corrections-integration`;
3. do not alter/rebase/retarget the candidate and do not include any other PR;
4. do not mutate `main`;
5. after merge, report exact merge SHA, parents, tree and exact new integration HEAD;
6. do **not** manually rerun CI — Main Coordinator owns post-merge CI operation/validation under permanent CI authority;
7. do not self-mark the slice `VERIFIED/FROZEN`;
8. do not start another FND-03 slice;
9. do not start FND-04;
10. do not release FC0-A.

Return beginning exactly:

`CODEX -> MAIN COORDINATOR — FND-03 SHARED RUNTIME SEAT ACCOUNTING MERGE HANDOFF`

Then **STOP**. Main will validate the exact integrated SHA and promote the slice only after post-merge evidence.

---

## 3. MAIN COORDINATOR -> FND-04 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 DEV

Do not implement FND-04 yet. On every `SIGA`, reler este arquivo live. Só iniciar quando Main mudar esta seção para `ACTIVE` e fornecer exact product base SHA/tree, branch, scope e acceptance.

Reserved branch:

`work/w15-fnd-04-script-tag-reference-resolution`

Target: `wave15/corrections-integration`.

FND-04 DEV será o único owner de produção do contrato; sem self-merge/self-freeze.

---

## 4. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 AUD  
**Default mode:** `READ_ONLY_REVIEW`

Não auditar candidate especulativo e não escrever testes enquanto `WAIT`. Quando ativado, Main fornecerá exact DEV candidate SHA/tree e `AUD_MODE`. Somente `AUD_MODE: WRITE_TESTS` autoriza testes em branch isolada indicada pelo Main. AUD nunca modifica produção do DEV, integration ou `main`, e nunca mergeia/congela.

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

FND-04 permanece bloqueado até ordem ACTIVE com exact integrated product base.

---

## 6. FND-03 REMAINING AFTER SHARED SEAT ACCOUNTING

Mesmo após #331 integrado/verificado, FND-03 global não congela automaticamente. Restam slices bounded esperados:

- license inspect/verify/install/replace/remove lifecycle;
- entitlement reevaluation/fencing quando licença autoritativa muda com Runtime ativo;
- integração com Installation switching #304;
- observability/rejection reasons finais ainda ausentes;
- regressões negativas/concurrency restantes exigidas por #301.

Product Owner binding: produto ainda não foi lançado; não há base instalada que exija compatibilidade comercial de quotas ESLIC1. ESLIC2 é o contrato comercial de session entitlements; ESLIC1 não recebe quota remota inferida/ilimitada.

---

## 7. PERMANENT GUARDS

- GitHub live é autoridade.
- `main` não recebe merge sem autorização protegida aplicável.
- Produto entra em integration por PR revisado; sem feature write direto na integration.
- Main pode atualizar documentação de coordenação e operar CI conforme seção 0.
- Red CI: diagnosticar antes de rerun.
- Evidência sempre no exact candidate/merge SHA.
- Required unexecuted test = `PENDING`.
- No force push/destructive rebase/evidence deletion.
- Runtime Session Class/licensing é teto restritivo; Authority é capability authority.
- `CommandExecute` separado de `ProcessValueWrite`.
- Web Runtime + EliteGO compartilham lease/quota authority; sem pools separados.
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
- PR conversation — candidate-local evidence

Agents executam a ordem, retornam evidência e param quando a ordem diz `STOP`/`WAIT`. Main promove estados e escreve a próxima ordem.

Ao Product Owner, após mudança de ordem, confirmar somente depois de readback live.

`Hora: HH:MM` em `America/Sao_Paulo`.