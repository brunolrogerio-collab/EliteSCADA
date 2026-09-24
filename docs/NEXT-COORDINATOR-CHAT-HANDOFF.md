# SUCCESSOR TAKEOVER SNAPSHOT — 2026-09-24

> **READ THIS SECTION FIRST.** It supersedes any older/current-state wording later in this historical handoff when there is a conflict. GitHub live remains the sole authority.

## Current product/Foundation state

- FND-06 is **VERIFIED/FROZEN** at exact product SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`, tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`.
- Exact final broad: EliteSCADA CI #1565 / run `35953557122` — SUCCESS (Web, Backend build/test/smoke, Chromium E2E).
- FND-04 remains VERIFIED/FROZEN.
- No FC0-A DEV, FND-05 or FND-07 is released.

## Post-FND06 audit — authoritative result

The Product Owner clarified that the **Main Coordinator owns and executes the audit**. A separate AUD chat is optional/advisory and is not a release prerequisite.

Audit:
`FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

Authoritative result:
`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — CHANGES_REQUIRED`

Exact audited checkpoint:
- SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`
- broad `35953557122` SUCCESS.

Primary report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`
commit `463d357f8a9c11f774f5da59480e4a17a19569f5`.

Confirmed release blockers:
1. `W15-P1-01` — Server Script bounded automatic recovery is missing; throttle can remain latched until explicit `ResetThrottle()`.
2. `W15-P1-06` — Engineering shell can synthesize `Demo Project` and no-model `unsaved/clean` while the authoritative public model is unavailable.

Contract conclusion:
- FND-05 = **ADDITIVE / COMPATIBLE** under frozen FND-03/FND-04/FND-06 guards.
- FND-07 = **COMPOSITIONAL / COMPATIBLE** under frozen FND-01/FND-02/FND-03 guards.
- No current FND-05/FND-07 decision requires breaking a frozen contract consumed by FC0-A DEVs.

## Second-pass deep audit — completed

A distinct second-pass audit was completed after the first CHANGES_REQUIRED result.

Report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-SECOND-PASS.md`

Report commit:
`43c266949236af377ba859c9b8f09fa2d3a3a31a`

Audit control rev 0012:
`62a731cc4291b751e9cb2e91e440fcf0d5ffb3bd`

Release-prep refinement:
`c78895b218608b511e61b90206189d2b641a1e71`

Second-pass conclusion:
`NO NEW PRE-FC0A BLOCKER IDENTIFIED`.

Additional/refined downstream findings:
- DEV-EDITOR must consume the frozen FND-06 compatibility seam for known-legacy marquee/geometry/z-order/multi-object authoring paths that still use strict built-in lookup.
- DEV-SCRIPT must make Script Assistant consume the FND-06 compatibility seam for known-legacy property discovery.
- Script Engineering already has cursor-aware insertion and timer/tagChanged authoring; those are regression/refinement scope, not greenfield.
- Python API Help still lacks a formal signature/parameter/return/example contract.
- DEV-LICENSING must surface requested vs granted Runtime class, explicit ViewOnly request and Interactive-quota fallback/reason UX; backend Authority/capacity contract is already sound.
- one minor Runtime API message still says `viewer` where canonical public vocabulary is `viewOnly`.
- W15-P2-01 Trends still lacks explicit realtime/reconnect/last-request/last-success/freshness observability.
- W15-P2-02 shared Popup/live-value path correctly handles numeric zero/quality but still lacks explicit freshness-age/reason telemetry.
- production host uses `EngineeringWorkspace(seedDemo:false)`; truthful neutral no-Demo workspace is already representable.
- existing Authority detach/attach/switch primitives further reduce FND-07 contract risk.

## Current active correction — P1-01

Active order:
`FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`

Control:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`

Control content SHA at takeover:
`6c25239f2f9cde8e7f2ef0c38bd9741168e491d3`

Work branch:
`work/w15-fc0a-p101-server-script-recovery`

Exact base:
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

Validation profile:
`SCRIPT_RUNTIME`

Live revalidation at takeover:
- work branch = **IDENTICAL** to exact base;
- ahead 0 / behind 0;
- changed files 0;
- open PR from that branch: **none**.

The same sequential CODEX used for prior Foundation work is the intended executor. On its next `SIGA`, it must re-read the live control and execute only the current P1-01 order.

## Queued correction — P1-06

Queued only:
`FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

Do **not** mix P1-06 into P1-01.

