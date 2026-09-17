# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para MAIN COORDINATOR <-> CODEX / DEV / AUD durante a Wave 15.**
>
> Este arquivo é a memória operacional persistente e o canal primário de ordens do Main Coordinator. Issues e PR comments podem espelhar decisões/evidências, mas não substituem a ordem ativa deste arquivo.
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

Esta seção é binding para qualquer novo chat, modelo ou pessoa que assuma a coordenação principal.

### 0.1 Bootstrap obrigatório

Antes de coordenar qualquer ação, o novo Main deve:

1. ler integralmente e no GitHub live este arquivo;
2. ler/revalidar, conforme relevantes: `PROJECT GOAL.md`, `LAST CHANGE.md`, `README.md`, `docs/README.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`, `docs/ROADMAP.md`, ADRs e handoffs ativos;
3. revalidar integration HEAD/tree, PRs, work branches, issues coordenadoras, Actions, blockers e dependências;
4. ler pelo menos os ledgers #297, #301 e #305 e a evidência do candidate ativo;
5. reconstruir explicitamente: Foundation state, missão ativa, lane owner, exact product base, candidate head/tree, CI, blockers e próximo gate;
6. comparar essa reconstrução com este arquivo.

Se GitHub live divergir deste documento, **GitHub live prevalece**. Corrija este arquivo antes da próxima ordem. Nunca continue apenas por memória de chat, resumo informal ou comentário isolado.

### 0.2 PRODUCT CHECKPOINT vs COORDINATION HEAD

Sempre diferencie:

- **PRODUCT CHECKPOINT SHA/TREE** — último código/infra de produto integrado e validado;
- **COORDINATION HEAD** — HEAD live da integração, que pode avançar só por commits documentais de coordenação.

Commits somente documentais não criam automaticamente um novo product base. Ao autorizar implementação, declare o exact product base. Se a integração estiver à frente apenas por coordenação/documentação, registre isso; se houver delta de produto/infra, reavalie a base.

### 0.3 Autoridade permanente do Main — comunicação e CI

O Product Owner autoriza permanentemente o Main Coordinator a:

- atualizar este handoff para publicar/alterar ordens;
- enviar/espelhar ordens aos CODEX, DEVs e AUDs em ledgers/PRs quando útil;
- confirmar ao Product Owner somente depois de escrita + readback live;
- inspecionar, disparar e rerodar CI/GitHub Actions necessária para validação pré-merge e pós-merge;
- acompanhar e validar CI no exact candidate SHA, exact merge SHA e exact integration SHA;
- rerodar job específico ou failed jobs quando isso responder a hipótese concreta ou gate obrigatório.

### 0.4 Guardas permanentes da autoridade de CI

A autoridade de CI do Main não equivale a autoridade de merge.

O Main pode operar CI sem pedir nova autorização a cada execução, desde que:

1. revalide exact SHA/ref;
2. diagnostique vermelho antes de rerun;
3. preserve o SHA que está sendo validado;
4. prefira o menor rerun suficiente;
5. use eventos normais do repositório quando disponíveis;
6. registre run/attempt/job e resultado material;
7. não faça loops cegos de rerun.

A autorização de CI não autoriza alterar código/teste/workflow para obter verde, commit artificial, retarget/rebase artificial, force push, merge automático ou escrita em `main`.

### 0.5 Autoridade de merge continua separada

CI verde nunca constitui autorização implícita de merge. Merge em branches de integração segue o work package/governança vigente. Merge em `main` permanece protegido e requer autorização final explícita do Product Owner quando exigida. `SIGA`, CI verde, aprovação, freeze ou conclusão de missão não equivalem a autorização de merge em `main`.

### 0.6 Uma ordem só existe depois de persistida e confirmada

Uma ordem é considerada entregue somente depois de:

1. atualizar a `CURRENT ORDER` da lane correta neste arquivo;
2. a escrita retornar sucesso;
3. fazer readback live e confirmar o texto;
4. opcionalmente espelhar em issue/PR;
5. só então informar ao Product Owner.

### 0.7 `SIGA`

Para CODEX / DEV / AUD: reler este arquivo live, localizar somente sua `CURRENT ORDER`, executar somente a ordem mais recente; `WAIT` = nenhuma mutação; `STOP` = devolver evidência e parar.

Para o Main: `SIGA` = continuar autonomamente o fluxo seguro já autorizado, revalidar GitHub live antes de decisões materiais, operar CI dentro da autoridade permanente e atualizar este arquivo sempre que a ordem mudar.

### 0.8 Auditoria e economia de execução

