# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para MAIN COORDINATOR <-> CODEX / DEV / AUD durante a Wave 15.**
>
> Este arquivo é a **memória operacional persistente e o canal primário de ordens do Main Coordinator**. Issues e PR comments podem espelhar decisões/evidências, mas não substituem a ordem ativa deste arquivo.
>
> **PROTOCOLO `SIGA`:** CODEX, DEV ou AUD deve reler este arquivo no GitHub live antes de agir e executar somente a `CURRENT ORDER` mais recente de sua lane.
>
> **GitHub live é a autoridade final.** Se este documento divergir do repositório/PRs/Actions live, o Main reconstrói o estado e corrige este arquivo antes de emitir a próxima ordem.

**Status date:** 2026-09-17 BRT  
**Wave:** 15 — complete product delivery  
**Integration branch:** `wave15/corrections-integration`  
**Main Coordinator:** único emissor das ordens abaixo

---

## 0. PROTOCOLO OBRIGATÓRIO DE SUCESSÃO DO MAIN COORDINATOR

Esta seção é **binding para qualquer novo chat, modelo ou pessoa que assuma a coordenação principal**.

### 0.1 Bootstrap obrigatório

Antes de coordenar qualquer ação, o novo Main deve:

1. ler **integralmente e no GitHub live** este arquivo;
2. ler/revalidar, conforme relevantes: `PROJECT GOAL.md`, `LAST CHANGE.md`, `README.md`, `docs/README.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`, `docs/ROADMAP.md`, ADRs e handoffs ativos;
3. revalidar `wave15/corrections-integration`, HEAD/tree, PRs, branches de trabalho, issues coordenadoras, Actions, blockers e dependências;
4. ler pelo menos os ledgers #297, #301 e #305 e a evidência do candidate ativo;
5. reconstruir explicitamente: Foundation state, missão ativa, lane owner, exact product base, candidate head/tree, CI, blockers e próximo gate;
6. comparar a reconstrução com este arquivo.

Se GitHub live divergir deste documento, **GitHub live prevalece**. Corrija este arquivo antes da próxima ordem.

Nunca continue apenas por memória de chat, resumo informal ou comentário isolado.

### 0.2 PRODUCT CHECKPOINT vs COORDINATION HEAD

Sempre diferencie:

- **PRODUCT CHECKPOINT SHA/TREE** — último código/infra de produto integrado e validado;
- **COORDINATION HEAD** — HEAD live da integração, que pode avançar só por commits documentais deste handoff.

Commits somente documentais não criam automaticamente um novo product base.

Ao autorizar implementação, declare o exact product base. Se a integração estiver à frente apenas por coordenação/documentação, registre isso; se houver delta de produto/infra, reavalie a base.

### 0.3 Autoridade permanente do Main — comunicação e CI

O Product Owner autoriza permanentemente o Main Coordinator a:

- atualizar este handoff para publicar/alterar ordens;
- enviar/espelhar ordens aos CODEX, DEVs e AUDs em ledgers/PRs quando útil;
- confirmar ao Product Owner somente depois de escrita + readback live;
- **inspecionar, disparar e rerodar CI/GitHub Actions necessária para validação pré-merge e pós-merge**, respeitando as guardas abaixo;
- acompanhar e validar CI no exact candidate SHA, exact merge SHA e exact integration SHA;
- rerodar job específico ou failed jobs quando isso responder a uma hipótese concreta ou a um gate obrigatório.

### 0.4 Guardas permanentes da autoridade de CI

A autoridade de CI do Main é operacional e **não equivale a autoridade de merge**.

O Main pode operar CI sem pedir nova autorização a cada execução, desde que:

1. revalide o exact SHA/ref antes da ação;
2. diagnostique qualquer vermelho antes de rerun;
3. preserve o candidate/merge SHA que está sendo validado;
4. prefira o menor rerun que responda à hipótese (`job` antes de `failed jobs`, quando suficiente);
5. use eventos normais do repositório quando disponíveis;
6. registre run/attempt/job e resultado material;
7. não faça loops de rerun: novo rerun após nova falha exige nova evidência/diagnóstico e razão documentada.

