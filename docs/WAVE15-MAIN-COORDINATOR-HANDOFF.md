# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para a interação MAIN COORDINATOR <-> CODEX/FOUNDATION WORK durante a Wave 15.**
>
> `docs/CURRENT-COORDINATOR-HANDOFF.md` é apenas o combinador/ponte curta. Este arquivo contém a ordem operacional detalhada.
>
> **GitHub live é a autoridade final.** Antes de agir, revalidar HEAD/tree, issues, PRs, Actions e os gatilhos reais dos workflows.

**Status date:** 2026-09-16 BRT  
**Wave:** 15 — complete product delivery  
**Integration branch:** `wave15/corrections-integration`

## 1. Estado corrente da Foundation

State machine:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

Estado corrente:

- FND-01 Working/lifecycle/bootstrap — **VERIFIED/FROZEN**;
- FND-02 Security Authority, incluindo AUTH-04 — **VERIFIED/FROZEN**;
- FND-08 common timing — **VERIFIED/FROZEN**;
- FND-03 Runtime Session Lease / Licensing v2 — **ACTIVE / NOT FROZEN**;
- FND-03 durable Runtime Session Lease v1 contract — **VERIFIED/FROZEN**;
- FND-03 machine-license v2 schema/codec + hardening contract — **VERIFIED/FROZEN**;
- FND-03 Runtime Admission / requested→granted / Authority enforcement — **ACTIVE**;
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**;
- FC0-A — **BLOCKED**;
- parallel feature DEV lanes — **BLOCKED** até liberação explícita do Main.

Um PR, branch, teste isolado ou CI verde anterior não implica `VERIFIED` ou `FROZEN`.

### Checkpoints que liberaram a missão atual

- Slice 1 durable Runtime Session Lease integrado/verificado no checkpoint `456c66f4966ab5302831f642a39690ae3a3402a5` com CI pós-merge #1531;
- machine-license v2 + hardening integrado pelo PR #327;
- product head do hardening: `dbcb1b05e84d5883a1dbeb28b31c641e776eadc8`;
- CI #1534 foi reexecutada como attempt 2 nesse exact product head e concluiu Backend, Web e Chromium E2E com sucesso;
- comparação `dbcb1b05... -> 7904f98a...` mostrou somente alterações de handoff documental, sem delta adicional de código de produto;
- PR #328 corrigiu os gatilhos de CI da Wave 15;
- exact integration checkpoint validado automaticamente por `push`: `897ae7ca243f0f75d0d8ddf81e4b53a80b37f4f5`, tree `2e61d6b0158b3773709088e0f8e6d9247806b4b7`;
- CI automática `EliteSCADA CI` run #1537 / ID `35160493083` no exact SHA `897ae7ca...`: Backend build/test/smoke **PASS**, Web build **PASS**, Chromium end-to-end **PASS**.

O contrato bounded de machine-license v2 está, portanto, consumível pelo próximo slice. FND-03 como um todo continua ativo e não congelado.

## 2. MAIN COORDINATOR -> CODEX — ORDEM ATIVA E BINDING

A única missão Foundation ativa para o Codex é:

**FND-03 — COMMON RUNTIME ADMISSION / REQUESTED→GRANTED SESSION CLASS / AUTHORITY ENFORCEMENT**

### Exact authorized product base

`897ae7ca243f0f75d0d8ddf81e4b53a80b37f4f5`

### Branch autorizada

`work/w15-fnd-03-runtime-admission-v1`

### Target

`wave15/corrections-integration`

O Codex deve revalidar GitHub live antes da primeira mutação. Se a integração estiver à frente apenas por commits documentais deste handoff, o exact authorized product base acima continua válido; se houver qualquer delta de produto/infra adicional, retornar ao Main antes de implementar.

## 3. Objetivo do slice Runtime Admission

Entregar uma única autoridade server-side para composição de Runtime Session Class e Authority, consumida pelos caminhos Runtime relevantes, sem criar um segundo sistema de identidade de sessão, de autorização ou de licença.