P1-06 activates only after P1-01 is reviewed/integrated/validated and Main advances the order.

## Release status

Current release matrix:
- DEV-EDITOR: HOLD
- DEV-SCRIPT-ENGINEERING: HOLD
- DEV-AUTHORITY-UX: HOLD
- DEV-LICENSING-UX: HOLD
- FND-05: HOLD
- FND-07: HOLD

Release requires:
1. P1-01 closeout;
2. P1-06 closeout;
3. exact-head + post-merge validation for each correction as applicable;
4. Main re-runs affected audit rows;
5. only then may Main decide `ACCEPTABLE / FC0A_RELEASE_APPROVED`.

## Immediate successor action

On takeover:
1. re-read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `LAST CHANGE.md`, this file, Issue #305 and the live P1-01 control;
2. revalidate `work/w15-fc0a-p101-server-script-recovery` against exact base;
3. inspect Issue #305 for a `FC0-A P1-01 CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`;
4. if no candidate exists and the CODEX order remains ACTIVE, keep that exact order active; do not invent another mission;
5. when candidate arrives, perform Main review, exact-head CI, merge authority check, post-merge broad, then advance to P1-06;
6. after both blockers close, rerun the affected FC0-A audit rows before any DEV/FND-05/FND-07 release.

Do not interpret older sections below this snapshot as current when they conflict with this section.

---

# Next Coordinator Chat Handoff — Permanent Bootstrap Prompt

Use the prompt below whenever a new Main Coordinator chat takes over EliteSCADA.

This prompt is intentionally **generic and state-independent**. It does not embed the current Wave, SHA, PR, issue, mission or CI state. The coordinator must reconstruct those from GitHub live.

---

# MAIN COORDINATOR — ELITESCADA

Assuma agora a função de **MAIN COORDINATOR do desenvolvimento do EliteSCADA**.

Repositório:

`brunolrogerio-collab/EliteSCADA`

Sua função é coordenar o projeto, não apenas responder perguntas.

Você é responsável por reconstruir o estado real do projeto, auditar arquitetura/contratos/PRs/testes, definir work packages, coordenar CODEX/DEVs/AUDs, controlar integração e dependências, operar CI quando necessário e manter o GitHub como memória persistente.

## 1. REGRA FUNDAMENTAL

**GITHUB LIVE É A AUTORIDADE SOBRE O ESTADO DO PROJETO.**

Não presuma que qualquer estado trazido pelo prompt, memória de chat, resumo, comentário antigo ou SHA histórico ainda esteja atual.

Antes de decisão material, revalide GitHub live.

Se houver divergência entre GitHub live e documentação operacional, GitHub live prevalece. Corrija a documentação stale antes de emitir nova ordem.

## 2. PRIMEIRO HANDOFF A LER

Descubra a Wave/etapa atual e leia integralmente o handoff operacional canônico correspondente.

Durante Wave 15, o canal canônico é:

`docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`

Esse handoff contém memória persistente, protocolo de sucessão e `CURRENT ORDER` de agentes.

Leia também, conforme aplicável:

- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `README.md`
- `docs/README.md`
- `docs/CURRENT-COORDINATOR-HANDOFF.md`
- `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
- `docs/ROADMAP.md`
- `docs/CHAT-COLLABORATION-PROTOCOL.md` quando existir/aplicável
- ADRs e handoffs ativos
- issues coordenadoras
- PRs/branches das missões ACTIVE
- Actions relevantes

## 3. PRIMEIRA EXECUÇÃO — NÃO PERGUNTE O ESTADO AO PRODUCT OWNER

Descubra o estado.

Antes de produzir uma ordem, reconstrua:

- Wave/etapa ativa;
- integration branch;
- HEAD/tree live;
- **PRODUCT CHECKPOINT SHA/TREE**;
- diferença entre product checkpoint e coordination/documentation HEAD;
- Foundation/workstream states;
- missões ACTIVE;
- lane owner;
- exact authorized product base;
- exact candidate head/tree;
- PR state;
- CI exata;
- blockers/dependencies;
- próximo gate;
- trabalho paralelo já ativo.

Depois compare essa reconstrução com o handoff canônico. Se ele estiver stale, atualize-o antes da próxima ordem.

## 4. STATE MACHINE

Quando aplicável:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

Nunca colapse estados:

- `PR_READY != INTEGRATED`
- `INTEGRATED != VERIFIED`
- `VERIFIED != FROZEN`

CI verde pré-merge não substitui validação do exact integrated SHA quando o gate exige pós-merge.

## 5. PRODUCT CHECKPOINT VS COORDINATION HEAD

Sempre diferencie:

**PRODUCT CHECKPOINT SHA/TREE** = último código/infra de produto integrado e validado.

**COORDINATION HEAD** = HEAD live da branch, que pode avançar somente por documentação de coordenação.

Commit documental não cria automaticamente novo product base.

Antes de autorizar DEV/CODEX, declare exact product base. Se a integração estiver à frente apenas por docs, registre isso. Se houver delta de produto/infra, reavalie a base.

## 6. PAPEL DO MAIN COORDINATOR

A auditoria arquitetural/contratual e a definição do work package são responsabilidade do Main.

Você deve:

- ler código/diff quando necessário;
- identificar a autoridade canônica;
- detectar pipelines/registries paralelos;
- revisar contratos e migrations;
- definir invariantes, scope e acceptance;
- definir testes e validation profile;
- definir base/branch/target;
- revisar handoffs e evidência independentemente;
- controlar sequencing e dependências;
- decidir correções e integração;
- manter memória persistente.

Não delegue ao Codex uma auditoria arquitetural aberta que você pode executar diretamente. Use Codex prioritariamente para implementação bounded, correção bounded ou investigação técnica estreita.

## 7. CHATS/AGENTES PARALELOS

Pode haver Main, CODEX, Foundation DEV, feature DEVs, AUD, CI/infra e outros agentes.

Antes de iniciar missão:

1. descubra missões ACTIVE;
2. confira branches/PRs;
3. confira ownership;
4. identifique superfícies compartilhadas;
5. confirme dependências frozen/not frozen;
6. evite conflito de escrita/arquitetura.

DEV implementa sua lane autorizada. AUD revisa/testa candidate indicado. AUD não substitui auditoria arquitetural do Main.

## 8. CANAL DE ORDENS

O handoff operacional canônico da Wave é o canal primário de ordens.

Quando a ordem mudar:

1. atualize `CURRENT ORDER` da lane correta;
2. confirme sucesso da escrita;
3. faça readback live;
4. opcionalmente espelhe em issue/PR;
5. só então diga ao Product Owner que a ordem foi dada.

Nunca diga “ordem dada” se ela existe apenas no chat.

## 9. AUTORIDADE PERMANENTE DE COMUNICAÇÃO

O Product Owner autoriza permanentemente o Main a:

- atualizar handoffs de coordenação;
- enviar/espelhar ordens aos CODEX/DEVs/AUDs;
- confirmar essas ordens depois de readback live.

Isso não autoriza automaticamente alteração de código de produto, merge, force push, mudança de workflow ou escrita em `main`.

## 10. AUTORIDADE PERMANENTE DE CI

O Product Owner autoriza permanentemente o Main Coordinator a **operar CI/GitHub Actions para validação pré-merge e pós-merge** sem pedir nova autorização a cada execução.

O Main pode:

- inspecionar runs/jobs/steps/logs/artifacts;
- disparar/rerodar CI necessária para exact candidate SHA;
- rerodar job específico ou failed jobs quando tecnicamente justificado;
- validar exact merge SHA / exact integration SHA pós-merge;
- operar CI de merge/checkpoint/release conforme os workflows existentes e a governança vigente;
- usar workflow dispatch ou mecanismo equivalente quando o workflow suporta, a ferramenta disponível permite e o exact ref/SHA correto é preservado.

### Guardas de CI

Antes de rerun:

- diagnostique o vermelho;
- identifique run/attempt/job/step/test/erro;
- determine se é regressão, flake, ambiente, teste stale, fixture, race, workflow, dependência ou incompatibilidade;
- formule uma hipótese concreta.

Ao rerodar:

- preserve o exact SHA que precisa ser validado;
- prefira o menor rerun suficiente;
- registre run/attempt/job e resultado material;
- não entre em loop de rerun; nova falha exige novo diagnóstico antes de nova execução.

Nunca use autoridade de CI para:

- alterar produto sem work package;
- enfraquecer teste/gate;
- alterar workflow YAML/filtros para obter verde;
- criar commit vazio/artificial só para acordar CI;
- retargetar/rebasear artificialmente PR para gerar nova execução;
- force push/destructive rebase;
- esconder/deletar evidência.

Se CI automática pós-merge não aparecer, leia o workflow real (`on:`, branches, paths, filters) antes de concluir ou disparar alternativa manual.

**CI verde não autoriza merge.**

## 11. AUTORIDADE DE MERGE É SEPARADA

Merge deve obedecer a governança live.

- Integração em branch coordenada exige a autorização aplicável ao work package/estado.
- Merge em `main` é protegido e requer autorização final explícita do Product Owner quando assim definido.
- `SIGA`, CI verde, aprovação, VERIFIED/FROZEN ou conclusão da Wave não significam automaticamente “merge em main”.

## 12. PROTOCOLO `SIGA`

Quando Product Owner disser `SIGA` ao Main:

- revalide GitHub live;
- continue autonomamente o fluxo seguro autorizado;
- tome decisões de coordenação;
- opere CI dentro da autoridade permanente quando necessário;
- atualize ordens no handoff;
- envie ordens diretamente aos agentes;
- não use o Product Owner como mensageiro.

Pare apenas por blocker real, decisão de Produto, risco material, autorização protegida ausente ou falta de trabalho seguro.

Para agentes, `SIGA` significa reler o handoff live e executar somente a `CURRENT ORDER` de sua lane.

`WAIT` = não mutar.  
`STOP` = devolver evidência e parar.

## 13. WORK PACKAGE BOUNDED

Uma missão deve declarar, quando aplicável:

- identificador/owner;
- parent issue;
- objective;
- exact base SHA/tree;
- branch;
- target;
- hard/soft dependencies;
- frozen contracts consumidos;
- owned boundary;
- forbidden scope;
- invariants;
- acceptance;
- testes;
- validation profile;
- CI gates;
- handoff format;
- STOP/BLOCKED conditions;
- proibições de merge/freeze/downstream.

Evite missões vagas.

## 14. REVIEW DE ENTREGA

Não aceite “concluído” como prova.

Revalide:

- base/head/tree;
- PR/diff;
- arquivos e contratos;
- scope leakage;
- testes/negativos/concorrência;
- migrations/compatibilidade;
- segurança;
- CI no exact candidate;
- skips/PENDING;
- riscos;
- downstream impact.

Procure duplicação de autoridade: segundo registry, resolver, licensing path, authorization path, quota authority ou estado concorrente para o mesmo conceito.

## 15. TESTES E EVIDÊNCIA

Use:

- `PASS`
- `FAIL`
- `PENDING`
- `NOT_APPLICABLE`

Teste obrigatório não executado = `PENDING`, nunca PASS.

Prefira validação proporcional ao risco, mas execute gates de checkpoint/release quando exigidos.

## 16. CI VERMELHA

Diagnostique antes de corrigir/rerodar.

Classifique causa possível:

- regressão real;
- stale test/fixture;
- ambiente;
- race/flake;
- workflow;
- dependência externa;
- incompatibilidade de contrato.

Faça a menor correção causal. Nunca flexibilize segurança ou contrato para obter verde.

## 17. INTEGRAÇÃO E PÓS-MERGE

Antes de integrar:

- candidate exato revisado;
- diff bounded;
- acceptance suficiente;
- CI conhecida;
- blockers resolvidos;
- autoridade de merge válida.

Depois do merge:

1. capture merge SHA;
2. parents;
3. tree;
4. novo integration HEAD;
5. valide CI no exact integrated SHA;
6. opere/rerode CI pós-merge diretamente se necessário e justificável pelas guardas;
7. somente depois promova `INTEGRATED -> VERIFIED -> FROZEN` do slice aplicável.

Nunca congele Foundation inteira só porque um slice terminou.

## 18. SEGURANÇA E AUTORIDADES

Mudanças em Identity, authentication, authorization, Security Authority, Licensing, lifecycle, package/import, Active Runtime, HA/fencing e session lease exigem revisão reforçada.

Nunca:

- conceda privilégio por nome de role;
- transporte segredo/chave privada em artefato Engineering;
- misture autoridades independentes por conveniência;
- enfraqueça fail-closed para CI passar.

## 19. GITHUB COMO MEMÓRIA PERSISTENTE

Decisão crítica não pode existir apenas no chat.

Persista conforme apropriado:

- ativação/base/scope;
- blocker;
- decisão arquitetural;
- revisão/correção requerida;
- CI material;
- integração/pós-merge;
- VERIFIED/FROZEN;
- dependência liberada;
- próxima ordem.

Atualize o menor conjunto autoritativo. Não transforme o repositório em log de polling trivial.

## 20. MEMÓRIA DE SUCESSÃO

Antes de terminar etapa material, deixe informação suficiente para outro coordenador reconstruir:

- o que ocorreu;
- por quê;
- estado;
- exact SHAs;
- PR/CI;
- blockers;
- próxima ordem.

Toda lição de processo que evite repetição de erro deve entrar no handoff operacional canônico.

## 21. PRIMEIRA AÇÃO AO RECEBER ESTE PROMPT

Não pergunte ao Product Owner qual é o estado.

Faça agora:

1. leia o handoff canônico live;
2. reconstrua GitHub live;
3. identifique divergências stale;
4. corrija o handoff se necessário;
5. identifique `CURRENT ORDER` de cada lane;
6. identifique entregas esperando decisão;
7. execute a próxima ação segura já autorizada;
8. opere CI se um gate pré/pós-merge exigir e as guardas permitirem;
9. envie ordens aos agentes pelos canais definidos;
10. retorne ao Product Owner apenas um resumo curto: estado real, validações, decisão, ordens efetivamente emitidas e blockers humanos.

Não termine apenas dizendo o que pretende fazer quando uma ação segura/autorizada puder ser executada agora.

## 22. COMPORTAMENTO

Seja um coordenador ativo:

- investigue;
- decida dentro da autoridade;
- emita ordens;
- opere CI dentro das guardas;
- revise retornos;
- controle sequencing;
- atualize memória persistente.

Mas nunca:

- invente fatos;
- ultrapasse autoridade de merge/código;
- declare sucesso sem evidência;
- esconda incerteza;
- use o Product Owner como mensageiro quando houver canal direto.

Ao final de cada interação de coordenação, informe:

`Hora: HH:MM`

usando `America/Sao_Paulo`.


## Current Wave 15 correction for successor

Important live routing:
- the same sequential CODEX chat/lane that worked prior Foundation stages including FND-04 is the active FND-06 executor;
- FND-04 control rev 0016 contains `ROUTE-SEQUENTIAL-CODEX-TO-FND06-12`;
- on `SIGA`, that CODEX must read `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md` and execute `FND06-CODEX-VISUAL-STABILITY-V2`;
- do not let the historical FND-04 frozen state turn the shared CODEX lane into WAIT.

FC0-A sequencing:
- after FND-06 VERIFIED/FROZEN, do **not** release DEVs immediately;
- first run `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
- audit control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-CONTROL.md`;
- audit must correlate final Wave 14 findings, Wave 15 premises/gaps, frozen contracts and FND-05/FND-07 compatibility;
- only `ACCEPTABLE / FC0A_RELEASE_APPROVED` releases the four FC0-A DEVs and allows FND-05/FND-07 to activate in parallel.


## Latest live blocker for successor — INFRA-CI-01B

Do not freeze FND-06 yet.

FND-06 PR #337 merged at `624f2eca456310a2c6156538b3616a06e3be075f`, but exact broad post-merge CI `35940661531` failed on generic PostgreSQL schema initialization `23505 / pg_namespace_nspname_index`.

Active same sequential CODEX order:
`INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`

Control:
`coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`

Work:
`work/w15-infra-ci-01b-postgresql-schema-init`

Do not blind-rerun the failed CI. Fix/review/merge the bounded infrastructure race, require exact broad green CI, then freeze FND-06 and activate the independent post-FND06 FC0-A audit.


## Main audit result after FND-06 freeze

Do not wait for a separate AUD chat to decide FC0-A. Product Owner clarified that the Main Coordinator is responsible for the audit.

Main completed:
`FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

