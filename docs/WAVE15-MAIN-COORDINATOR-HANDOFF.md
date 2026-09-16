# Wave 15 — Main Coordinator Handoff

> **HANDOFF OPERACIONAL VIVO E CANÔNICO para a interação MAIN COORDINATOR <-> CODEX/FOUNDATION WORK durante a Wave 15.**
>
> `docs/CURRENT-COORDINATOR-HANDOFF.md` é apenas o combinador/ponte curta. Este arquivo contém a ordem operacional detalhada.
>
> **GitHub live é a autoridade final.** Antes de agir, revalidar HEAD/tree, issues, PRs e Actions.

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
- FND-03 Slice 1 durable Runtime Session Leases — **INTEGRATED / VERIFIED** no checkpoint `456c66f4966ab5302831f642a39690ae3a3402a5`;
- PR #325 `FND-03 machine-license v2 schema/codec` — **INTEGRATED, MAS AINDA NÃO VERIFIED/FROZEN** após revisão semântica do Main;
- integração atual antes do hardening: `ac2b7f49f53734132d88c7d367b88d33383c384a`, tree `3d3e7368047f77b714bf270e689b69f2dad62ef7`;
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN**;
- FC0-A — **BLOCKED**;
- parallel feature DEV lanes — **BLOCKED** até liberação explícita do Main.

Um PR, branch, teste isolado ou CI verde anterior não implica `VERIFIED` ou `FROZEN`.

## 2. MAIN COORDINATOR -> CODEX — ORDEM ATIVA E BINDING

A ordem ativa é:

**FND-03 — LICENSE V2 HARDENING**

Binding principal: #301 comentário `5703758565`.  
Sequencing/graph: #305 comentário `5703760732`.

Esta ordem **SUPERA E BLOQUEIA** a autorização anterior para iniciar:

`FND-03 — COMMON RUNTIME ADMISSION / REQUESTED→GRANTED SESSION CLASS / AUTHORITY ENFORCEMENT`

Runtime Admission **NÃO deve ser iniciado agora**.

### Exact base

`ac2b7f49f53734132d88c7d367b88d33383c384a`

### Branch autorizada

`work/w15-fnd-03-machine-license-v2-hardening`

### Target

`wave15/corrections-integration`

### Se o Codex já tiver começado Runtime Admission localmente

- parar esse trabalho imediatamente sem descartar evidência/local changes;
- não commitar/pushar/abrir PR do Admission;
- preservar o worktree apenas como trabalho suspenso;
- mudar para o hardening a partir do exact base acima;
- Runtime Admission só será retomado após revisão, integração e verificação do hardening pelo Main.

Na última verificação do Main, **não existia branch remota nem PR** para `work/w15-fnd-03-runtime-admission-v1`.

## 3. Motivo do hardening

O PR #325 integrou ESLIC2 com `viewOnlySeats`, `interactiveSeats` e `haRuntime`, preservando ESLIC1, RSA-PSS/SHA-256, hardware binding, expiry e o caminho canônico do codec.

A revisão posterior do Main encontrou dois gaps que impedem tratar o contrato como frozen:

1. `EliteScadaLicenseV2Payload` usa `int/int/bool` não-nullable; um campo omitido no JSON pode colapsar para `0/0/false`, impedindo o verifier de provar presença explícita dos novos entitlements;
2. #301 exige semântica comercial inequívoca para capacidade: **totais efetivos** ou **adições sobre Demo**. O PR #325 ainda não congelou isso semanticamente.

Além disso, o teste de assentos negativos do PR #325 exercita `CreateSignedLicenseV2`, mas não prova que o **verifier** rejeita payload ESLIC2 externo/raw-signed malformado.

## 4. Semântica comercial binding de ESLIC2

Para ESLIC2:

- `viewOnlySeats` e `interactiveSeats` representam as **capacidades totais efetivas concorrentes de Runtime Session Lease remoto/cliente** autorizadas pela licença comercial válida;
- **não** são adições sobre o Demo 2+2;
- estado Demo/no-valid-commercial-license continua usando a política Demo separada de `2 Interactive + 2 View Only` definida em #301;
- uma licença ESLIC2 válida usa diretamente seus totais assinados, sem `+2` implícito;
- `0` é valor comercial válido e explícito, significando capacidade zero para aquela classe;
- `haRuntime=false` explícito é válido e deve ser distinguível de campo ausente;
- Runtime local/canônico da instalação não consome esses assentos remotos;
- Web Runtime e EliteGO compartilharão esses mesmos totais nos slices posteriores.