O resultado deve garantir que:

- `requestedClass` é somente a preferência/solicitação do cliente;
- `grantedClass` é calculado pelo servidor;
- `Interactive` é somente um teto de classe de sessão, nunca uma concessão de capability;
- efetiva permissão de mutação = `grantedClass == Interactive` **e** capability correspondente concedida pela Authority canônica no scope/hierarchy aplicável;
- pedido explícito de `ViewOnly` sempre resulta em `ViewOnly`, mesmo para usuário com Authority ampla;
- usuário cuja Authority efetiva é intrinsecamente read-only para Runtime deve ser downscoped para `ViewOnly` mesmo se solicitar `Interactive`;
- `CommandExecute` permanece independente de `ProcessValueWrite`;
- `ViewOnly` é fail-closed no backend para comando e process write, inclusive contra cliente direto/modificado;
- REST, WebSocket e reconnect/resume usam a mesma identidade lógica de lease já congelada, não identidades paralelas por transporte;
- Web Runtime e futuro EliteGO devem poder consumir o mesmo contrato, sem pools ou políticas divergentes.

## 4. Entradas Foundation congeladas que o slice deve reutilizar

O Codex deve reutilizar, sem redesign silencioso:

1. FND-02 Security Authority e AUTH-04 frozen;
2. Runtime Session Lease v1 frozen, incluindo identidade lógica `(subject, clientInstanceId)`, runtime identity, generation/CAS e expiração;
3. machine-license v2 frozen, incluindo ESLIC1 compatibility e ESLIC2 `viewOnlySeats`, `interactiveSeats`, `haRuntime`;
4. semântica ESLIC2: assentos são **totais efetivos de capacidade comercial remota/cliente**, nunca `Demo + capacidade`;
5. Demo/no-valid-commercial-license permanece política separada `2 Interactive + 2 View Only` conforme #301;
6. backend authorization canônico já existente;
7. separação entre Runtime Session Class/licensing e Authority de usuário.

Se o slice descobrir que precisa alterar semanticamente qualquer contrato acima, deve retornar `BLOCKED-CONTRACT` antes de criar workaround paralelo.

## 5. Escopo obrigatório do Runtime Admission

O Codex deve:

1. criar/fechar um serviço/contrato único de decisão de classe de sessão no servidor;
2. produzir decisão determinística de `requestedClass -> grantedClass` a partir da solicitação e do resultado relevante da Authority;
3. garantir downscope explícito `ViewOnly -> ViewOnly`;
4. garantir downscope `Interactive -> ViewOnly` quando a Authority efetiva não permite qualquer mutação Runtime que justifique Interactive;
5. preservar `Interactive` como teto quando a Authority permitir ação mutável, sem conceder capability ausente;
6. integrar o enforcement server-side da classe concedida aos pontos mutáveis relevantes, mantendo `CommandExecute` separado de `ProcessValueWrite`;
7. vincular a decisão à identidade lógica da Runtime Session Lease, incluindo `subject`, `clientInstanceId` e `generation`/equivalente necessário para impedir reaproveitamento indevido;
8. fazer REST/WebSocket/reconnect convergirem para a mesma decisão e lease lógica;
9. rejeitar/falhar fechado quando a identificação de sessão necessária estiver ausente, inválida, obsoleta ou inconsistente;
10. expor reason/result codes determinísticos suficientes para diagnóstico e para o próximo slice de quota, sem depender de texto livre como contrato;
11. preservar ESLIC1 sem inferir novos entitlements comerciais;
12. manter ESLIC2 disponível como entrada canônica para o próximo slice de capacity accounting, sem criar contagem paralela nesta implementação.

### Regra sobre quota neste slice

Este slice **não deve fingir que capacidade concorrente já foi reservada**.

O contrato de Admission deve separar claramente:

- resolução/eligibilidade de classe e Authority, entregue neste slice;
- reserva/consumo concorrente de capacidade Interactive/View Only, que pertence ao slice seguinte de shared quota accounting.

