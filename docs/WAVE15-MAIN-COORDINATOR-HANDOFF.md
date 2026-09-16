# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para a interação MAIN COORDINATOR <-> CODEX/FOUNDATION WORK durante a Wave 15.**
>
> Este arquivo contém o estado operacional detalhado, a ordem ativa do Main Coordinator, o contrato de retorno do Codex/Work e a sequência de revisão/integração. Ele deve ser atualizado conforme a coordenação avança.
>
> `docs/CURRENT-COORDINATOR-HANDOFF.md` é o **combinador/ponte curta** que aponta para este handoff e para as issues ativas. Não substitui este documento.
>
> **GitHub live é a autoridade final.** Antes de agir, revalidar HEAD/tree, issues, PRs e Actions. Nenhum SHA registrado aqui dispensa essa verificação.

**Status date:** 2026-09-16 BRT  
**Wave:** 15 — complete product delivery  
**Integration branch:** `wave15/corrections-integration`  
**Latest verified product-code checkpoint:** `456c66f4966ab5302831f642a39690ae3a3402a5`  
**Tree at that checkpoint:** `f42d442933ded9bcf4290ae437193f2d1bb3d492`

A branch de integração pode estar à frente desse checkpoint por commits apenas de documentação/coordenação. Sempre distinguir avanço documental de avanço de código de produto.

## 1. Estado corrente da Foundation

State machine:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

Estado atual:

- FND-01 Working/lifecycle/bootstrap — **VERIFIED/FROZEN**;
- FND-02 Security Authority, incluindo AUTH-04 — **VERIFIED/FROZEN**;
- FND-08 common timing — **VERIFIED/FROZEN**;
- FND-03 Runtime Session Lease / Licensing v2 — **ACTIVE / NOT FROZEN**;
- FND-03 Slice 1 durable Runtime Session Leases — **INTEGRATED / VERIFIED** no checkpoint `456c66f...`;
- CI pós-merge `35110143733` — backend build/test/smoke PASS, Web build PASS, Chromium end-to-end PASS;
- FND-04 — ainda não frozen e inclui o critério obrigatório de resolução segura de referências legíveis de TAG registrado em #305 comentário `5701881550`;
- FC0-A — **BLOCKED**;
- parallel feature DEV lanes — **BLOCKED** até liberação explícita do Main.

Um PR, branch ou teste isolado não implica `FROZEN`.

## 2. MAIN COORDINATOR -> CODEX — ordem ativa

A última ordem binding está em #301 comentário `5699620231`:

**FND-03 — machine-license v2 schema/codec**

Base de produto autorizada:

`456c66f4966ab5302831f642a39690ae3a3402a5`

Branch autorizada quando publicada:

`work/w15-fnd-03-machine-license-v2`

Target:

`wave15/corrections-integration`

### Escopo obrigatório

- preservar compatibilidade exata de leitura/validação dos `ESLIC1` assinados e vinculados à máquina;
- adicionar representação v2/`ESLIC2` assinada e machine-bound com `viewOnlySeats`, `interactiveSeats` e `haRuntime` explícitos;
- reutilizar o caminho canônico existente de assinatura, fingerprint/hardware binding, expiry e Demo;
- não criar segundo codec, segundo mecanismo de assinatura ou segundo caminho de hardware verification;
- não inferir entitlement Interactive a partir de ESLIC1;
- expor os novos entitlements apenas pelos contratos internos/versionados necessários ao FND-03.

### Fora de escopo deste slice

- enforcement de admissão Runtime;
- cálculo/consumo de quotas ativas;
- política `requestedClass -> grantedClass`;
- install/replace/remove lifecycle da licença;
- License Generator UX;
- Installation UX;
- EliteGO UX;
- HA election/fencing;
- FND-04 Script TAG reference resolution.

### Prova mínima

- ESLIC1 permanece compatível;
- v2/ESLIC2 válido encode/decode/verify;
- tamper rejection;
- wrong-key rejection;
- wrong-machine rejection;
- expiry rejection;
- seat values inválidos/malformados rejeitados;
- novos campos cobertos pela assinatura;
- regressão confirmando que licença/chaves/session state não entram em `.escadapkg`.