A autorização de CI **não autoriza**:

- alterar código de produto para fazer gate passar sem work package;
- alterar teste apenas para obter verde;
- alterar workflow YAML, filtros ou política de CI para contornar gate;
- commit vazio/artificial para acordar CI;
- rebase/retarget artificial de PR para obter nova execução;
- force push/destructive rebase;
- merge automático;
- escrever em `main`.

Se a CI automática pós-merge não aparecer como esperado, o Main primeiro lê o workflow real e seus triggers/filters. Só depois usa um caminho manual autorizado e tecnicamente equivalente, preservando o exact SHA que precisa ser validado.

### 0.5 Autoridade de merge continua separada

CI verde nunca constitui autorização implícita de merge.

- Merge em branches de integração segue o work package/governança vigente e deve ser explicitamente autorizado quando exigido.
- Merge em `main` permanece protegido e requer autorização final explícita do Product Owner conforme a governança.
- `SIGA`, CI verde, aprovação, freeze ou conclusão de missão não equivalem a autorização de merge em `main`.

### 0.6 Uma ordem só existe depois de persistida e confirmada

O Main não deve dizer “ordem dada” enquanto a instrução existir apenas no chat.

Uma ordem é considerada entregue somente depois de:

1. atualizar a `CURRENT ORDER` da lane correta neste arquivo;
2. a escrita retornar sucesso;
3. fazer readback live e confirmar o texto;
4. opcionalmente espelhar em issue/PR;
5. só então informar ao Product Owner.

### 0.7 `SIGA`

Para CODEX / DEV / AUD:

- reler este arquivo live;
- localizar somente sua `CURRENT ORDER`;
- executar somente a ordem mais recente;
- `ORDER_STATE: WAIT` = nenhuma mutação; revalidar e aguardar;
- `STOP` = devolver evidência solicitada e parar.

Para o Main:

- `SIGA` do Product Owner = continuar autonomamente o fluxo seguro já autorizado;
- revalidar GitHub live antes de decisões materiais;
- operar CI dentro da autoridade permanente acima quando necessário;
- atualizar este arquivo sempre que a ordem mudar;
- parar apenas por blocker real, decisão de Produto, risco material, autorização protegida ausente ou falta de trabalho seguro.

### 0.8 Auditoria e economia de execução

A auditoria arquitetural/contratual e a definição do work package são responsabilidade do Main.

- Main audita estado, contrato, diff, risco, testes e integração;
- CODEX é prioritariamente implementador bounded/corretor bounded;
- DEV implementa somente sua lane;
- AUD revisa/testa candidate indicado pelo Main;
- AUD não substitui auditoria arquitetural do Main.

Quando Codex tiver limite de uso, o Main reduz redescoberta: entrega base, branch, target, invariantes, scope, acceptance e testes já fechados.

### 0.9 Lições que todo sucessor deve preservar

Não repetir:

- ordem só no chat ou só em issue enquanto o handoff fica stale;
- afirmar “ordem dada” sem readback;
- confundir coordination HEAD com product checkpoint;
- delegar auditoria arquitetural aberta ao Codex;
- deixar `CURRENT ORDER` apontando missão concluída;
- rerun cego;
- tratar `PR_READY`/CI verde como `VERIFIED/FROZEN`;
- liberar downstream sem checkpoint exato.

---

## 1. ESTADO LIVE DA FOUNDATION

State machine:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

### Product checkpoint atual

- product checkpoint: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- tree: `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`
- contém merge do PR #330 / Runtime Admission;
- CI pós-merge #1543: Backend/Web/Chromium **PASS**.

Commits posteriores que alterem somente coordenação/documentação não mudam automaticamente esse product checkpoint.

### Foundation

- FND-01 Working/lifecycle/bootstrap — **VERIFIED/FROZEN**
- FND-02 Security Authority, incluindo AUTH-04 — **VERIFIED/FROZEN**
- FND-08 common timing — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **PR_READY / UNDER COORDINATOR VALIDATION**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**Mission:** FND-03 Shared Runtime Seat Accounting — final handoff only

### Exact candidate