Se o nome público existente `grantedClass` implicar semanticamente que um assento já foi reservado, o Codex deve ajustar o contrato mínimo para deixar explícita essa fronteira e retornar a decisão ao Main. Não criar uma falsa concessão licenciada só para encaixar nomenclatura.

## 6. Fora de escopo deste slice

- implementação de shared concurrent seat accounting Web + EliteGO;
- contadores finais, overflow e competição por assentos;
- license install/replace/remove lifecycle;
- License Generator UX;
- Installation UX;
- EliteGO UX;
- HA election/fencing;
- FND-04 Script TAG Reference Resolution;
- redefinir Authority ou criar role-name magic;
- reabrir FND-01/FND-02;
- segundo session registry, segundo license resolver ou segundo authorization pipeline.

## 7. Critérios de aceite obrigatórios

No exact candidate, o Codex deve demonstrar pelo menos:

1. `requested ViewOnly` permanece `ViewOnly` para Authority ampla;
2. `requested Interactive` por subject Runtime intrinsecamente read-only é downscoped a `ViewOnly`;
3. `requested Interactive` por subject com mutação Runtime aplicável pode manter teto `Interactive`, sem ganhar capability adicional;
4. Authority com `CommandExecute` e sem `ProcessValueWrite` consegue apenas o primeiro quando a classe permite;
5. Authority com `ProcessValueWrite` e sem `CommandExecute` não ganha command por estar Interactive;
6. sessão `ViewOnly` falha fechada para `CommandExecute` e `ProcessValueWrite` mesmo com Authority que permitiria ambos;
7. chamada REST direta/modificada não consegue ignorar a classe concedida;
8. caminho WebSocket direto/modificado não consegue ignorar a classe concedida;
9. REST + WebSocket + reconnect do mesmo `(subject, clientInstanceId)` preservam uma única lease/decisão lógica e geração consistente;
10. `clientInstanceId`/generation ausente, adulterado, expirado ou stale não recupera Interactive por fallback;
11. nenhuma decisão usa nomes de role como `Administrator`, `Operator` ou equivalentes como política de licensing;
12. ESLIC1 continua sem `SessionEntitlements` inferidos;
13. ESLIC2 continua expondo seus entitlements assinados sem alterar a semântica de totais efetivos;
14. nenhum estado de sessão/licença/chave privada entra em `.escadapkg`;
15. regressões existentes de Authority, licensing e Runtime permanecem verdes;
16. build/test relevantes e EliteSCADA CI no exact PR head ficam `PASS`; requisito não executado = `PENDING`.

## 8. Evidências obrigatórias do Codex

Entregar:

- exact base SHA;
- exact head SHA/tree;
- branch + PR;
- arquivos/símbolos alterados;
- contrato de decisão de classe e reason codes;
- pontos REST/WebSocket/reconnect integrados;
- matriz dos 16 critérios `PASS | FAIL | PENDING` com evidência concreta;
- testes locais e quantidade/resultados;
- Actions run ID e job IDs exatos;
- skips/limitações ambientais;
- riscos residuais;
- itens deliberadamente deixados para quota accounting;
- confirmação explícita de que não criou segundo lease registry, segundo licensing path ou segundo Authority pipeline.

## 9. CODEX -> MAIN COORDINATOR — retorno obrigatório

O retorno normal deve começar exatamente por:

`CODEX -> MAIN COORDINATOR — FND-03 RUNTIME ADMISSION HANDOFF`

Se houver dependência real de mudança em contrato frozen:

`CODEX -> MAIN COORDINATOR — FND-03 RUNTIME ADMISSION BLOCKED-CONTRACT`

Se o bloqueio for somente ambiental:

`CODEX -> MAIN COORDINATOR — FND-03 RUNTIME ADMISSION BLOCKED-ENV`

Codex não deve auto-mergear, auto-verificar, auto-congelar FND-03, iniciar o slice de quotas, iniciar FND-04 nem liberar FC0-A.

## 10. MAIN COORDINATOR — tratamento do retorno

Ao receber o handoff:

1. revalidar live base/head/tree/PR;
2. revisar diff real e scope leakage;
3. verificar se há exatamente um admission path sem segunda Authority/licensing/lease authority;
4. revisar fail-closed de ViewOnly e separação `CommandExecute`/`ProcessValueWrite`;
5. conferir REST/WebSocket/reconnect e generation/stale behavior;
6. conferir os testes negativos, não apenas happy path;
7. validar CI no exact head;
8. diagnosticar qualquer vermelho antes de rerun;
9. integrar somente com evidência bounded suficiente;
10. validar merge SHA/parents/tree e CI pós-merge conforme o workflow vivo;
11. só então promover este slice e emitir o próximo exact base para shared quota accounting.

## 11. Próximo slice planejado, NÃO ATIVO

Após Runtime Admission integrado/verificado, o próximo slice de FND-03 previsto é:

**FND-03 — SHARED RUNTIME SEAT ACCOUNTING / WEB + ELITEGO INTERACTIVE & VIEW ONLY QUOTAS**

Ele deve implementar reserva/consumo concorrente dos totais ESLIC2/Demo usando a mesma lease lógica. Não deve ser iniciado antecipadamente.

Depois dele ainda permanecem, em slices bounded:

- license inspect/verify/install/replace/remove lifecycle e reavaliação/fencing de leases;
- integração completa com Installation switching #304;
- observabilidade/rejection reasons finais;
- concurrency/negative regressions restantes;
- demais critérios de #301 antes de FND-03 como um todo ficar `VERIFIED/FROZEN`.

## 12. FND-04 — contrato operacional de Script TAG Reference Resolution

**Status:** `QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN`.

FND-04 não inicia enquanto FND-03 for a missão Foundation ativa, salvo nova ordem explícita do Main.

Branch reservada para quando for ativado:

`work/w15-fnd-04-script-tag-reference-resolution`

Target:

`wave15/corrections-integration`

### 12.1 Objetivo

Entregar contrato compartilhado, determinístico e versionável para referências de TAG em Server Script / Script Engineering no qual:

- Python visível usa referência canônica humana, normalmente path completo, por exemplo `EEE.Process.LevelPct`;
- `TagId`/Guid permanece identidade interna autoritativa;
- `tag_read` e `tag_write` usam a mesma semântica de resolução;
- backend/runtime prova que a referência textual corresponde ao `TagId` esperado antes de read/write;
- rename/move/path reuse nunca retargeta silenciosamente script para outra TAG;
- downstream `DEV-SCRIPT-ENGINEERING` recebe contrato congelado para Object Browser, autocomplete, busca, cursor insertion e diagnóstico.

### 12.2 Entradas obrigatórias

Na ativação, Codex deve revalidar:

- exact base SHA/tree emitido pelo Main;
- #305 comentário binding `5701881550`;
- W15-P1-05;
- FND-01/FND-02 frozen;
- stable `TagId`/Guid e TAG registry atual;
- `tag_read`, `tag_write`, Script APIs e callers;
- persistence/bindings/`TagValueReference` equivalentes;
- save/load/export/import/package;
- scripts legados com GUID/TagId.

Se depender de alteração de contrato frozen externo, retornar `BLOCKED-CONTRACT`.

### 12.3 Saídas obrigatórias

- um único resolver compartilhado para read/write;
- binding persistido/versionável `referência visível <-> TagId esperado`;
- estados equivalentes a `found/notFound/ambiguous/stale/identityDrift`;
- no silent retarget;
- round-trip seguro;
- política explícita para legacy GUID/TagId;
- API/diagnóstico consumível pelo DEV;
- documentação curta das invariantes frozen.

### 12.4 Critérios de aceite FND-04