A auditoria arquitetural/contratual e a definição do work package são responsabilidade do Main. CODEX é prioritariamente implementador/corretor bounded; DEV implementa somente sua lane; AUD revisa/testa candidate indicado pelo Main. Quando Codex tiver limite de uso, o Main reduz redescoberta e entrega work package já fechado.

### 0.9 Lições permanentes

Não repetir:

- ordem só no chat ou só em issue enquanto o handoff fica stale;
- afirmar “ordem dada” sem readback;
- confundir coordination HEAD com product checkpoint;
- delegar auditoria arquitetural aberta ao Codex;
- deixar `CURRENT ORDER` apontando missão concluída;
- rerun cego;
- tratar `PR_READY`/CI verde como `VERIFIED/FROZEN`;
- liberar downstream sem checkpoint exato;
- transformar um acceptance obrigatório `PENDING` em `PASS` por inferência.

---

## 1. ESTADO LIVE DA FOUNDATION

State machine:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

### Product checkpoint atual

- product checkpoint: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- tree: `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`
- contém PR #330 / Runtime Admission;
- CI pós-merge #1543: Backend/Web/Chromium PASS.

### Coordination HEAD live na última revisão

- `wave15/corrections-integration@4917ef65fd857e4dccac90fe385a619538d38d1d`
- comparação `6f02b9e3... -> 4917ef65...`: 6 commits e **somente** `LAST CHANGE.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`, `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
- portanto o authorized product base do PR #331 permanece `6f02b9e3...`.

### Foundation

- FND-01 Working/lifecycle/bootstrap — **VERIFIED/FROZEN**
- FND-02 Security Authority incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 common timing — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **PR_READY / CORRECTION TEST-ONLY REQUIRED**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: ACTIVE**  
**Mission:** FND-03 Shared Runtime Seat Accounting — close required concurrency acceptance with tests only

### Exact candidate reviewed

- PR: `#331` — `FND-03: enforce shared runtime seat accounting`
- authorized product base: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- candidate branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- reviewed candidate head: `09f81e97369089def481ceb25629779a5aba8aff`
- reviewed candidate tree: `8a81cb965f754fb4299800333f8a5a5c28d1044e`
- target: `wave15/corrections-integration`
- raw GitHub PR state at review: OPEN / mergeable=true / mergeable_state=clean / not merged.
- changed surface: 8 files, only Runtime seat-accounting source/tests; no overlap with the coordination-only commits after the product base.

### Exact CI evidence on reviewed head

EliteSCADA CI #1544 / run `35255337014`, latest attempt on unchanged head:

- Chromium E2E `105348050154` — SUCCESS
- Web `105348051287` — SUCCESS
- Backend build/test/smoke `105348092685` — SUCCESS

### Main review decision

The implementation architecture is bounded and consistent with the approved design: existing logical lease ledger remains the single seat ledger; in-memory uses its gate; PostgreSQL uses the existing advisory-lock transaction; no second quota/licensing/Authority/session authority was introduced.

However, the final Codex handoff explicitly marked one binding acceptance item as **PENDING**:

`Explicit named Web-vs-EliteGO mixed-client test and high-concurrency distinct mixed identities`.

This work package had required both shared Web+EliteGO pools and high-concurrency mixed admissions. Required but unexecuted evidence remains `PENDING`; Main will not infer PASS from implementation brand-agnosticism.

### ORDER CODEX-331-TEST-CLOSE-03

Perform **tests-only correction** on the existing PR #331 branch.

Required tests:

1. **Explicit Web + EliteGO shared-pool test**
   - use distinct logical identities whose `clientInstanceId` values are visibly mixed, e.g. `web-*` and `elitego-*`;
   - prove both client families consume the exact same Interactive/ViewOnly pools;
   - prove there is no brand-specific/separate capacity pool.

2. **High-concurrency distinct-identity oversubscription test**
   - run many concurrent admissions with distinct logical identities, mixing `web-*` and `elitego-*` names;
   - assert admitted Interactive count never exceeds configured Interactive total;
   - assert admitted ViewOnly count never exceeds configured ViewOnly total;
   - assert total active logical leases never exceeds eligible licensed capacity;
   - assert no duplicate logical lease identity/session artifacts are produced.

Existing PostgreSQL cross-instance last-seat race test remains valid evidence for multi-store atomicity and does not need redesign. Prefer the smallest deterministic additional test surface that closes the PENDING criterion.

### Binding constraints

