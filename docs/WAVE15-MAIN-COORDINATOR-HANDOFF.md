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
- **INTEGRATED CANDIDATE PENDING VERIFICATION** — código já integrado, mas ainda aguardando gate pós-merge no exact integrated SHA;
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

### Último PRODUCT CHECKPOINT verificado

- `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- tree `53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`
- contém PR #330 / Runtime Admission;
- CI pós-merge #1543: Backend/Web/Chromium PASS.

### INTEGRATED CANDIDATE PENDING VERIFICATION

PR #331 / Shared Runtime Seat Accounting foi integrado com sucesso.

- merge / integration HEAD: `a7067ac99f9f88fcd17f740b915d8c4f57c556fc`
- tree: `eed22a377fea2778d3e78143d706e4de0ef9ce38`
- parent 1: `11a0f32736def27fbbafb8b718abc428bba62056` — coordination/documentation HEAD anterior
- parent 2: `6789a989c85e7210c167945665f4ab6c8cef53a0` — approved PR #331 candidate
- PR #331: CLOSED / MERGED

Natural push CI foi disparada automaticamente pelo workflow existente `.github/workflows/dotnet-ci.yml`, que inclui `push` para `wave15/corrections-integration` quando há mudanças de produto/código.

Post-merge gate:

- EliteSCADA CI #1546
- run `35269829080`
- exact head SHA: `a7067ac99f9f88fcd17f740b915d8c4f57c556fc`
- Backend build, test and smoke `105366045111` — **SUCCESS**
- Web build `105366045298` — **SUCCESS**
- Chromium end-to-end `105366583742` — **IN PROGRESS** na última revalidação

Main **não** dispara execução duplicada enquanto o gate natural está rodando.

### Foundation

- FND-01 — **VERIFIED/FROZEN**
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **INTEGRATED / POST-MERGE VERIFICATION RUNNING**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**
- FC0-A — **BLOCKED**

---

## 2. MAIN COORDINATOR -> CODEX — CURRENT ORDER

**ORDER_STATE: WAIT**  
**Mission:** FND-03 Shared Runtime Seat Accounting — merged; Main owns post-merge validation

CODEX must make **no mutation** now.

On `SIGA`:

1. reread this handoff live;
2. confirm `ORDER_STATE: WAIT`;
3. do not rerun CI, alter PR #331, start another FND-03 slice, start FND-04 or release FC0-A;
4. wait for Main to finish CI #1546 and publish the next explicit work package.

Main Coordinator owns:

- exact integrated SHA verification;
- CI #1546 diagnosis/operation if necessary;
- promotion of Shared Runtime Seat Accounting to `VERIFIED/FROZEN` only after gate completion;
- definition of the next FND-03 slice.

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
