# Wave 14 — registro parcial de auditoria pós-C26

Data: 2026-09-10  
Escopo: registro de evidências já coletadas em modo de auditoria. Este documento não altera código, configuração, processos, GitHub ou estado do produto.

## Referências verificadas

- Issue GitHub #289 e seus comentários de auditoria e revalidação.
- Candidato técnico: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`.
- Pacote EEE Demo: SHA-256 `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`.
- C26 aceito: `08e2530671de10d48933c4b712a1a1abc9e41dce`; C11 canônico: `19d5257d970f53ae798c5fa53946fce07c586452`.
- Handoff de coordenação Wave 14, CURRENT-COORDINATOR-HANDOFF.md e LAST CHANGE.md.

## RECHECK-SIM-PUMP-LEVEL — refutado

A amostragem local autenticada da API exibiu `EEE.P01.LevelPct` diminuindo continuamente (por exemplo, até 61,14...), com processos API e Vite ativos e respostas locais HTTP 200 em aproximadamente 2–4 ms. A simulação e o Runtime não apresentaram sinal de falha durante a captura.

O acesso pelo navegador externo falhou depois da tela de conexão do encaminhamento, em fluxo de autenticação do GitHub Codespaces (`pf-signin` / `ERR_HTTP_RESPONSE_CODE_FAILURE`). A porta 5173 estava encaminhada como privada. Assim, a indisponibilidade observada é classificada, até nova reprodução, como evidência de limitação de encaminhamento/autenticação/proxy do Codespaces; não como defeito confirmado da simulação ou do Runtime.

## UIAUD-289-003 — representação no pacote revalidada

A inspeção somente leitura do pacote EEE Demo mostrou que os estados de bomba não usam propriedades incompatíveis em `core.text`:

- `pump-stopped-plate`, `pump-running-plate` e `pump-fault-plate` são `core.rectangle`, com cor, raio e z-order próprios.
- Os textos `PARADA`, `OPERANDO` e `FALHA` são elementos `core.text` distintos, posicionados acima das respectivas placas.
- Os estados OPERANDO e FALHA têm visibilidade ligada aos TAGs `EEE.P01.Running` e `EEE.P01.Fault`; PARADA é o estado-base sem binding de visibilidade.

Isso refuta a representação histórica inválida que tentava usar `backgroundColor` e `cornerRadius` em `core.text`. A validação visual comportamental no navegador ainda permanece pendente, pois o encaminhamento privado não se manteve disponível.

## UIAUD-289-001 — correlação de compatibilidade legada

A investigação somente leitura no editor indica uma incompatibilidade genérica de tipos persistidos legados:

- O catálogo atual reconhece `core.valueDisplay`.
- O catálogo não contém os identificadores simples `value` nem `dynamo`.
- Referências a esses identificadores ainda existem no caminho legado do editor, incluindo o modelo canônico legado.
- O histórico da issue registra que selecionar `pump01 · DYN` e `current · VALU` aciona erro de schema desconhecido antes de o editor de propriedades poder ser usado.

Hipótese de diagnóstico a confirmar: o fluxo de autoria dinâmica recebe tipos legados e consulta o catálogo atual sem uma normalização/alias ou sem uma degradação segura para objeto desconhecido. A classificação continua GENERIC PRODUCT. Não houve alteração de código nem tentativa de correção.

## Limitações e próximos passos

- A tentativa de autenticação visual no Engineering não concluiu com sucesso nesta automação; esse resultado não foi classificado como defeito de credencial ou de produto.
- Prosseguir com a confirmação do caminho de chamada de schema e, depois, com a correlação de UIAUD-289-002, sempre em modo somente leitura.
- Reproduzir visualmente Runtime/Engineering quando o encaminhamento privado do Codespaces estiver estável; não reiniciar processos de forma especulativa.

## Integridade do trabalho

Nenhum arquivo de produto foi alterado. Não foram criados patches, commits, pull requests ou merges. Este registro não contém senhas, cookies, tokens ou outros segredos.

## Complemento — confirmação do caminho UIAUD-289-001

A leitura do código confirmou que `listDynamicPropertyDestinations(element)` chama `getBuiltinVisualObjectSchema(element.type)` diretamente. `DynamicPropertyEditor` memoriza e executa essa função com dependência de `element.type`, sem uma camada local de normalização ou recuperação para tipo desconhecido.

Combinado com a ausência de `dynamo` e `value` no catálogo atual e com a exceção já registrada na issue, isso confirma o mecanismo: objetos persistidos com esses identificadores legados alcançam a busca atual de schema sem compatibilidade. A origem dos identificadores persistidos e a correção adequada continuam fora do escopo desta auditoria.


## UIAUD-289-002 — divergência Runtime × Working não reproduzida no estado atual

- **Status:** REFUTADO para o candidato e o preview atuais; permanece como observação histórica a revalidar se reaparecer.
- **Severidade:** P2 pelo impacto potencial de editar o contexto errado; sem defeito ativo confirmado nesta captura.
- **Área:** Engineering / Runtime / ciclo Working–Published–Active.
- **Título:** Runtime e Engineering apresentam o mesmo projeto 'eee-demo' após o bootstrap pós-C26.
- **Passos de reprodução:** (1) iniciar o preview pós-C26; (2) conferir o descritor do Working, o estado de persistência, o ciclo e a aplicação ativa; (3) comparar as chaves e nomes; (4) revisar a regra de ativação quando as chaves divergem.
- **Esperado:** Runtime configurado, Working, Published e Active devem apontar para o mesmo projeto, ou a interface deve avisar e impedir ativação cruzada.
- **Observado:** os artefatos reais do preview registram Working 'projectKey=eee-demo', 'projectName=EliteSCADA — EEE Demo', persistência configurada para 'eee-demo', Published/Active na revisão 2 e Runtime ativo em 'eee-demo'. A tentativa sem sessão contra os endpoints protegidos respondeu 401, comportamento coerente com segurança habilitada. No frontend, uma divergência aciona o banner “O projeto Runtime não corresponde ao Working”; 'canActivate' e o backend comparam as chaves e bloqueiam ativação de outro projeto.
- **Reprodutibilidade:** 0/1 no estado atual. A observação anterior do Product Owner não foi repetida nesta revalidação.
- **Classificação:** UNCERTAIN. Não há evidência atual de defeito de produto; a diferença anterior pode ter ocorrido antes do checkout/bootstrap do pacote ou durante reinicialização do Codespace.
- **Evidência:** '.preview/workspace.json', '.preview/persistence-status.json', '.preview/lifecycle.json', '.preview/runtime-application.json'; 'scripts/preview/launch-post-c26-preview.sh' configura 'EngineeringRuntime__ProjectKey=eee-demo'; 'EngineeringLifecycleWorkspace.tsx', 'EngineeringLifecycleWorkspace.logic.ts' e 'EngineeringPersistenceApi.cs' contêm aviso e bloqueio por chave divergente.
- **Notas:** o 'EngineeringWorkspace' nasce em memória com 'SeedDemo()', mas o bootstrap auditado faz checkout do pacote EEE e substitui seu descritor pelo projeto canônico. Se a divergência reaparecer, capturar visualmente o cabeçalho do Engineering e os quatro endpoints autenticados antes de reiniciar o preview.


## Complemento — UIAUD-289-002 / identidade Working × Runtime

A revalidação visual posterior não altera a conclusão sobre os artefatos: Runtime, workspace persistido, lifecycle e aplicação ativa permanecem alinhados em `eee-demo`. Contudo, ao retornar do Runtime para `/engineering`, a carga do modelo público falhou e o shell exibiu `Demo Project`, schema `—`, revisão-base `Ainda não salvo` e snapshot `—`. Esse nome é um valor de fallback da interface quando o snapshot não existe; portanto, não constitui evidência de que o Working real tenha mudado para outro projeto. A observação anterior de divergência visual deve ser lida em conjunto com UIAUD-289-004.

## UIAUD-289-004 — Engineering aparenta projeto alternativo durante falha de carga

- **Severidade:** P2
- **Área:** Engineering / navegação / UX de erros
- **Título:** Falha ao carregar o modelo público mantém shell com “Demo Project” e bloqueia os módulos
- **Passos de reprodução:** (1) Com sessão autenticada, abrir o Runtime; (2) acionar `Engineering`; (3) aguardar a montagem completa do shell; (4) observar cabeçalho, contexto do projeto e painel central; (5) acionar `Tentar novamente`; (6) tentar abrir `Scripts` pela navegação lateral.
- **Esperado:** a interface deve indicar estado de carregamento ou indisponibilidade sem apresentar uma identidade de projeto que possa ser confundida com o Working real; a repetição deve recuperar a carga quando o serviço voltar ou fornecer diagnóstico acionável. Os módulos dependentes devem ficar explicitamente indisponíveis.
- **Observado:** o shell autenticado apareceu com `Demo Project`, schema `—`, revisão-base `Ainda não salvo` e snapshot `—`, ao mesmo tempo em que o painel central informou `Não foi possível carregar o modelo público de Engenharia.` e `Failed to fetch`. `Tentar novamente` não recuperou a carga. O acionamento de `Scripts` não abriu o workspace; a tela de erro permaneceu.
- **Reprodutibilidade:** 2/2 para a falha e para a ausência de recuperação no mesmo ciclo autenticado.
- **Classificação:** GENERIC PRODUCT. A causa inicial da requisição pode envolver o proxy privado do Codespaces, mas o fallback enganoso, o bloqueio silencioso dos módulos e a ausência de orientação são comportamentos da interface aplicáveis a qualquer falha de rede.
- **Evidência:** inspeção visual e árvore de acessibilidade do navegador direto autenticado em 2026-09-10, URL `/engineering`; textos visíveis acima; conta de preview exibida no shell. A captura visual foi observada durante a sessão, mas não foi anexada porque a transferência para o repositório foi interrompida e o arquivo parcial foi removido.
- **Notas:** os artefatos locais de preview continuam identificando `eee-demo`; `Demo Project` é fallback do shell quando o snapshot público não foi obtido. Este finding também bloqueou a tentativa prática de PO-PRE-07 nesta rodada.

## UIAUD-289-005 — transição Runtime ↔ Engineering excede tempo de resposta aceitável

- **Severidade:** P2
- **Área:** Runtime / Engineering / navegação
- **Título:** Troca entre Runtime e Engineering levou aproximadamente 16–21 segundos
- **Passos de reprodução:** (1) No Engineering autenticado, acionar `Runtime`; (2) medir até o Runtime completo e interativo; (3) acionar `Engineering`; (4) medir até o shell final e observar o resultado da carga.
- **Esperado:** a troca deve concluir em poucos segundos, com indicador contínuo e estado final utilizável ou erro claro.
- **Observado:** Engineering → Runtime mostrou apenas `EliteSCADA` após cerca de 6 s e completou em aproximadamente 16 s. Runtime → Engineering consumiu cerca de 11 s na navegação e aproximadamente 21 s até o estado final; esse estado terminou em UIAUD-289-004 (`Failed to fetch`).
- **Reprodutibilidade:** 1/1 ciclo completo medido nesta rodada; o sintoma é coerente com PO-PRE-10.
- **Classificação:** UNCERTAIN. As APIs locais responderam em cerca de 2–4 ms e a porta pública estava sujeita ao proxy/autenticação privada do Codespaces. Há sintoma real para o usuário, mas ainda não há separação causal suficiente entre produto, proxy e ambiente.
- **Evidência:** cronometragem observacional no navegador direto autenticado em 2026-09-10; Runtime final exibiu `EliteSCADA — EEE Demo`, revisão 2 e valores dinâmicos; retorno terminou no erro de carga do Engineering.
- **Notas:** PO-PRE-09 não foi reproduzido como HTTP 404 de rota nesta tentativa: o shell de `/engineering` carregou, mas uma requisição interna do modelo público terminou em `Failed to fetch`. Investigar logs de proxy/servidor em reprodução técnica posterior.

## Estado de PO-PRE-07 — tentativa prática de Scripts

A tentativa prática solicitada pelo Product Owner foi iniciada no produto real: após a falha de carga do Engineering, foi acionado `Scripts` para procurar um fluxo de comparação entre dois TAGs e alteração de cor/estado de objeto. O workspace de Scripts não abriu e permaneceu a tela de erro de UIAUD-289-004. Assim, nesta rodada, a capacidade de descobrir sintaxe, objetos, TAGs, propriedades e APIs pela interface fica **BLOQUEADA**, não confirmada nem refutada. A inspeção documental/estrutural pode complementar o diagnóstico, mas não substitui a tentativa prática exigida.


### Complemento estrutural de PO-PRE-07

Sem substituir a reprodução prática, a leitura somente leitura da implementação mostra que `PythonMonacoEditor` monta `PythonScriptAssistant`. O painel se apresenta como `Assistente de Script / Objetos do Projeto`, oferece pesquisa por `TAG, tela, objeto, propriedade, Dynamo ou API`, separa TAGs, telas, popups, Client Memory e APIs disponíveis e permite inserir snippets no cursor. O catálogo gera, entre outros, `tag_read`, `tag_write`, `visual_property_read`, `visual_property_write`, limpeza de override e tween usando referências canônicas. O provedor de autocomplete do Monaco localizado em `PythonMonacoEditor.tsx` produz sugestões de entry points; não foram localizadas sugestões de API/TAG nesse provedor. Também não foi localizado exemplo composto que compare dois `tag_read` e condicione uma chamada `visual_property_write`. Portanto, há mecanismos explícitos para descobrir e inserir as operações elementares, mas a composição solicitada ainda depende de conhecimento de Python e precisa ser validada na UI funcional.

### Nota de governança do registro

A frase histórica da seção `Integridade do trabalho` descrevia o estado no momento em que o primeiro registro foi criado. Por solicitação posterior do usuário, este relatório passou a ser versionado no branch vivo de auditoria por commits exclusivamente documentais. Nenhum arquivo de produto, configuração de runtime ou código-fonte foi alterado; não houve patch de produto, pull request ou merge.


## Complemento corretivo — UIAUD-289-002 confirmado no produto real

A conclusão anterior baseada apenas nos artefatos de `.preview` foi refutada pela autoridade observável do produto após a recuperação da requisição pública. O Engineering carregou um snapshot válido de `Demo Project` (chave `demo`, versão de mudança 0, sem revisão-base, Published ou Active), enquanto o Runtime continuou em `eee-demo`, revisão 2. O próprio Ciclo do Engineering exibiu: `O projeto Runtime não corresponde ao Working` e `Projeto Runtime configurado: eee-demo. A ativação fica bloqueada porque o backend rejeitará outra chave.`

- **Severidade:** P1
- **Área:** Engineering / Runtime / ciclo autoritativo / bootstrap
- **Título:** Working real abre em Demo Project enquanto Runtime executa eee-demo
- **Passos de reprodução:** (1) Abrir o Runtime autenticado e confirmar `EliteSCADA — EEE Demo`, revisão 2; (2) abrir `/engineering`; (3) se ocorrer `Failed to fetch`, usar `Tentar novamente`; (4) na visão geral, ler Projeto, Working, revisões e Runtime ao vivo.
- **Esperado:** o Working disponível para edição deve corresponder ao projeto Runtime configurado e ao candidato EEE carregado, ou o Engineering deve oferecer fluxo explícito e seguro de checkout do mesmo projeto antes de permitir trabalho.
- **Observado:** Working `Demo Project` / `demo`, versão 0, ciclo vazio e sem revisões; Runtime configurado `eee-demo`, ao vivo r2; ativação explicitamente bloqueada por divergência.
- **Reprodutibilidade:** 2/2 carregamentos públicos bem-sucedidos em abas novas na mesma sessão autenticada.
- **Classificação:** EEE-SPECIFIC. O mecanismo de bloqueio está correto, mas o estado entregue para esta auditoria pós-C26 não corresponde ao pacote EEE candidato.
- **Evidência:** `evidencias/UIAUD-289-002-runtime-working-mismatch.jpg`, SHA-256 `aa849f19c56263bef8efa15dd0cbe817ea20f2c7a4243ef2b71803302911374e`; árvore de acessibilidade do produto real com os mesmos valores. Os artefatos de `.preview` que indicavam `eee-demo` ficam registrados como evidência contraditória de bootstrap/persistência, não como autoridade superior à UI/API viva.
- **Notas:** a tela de erro anterior realmente usa `Demo Project` como fallback, mas a recuperação posterior provou que o snapshot público carregado também é `demo`. Portanto, UIAUD-289-004 permanece válido como UX de falha, e UIAUD-289-002 volta ao estado confirmado.

## Complemento de reprodução visual — UIAUD-289-001

- **Severidade:** P1
- **Área:** Engineering / Screen Editor / Properties / compatibilidade de tipos legados
- **Título:** Selecionar objeto legado na árvore derruba toda a aplicação para tela vazia
- **Passos de reprodução:** (1) Abrir Engineering; (2) abrir `Telas`; (3) manter `Demo Overview`; (4) na árvore `Estrutura`, selecionar `tank01 · tank`.
- **Esperado:** o objeto deve ser selecionado e o painel Properties deve abrir; se o tipo legado não for editável, a UI deve preservar o workspace e apresentar diagnóstico localizado.
- **Observado:** imediatamente após o clique, todo o shell, a navegação e o editor desaparecem. Resta somente o fundo escuro, sem mensagem, ação de recuperação ou conteúdo acessível além da raiz da página.
- **Reprodutibilidade:** 2/2, incluindo nova aba e nova carga completa do Engineering.
- **Classificação:** GENERIC PRODUCT. O crash decorre do caminho genérico de inspeção/schema para objeto persistido com tipo legado, independentemente do projeto EEE.
- **Evidência:** `evidencias/UIAUD-289-001-screen-editor-blank-after-selection.jpg`, SHA-256 `9de07e850c721c9e2efea07a27153bd7ca4f942c30ab9bfe2ffb9813239d1a20`; antes do clique, o preview identificava `Legacy visual type: tank` e a árvore listava `tank01 · tank`.
- **Notas:** este crash bloqueia o teste prático de Properties, bindings, texto, mover/redimensionar, copiar/colar, undo/redo, grupo, lock, alinhamento e z-order sobre os quatro objetos já persistidos. Controles de zoom, grid e snap eram visíveis antes do crash.

## UIAUD-289-006 — navegação do Engineering depende da rolagem global

- **Severidade:** P2
- **Área:** Engineering / navegação / responsividade
- **Título:** Menu lateral não possui rolagem independente e perde itens/seleção durante páginas longas
- **Passos de reprodução:** (1) Abrir Engineering em viewport 1265 × 712; (2) abrir Scripts ou a visão geral; (3) usar Page Down para alcançar itens inferiores da navegação.
- **Esperado:** a navegação deve permanecer utilizável por rolagem própria ou modo recolhido, preservando acesso ao módulo atual e ao conteúdo principal.
- **Observado:** a rolagem é global. Cabeçalho superior e itens iniciais do menu somem juntos; em Scripts, o conteúdo principal ficou fora da viewport enquanto o usuário rolava apenas para alcançar Templates, Telas, Segurança e Diagnósticos. Não havia controle de recolhimento no shell geral.
- **Reprodutibilidade:** 1/1 fluxo deliberado; coerente com PO-PRE-01.
- **Classificação:** GENERIC PRODUCT.
- **Evidência:** inspeção visual no navegador real; scrollbar única na borda direita e ausência de scrollbar/controle próprio da navegação. O Screen Editor possui controles locais de recolhimento, mas eles não resolvem o shell geral.
- **Notas:** o problema cresce com a faixa persistente de proteção e com páginas longas.

## UIAUD-289-007 — catálogos de objetos e bibliotecas não oferecem preview útil

- **Severidade:** P2
- **Área:** Engineering / Templates / Equipamentos / Dínamos / Bibliotecas
- **Título:** Listagens apresentam metadados textuais sem representação visual do recurso
- **Passos de reprodução:** (1) Abrir Templates, Equipamentos e Dínamos; (2) observar as linhas; (3) abrir Bibliotecas; (4) observar os recursos disponíveis para exportação.
- **Esperado:** componentes visuais reutilizáveis devem ter miniatura ou preview acionável, dimensões e interface relevantes para permitir escolha segura.
- **Observado:** Templates, Equipamentos e Dínamos são tabelas somente leitura com chave, nome e contagens/valores; as linhas não expõem preview visual. Bibliotecas lista recursos por checkbox, nome, tipo e chave; não há miniatura nem ação de preview do recurso antes da seleção/exportação.
- **Reprodutibilidade:** 1/1 visita a cada módulo; confirma PO-PRE-02 e PO-PRE-06.
- **Classificação:** GENERIC PRODUCT.
- **Evidência:** UI real: `Templates` mostrou `pump.standard / Standard Pump / 4`; `Equipamentos`, `Demo.P01 / Pump P01 / pump.standard`; `Dínamos`, oito linhas textuais; `Bibliotecas`, recursos selecionáveis por checkbox.
- **Notas:** a paleta interna do Screen Editor exibe dimensões e um painel denominado `Preview do dínamo selecionado`, mas isso não corrige a ausência de preview nos catálogos próprios nem na exportação de biblioteca.

## UIAUD-289-008 — faixa de proteção domina a abertura do Engineering

- **Severidade:** P2
- **Área:** Engineering / segurança / responsividade
- **Título:** Engineering Lock ocupa grande parte da viewport em todas as páginas
- **Passos de reprodução:** abrir qualquer módulo do Engineering em viewport 1265 × 712.
- **Esperado:** o estado de proteção deve permanecer acessível sem deslocar continuamente o contexto principal, principalmente quando nenhuma ação está disponível.
- **Observado:** a faixa `PROTEÇÃO DA APLICAÇÃO / Engineering Lock` aparece antes do cabeçalho do workspace, com campo de segredo, quatro botões desabilitados e texto explicativo. Na viewport auditada ocupa aproximadamente 140 px e empurra conteúdo e navegação para baixo.
- **Reprodutibilidade:** presente em todos os módulos abertos nesta sessão; confirma PO-PRE-08.
- **Classificação:** GENERIC PRODUCT.
- **Evidência:** inspeção visual nas páginas Visão geral, Scripts, Bibliotecas, Templates, Equipamentos, Dínamos e Telas.
- **Notas:** a faixa desaparece ao rolar a página, mas contribui diretamente para UIAUD-289-006 e não se adapta ao estado sem segredo configurado/desbloqueado.


## Complemento de reprodução visual — UIAUD-289-001 no Popup Editor

A reprodução foi estendida ao Popup Editor após a confirmação no Screen Editor.

- **Severidade:** P1
- **Área:** Engineering / Popup Editor / Properties / compatibilidade de tipos legados
- **Título:** Selecionar objeto legado na árvore do Popup Editor derruba toda a aplicação para tela vazia
- **Passos de reprodução:** (1) Abrir Engineering; (2) abrir `Popups`; (3) manter `Standard Pump Popup`; (4) na árvore `Estrutura`, selecionar `current · value`.
- **Esperado:** o objeto deve ser selecionado e o painel Properties deve abrir; se o tipo legado não for editável, a UI deve manter o workspace e expor diagnóstico localizado.
- **Observado:** imediatamente após o clique, shell, navegação e editor somem. Permanece apenas o fundo escuro; a árvore de acessibilidade fica somente na raiz da página, sem mensagem, recuperação ou conteúdo acessível.
- **Reprodutibilidade:** 1/1 no Popup Editor, além de 2/2 já confirmados no Screen Editor.
- **Classificação:** GENERIC PRODUCT. O comportamento transversal confirma que o caminho genérico de seleção/inspeção de visual legado não contém a falha.
- **Evidência:** `evidencias/UIAUD-289-001-popup-editor-blank-after-selection.jpg`, SHA-256 `79a64b94019d2ebf0ce84e69ddb6e5f59c5afcfe24b0dff7c6b8812c0a15a99a`; a tela imediatamente anterior listava `current · value`, `frequency · value` e `fault · status` na árvore.
- **Notas:** o Popup Editor expunha antes do clique paleta, biblioteca de dínamos, preview, controles de grid/snap/zoom, barra de edição e Properties. O crash impede testar tais controles nos objetos persistidos do popup.

## Rechecagens deliberadas PO-PRE-03 e PO-PRE-05

- **PO-PRE-03 — TAGs e Data Sources:** refutado quanto a uma seleção conflitante. Em `TAGs`, a seleção na lista de leitura foi propagada corretamente para o detalhe e rascunho (Flow → Tank Level, 1/1). Há duas representações da entidade — lista de consulta e lista de seleção do editor — que aumentam a densidade vertical, mas não houve dois estados ativos discordantes. A ausência de navegação com rolagem própria já permanece registrada em UIAUD-289-006. Em `Data Sources`, o único source pôde ser inspecionado com schema, intervalo, edição individual e ações explícitas de lote/remoção; nenhuma mutação foi realizada.
- **PO-PRE-05 — Segurança:** refutado para este passe. A tela apresenta os papéis `developer` e `operator` com permissões legíveis, distingue conta atual, explica a consequência de sessão, separa redefinição de senha e mantém salvar desabilitado até mudança. Nenhuma alteração de conta, papel ou senha foi realizada.


## Rechecagem de conectividade pública — PO-PRE-09 e UIAUD-289-005

Em 10/09/2026, após o crash do Popup Editor, a abertura de uma nova aba direta do produto pelo cliente de auditoria falhou com `ERR_BLOCKED_BY_CLIENT`; isso foi classificado como limitação do cliente de auditoria, não como defeito do produto. Em seguida, o serviço local do Codespace respondeu `HTTP 200` em `0.002499 s` para `http://localhost:5173/`, enquanto o endereço público informado respondeu `HTTP 404` em `0.052637 s` para uma requisição sem a sessão autenticada de navegador.