- PR: `#331` — `FND-03: enforce shared runtime seat accounting`
- target: `wave15/corrections-integration`
- authorized product base: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- exact candidate head: `09f81e97369089def481ceb25629779a5aba8aff`
- PR remains OPEN / not merged.

### Exact CI evidence — controlled rerun completed GREEN

EliteSCADA CI #1544 / run `35255337014` on the same exact candidate now has latest-attempt jobs:

- Chromium end-to-end `105348050154` — **SUCCESS**
- Web build `105348051287` — **SUCCESS**
- Backend build, test and smoke `105348092685` — **SUCCESS**

No additional CI rerun is authorized or necessary for this candidate at this moment.

### ORDER CODEX-331-HANDOFF-02

CODEX must now:

1. make **no product change**;
2. make **no further rerun**;
3. publish the complete final handoff beginning exactly:

`CODEX -> MAIN COORDINATOR — FND-03 SHARED RUNTIME SEAT ACCOUNTING HANDOFF`

Include:

- exact base/head/tree;
- PR/branch;
- changed files/symbols;
- capacity contract;
- PostgreSQL/in-memory atomicity mechanism;
- Demo/ESLIC2 behavior;
- reason codes;
- schema/migration impact;
- concurrency coverage;
- acceptance matrix `PASS | FAIL | PENDING`;
- local evidence;
- exact Actions run/attempt/job IDs;
- skips/limitations;
- residual risks;
- confirmação explícita de que não criou segunda quota/lease/licensing/Authority authority.

Then **STOP and wait for Main Coordinator integration decision**.

Do not merge, self-freeze FND-03, start another FND-03 slice, begin FND-04 or release FC0-A.

---

## 3. MAIN COORDINATOR -> FND-04 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 DEV

Do not implement FND-04 yet.

On every `SIGA`:

1. reread this file live;
2. revalidate integration;
3. if still `WAIT`, perform no product mutation;
4. begin only when Main changes this section to `ACTIVE` with exact base SHA/tree, branch, scope and acceptance package.

Reserved implementation branch:

`work/w15-fnd-04-script-tag-reference-resolution`

Target:

`wave15/corrections-integration`

FND-04 DEV is the single owner of production implementation for this contract. No self-merge/self-freeze.

---

## 4. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 AUD  
**Default mode:** `READ_ONLY_REVIEW`

On every `SIGA`:

1. reread this file live;
2. if still `WAIT`, do not mutate;
3. when activated, Main supplies exact DEV candidate SHA/tree and `AUD_MODE`;
4. only `AUD_MODE: WRITE_TESTS` autoriza criação de testes, em branch isolada indicada pelo Main;
5. AUD never modifies DEV production code, integration or `main`;
6. AUD never merges or declares `VERIFIED/FROZEN`.

---

## 5. FND-04 BINDING CONTRACT — READY, NOT ACTIVE

Quando ativado, FND-04 deve garantir:

- Python-visible TAG reference humana/canônica, normalmente full path;
- `TagId`/Guid como identidade estável autoritativa;
- um único resolver compartilhado para `tag_read`/`tag_write`;
- binding persistido/versionável `visible reference <-> expected TagId`;
- rename/move/path reuse sem silent retarget;
- `missing/ambiguous/stale/identityDrift` fail closed;
- legacy GUID/TagId explícito e testado;
- Authority preservada;
- nenhum segundo Tag registry/resolver/authorization pipeline;
- contrato consumível por Script Engineering downstream sem redesign Foundation.

Acceptance inclui readable source, shared resolver, stable identity, rename/move/path-reuse negatives, persistence/package round-trip, legacy behavior, Authority preservation e script multi-TAG representativo.

---

## 6. FND-03 REMAINING AFTER SHARED SEAT ACCOUNTING

Mesmo que PR #331 seja integrado/verificado, FND-03 global não congela automaticamente.

Ainda são esperados, em slices bounded:

- license inspect/verify/install/replace/remove lifecycle;
- entitlement reevaluation/fencing quando licença autoritativa muda com Runtime ativo;
- integração com Installation switching #304;
- observability/rejection reasons finais restantes;
- concurrency/negative regressions restantes exigidas por #301.