Result:
`CHANGES_REQUIRED`

Authoritative report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`
commit `463d357f8a9c11f774f5da59480e4a17a19569f5`.

Confirmed blockers:
- W15-P1-01 Server Script bounded recovery;
- W15-P1-06 truthful Engineering fallback.

No current contract break is required by FND-05/FND-07.

Current active executor order:
`FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`

Same sequential CODEX reads:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`

Work branch:
`work/w15-fc0a-p101-server-script-recovery`

P1-06 is queued after P1-01. FC0-A DEVs, FND-05 and FND-07 remain HOLD until both blockers close and Main re-audits affected rows.


## Post-FND06 audit — second-pass deep review

Main completed a distinct second-pass audit on the frozen baseline:
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`.

Report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-SECOND-PASS.md`

report commit:
`43c266949236af377ba859c9b8f09fa2d3a3a31a`

audit control rev 0012:
`62a731cc4291b751e9cb2e91e440fcf0d5ffb3bd`

release-prep refinement:
`c78895b218608b511e61b90206189d2b641a1e71`

Second-pass conclusion:
`NO NEW PRE-FC0A BLOCKER IDENTIFIED`.

The two confirmed release blockers remain:
- W15-P1-01 Server Script bounded recovery;
- W15-P1-06 truthful Engineering no-model/loading/error state.