Este resultado reforça a classificação **UNCERTAIN** de UIAUD-289-005: existe uma diferença real entre serviço local saudável e acesso público mediado por proxy/autenticação, mas a evidência atual não isola se o 404 é regra de acesso do Codespaces, perda de encaminhamento ou defeito da aplicação. Não promover para defeito GENERIC PRODUCT nem EEE-SPECIFIC sem reprodução técnica autenticada fora deste cliente.


## Complemento de medição — UIAUD-289-005

A rechecagem no produto real confirmou que a lentidão é recorrente e ultrapassa a faixa inicialmente observada: Engineering → Runtime levou aproximadamente 27 s até a visão operacional ficar utilizável; Runtime → Histórico excedeu 30 s até interromper a sessão de automação, embora a rota posteriormente tenha carregado e permitido consultas. Como contraprova, o serviço local do Codespace continuou respondendo Runtime em ~2 ms e Engineering em ~21 ms.

A classificação permanece **P2 / UNCERTAIN**: há degradação relevante para o operador na rota pública, mas as medições locais e a intermitência de acesso da porta apontam proxy/autenticação/Codespaces como fator ainda não separado do produto.

No Histórico, as consultas somente leitura de uma hora para `Amostras do historian` e `Eventos de alarme` retornaram zero registros de maneira clara, sem erro de UI. Isso é coerente com o Working carregado, que informa zero políticas de histórico; não foi aberto finding para ausência de dados.