Nenhum downstream pode reinterpretar esses campos sem novo delta Foundation binding.

## 5. Escopo obrigatório do hardening

O Codex deve:

1. exigir presença explícita dos campos ESLIC2 obrigatórios: `schemaVersion`, `licenseId`, `machineFingerprint`, `tier`, `issuedAtUtc`, `keyId`, `viewOnlySeats`, `interactiveSeats`, `haRuntime`; `notAfterUtc` permanece opcional/nullable;
2. distinguir corretamente ausência de `0`/`false` explícitos;
3. fazer parsing ESLIC2 estrito e determinístico: tipo JSON incorreto, valor malformado, propriedade obrigatória duplicada, schema incompatível e propriedade ESLIC2 desconhecida falham fechados;
4. garantir que `VerifyLicense` converta input ESLIC2 malformado em `LicenseState.Invalid`, sem exceção não tratada escapar para o caller;
5. preservar ESLIC1 exatamente compatível;
6. preservar assinatura RSA-PSS/SHA-256, trust anchors, machine binding, expiry e caminho canônico do codec;
7. auditar a projeção V2 -> `LicenseVerificationResult.License`, que atualmente carrega `SchemaVersion=2`, contra os consumidores existentes de TAG/runtime;
8. corrigir apenas se houver incompatibilidade real e comprovada, sem falsificar a origem/versionamento do payload;
9. documentar no contrato/código que os assentos ESLIC2 são **effective totals** e não Demo additions.

### Fora de escopo

- Runtime Admission;
- requestedClass -> grantedClass;
- quota accounting ativo;
- license install/replace/remove lifecycle;
- License Generator/UI;
- Installation UX;
- EliteGO UX;
- HA election/fencing;
- FND-04.

## 6. Critérios de aceite e regressão do hardening

No exact candidate, deve existir evidência de:

- ESLIC2 válido com `viewOnlySeats=0`, `interactiveSeats=0` e `haRuntime=false` explícitos verificando com sucesso;
- omissão de cada campo obrigatório falhando fechada, incluindo os três novos entitlements;
- assento negativo raw-signed falhando em `VerifyLicense`, não só no creator;
- seat em string/quoted number falhando;
- `haRuntime` não booleano falhando;
- null em campo obrigatório não-nullable falhando;
- propriedade obrigatória duplicada falhando;
- propriedade desconhecida em ESLIC2 schema 2 falhando;
- schemaVersion ESLIC2 incompatível falhando;
- tamper/wrong-key/wrong-machine/expiry permanecendo verdes;
- ESLIC1 permanecendo aceito com `SessionEntitlements == null`;
- regressão provando que ESLIC2 válido continua atravessando os consumidores existentes de TAG/runtime sem regressão de schema;
- `.escadapkg` permanecendo sem license/private key/fingerprint/session state;
- build/test relevantes no exact head;
- CI requerida pelo Main no exact head, com `PASS | FAIL | PENDING` explícito.

Teste requerido não executado = `PENDING`, nunca `PASS`.

## 7. CODEX -> MAIN COORDINATOR — retorno obrigatório do hardening

O retorno deve começar exatamente por:

`CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 HARDENING HANDOFF`

E conter:

1. `Status: PR_READY | BLOCKED-CONTRACT | BLOCKED-ENV`;
2. exact base SHA;
3. exact head SHA/tree;
4. branch e PR;
5. arquivos/símbolos alterados;
6. mecanismo escolhido para presença/strict-schema ESLIC2;
7. prova de que capacity semantics = **effective totals**;
8. matriz de critérios com `PASS | FAIL | PENDING`;
9. testes locais;
10. CI run/job IDs exatos;
11. skips/limitações;
12. riscos residuais;
13. itens deliberadamente não alterados;
14. confirmação de preservação de ESLIC1, signing path e hardware binding.

Codex não deve auto-mergear, auto-verificar, auto-congelar FND-03 nem liberar Runtime Admission.

## 8. MAIN COORDINATOR — tratamento do retorno