New/refined downstream findings:
- DEV-EDITOR must consume FND-06 compatibility for known-legacy marquee/geometry/z-order/multi-object authoring paths that still use strict built-in lookup;
- DEV-SCRIPT must make Script Assistant consume the FND-06 compatibility seam for known-legacy property discovery;
- Script Engineering already has cursor-aware insertion and timer/tagChanged authoring, so those are regression/refinement scope, not greenfield;
- Python API Help still lacks a formal signature/parameter/return/example contract;
- DEV-LICENSING must surface requested vs granted Runtime class, explicit ViewOnly request and Interactive-quota fallback/reason UX; backend Authority/capacity contract is already sound;
- one minor Runtime API message still says `viewer` where canonical public vocabulary is `viewOnly`;
- W15-P2-01 Trends still lacks explicit realtime/reconnect/last-request/last-success/freshness observability;
- W15-P2-02 shared Popup/live-value path correctly handles numeric zero/quality but still lacks explicit freshness-age/reason telemetry.

Contract result strengthened:
- FND-05 remains ADDITIVE / COMPATIBLE;
- FND-07 remains COMPOSITIONAL / COMPATIBLE;
- production host uses `EngineeringWorkspace(seedDemo:false)`, so a truthful neutral no-Demo workspace is already representable;
- existing Authority detach/attach/switch primitives further reduce FND-07 contract risk.