## UIAUD-289-009 — painel de Tendências não permanece disponível

- **Severidade:** P2
- **Área:** Runtime / Trends
- **Título:** TENDÊNCIAS entra em “Conectando dados ao vivo…” e retorna à visão operacional sem diagnóstico
- **Passos de reprodução:** (1) Abrir Runtime autenticado; (2) clicar `TENDÊNCIAS`; (3) aguardar cerca de 3–4 s.
- **Esperado:** o painel deve mostrar o gráfico com as séries configuradas ou um erro explícito com opção de recuperar.
- **Observado:** o gráfico vazio aparece com cinco séries na legenda e `Conectando dados ao vivo…`; em seguida, o produto retorna sozinho à visão operacional. Em uma repetição, a tela de retorno exibiu valores `—` e `QUALIDADE RUIM`, sem explicar a falha da tendência.
- **Reprodutibilidade:** 2/2 na mesma sessão autenticada.
- **Classificação:** GENERIC PRODUCT. O comportamento é do painel Runtime e não depende do descompasso Working/EEE.
- **Evidência:** captura visual durante o estado `Conectando dados ao vivo…` e árvore de acessibilidade do produto real; persistência da imagem será concluída no próximo registro.
- **Notas:** o Historian separado oferece consulta somente leitura e informou zero registros para a janela de uma hora, coerente com zero políticas de histórico no Working, mas o Runtime deveria comunicar essa indisponibilidade em vez de abandonar o painel.