1. fonte gerada legível, sem GUID como representação normal;
2. resolver único para read/write;
3. identidade estável validada;
4. rename/move não retargeta silenciosamente;
5. reuse de path por outro TagId falha como drift/stale;
6. missing/ambiguous fail closed;
7. comportamento de rename/move definido e testado;
8. selectors alternativos preservam identidade/fail-closed;
9. diagnóstico mostra referência e identidade quando útil;
10. save/load/export/import/package preservam binding pertinente;
11. legacy GUID/TagId tem política testada;
12. Authority permanece aplicada;
13. não surge segundo Tag registry/resolver/pipeline de autorização;
14. regressão representativa com duas leituras, comparação e ação condicional em source legível;
15. contrato consumível pelo `DEV-SCRIPT-ENGINEERING` sem redesign Foundation.

### 12.5 Evidências obrigatórias

- exact base/head/tree;
- PR/diff bounded;
- resolver states;
- read/write pela referência legível;
- rename/move/path reuse;
- round-trip persistence/package pertinente;
- legacy migration/compatibility;
- script multi-TAG;
- prova de Authority preservada;
- testes/CI com `PASS | FAIL | PENDING`;
- riscos e itens downstream.

### 12.6 Retorno Codex FND-04

Cabeçalho exato:

`CODEX -> MAIN COORDINATOR — FND-04 SCRIPT TAG REFERENCE CONTRACT HANDOFF`

Se bloquear contrato frozen externo:

`CODEX -> MAIN COORDINATOR — FND-04 BLOCKED-CONTRACT`

Codex não auto-mergeia, não congela FND-04 e não libera DEV-SCRIPT-ENGINEERING.

## 13. FC0-A

FC0-A exige:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

mais FND-08 frozen, INFRA-CI-01 ready/frozen e exact integration checkpoint com os gates requeridos.

Somente a liberação explícita do Main abre os DEVs dependentes.

## 14. Política operacional de GitHub Actions

Nunca concluir que uma Action não pode ser executada apenas porque `workflow_dispatch` não está disponível.

Antes de qualquer conclusão sobre execução de CI:

1. ler o workflow real em `.github/workflows/`;
2. verificar `on:`;
3. verificar evento (`pull_request`, `push`, `workflow_dispatch` ou outro);
4. verificar branch/base filters;
5. verificar `paths`/`paths-ignore`;
6. provocar preferencialmente o evento automático normal do projeto;
7. usar rerun somente para o mesmo candidate/run quando isso responde ao objetivo;
8. lembrar que rerun de um workflow existente continua associado ao SHA original e não valida automaticamente um novo merge SHA.

Após PR #328, `EliteSCADA CI` passa a aceitar:

- `pull_request` para `main` e `wave15/corrections-integration`;
- `push` relevante para `main`, `wave14/corrections-integration` e `wave15/corrections-integration`;
- `workflow_dispatch` continua disponível como alternativa manual.

Assim, desenvolvimento e validação Wave 15 devem usar PR/push automáticos como caminho normal sempre que os filtros forem satisfeitos.

## 15. Relação entre documentos

### `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`

Handoff operacional vivo e canônico Main Coordinator <-> Codex/Foundation Work.

### `docs/CURRENT-COORDINATOR-HANDOFF.md`

Combinador/ponte curta. Deve apontar para este handoff, ordem ativa e issues/PRs relevantes. Não substitui este documento.

### `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`

Protocolo genérico para troca do chat do Main Coordinator.

### Issues

- #305 — dependency graph / checkpoints / sequencing;
- #301 — FND-03 / Licensing ledger;
- #297 — Wave 15 global ledger.

## 16. Guardas permanentes

- GitHub live é autoridade;
- no direct `main`;
- no direct feature write to integration;
- no force push/destructive rebase/evidence deletion;
- red CI diagnosticado antes de rerun;
- exact-head evidence only;
- required but unexecuted test = `PENDING`;
- no weakening Security/Authority/Licensing/lifecycle/Runtime/Historian/Driver contracts;
- no EEE-only workaround para defeito genérico;
- stable IDs outrank mutable names/paths;
- no downstream silent redesign of frozen contracts;
- Runtime/Active permanece independente de `.escadalib`;
- Alarm, Operational Event e Audit permanecem distintos.

For Product Owner control, coordination messages end with current America/Sao_Paulo time as:

`Hora: HH:MM`