No DEV/FND-05/FND-07 release occurs until P1-01 and P1-06 close and Main reruns the affected release rows.


## Main Coordinator takeover — third post-FND06 audit pass (2026-09-24)

Live takeover revalidated against GitHub after coordinator-chat rotation.

- frozen product checkpoint remains `560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- post-FND06 FC0-A audit remains `CHANGES_REQUIRED`;
- third Main pass outcome: `NO NEW PRE-FC0A BLOCKER IDENTIFIED`;
- authoritative third-pass report: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-THIRD-PASS.md`, commit `b144de748337045bf447a5142d825fa0beac1ff1`;
- audit control rev 0013: `0d27fe59d154c161418c996a4ea2df6fe59066e6`;
- confirmed release blockers remain only W15-P1-01 and W15-P1-06;
- active order remains `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1` on `work/w15-fc0a-p101-server-script-recovery`;
- work branch revalidated identical to exact base: 0 ahead / 0 behind / no changed files / no PR;
- P1-06 remains queued separately; it must not be mixed into P1-01;
- DEV-EDITOR, DEV-SCRIPT-ENGINEERING, DEV-AUTHORITY-UX, DEV-LICENSING-UX, FND-05 and FND-07 remain HOLD.

Third-pass refinements are downstream/pre-activation only: P2-03 shell responsiveness remains open; P2-04 account accessibility is substantially implemented but needs focused keyboard regression; P2-05/P2-06 remain Engineering layout-density residuals; P2-07 is partially implemented and should not rebuild the existing Dynamo insertion preview; Help routing is surface-level rather than Engineering-section-contextual; FND-07 gained an explicit Engineering Lock × detach/switch/neutral-bootstrap acceptance guard. FND-05 remains additive/compatible; FND-07 remains compositional/compatible.

FND control refreshes:
- FND-05 hold/activation metadata: `0c69dd307880b6afcd89f352cf76fef183087410`;
- FND-07 hold metadata + Lock/detach acceptance guard: `d0aba9e375a8330b7720b32f4defcdabf0ec12ae`.

No product code, product branch or release state was changed by this audit pass.


## Main decision — consolidated FC0-A correction package (2026-09-24)

Product Owner requested that all known FC0-A findings be corrected now where safely possible, with one larger CODEX delivery before the next Main review.

Active order:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V2`
- control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`
- control commit: `0ab4a2f9226a1f3710aa516dff9534d880023218`
- branch: `work/w15-fc0a-consolidated-corrections`
- exact base: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- exact base tree: `674019fbbc21001a2d68deb853c2c0b293e0a5cb`
- one consolidated PR only after the package is complete and exact-head validation is green.

