# Next Coordinator Chat Handoff

Copy the text below into the new coordinator chat.

---

Assuma a coordenação da Wave 14 do EliteSCADA a partir deste ponto.

## REGRA FUNDAMENTAL

**GitHub é a memória oficial e a única autoridade sobre o estado do projeto.**

Antes de qualquer decisão, diagnóstico, alteração de código, atualização de documentação, ação em PR, rerun ou merge, **REVALIDE TODO O ESTADO AO VIVO NO GITHUB**. Este texto é apenas um handoff operacional. Se houver divergência entre ele e o GitHub ao vivo, o GitHub prevalece.

Repositório:

`brunolrogerio-collab/EliteSCADA`

Branch de integração:

`wave14/corrections-integration`

PR de integração:

`#212 -> main`

O PR #212 deve permanecer **OPEN/DRAFT** e **NÃO DEVE SER MERGEADO EM MAIN** sem autorização posterior, específica e explícita do Product Owner.

Branch C26 ativa:

`wave14/c26-po-homologation-corrections`

Issue coordenadora C26:

`#286`

PR de implementação C26 -> C11:

`#287`

PR de validação C26 -> main:

`#288` — **VALIDATION ONLY / MUST NEVER MERGE**

C11:

- branch `wave14/c11-remaining-corrections`
- PR `#263`
- PR de validação `#266` — **MUST NEVER MERGE**

Pre-C26 Preview:

`#285` — preservar sem alterações como evidência histórica da homologação anterior ao C26.

## LEITURA OBRIGATÓRIA

Leia ao vivo, a partir da branch C26, nesta ordem:

1. `docs/CURRENT-COORDINATOR-HANDOFF.md`
2. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
3. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
5. `LAST CHANGE.md`
6. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
7. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

Depois revalide ao vivo:

- HEAD da branch `wave14/c26-po-homologation-corrections`;
- issue `#286` integralmente;
- PRs `#287`, `#288` e `#212`;
- workflows/checks do HEAD exato.

## ESTADO EXATO NO MOMENTO DA TRANSFERÊNCIA

HEAD C26 no momento deste handoff:

`ba901cbec8efece22ad9f2c0aa39330f98a5262e`

Commit:

`docs(w14-c26): record C26.4 and C26.5 validation`

Esse HEAD é **somente documentação**.

Último SHA exato de produto/testes com os cinco gates normais verdes:

`2a69605b746f8035a99cb7202e44329993272409`

Estado C26 no momento da transferência:

- C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
- C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
- C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
- C26.4 Runtime popup layout/composition — **IMPLEMENTED / VALIDATED**
- C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
- C26.6 Engineering shell/workspace usability — **ACTIVE, SEM COMMIT DE PRODUTO AINDA**
- C26.7+ — pendente, conforme ordem da issue #286 ao vivo.

## CI PENDENTE QUE DEVE SER RESOLVIDO PRIMEIRO

No HEAD documental `ba901cbe...`, a situação ao vivo no momento da transferência era:

- EliteSCADA CI #1439 / run `34167675250` — **SUCCESS**
- Preview Licensing CI #387 / run `34167675246` — **SUCCESS**
- Wave 11 Active HMI Runtime #365 / run `34167675231` — **SUCCESS**
- L3 Seven-Driver Lab #343 / run `34167675235` — **SUCCESS**
- Interop Lab Smoke #264 / run `34167675264` — **FAILURE**

Interop #264:

- failed job `101881863012`
- job `common-peer-stack`
- failed step `MQTT round-trip smoke`
- a causa exata no log **AINDA NÃO FOI EXTRAÍDA/CLASSIFICADA**.

Portanto:

- **não** classifique como transitório por suposição;
- **não** faça rerun cego;
- **não** comece um commit de produto da C26.6 antes de fechar a causalidade desse vermelho documental.

### SUA PRIMEIRA AÇÃO TÉCNICA

1. Revalide HEAD, issue #286, PRs protegidos e workflows ao vivo.
2. Busque o log exato do job `101881863012`.
3. Extraia a linha/contexto exato da falha de `MQTT round-trip smoke` e faça o diagnóstico.
4. Só se a evidência demonstrar falha ambiental/transitória sem relação com o commit documental, faça rerun **APENAS do job falho `101881863012`**, não do workflow inteiro.
5. Aguarde/polhe o resultado e revalide ao vivo.
6. Somente depois que a causalidade do CI estiver explicada, prossiga para implementação C26.6.

## C26.4 VALIDADA

Commits:

- `2a6f742d87e84e5dbe7fd04e48b2b4c708f6652d`
- `fb0a6c21740d5be0ea322bad9fe77dcff2de5725`

A correção é genérica para composição de Popup Runtime usando geometria authored/logical bounds, com reset específico do renderer em Runtime, stacking coerente e estabilidade da Screen subjacente.

Regression principal:

`web/scada-web/tests-wave11/c26-popup-composition.spec.ts`

## C26.5 VALIDADA

Commits:

- `cd2ace19004601430b49f922ca0a1be785a400f2`
- `2a69605b746f8035a99cb7202e44329993272409`

Diagnóstico importante:

- `/api/historical/query` existe no backend;
- a rota é mapeada somente quando HistoricalQuery está habilitado;
- o harness Wave11 habilita a feature e fornece cursor key;
- Preview #285 inicia TimescaleDB/Historian, mas não habilita a HistoricalQuery/cursor-key configuration;
- o `HTTP 404` visto pelo PO era portanto uma **lacuna de configuração do harness do Preview pré-C26**, não uma rota backend inexistente e não um estado válido de “sem dados”.

Não altere #285. O **novo Preview pós-C26** deverá habilitar HistoricalQuery com configuração segura de preview/dev.

Semântica de produto já travada por regressão:

- falha HTTP/backend continua sendo erro;
- query válida com zero rows é “sem dados”;
- texto técnico cru de transporte não aparece como conteúdo operacional do gráfico;
- Alarm / Operational Event / Audit permanecem conceitos e autoridades separados.

O SHA `2a69605...` ficou verde nos cinco gates:

- EliteSCADA CI #1438 / `34166715032`
- Preview Licensing CI #386 / `34166715046`
- Interop Lab Smoke #263 / `34166715034`
- Wave 11 Active HMI Runtime #364 / `34166715044`
- L3 Seven-Driver Lab #342 / `34166715029`

## C26.6 — ESCOPO ATIVO

Aceitação do Product Owner:

- Engineering shell/workspace utilizável em viewport limitado;
- painéis de navegação/editor podem recolher/expandir;
- painéis longos fazem scroll independente quando apropriado;
- canvas continua dominante;
- Properties continua alcançável;
- nenhuma capability é perdida.

Arquivos já identificados como relevantes, mas **REFETCH AO VIVO antes de editar**:

- `web/scada-web/src/engineering/EngineeringApp.tsx`
- `web/scada-web/src/engineering/engineering.css`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.css`

Diagnóstico preliminar:

- sidebar global Engineering ~236 px;
- Screen/Popup list ~210–260 px;
- palette ~135–170 px;
- inspector/Properties ~160–210 px;
- tudo isso pode pressionar horizontalmente o canvas ao mesmo tempo;
- o comportamento responsivo atual rearranja parte do layout, mas não oferece um modelo completo de collapse/expand controlado pelo usuário.

Direção genérica esperada:

- collapse/expand da navegação global Engineering;
- collapse/expand dos painéis laterais relevantes do editor, sempre com restauração acessível;
- scroll vertical independente em nav/list/palette/Properties longos;
- canvas absorve o espaço restante e permanece a área principal;
- Properties nunca some sem caminho claro de restauração;
- nenhuma capability/authority removida;
- regression/E2E em viewport restrito comprovando colapso/restauração, canvas utilizável e Properties acessível.

Não resolva C26.6 simplesmente diminuindo larguras ou escondendo Properties.

Antes de escrever, localize ao vivo os testes Engineering/Visual Editor já existentes e acrescente a regressão no conjunto canônico apropriado.

## COMPORTAMENTO DE EXECUÇÃO

Quando eu disser **“siga”**, avance autonomamente pelas próximas tarefas seguras, sem parar após cada microetapa apenas para informar que vai continuar.

Pare apenas quando houver:

- bloqueio real;
- decisão minha indispensável;
- ou marco substancial concluído que justifique fechamento da resposta.

**“siga” nunca autoriza merge de #212, #266 ou #288.**

## GUARDRAILS NÃO NEGOCIÁVEIS

- não alterar `main` diretamente;
- não fazer force push;
- não fazer rebase destrutivo;
- não excluir branches;
- não fazer limpeza fora de escopo;
- nunca enfraquecer testes/validações para obter CI verde;
- nunca enfraquecer segurança, autenticação, autorização, identity, Engineering Lock, licensing, lifecycle, package, drivers ou Runtime authority;
- Runtime/Active não pode depender de `.escadalib`;
- backend continua autoridade onde o contrato assim determina;
- Active Revision continua autoridade de Runtime;
- Alarm / Operational Event / Audit permanecem separados;
- diagnosticar todo CI vermelho antes de qualquer rerun.

## SEQUÊNCIA PÓS-C26

Depois de C26.1–C26.10 aceitos conforme issue #286 ao vivo:

1. integrar C26 em C11 via #287 quando a sequência autorizar;
2. regenerar package/checksum/provenance;
3. criar **NOVO Preview pós-C26**;
4. não reutilizar #285;
5. homologação real do Product Owner em Codespace;
6. resolver eventuais gaps genéricos restantes;
7. só então avançar para variante real EEE Modbus/PLC;
8. aceitação final Wave14.

## OBRIGAÇÕES WAVE14 AINDA MANDATÓRIAS

Não deixe desaparecer do backlog:

1. backup/restore protegido do sistema completo, separado de `.escadapkg`;
2. administração segura de backup/export/import/restore do Historian;
3. scaling genérico TAG raw -> engineering:
   - HMI/Alarm/Historian/Trend consomem valor canônico em engenharia;
   - writes aplicam transformação inversa explicitamente e falham fechado;
4. configuração humana de casas decimais persistindo em Save -> Publish -> Activate -> Runtime -> package export/import;
5. variante real EEE Modbus/PLC apenas depois dos mecanismos genéricos, package corrigido e nova homologação.

Comece revalidando o GitHub ao vivo. Não confie neste texto onde GitHub puder responder diretamente.

---
