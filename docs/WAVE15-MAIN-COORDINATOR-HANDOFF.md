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

## 7. FND-04 queued — referências legíveis de TAG em Python

#305 comentário `5701881550` adicionou um critério obrigatório antes do freeze de FND-04 para DEV-SCRIPT-ENGINEERING:

- `tag_read` / `tag_write` gerados usam caminho canônico legível da TAG;
- leitura e escrita compartilham uma única semântica de resolução;
- `TagId` continua sendo a identidade interna autoritativa;
- binding estável detecta rename/path-reuse identity drift;
- missing/ambiguous/stale fail closed;
- rename/move ou reutilização do caminho antigo nunca retargeta silenciosamente para outra TAG;
- diagnósticos podem mostrar a referência legível e a identidade estável esperada/resolvida;
- selectors preservam a mesma segurança de identidade.

Esse delta fica **queued atrás do FND-03 ativo** e não interrompe o machine-license-v2 slice.

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