The former P1-01-only order/branch is superseded unused; it was identical to base with no PR at supersession.

The package includes the two mandatory blockers plus confirmed shared/downstream residuals that can be safely brought forward: shell responsiveness, account keyboard regression, Engineering scroll composition, Engineering Lock density, Trends/live-value freshness observability, contextual Help routing, canonical viewOnly wording, frozen FND-06 compatibility consumption in Editor/Script Assistant, structured Script API Help/representative recipe, Runtime Session/Licensing requested/granted UX, and Template/Equipment/Library inspection.

Evidence-bounded/speculative items and future Foundations remain outside the package. Frozen FND-01/02/03/04/06/08 semantics may not be redefined.

Sequential CODEX route updated to rev 0027 / commit `93d79a773d7571677a9f43e6f3a80ad21aa25612`.
No intermediate Main review is required; CODEX returns one final candidate handoff unless a real contract/base/environment blocker prevents safe completion.


## Main decision — six prepared parallel DEV chats / CODEX as sequential validator (2026-09-24)

Product Owner clarified that the previous `normally no more than four active coding DEVs` rule was a historical operational throttle for a different context and is **not** a permanent Wave 15 concurrency limit.

Prepared post-FC0A implementation chats:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05 DEV;
- FND-07 DEV.

Canonical cross-lane coordination:
- branch: `coord/w15-parallel-dev-control`
- file: `docs/WAVE15-PARALLEL-DEV-CONTROL.md`
- prepared control commit: `87479d0bc3042435935ac1dbaa119d3a7ed72bd6`

All six are **PREPARED / BLOCKED** until Main records `FC0A_RELEASE_APPROVED` on an exact integrated SHA/tree. No work branch is created before that exact activation base is known.

Execution pipeline:
`normal DEV chat implements code -> Main reviews -> same DEV corrects material defects -> Main accepts exact candidate for CODEX -> sequential scarce CODEX writes/extends tests, runs focused/adversarial local validation and exact-head T1 -> Main finalizes integration`.

CODEX may make small validation-driven corrections that do not redesign the feature. Material product/design defects return to the owning DEV. Frozen-contract insufficiency returns to Main.

Feature DEV results stay in **separate PRs**; they are not combined into a raw monolithic implementation package. After individually accepted/T1-green merges, Main runs broader integrated T2 on the exact integration head.

FND-05/FND-07 also move to normal-chat DEV implementation:
- FND-05 prepared order: `FND05-DEV-HA-AUTHORITY-V1`; dedicated control rev 0004 / commit `f3f685cff16ebfef53db4aa69b061d7bac773829`.
- FND-07 prepared order: `FND07-DEV-DETACH-NEUTRAL-V1`; dedicated control rev 0003 / commit `ae92f5f1ec55464eea7f231c178d0a05dc04df13`.

Foundation validation remains stricter:
`DEV -> Main contract review -> CODEX adversarial/focused validation -> exact-head T1 -> Main merge -> post-merge validation -> VERIFIED/FROZEN`.

FND-05 additionally requires `CODEX_HA_ADVERSARIAL_GREEN`.

Release preparation was updated:
- `coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`
- commit `349ed55e74b237c77dec64971a7f8dac3ee97c7e`.

ROADMAP current coordination update:
- commit `08b11987b78adee134de77906fd57b2af199e5fb`.

Current FC0-A product work remains PR #340 / V2 IN PROGRESS. This coordination preparation does not activate any downstream lane and does not alter the current CODEX mission.

Bootstrap texts for each lane will be generated on Product Owner request; the bootstrap is only onboarding convenience and GitHub live controls remain authoritative.


## Main decision — two-stage fresh-install partial preview (2026-09-24)

After the four feature DEV lanes are integrated/T2-verified and FND-05/FND-07 are independently VERIFIED/FROZEN, Main will run a partial first-project product audit before later EEE/complete-product acceptance.

Prepared control:
- branch: `coord/w15-fresh-install-preview-control`
- file: `docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`
- prepared commit: `8d209c8f0cacf5c1feb050632645c9537e1f09dc`

Prepared gates:
- `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`
- `W15-FIRST-PROJECT-HUMAN-PREVIEW-01`

The two first-project journeys are independent.

CODEX moment:
- fresh installation / no project / no EEE / no hidden Demo project;
- user-like browser exploration;
- high-level objective only: create a first SCADA application from zero and reach a functional truthful Runtime;
- no source/control/database/internal API lookup during the black-box journey;
- no code correction during exploration;
- source/log/API diagnosis is allowed only after the journey completes or is blocked;
- detailed CODEX findings are persisted but not surfaced to Product Owner before the human preview.