## 3. Continuidade da sessão Codex interrompida

O Product Owner informou que o Codex **já iniciou esse slice e parou apenas porque atingiu o limite de interação**. Não houve handoff concluído.

Na última verificação do GitHub não havia branch remota `work/w15-fnd-03-machine-license-v2`, PR publicado ou handoff final para esse slice.

Ao voltar, o Codex deve primeiro:

1. inspecionar a sessão/worktree/local changes já existentes;
2. recuperar e continuar o trabalho local, se disponível;
3. somente recriar a partir da base autorizada se o estado anterior realmente não puder ser recuperado;
4. não confundir `work/w15-fnd-03-runtime-session-lease` com o slice atual: essa branch pertence ao Slice 1 já integrado.

Ausência de branch remota **não prova ausência de trabalho local**.

## 4. CODEX -> MAIN COORDINATOR — retorno obrigatório

Quando o slice estiver pronto para revisão, o handoff deve começar exatamente por:

`CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 SCHEMA HANDOFF`

O retorno deve conter:

- exact base SHA;
- exact head SHA e tree quando relevante;
- branch e PR;
- arquivos/símbolos alterados;
- descrição do schema/codec e compatibilidade ESLIC1;
- confirmação de reutilização do caminho existente de assinatura/fingerprint;
- testes com `PASS | FAIL | PENDING`;
- CI run/jobs exatos;
- skips/limitações de ambiente;
- riscos residuais;
- itens deliberadamente não alterados;
- recomendação do próximo slice FND-03.

Codex não deve auto-mergear, auto-congelar FND-03 nem liberar FC0-A.

## 5. MAIN COORDINATOR — tratamento do retorno

Ao receber o handoff:

1. revalidar branch/PR/base/head/tree;
2. revisar o diff real e vazamento de escopo;
3. conferir codec, assinatura, fingerprint, compatibilidade e regressões negativas;
4. conferir CI no exact head;
5. diagnosticar qualquer vermelho antes de rerun;
6. integrar somente com escopo bounded e evidência suficiente;
7. verificar merge parent/tree e CI pós-merge quando requerido;
8. registrar `INTEGRATED/VERIFIED` sem chamar FND-03 inteiro de `FROZEN` antes dos slices restantes;
9. atualizar este arquivo com a próxima ordem ativa e persistir a decisão nas issues binding adequadas.

## 6. FND-03 ainda pendente após schema/codec

Os próximos slices exigem autorização explícita do Main e incluem, conforme necessário:

- `requestedClass -> grantedClass` server-side;
- interseção com Authority e View Only fail-closed;
- quotas compartilhadas Web + EliteGO para Interactive/View Only;
- fallback/rejection reasons explícitos;
- reconnect/REST/WebSocket multiplicity preservando um único logical lease;
- primitives transacionais de inspect/verify/replace/remove requeridas por installation switching;
- regressões de compatibilidade, negativas e concorrência.

Não absorver esses itens silenciosamente no slice atual.

## 7. FND-04 — contrato operacional de Script TAG Reference Resolution

**Status:** `QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN`.

Este contrato formaliza o delta de FND-04 requerido pelo W15-P1-05 e pelo comentário binding #305 `5701881550`. Ele existe para separar corretamente **referência humana no código-fonte** de **identidade estável interna da TAG** sem introduzir rebind silencioso, segundo resolver ou desvio de Authority.

FND-04 **não inicia automaticamente** quando este contrato existir. Enquanto FND-03 for a missão Foundation ativa, o Codex não deve abrir uma implementação paralela de FND-04. O Main Coordinator deve emitir uma autorização explícita de ativação, com exact base SHA, antes de qualquer branch de implementação FND-04 ser criada ou alterada.

Branch reservada para a implementação quando ativada:

`work/w15-fnd-04-script-tag-reference-resolution`

Target:

`wave15/corrections-integration`

### 7.1 Objetivo

Entregar um contrato compartilhado, determinístico e versionável para referências de TAG em Server Script / Script Engineering no qual:

