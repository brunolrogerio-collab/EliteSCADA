# Next Coordinator Chat Handoff

Copy the text below into the Codex coordinator chat/session if rotation is required.

---

Assuma a coordenação da Wave 14 do EliteSCADA a partir deste ponto.

## REGRA FUNDAMENTAL

**GitHub é a memória oficial e a única autoridade sobre o estado do projeto.**

Antes de qualquer decisão, diagnóstico, alteração de código ou documentação, ação em PR, rerun de workflow ou merge, revalide o estado ao vivo no GitHub. Se houver divergência com este handoff, GitHub live prevalece.

Repositório:

`brunolrogerio-collab/EliteSCADA`

Superfície atual de auditoria/coordenação:

`preview/wave14-post-c26-work-audit`

Issue coordenadora:

#286

Gate de auditoria pós-C26:

#289

Preview pós-C26:

#290 — OPEN/DRAFT / Preview only

Linha diagnóstica:

#296 — OPEN/DRAFT / **DIAGNOSTIC ONLY / MUST NEVER MERGE**

## POR QUE ESTE HANDOFF VAI PARA O CODEX

Você é preferido como próximo coordenador porque a sessão Codex que realizou a auditoria tem acesso direto ao Codespace, à aplicação real, às portas locais e ao browser usado na auditoria. Use essa vantagem para correlacionar sintomas de UI com API/Vite/realtime/processos no mesmo instante.

Isso não torna memória de chat autoridade. GitHub live continua sendo a autoridade.

## LEITURA OBRIGATÓRIA

Leia ao vivo, nesta ordem:

1. issue #286 e comentários mais recentes;
2. issue #289, incluindo o checkpoint `5619800919`, lembrando que ele é anterior aos últimos commits de auditoria;
3. PR #290 e seu head/base atuais;
4. `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` no HEAD vivo do Preview;
5. `docs/WAVE14-POST-C26-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`;
6. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
7. `LAST CHANGE.md`;
8. PR #296 e seus checks/runs mais recentes.

Depois revalide também #212, #263, #266, #285, #288, #292, #293 e o C11 canônico.

## REGRA DE CRONOLOGIA DOS FINDINGS

A auditoria evoluiu depois do checkpoint `5619800919`. O arquivo `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` recebeu evidências diretas posteriores do Codespace e do produto real.

Se houver conflito entre um checkpoint antigo e uma seção/commit posterior, a evidência cronologicamente mais nova, depois de revalidada, prevalece.

Os números UIAUD também evoluíram durante a auditoria. **Não abra ou implemente correção baseado apenas no número. Sempre reconcilie ID + título + evidência + seção cronologicamente mais recente.**

## BASELINE TÉCNICO

- C11 canônico corrigido: `19d5257d970f53ae798c5fa53946fce07c586452`;
- C26 aceito: `08e2530671de10d48933c4b712a1a1abc9e41dce`;
- candidato técnico pós-C26 validado: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`;
- package congelado SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`;
- Post-C26 Canonical Preview run `34403903462`: SUCCESS;
- Post-C26 Audit State Readiness run `34403903471`: SUCCESS.

O HEAD do Preview contém commits posteriores de documentação/evidência. Não confunda esse HEAD com um novo candidato de produto validado.

## ESTADO DOS DIAGNÓSTICOS

### RECHECK-SIM-PUMP-LEVEL

Não trate mais como defeito confirmado da simulação/Server Script. O recheck anterior viu snapshot congelado >15 min, mas a auditoria posterior no Codespace mostrou `EEE.P01.LevelPct` variando continuamente, API e Vite locais saudáveis e respostas em milissegundos. A falha observada passou a se concentrar no encaminhamento/autenticação/proxy público do Codespaces.

PR #296 existe apenas para diagnóstico e MUST NEVER MERGE. O último run registrado (`34505442984`, job `102966371728`) falhou como `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE` com 0 s de observação. Não rerodar cegamente.

Se o freeze reaparecer, capture local API 5080, local Vite 5173, forwarded browser, TAG value/timestamp/quality, realtime/WebSocket, processos e Server Script diagnostics antes de restart/reopen.

### UIAUD-289-001 — P1 GENERIC PRODUCT — CONFIRMADO

Screen Editor e Popup Editor podem derrubar toda a aplicação para tela vazia ao selecionar objetos visuais persistidos de tipos legados. Há evidência com `tank`, `value` e `dynamo`.

O caminho `listDynamicPropertyDestinations(element)` chega a `getBuiltinVisualObjectSchema(element.type)` sem normalização/recovery adequada para esses tipos legados.

Diagnostique a estratégia genérica correta de compatibilidade/migração/degradação. Não enfraqueça validação de tipo desconhecido como workaround. Exigir regressões determinísticas em Screen + Popup.

### UIAUD-289-002 — P1 — CONFIRMADO NO PRODUTO REAL

A evidência mais nova confirmou Engineering Working `Demo Project` / key `demo`, versão 0, enquanto Runtime permanece `eee-demo`, Active revision 2. O lifecycle bloqueia corretamente ativação cruzada.

