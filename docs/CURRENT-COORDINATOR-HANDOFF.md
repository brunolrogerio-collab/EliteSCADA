# Current Coordinator Handoff — Wave 15

> **ÚNICO handoff operacional corrente entre MAIN COORDINATOR e CODEX/FOUNDATION WORK.**
>
> Este arquivo é o combinador da coordenação ativa: a ordem atual do coordenador, o estado que o Codex deve retomar, o formato do retorno do Codex e a próxima ação segura do coordenador ficam aqui. Não criar um segundo arquivo `*-CURRENT*` ou outro handoff paralelo para o mesmo estado.
>
> **GitHub live é a autoridade final.** Antes de agir, revalidar HEAD/tree, issues, PRs e Actions. Este arquivo combina a coordenação; não substitui evidência viva.

**Status date:** 2026-09-16 BRT  
**Latest verified product-code checkpoint:** `456c66f4966ab5302831f642a39690ae3a3402a5`  
**Tree at that checkpoint:** `f42d442933ded9bcf4290ae437193f2d1bb3d492`

A branch `wave15/corrections-integration` pode estar à frente desse checkpoint por commits apenas de documentação/coordenação. Sempre distinguir avanço documental de avanço de código de produto.

## 1. Estado corrente

- Wave 15 complete-product delivery — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 Security Authority, incluindo AUTH-04 — VERIFIED/FROZEN.
- FND-08 common timing — VERIFIED/FROZEN.
- FND-03 Runtime Session Lease / Licensing v2 — **ACTIVE / NOT FROZEN**.
- FND-03 Slice 1 durable Runtime Session Leases — **INTEGRATED / VERIFIED** no checkpoint de produto `456c66f...`.
- CI pós-merge `35110143733` — backend build/test/smoke PASS, Web build PASS, Chromium end-to-end PASS.
- FND-04 — ainda não frozen; inclui o critério obrigatório de resolução segura de referências legíveis de TAG registrado em #305 comentário `5701881550`.
- FC0-A — **BLOCKED**.
- Parallel feature DEV lanes — **BLOCKED** até registro explícito de FC0-A.

State machine compartilhada:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

Nenhum consumidor downstream pode inferir `FROZEN` de branch, PR ou teste isolado.

## 2. MAIN COORDINATOR -> CODEX — ordem ativa

A última ordem binding do Main Coordinator está em #301 comentário `5699620231` e continua sendo:

**FND-03 — machine-license v2 schema/codec**

Base de produto autorizada:

`456c66f4966ab5302831f642a39690ae3a3402a5`

Branch autorizada quando o trabalho for publicado:

`work/w15-fnd-03-machine-license-v2`

Target:

`wave15/corrections-integration`

### Escopo obrigatório deste slice

- preservar compatibilidade exata de leitura/validação dos atuais `ESLIC1` assinados e vinculados à máquina;
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

## 3. Continuidade da sessão interrompida

O Product Owner informou que o Codex **já iniciou esse slice e parou apenas porque atingiu o limite de interação**. Não houve handoff concluído.

Na última verificação do GitHub:

- não havia branch remota `work/w15-fnd-03-machine-license-v2`;
- não havia PR publicado para esse slice;
- não havia handoff final do Codex.

Portanto, a primeira ação ao Codex voltar é:

1. inspecionar a sessão/worktree/local changes já existentes;
2. recuperar e continuar o que já foi feito, se disponível;
3. somente recriar o trabalho a partir da base autorizada se o estado local anterior realmente não puder ser recuperado;
4. não usar `work/w15-fnd-03-runtime-session-lease` como branch do slice atual: ela pertence ao Slice 1 já integrado.

Ausência de branch no GitHub **não prova ausência de trabalho local**.

## 4. CODEX -> MAIN COORDINATOR — retorno obrigatório

Quando o slice estiver pronto para revisão, o Codex deve publicar branch/PR e registrar o handoff começando exatamente por:

`CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 SCHEMA HANDOFF`

O retorno deve conter, no mínimo:

- exact base SHA;
- exact head SHA e tree quando relevante;
- branch e PR;
- arquivos/símbolos alterados;
- descrição do schema/codec e compatibilidade ESLIC1;
- confirmação de que assinatura/fingerprint existentes foram reutilizados;
- testes executados com `PASS | FAIL | PENDING`;
- CI run/jobs exatos;
- skips/limitações de ambiente;
- riscos residuais;
- itens deliberadamente não alterados;
- recomendação do próximo slice FND-03.

Codex não deve auto-mergear, auto-congelar FND-03 nem liberar FC0-A.

## 5. MAIN COORDINATOR — tratamento do retorno do Codex

Ao receber o handoff:

1. revalidar branch/PR/base/head/tree no GitHub;
2. revisar diff real e verificar vazamento de escopo;
3. conferir codec, assinatura, fingerprint, compatibilidade e regressões negativas;
4. conferir CI no exact head;
5. diagnosticar qualquer vermelho antes de rerun;
6. integrar somente se o slice estiver bounded e com evidência suficiente;
7. verificar merge parent/tree e CI pós-merge quando requerido;
8. registrar `INTEGRATED/VERIFIED` sem chamar FND-03 inteiro de `FROZEN` antes dos slices restantes;
9. emitir a próxima ordem ao Codex aqui e nas issues binding adequadas.

Este arquivo deve então ser atualizado para que **a próxima ordem substitua claramente a anterior**, sem abrir um segundo handoff corrente.

## 6. FND-03 ainda pendente depois do schema/codec

Após o slice atual, FND-03 ainda precisa de autorização explícita para os slices restantes, incluindo conforme necessário:

- `requestedClass -> grantedClass` server-side;
- interseção com Authority e View Only fail-closed;
- quotas compartilhadas Web + EliteGO para Interactive/View Only;
- fallback/rejection reasons explícitos;
- reconnect/REST/WebSocket multiplicity preservando um único logical lease;
- primitives transacionais de inspect/verify/replace/remove requeridas por installation switching;
- regressões de compatibilidade, negativas e concorrência.

Não absorver esses itens silenciosamente no slice de schema/codec.

## 7. FND-04 queued — referências legíveis de TAG em Python

O requisito W15-P1-05 de Python legível não é apenas UX. #305 comentário `5701881550` tornou obrigatório, antes do freeze de FND-04 para DEV-SCRIPT-ENGINEERING:

- `tag_read` / `tag_write` gerados usam caminho canônico legível da TAG;
- leitura e escrita compartilham uma única semântica de resolução;
- `TagId` continua sendo a identidade interna autoritativa;
- binding estável permite detectar rename/path-reuse identity drift;
- missing/ambiguous/stale fail closed;
- rename/move ou reutilização do caminho antigo nunca retargeta silenciosamente para outra TAG;
- diagnósticos podem mostrar referência legível e identidade estável esperada/resolvida;
- selectors preservam a mesma segurança de identidade.

Esse delta fica **queued atrás do FND-03 ativo**. Não interromper o machine-license-v2 slice para implementá-lo.

## 8. FC0-A

FC0-A exige:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

mais:

- FND-08 frozen;
- INFRA-CI-01 ready/frozen;
- um exact integration checkpoint com gates requeridos.

Somente o registro explícito do Main libera:

- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX.

## 9. Superfícies de coordenação

**Handoff operacional corrente e combinador Main <-> Codex:**

`docs/CURRENT-COORDINATOR-HANDOFF.md`

Demais fontes:

- `LAST CHANGE.md` — resumo curto do ponto de retomada;
- #305 — dependency graph / Foundation checkpoints / sequencing;
- #301 — FND-03 / Licensing ledger;
- #297 — Wave 15 global ledger;
- `docs/ROADMAP.md` — sequencing/checkpoints;
- `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md` — protocolo genérico para substituir o Main Coordinator;
- `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` — **snapshot histórico**, não handoff operacional corrente.

Não criar outro arquivo corrente concorrente com `CURRENT-COORDINATOR-HANDOFF.md`.

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