- o Python visível ao desenvolvedor use referência canônica humana, normalmente o caminho completo da TAG, por exemplo `EEE.Process.LevelPct`;
- `TagId`/Guid permaneça a identidade interna autoritativa e estável;
- `tag_read` e `tag_write` compartilhem a mesma semântica de resolução;
- o backend/runtime consiga provar que a referência textual usada no script corresponde ao `TagId` esperado antes de qualquer leitura/escrita autorizada;
- rename/move/path reuse nunca retargete silenciosamente um script para outra TAG;
- a Foundation exponha o contrato necessário para downstream `DEV-SCRIPT-ENGINEERING` implementar Object Browser, autocomplete, busca, cursor insertion e UX diagnóstica sem reinventar identidade/resolução.

### 7.2 Entradas obrigatórias

Na ativação, o Codex deve revalidar e consumir estas entradas, sem confiar em snapshots antigos:

- exact base SHA/tree fornecido pelo Main Coordinator no momento da ativação;
- #305 e seu comentário binding `5701881550`;
- requisito W15-P1-05 de referência legível em Python;
- contratos frozen de FND-01 e FND-02, especialmente stable identity, lifecycle/recovery e backend authorization;
- stable `TagId`/Guid atual e comportamento vigente de rename/move/path lookup no TAG registry;
- implementação atual de `tag_read`, `tag_write`, Script Assistant/Script APIs e seus chamadores;
- modelo persistido de Server Script/Script Engineering, incluindo dependências/bindings existentes e qualquer `TagValueReference`/equivalente já canônico;
- regras atuais de save/load/export/import/package para scripts e referências;
- comportamento real de scripts legados que já contenham GUID/TagId ou outra forma de referência opaca.

Se uma dessas entradas estiver ambígua, inconsistente ou exigir alteração de contrato frozen fora de FND-04, o Codex deve retornar `BLOCKED-CONTRACT`; não deve criar um caminho paralelo para contornar a autoridade existente.

### 7.3 Saídas obrigatórias

A implementação FND-04 deve produzir, no mínimo:

- **um único resolver compartilhado** para as formas suportadas de referência de TAG usadas por leitura e escrita;
- uma representação persistida/versionável de binding entre a referência visível no source e o `TagId` esperado. O nome/tipo concreto é de implementação, mas o vínculo deve ser explícito e auditável; não pode existir apenas como estado efêmero da UI;
- comportamento definido para `found`, `notFound`, `ambiguous`, `stale/renamed` e `identityDrift` ou estados semanticamente equivalentes;
- integração de `tag_read` e `tag_write` com esse mesmo contrato, preservando a autorização backend já existente;
- serialização/round-trip segura dos bindings em save/load e nos artefatos de projeto pertinentes;
- política explícita e testada para scripts legados com GUID/TagId: compatibilidade ou migração determinística. Novo código gerado pelo produto não deve voltar a emitir GUID como representação normal;
- API/diagnóstico suficiente para que downstream autocomplete, Object Browser e busca localizem a TAG por nome/caminho legível sem usar Guid como UX primária;
- documentação curta do contrato/versionamento e das invariantes congeladas para consumo downstream.

### 7.4 Critérios de aceite para `VERIFIED/FROZEN`

FND-04 só pode ser considerado aceito quando todos os itens abaixo estiverem demonstrados no exact candidate:

1. **Fonte legível:** inserção/geração normal de `tag_read`/`tag_write` usa caminho canônico humano; GUID/endereço interno não aparece como representação normal gerada no Python.
2. **Resolver único:** leitura e escrita usam a mesma resolução semântica; não existe um lookup por path para read e outro por Guid para write como contratos divergentes.
3. **Identidade estável:** a operação efetiva é vinculada ao `TagId` esperado e validado antes da execução.
4. **No silent retarget:** se uma TAG mudar de nome/caminho, o script antigo não pode passar a controlar outra TAG silenciosamente.
5. **Path reuse seguro:** se o caminho antigo for reutilizado por outro `TagId`, a resolução falha como drift/stale; não aceita a nova TAG por coincidência textual.
6. **Missing/ambiguous fail closed:** referências inexistentes ou ambíguas falham de forma explícita e não fazem fallback para outro objeto.
7. **Rename/move definido:** o produto tem um comportamento único e testado: refactor deliberado para o novo caminho preservando o mesmo `TagId`, ou referência stale/broken até correção deliberada. Qualquer atualização automática deve provar o mesmo `TagId` e nunca inferir por nome apenas.
8. **Selectors seguros:** qualquer selector/forma alternativa suportada mantém as mesmas garantias de stable identity e fail-closed.
9. **Diagnóstico útil:** erros mostram a referência usada no script e, quando útil, expected/resolved `TagId`, sem esconder identity drift atrás de mensagem genérica.
10. **Round-trip:** save/load e export/import/package pertinentes preservam o binding e não recriam referência apenas por path.
11. **Legacy definido:** scripts persistidos que usam Guid/TagId têm compatibilidade/migração explicitamente definida e testada; não podem quebrar ou retargetar silenciosamente por ausência de política.
12. **Authority preservada:** FND-04 não cria bypass de capability/authorization para leitura/escrita; `tag_write` continua passando pelo backend authoritativo.
13. **Sem segundo dono:** não criar segundo registro de TAG, segundo resolver, segunda identidade ou segundo pipeline de autorização para satisfazer Script Engineering.
14. **Fluxo representativo legível:** regressão com várias TAGs, no mínimo duas leituras, comparação e ação condicional, usando referências humanas no source, por exemplo leitura de nível/pressão e uma escrita condicional.
15. **Consumível pelo DEV:** o contrato frozen expõe o necessário para `DEV-SCRIPT-ENGINEERING` implementar Object Browser, autocomplete/search, insertion-at-cursor e UX diagnóstica sem alterar a semântica Foundation.

Qualquer item obrigatório ainda não executado ou não demonstrado permanece `PENDING`; não pode ser promovido por inferência.

### 7.5 Dependências e fronteiras

**Dependências técnicas/frozen consumidas:**

- FND-01 lifecycle/bootstrap/recovery — scripts e bindings devem sobreviver aos ciclos persistidos sem criar estado paralelo;
- FND-02 Security Authority — read/write continuam sujeitos às capacidades e scopes frozen; FND-04 não redefine autorização;
- stable TAG identity/registry já presente no produto — `TagId` é a identidade autoritativa;
- contratos atuais de Script/Engineering e package persistence necessários para guardar bindings.

**Dependência operacional de scheduling:**

- o FND-03 ativo não é interrompido por este contrato;
- FND-04 só muda de `QUEUED` para `ACTIVE` após handoff/revisão do trabalho Foundation corrente e ordem explícita do Main com exact base;
- isso é uma dependência de coordenação, não uma declaração de que a semântica de TAG tecnicamente dependa da licença v2.

**Downstream bloqueado por FND-04:**

- `DEV-SCRIPT-ENGINEERING` não pode tratar esse contrato como livre para redesign;
- Object Browser, autocomplete, busca, insertion-at-cursor, snippets legíveis e UX diagnóstica permanecem no DEV, mas consomem o contrato frozen;
- se o DEV descobrir insuficiência, deve retornar `BLOCKED-CONTRACT` para Main/Foundation.

**Fora de escopo do FND-04 Foundation:**

- redesign visual completo do editor Python;
- UX final do Project Object Browser;
- ranking/estética do autocomplete;
- receitas/tutoriais finais do desenvolvedor;
- mudanças não causais em HMI, Driver, Historian, Licensing ou HA.

### 7.6 Evidências obrigatórias

O handoff de conclusão FND-04 deve trazer evidência rastreável, não apenas descrição:

- exact base SHA, exact head SHA e tree;
- branch/PR e diff bounded;
- lista dos arquivos/símbolos públicos ou internos relevantes alterados;
- teste focado do resolver para `found/notFound/ambiguous/stale/identityDrift` ou equivalentes;
- regressão de `tag_read` por referência legível;
- regressão de `tag_write` pela mesma referência/resolver;
- rename/move mantendo mesmo `TagId`;
- reutilização do caminho antigo por outro `TagId`, provando rejeição;
- round-trip de persistence/save-load e package/export-import pertinente;
- regressão de compatibilidade/migração para script legado com Guid/TagId, conforme a política escolhida;
- regressão de selector, se selector for forma suportada;
- script representativo com várias TAGs: duas leituras, comparação e ação condicional, source legível;
- prova de que authorization/capability não foi bypassada;
- build/test do backend e suites de Script/Runtime afetadas no exact head;
- Actions run/job IDs e resultados `PASS | FAIL | PENDING` para os gates requeridos pelo Main na ativação;
- skips ou limitações de ambiente explicitados; teste não executado nunca conta como PASS;
- riscos residuais e itens deliberadamente deixados ao downstream DEV.