Diagnostique bootstrap/checkout/persistência que entrega Working incorreto para a auditoria EEE. Não mude Active nem ative `demo` só para alinhar telas.

### Engineering `Failed to fetch` / recovery

Há reprodução real de falha transitória carregando o modelo público de Engineering. O shell pode exibir fallback enganoso `Demo Project`, dados de revisão ausentes e módulos bloqueados. Em recheck posterior, `Tentar novamente` recuperou após atraso.

Separe causa de transporte/proxy do defeito de UX. Correlacione local API 5080, Vite 5173 e browser encaminhado antes de atribuir a origem.

### Latência de rotas

Transições entre Engineering, Runtime, Histórico, Auditoria e Manual foram observadas levando dezenas de segundos, enquanto endpoints locais respondiam em milissegundos. Classificação continua UNCERTAIN até separar proxy/Codespaces de produto.

### Runtime Trends

Reproduzido: `TENDÊNCIAS` entra em `Conectando dados ao vivo…` e retorna silenciosamente à visão operacional. Diagnosticar com realtime/historian/projection local.

### Runtime Popups

Reproduzido: detalhes de P01/P02 podem abrir com `—` mesmo com cartões e TAG Monitor mostrando valores Good, e popup pode desaparecer/retornar silenciosamente após alguns segundos. A indisponibilidade geral da ponte de TAGs ficou menos provável; correlacionar binding, projection/re-render e navigation state.

### P2 genéricos já preservados na auditoria

Tratar depois dos P1/diagnósticos incertos prioritários:

- navegação do Engineering depende de rolagem global e não possui modelo adequado de rolagem independente/colapso;
- catálogos Templates/Equipamentos/Dínamos/Bibliotecas não oferecem preview visual útil;
- faixa Engineering Lock consome espaço excessivo de viewport;
- cabeçalho compartilhado Runtime/Engineering se sobrepõe em largura comum de notebook;
- menu de conta/sessão possui controles sem nome acessível;
- experiência de fallback/erro do Engineering pode induzir identidade errada do projeto.

### Scripts / PO-PRE-07

A tentativa prática ficou bloqueada pela instabilidade do Engineering. `PythonScriptAssistant` oferece busca/snippets para TAG e propriedades visuais, mas a auditoria não comprovou descoberta suficiente para o cenário composto solicitado pelo PO. Reexecutar como fluxo real depois de estabilizar Engineering.

### Alarm / Event / Historian

Preserve as autoridades separadas. O recheck mais recente mostrou Alarm global estável e Event operacional funcionando após atraso transitório. Historian retornou zero registros claramente para a janela consultada, coerente com Working reportando zero policies. Não criar defeito de Historian apenas por ausência de dados.

O antigo `UIAUD-289-016` permanece sem evidência suficiente até existir ocorrência comparável no mesmo instante.

## ORDEM DE TRABALHO RECOMENDADA

1. Revalidar tudo ao vivo e confirmar associação do Codespace atual com o Preview/candidato.
2. Correlacionar `Failed to fetch` e latência pela API local 5080, Vite local 5173 e browser encaminhado, sem reiniciar antes de capturar evidência.
3. Diagnosticar o Working `demo` vs Runtime `eee-demo` e corrigir somente quando a causa de bootstrap/checkout/persistência estiver comprovada.
4. Diagnosticar/corrigir UIAUD-289-001 genericamente com regressões Screen + Popup e tipos legados representativos.
5. Diagnosticar Runtime Trends e Popups com TAG/realtime/projection/nav-state correlacionados.
6. Depois executar as correções P2 genéricas determinísticas preservadas no arquivo de auditoria.
7. Reexecutar o cenário prático de Scripts/PO-PRE-07 após estabilizar Engineering.
8. Não corrigir findings UNCERTAIN sem reprodução técnica.
9. Para cada defeito confirmado, descobrir/revalidar a rota de correção autorizada naquele momento, implementar genericamente quando aplicável, adicionar regressão determinística e validar um novo exact SHA.
10. Targeted Work recheck somente quando justificado pela correção. Final PO homologation continua bloqueada até concluir correções e revalidação.

## GUARDRAILS PERMANENTES

- nunca alterar `main` diretamente;
- #212 permanece OPEN/DRAFT e só pode mergear em `main` após autorização futura, separada e explícita do Product Owner;
- #266 / #288 / #292 / #293 permanecem validation-only / MUST NEVER MERGE onde aplicável;
- #296 MUST NEVER MERGE;
- preservar #285 como evidência histórica PRE-C26;
- #290 é Preview-only e não é rota para `main`;
- sem force push, rebase destrutivo, branch deletion ou rerun cego;
- nunca enfraquecer security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics, drivers ou tests;
- Runtime/Active permanece independente de `.escadalib`;
- Alarm / Operational Event / Audit permanecem distintos;
- não mascarar defeito genérico com workaround EEE-specific;
- Wave13 #205/#207 permanece pausada.

Quando o Product Owner disser `siga`, avance autonomamente pelas próximas ações seguras. `siga` nunca autoriza merge protegido.

No fim de cada interação substancial, atualize a memória oficial no GitHub de forma precisa e mínima, sem transformar comentários em diário de bordo ruidoso.

---