Ao receber o handoff do hardening, o Main deve:

1. revalidar branch/PR/base/head/tree;
2. revisar diff real e vazamento de escopo;
3. conferir presença estrita, fail-closed e compatibilidade ESLIC1;
4. conferir a auditoria dos consumidores de `LicenseVerificationResult.License`;
5. conferir testes raw-signed no verifier;
6. conferir CI no exact head;
7. integrar somente com evidência suficiente;
8. verificar merge SHA/parents/tree;
9. executar/confirmar CI pós-integração quando requerida;
10. somente então marcar o machine-license-v2 slice `INTEGRATED / VERIFIED`;
11. emitir nova exact base e reautorizar Runtime Admission;
12. atualizar este handoff e #301/#305.

## 9. Próximo slice planejado, MAS BLOQUEADO

Após hardening integrado/verificado, o próximo slice planejado de FND-03 é:

**FND-03 — COMMON RUNTIME ADMISSION / REQUESTED→GRANTED SESSION CLASS / AUTHORITY ENFORCEMENT**

Branch reservada futura:

`work/w15-fnd-03-runtime-admission-v1`

Ela não está ativa agora. O Main emitirá um novo exact base após o hardening.

## 10. FND-03 ainda pendente depois do hardening

Além de Runtime Admission, FND-03 ainda deverá fechar, em slices bounded conforme decisão do Main:

- shared Web + EliteGO Interactive/View Only quota accounting;
- fallback/rejection reasons;
- logical lease identity across REST/WebSocket/reconnect;
- transactional inspect/verify/replace/remove primitives requeridos por Installation switching;
- concurrency/negative regressions;
- demais critérios de #301 necessários antes de FND-03 `VERIFIED/FROZEN`.

## 11. FND-04 — contrato operacional de Script TAG Reference Resolution

**Status:** `QUEUED / CONTRACT DEFINED / NOT ACTIVE / NOT FROZEN`.

FND-04 não inicia enquanto FND-03 for a missão Foundation ativa, salvo nova ordem explícita do Main.

Branch reservada para quando for ativado:

`work/w15-fnd-04-script-tag-reference-resolution`

Target:

`wave15/corrections-integration`

### 11.1 Objetivo

Entregar contrato compartilhado, determinístico e versionável para referências de TAG em Server Script / Script Engineering no qual:

- Python visível usa referência canônica humana, normalmente path completo, por exemplo `EEE.Process.LevelPct`;
- `TagId`/Guid permanece identidade interna autoritativa;
- `tag_read` e `tag_write` usam a mesma semântica de resolução;
- backend/runtime prova que a referência textual corresponde ao `TagId` esperado antes de read/write;
- rename/move/path reuse nunca retargeta silenciosamente script para outra TAG;
- downstream `DEV-SCRIPT-ENGINEERING` recebe contrato congelado para Object Browser, autocomplete, busca, cursor insertion e diagnóstico.

### 11.2 Entradas obrigatórias

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

### 11.3 Saídas obrigatórias

- um único resolver compartilhado para read/write;
- binding persistido/versionável `referência visível <-> TagId esperado`;
- estados equivalentes a `found/notFound/ambiguous/stale/identityDrift`;
- no silent retarget;
- round-trip seguro;
- política explícita para legacy GUID/TagId;
- API/diagnóstico consumível pelo DEV;
- documentação curta das invariantes frozen.

### 11.4 Critérios de aceite FND-04

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

### 11.5 Evidências obrigatórias

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

### 11.6 Retorno Codex FND-04

Cabeçalho exato:

`CODEX -> MAIN COORDINATOR — FND-04 SCRIPT TAG REFERENCE CONTRACT HANDOFF`

Se bloquear contrato frozen externo:

`CODEX -> MAIN COORDINATOR — FND-04 BLOCKED-CONTRACT`

Codex não auto-mergeia, não congela FND-04 e não libera DEV-SCRIPT-ENGINEERING.

## 12. FC0-A

FC0-A exige:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

mais FND-08 frozen, INFRA-CI-01 ready/frozen e exact integration checkpoint com os gates requeridos.

Somente a liberação explícita do Main abre os DEVs dependentes.

## 13. Relação entre documentos

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

## 14. Guardas permanentes

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