- **Do not modify production code** unless a newly added required test exposes a real product defect. If that occurs, STOP and return `BLOCKED-TEST-REVEALED-DEFECT` with evidence before changing product code.
- Do not change licensing semantics, seat policy, Authority, lease identity, REST/WSS behavior or reason codes.
- Do not touch C04 or unrelated tests.
- Do not change workflow YAML or CI policy.
- Do not rebase/retarget for cosmetic reasons.
- Do not merge PR #331.
- Do not start another FND-03 slice.
- Do not start FND-04.
- Do not release FC0-A.

### Validation and return

After adding only the required tests:

1. run the smallest focused local tests that exercise them;
2. let normal PR CI validate the **new exact head**;
3. if CI is green, return beginning exactly:

`CODEX -> MAIN COORDINATOR — FND-03 SHARED RUNTIME SEAT ACCOUNTING ACCEPTANCE-CLOSE HANDOFF`

Include new head/tree, exact test names, PASS evidence for the formerly PENDING criterion, full exact-head Actions run/attempt/job IDs, and confirmation of **zero production-file changes** versus `09f81e97369089def481ceb25629779a5aba8aff`.

Then STOP for Main integration decision.

If a required test exposes a product defect, return instead:

`CODEX -> MAIN COORDINATOR — FND-03 SHARED RUNTIME SEAT ACCOUNTING BLOCKED-TEST-REVEALED-DEFECT`

and STOP before product correction.

---

## 3. MAIN COORDINATOR -> FND-04 DEV — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 DEV

Do not implement FND-04 yet. On every `SIGA`, reler este arquivo. Só iniciar quando Main mudar esta seção para `ACTIVE` e fornecer exact product base SHA/tree, branch, scope e acceptance.

Reserved implementation branch:

`work/w15-fnd-04-script-tag-reference-resolution`

Target: `wave15/corrections-integration`.

FND-04 DEV será o único owner de implementação de produção do contrato. Sem self-merge/self-freeze.

---

## 4. MAIN COORDINATOR -> FND-04 AUD — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Lane:** FND-04 AUD  
**Default mode:** `READ_ONLY_REVIEW`

Não auditar candidate especulativo e não escrever testes enquanto `WAIT`. Quando ativado, Main fornecerá exact DEV candidate SHA/tree e `AUD_MODE`. Somente `AUD_MODE: WRITE_TESTS` autoriza criação de testes, em branch isolada indicada pelo Main. AUD nunca modifica produção do DEV, integração ou `main`, e nunca mergeia/congela.

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

Mesmo após PR #331 integrado/verificado, FND-03 global não congela automaticamente. Ainda são esperados, em slices bounded:

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
- Architectural/contract audit remains Main responsibility unless narrowly delegated.
- Lane agent never chooses its own next mission.

---

## 8. COMMUNICATION / LEDGER RULE

### Primary live orders

`docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`

### Historical/evidence ledgers

- #301 — FND-03 / licensing
- #305 — dependency/checkpoint
- PR conversations — candidate-local evidence
- #297 — Wave 15 global

Comments can mirror orders for traceability; agents use this file as canonical active-order source.

### Return discipline

Agents execute active order, return evidence and STOP/WAIT. Main alone promotes mission state, authorizes integration/freeze within governance and writes next order.

---

## 9. MAIN COORDINATOR REVIEW SEQUENCE

After agent handoff:

1. revalidate exact base/head/tree/PR live;
2. inspect real diff and scope leakage;
3. inspect relevant tests and negative/concurrency coverage;
4. inspect exact-head CI;
5. diagnose red gates before rerun;
6. integrate only with bounded evidence sufficient;
7. capture merge SHA/parents/tree;
8. validate post-merge CI on exact integrated SHA;
9. only then promote/freeze the bounded slice;
10. update this file with the next binding order;
11. read back live before reporting “ordem dada”.

---

## 10. MEMÓRIA OPERACIONAL PERSISTENTE

- arquivo canônico primeiro; comentários depois;
- write success + readback antes de confirmar ordem;
- Main audita/especifica; implementadores implementam; AUD verifica candidate;
- todo merge/checkpoint que muda missão exige atualização deste arquivo;
- PRODUCT CHECKPOINT e COORDINATION HEAD ficam separados;
- CI vermelho é diagnosticado antes de rerun;
- `PR_READY != INTEGRATED != VERIFIED != FROZEN`;
- acceptance obrigatório `PENDING` não vira PASS por interpretação;
- preservar cota do Codex com work packages bounded e auditoria feita pelo Main.

---

For Product Owner control, Main Coordinator reports current America/Sao_Paulo time as:

`Hora: HH:MM`