Human moment:
- second independent clean environment;
- Product Owner repeats the same high-level first-project mission as a real user;
- no CODEX-created project;
- no detailed CODEX report/checklist before the unaided human journey completes;
- needing help or becoming blocked is itself audit evidence.

After both journeys:
- Main unseals and compares findings as BOTH / CODEX_ONLY / HUMAN_ONLY / PATH_DIVERGENCE / NOT_REPRODUCED;
- a directed follow-up may then cover restart/persistence, Authority/ViewOnly, FND-07 detach -> neutral bootstrap -> Project B -> B/A switching, and a separate HA user-surface/manual transfer check;
- material blockers are corrected/owned; non-blocking onboarding/usability gaps may feed later Installation UX/product convergence.

T1/T2/T3/T4 green evidence does not replace these audits.

Possible partial-preview dispositions:
- `ACCEPTABLE_FOR_NEXT_CONVERGENCE`
- `CHANGES_REQUIRED`

Neither is final Wave 15 acceptance. EEE v15, later T3/T4 and final fresh complete-product Preview remain mandatory.

Roadmap update: `57729b9db0ebcac2c748a9f6f0101eb48c5b625b`.
Six-lane control link update: `e130b5353c320e9c22f8e1f8f3a7e42a2dd6946c`.

This preparation does not activate the preview now and does not alter current PR #340 / CODEX V2 execution.


## Main review — PR #340 V2 complete / narrow V3 closeout active (2026-09-24)

Main reviewed final V2 candidate:
- PR #340;
- head `150b808140a5fdf80afd0c88d46ea80f83f630b2`;
- tree `c2bfbbfe705be64e5c1aac314f96bf8851a913b0`;
- 15 commits / 41 changed files;
- natural Wave 15 T1 `36033152318`: SUCCESS across classification, Common T1 sanity, Focused .NET, Focused Chromium, Web semantic build and final gate.

Disposition:
`FC0-A CONSOLIDATED V2 -> MAIN COORDINATOR — CHANGES_REQUIRED / NARROW V3`

Accepted V2 closures remain preserved. Two bounded groups remain before merge:
1. actual user-facing Runtime Session Class request/status surface for ViewOnly/Interactive requested-vs-granted/reason truth without FND-03 redesign;
2. missing R6 mounted evidence for shell no-overflow, Engineering independent scroll, compact Engineering Lock lifecycle and known-legacy advanced-authoring/unknown containment.

Binding control:
- `coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`
- order `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V4`
- section 12
- commit `6a13c88fd112fe94e9c87e1206d9840be75161eb`.

Sequential CODEX route:
- rev 0030
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V3-18`
- commit `7011f7e2a29f37c105657b1a56ceac62a6ef4d58`.

PR #340 Main review comment: `5819091955`.
Issue #305 ledger comment: `5819092476`.

No merge/freeze/release occurred. All downstream six lanes remain PREPARED/HOLD until FC0-A final acceptance.


## Main review — PR #340 V3 product accepted / final evidence-only closeout active (2026-09-24)

Exact V3:
- head `1efc16994ea7b11857b0a1aac7da1690276e6d07`
- tree `da022f95a0c2342188a34ffe6dc830acf84d873a`
- natural T1 `36036316797`: SUCCESS.

Main accepts the V3 Runtime Session Class product surface and shell compact-width product direction.

Merge remains blocked only on final test/evidence closure:
- explicit Engineering scroll-composition proof;
- compact Engineering Lock lifecycle proof including lock-now + clear;
- known-legacy advanced-authoring + arbitrary-unknown containment proof;
- focused execution of owner specs that natural T1 did not select.

The T1 Chromium job on V3 executed 16 tests but did not select the Runtime Session mounted spec, app-shell, Engineering Lock or legacy owner-model specs, so its green result is not used as proof for those rows.

Binding control:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V5`
- section 13
- commit `1485bd3d832a57b109d347e0b931b21334d56844`

CODEX route:
- rev 0031
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V4-19`
- commit `63afda347e798d5e45292161fab382911093ae42`

This is test/validation-only. No new feature scope is authorized.

PR #340 Main comment: `5819317792`.
Issue #305 ledger: `5819318386`.

No merge/freeze/release occurred. Six downstream lanes remain PREPARED/HOLD.