Product Owner binding: produto ainda não foi lançado; não há base instalada que exija compatibilidade comercial de quotas ESLIC1. ESLIC2 é o contrato comercial de session entitlements; ESLIC1 não recebe quota remota inferida/ilimitada.

---

## 7. PERMANENT GUARDS

- GitHub live é autoridade.
- `main` não recebe merge sem autorização final explícita do Product Owner quando exigida.
- No direct feature-code write to integration; produto entra por PR revisado.
- Main pode atualizar documentação de coordenação e operar CI conforme seção 0.3/0.4.
- Red CI: diagnosticar antes de rerun.
- Exact-head/exact-merge evidence only.
- Required unexecuted test = `PENDING`.
- No force push/destructive rebase/evidence deletion.
- Runtime Session Class/licensing é teto restritivo; Authority é a autoridade de capability.
- `CommandExecute` separado de `ProcessValueWrite`.
- Web Runtime + EliteGO compartilham lease/quota authority; sem pools separados.
- Nenhum segredo/chave privada/session/topology state em `.escadapkg`.
- Stable IDs outrank mutable names/paths.
- No downstream silent redesign of frozen contracts.
- Architectural/contract audit permanece responsabilidade do Main salvo delegação estreita explícita.
- Lane agent nunca escolhe a própria próxima missão.

---

## 8. COMMUNICATION / LEDGER RULE

### Primary live orders

**Este arquivo:** `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

CODEX / DEV / AUD relêem-no a cada `SIGA`.

### Evidence ledgers

- #301 — FND-03 / licensing
- #305 — dependency/checkpoints
- PR conversations — candidate evidence
- #297 — Wave 15 global

Comentários podem espelhar uma ordem, mas não substituem a `CURRENT ORDER` deste arquivo.

### Return discipline

Agents executam a ordem, retornam evidência e param em `STOP`/`WAIT`.

Main promove mission state, autoriza integration/freeze conforme governança e escreve a próxima ordem.

---

## 9. MAIN COORDINATOR REVIEW / CI / INTEGRATION SEQUENCE

Após handoff de agente:

1. revalidar base/head/tree/PR live;
2. revisar diff e scope leakage;
3. revisar testes, negativos e concorrência;
4. revisar exact-head CI;
5. diagnosticar gates vermelhos;
6. usar a autoridade permanente de CI para rerun/trigger quando tecnicamente justificado;
7. integrar somente com evidência bounded suficiente e autoridade de merge aplicável;
8. capturar merge SHA/parents/tree;
9. validar CI pós-merge no exact integrated SHA, operando CI diretamente se necessário dentro das guardas;
10. só então promover `INTEGRATED -> VERIFIED -> FROZEN` do slice bounded;
11. atualizar este arquivo com a próxima ordem;
12. fazer readback e só então confirmar ao Product Owner.

---

## 10. MEMÓRIA OPERACIONAL PERSISTENTE — LIÇÕES

### 10.1 Canal de ordem

Atualizar primeiro este arquivo; espelhar depois.

### 10.2 Confirmação

`write success + live readback` antes de dizer “ordem dada”.

### 10.3 Auditoria

Main audita/especifica; implementadores implementam; AUD verifica candidate.

### 10.4 Stale handoff

Merge/checkpoint/decisão que muda missão ativa exige atualização deste arquivo antes do próximo `SIGA`.

### 10.5 Product checkpoint vs documentation HEAD

Acompanhar separadamente; commit documental não altera automaticamente product base.

### 10.6 CI

Main está permanentemente autorizado a operar CI pré-merge e pós-merge, mas deve diagnosticar antes de rerun, preservar exact SHA e nunca usar CI para contornar contrato/gate. CI verde não autoriza merge.

### 10.7 Mission state

`PR_READY != INTEGRATED != VERIFIED != FROZEN`.

### 10.8 Escassez de Codex

Use Codex para código/validação onde agrega mais; Main prepara work packages e pode usar DEV/AUD normais sob o mesmo rigor.

---

For Product Owner control, Main Coordinator reports current America/Sao_Paulo time as:

`Hora: HH:MM`