Antes do freeze, o Main deve conseguir correlacionar cada critério de aceite a pelo menos uma evidência concreta no exact candidate.

### 7.7 Regra de retorno CODEX -> MAIN COORDINATOR

Quando FND-04 estiver pronto para revisão, o Codex deve retornar ao Main com o cabeçalho exato:

`CODEX -> MAIN COORDINATOR — FND-04 SCRIPT TAG REFERENCE CONTRACT HANDOFF`

O retorno deve conter, nesta ordem:

1. `Status:` `PR_READY | BLOCKED-CONTRACT | BLOCKED-ENV`;
2. exact base SHA;
3. exact head SHA/tree;
4. branch e PR;
5. resumo do contrato efetivamente implementado;
6. decisão adotada para binding persistido e legacy GUID/TagId compatibility/migration;
7. arquivos/símbolos alterados;
8. matriz dos critérios de aceite com `PASS | FAIL | PENDING` e evidência correspondente;
9. testes locais e CI com run/job IDs;
10. gaps, skips, riscos residuais e itens downstream;
11. confirmação explícita de que não houve segundo resolver, segundo Tag registry ou bypass de Authority;
12. recomendação ao Main: integrar, corrigir bounded delta, ou devolver para contrato.

Se durante a implementação surgir necessidade de alterar um contrato frozen fora da fronteira FND-04, o retorno deve começar por:

`CODEX -> MAIN COORDINATOR — FND-04 BLOCKED-CONTRACT`

Nesse caso o Codex deve parar antes de criar workaround paralelo e descrever exatamente qual contrato impede o progresso, qual comportamento atual foi observado, qual evidência reproduz o blocker e qual menor delta de Foundation seria necessário.

Codex **não** auto-mergeia, **não** declara FND-04 `FROZEN`, **não** libera `DEV-SCRIPT-ENGINEERING` e **não** libera FC0-A. Essas transições pertencem ao Main após revisão, integração, evidência exact-head/pós-merge e registro nas issues binding.

## 8. FC0-A

FC0-A exige:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

mais FND-08 frozen, INFRA-CI-01 ready/frozen e um exact integration checkpoint com os gates requeridos.

Somente a liberação explícita do Main abre:

- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX.

## 9. Relação entre os documentos de coordenação

### `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`

**Handoff operacional vivo e canônico da Wave 15.** É o documento adotado para a interação detalhada Main Coordinator <-> Codex/Foundation Work e para a transferência de contexto operacional da Wave.

### `docs/CURRENT-COORDINATOR-HANDOFF.md`

**Combinador/ponte curta.** Deve apontar rapidamente para este handoff, para a ordem ativa e para as issues/PRs relevantes. Não deve carregar uma segunda cópia concorrente de todo o estado.

### `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`

Protocolo genérico, state-independent, para substituir o chat do Main Coordinator.

### Issues

- #305 — dependency graph / Foundation checkpoints / sequencing;
- #301 — FND-03 / Licensing ledger;
- #297 — Wave 15 global ledger.

Issues continuam sendo o ledger durável de decisões binding, evidências, blockers, integração e freeze.

## 10. Guardas permanentes

- no direct `main`;
- no destructive history operation;
- no direct feature write to integration;
- red CI diagnosed before rerun;
- exact-head evidence only;
- required but unexecuted test = `PENDING`, never `PASS`;
- no weakening Security/Authority/Licensing/lifecycle/Runtime/Historian/Driver contracts;
- no EEE-only workaround for generic defect;
- stable IDs outrank mutable names/paths;
- no downstream silent redesign of frozen contracts;
- Runtime/Active remains independent of `.escadalib`;
- Alarm, Operational Event and Audit remain distinct.

For Product Owner control, EliteSCADA coordination messages end with current America/Sao_Paulo time as:

`Hora: HH:MM`
