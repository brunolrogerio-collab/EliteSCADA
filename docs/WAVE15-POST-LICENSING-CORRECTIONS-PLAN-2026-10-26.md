# Plano consolidado de correções e auditorias — 26/10/2026

**Autorizado pelo proprietário / em fila.** Programa [#788](https://github.com/brunolrogerio-collab/EliteSCADA/issues/788), pais #297/#306 e coordenação #305. Este documento reúne trabalho futuro; não declara as funcionalidades implementadas, a auditoria concluída ou defeitos corrigidos.

## Sequência e limites

1. A frente ativa continua Help/manual #424/#425, PR draft/NO_MERGE #772.
2. **Licenciamento do produto #301 permanece a próxima etapa.**
3. Os pacotes abaixo entram na fila pós-licenciamento. A precedência já autorizada #779 → #780 → #781 → #782 é preservada; o programa não abre lanes paralelas ou ativa outra frente agora. A ativação de cada lote fixa base/head e depende de seus contratos e evidências.

Base de produto observada: `wave15/corrections-integration@d41c2f84ba179f5eae01dc1df96c9c1a28830dc7`, a revalidar antes de implementar. Checkpoint do Help anterior ao registro: `97832859558e06d723e2b999ddddc818cd0c91f7`, T1 40340409686 aprovado. Esse resultado não se transfere a commits posteriores.

Preservar o trabalho paralelo do proprietário, Preview/DEMO, dados, volumes e licenças. Uma tarefa de Intelligence não autoriza alterar HA; uma correção documental não autoriza mudar o comportamento do produto.

## Registro unificado

| Pacote | Issue | Situação e resultado esperado |
|---|---|---|
| Supressão individual/hierárquica | [#789](https://github.com/brunolrogerio-collab/EliteSCADA/issues/789) | Requisito novo a fechar; reutilizar shelving quando adequado e explicar estado efetivo por alarme, área e subárea |
| CheckBox e ComboBox autoráveis | [#790](https://github.com/brunolrogerio-collab/EliteSCADA/issues/790) | Inventariar/reutilizar o que existir e fechar o fluxo completo de objeto no editor/Runtime; checkbox HTML de configuração não conta |
| Aderência de Intelligence | [#791](https://github.com/brunolrogerio-collab/EliteSCADA/issues/791) | Auditoria autorizada, ainda não executada; comparar premissas, contratos, APIs, UI e percurso real do gestor |
| Matter e correções upstream | [#792](https://github.com/brunolrogerio-collab/EliteSCADA/issues/792) | Revalidar o artefato experimental exato e decidir eventual atualização com evidência, sem promoção automática |
| Atraso de ativação de alarme | #751 | Defeito Runtime reproduzido; campo salvo, comportamento de atraso ausente |
| RequiresAcknowledgement=false | #753 | Defeito Runtime reproduzido; retorno continua exigindo ACK |
| Seleção Home/Building | #785 | Corrigir seleção explícita HAB/Zigbee2MQTT, sem materializar tudo silenciosamente |
| Vínculo por driver | #786 | Corrigir projeção de autoria KNX/Zigbee/Z-Wave/S7/Logix conforme cada contrato, sem relaxar os validadores |
| Inventário Zigbee2MQTT | #787 | Interpretar definition.exposes do formato externo, preservando identidade e limites |
| Ocorrência HA-D2 | #423 | Falha de teste comprovada; causa não determinada, sem classificar como timing ou defeito de produto específico sem prova |
| Operação e interface geral | #779–#782 / #357 | Fila existente e auditoria UI coordenadas com este programa; sem duplicar issues já cumpridas |

Falha HA-D2: T1 40333665886 em `4433d392` terminou com standby synchronization-incomplete. O T1 posterior 40340409686 passou sem mudança de HA. Preservar a ocorrência [6418083919](https://github.com/brunolrogerio-collab/EliteSCADA/issues/423#issuecomment-6418083919); o novo verde não explica nem corrige a causa. Aceite manual de HA permanece separado.

## 1. Supressão de alarmes

O código já possui shelving individual (`IAlarmEngine`, `InMemoryAlarmEngine`, `AlarmShelvingEndpointExtensions`) e autorização/auditoria. `AlarmDefinition.Area` é texto, não prova de uma política hierárquica persistente. A implementação começa por um ADR curto para reutilizar ou estender o mecanismo adequado.

O contrato deve definir avaliação da condição, anúncio/lista/som, histórico, ACK, retorno, expiração e reativação. Não equiparar supressão a desabilitar uma definição ou reconhecer o alarme. Relacionar áreas/subáreas a identidades estáveis e à árvore operacional correta; não inferir parentesco por prefixo de nome ou confundir essa árvore com policy de autorização.

Composição proposta a validar: qualquer causa individual ou de área ancestral ativa suprime o alarme. Liberar uma causa não elimina as demais. A API devolve estado efetivo, causas e alvos legíveis. Persistência, restart, concorrência e observação HA usam os mecanismos existentes; uma seleção do navegador não é a autoridade. Respeitar restrições por alarme, permissão e escopo, com validação antes de mutação e sem resultado parcial oculto.

Aceite funcional: selecionar um alarme, uma área e uma subárea; verificar irmãos, ancestrais, causas simultâneas, liberação e expiração; comprovar auditoria e rejeições sem mutação. O popup de um motor deve mostrar e solicitar a supressão real de seus alarmes, inclusive a indicação de herança. Não disfarçar um alarme suprimido como Normal.

## 2. Objetos CheckBox e ComboBox

Reutilizar contratos, toolbox, serialização, bindings/eventos e renderer visuais existentes. O controle deve funcionar em Tela, Popup e Template, com salvar/reabrir, biblioteca, exportação/importação e publicação/ativação. Sem uma segunda árvore visual ou dependência de controle nativo de um sistema operacional.

- CheckBox: booleano, estado herdado/indeterminado quando necessário, rótulo, temas, qualidade, pending, erro e estado confirmado pelo backend. Não manter sucesso visual fictício após negação.
- ComboBox: opções tipadas valor/rótulo ou Enum existente, seleção atual, ausência de opção, incompatibilidade de tipo, lista vazia e modo somente leitura. Não exigir IDs internos do autor.
- Vínculo de TAG, CommandExecute e ação de alarme mantêm suas autorizações distintas. A supressão usa ação tipada da #789, sem exigir HTTP/script arbitrário. View Only observa sem operar; licença nunca concede capacidade ausente na Authority.
- Controles genéricos não exigem licença Intelligence. Verificar teclado, toque, mobile, temas, três idiomas e atualização externa concorrente.

O exemplo obrigatório é o CheckBox de supressão no popup de motor. O ComboBox precisa de exemplo operacional tipado, não apenas de um desenho no canvas.

## 3. Auditoria de Intelligence

**Dashboards gerados no Runtime são um caminho aceito pelo proprietário.** A questão é se o gestor consegue analisar ativos/processos com o conteúdo originalmente pedido, não a existência de uma segunda página de dashboards.

Ler #588, decisões posteriores, ADRs, fontes e evidências integradas. Há snapshots históricos no corpo da issue; não usá-los sozinhos como estado atual. Preservar Stage 4 no escopo experimental aceito, os seis layouts já aceitos e o adiamento de campo #701. Nenhuma contagem de PRs/stages substitui prova do percurso do usuário.

Matriz de saída: requisito original → expectativa do usuário → contrato persistido → API/autorização → campos/ações da UI → resultado no Runtime → teste/evidência → disposição → issue de correção → Help afetado. Distinguir implementado aguardando aceite, defeito residual comprovado e funcionalidade não entregue. Falta de teste/evidência deve ser anotada, sem convertê-la automaticamente em defeito.

Cobertura mínima:

1. Perfis de equipamentos/conjuntos, sinais obrigatórios/opcionais e unidades; motor/bomba com corrente, tensão, velocidade, pressão/vazão e vibração apenas quando disponível. Sinal ausente não pode bloquear análises que não dependam dele.
2. Equipamentos/perfis ligados a etapas de processo, visão útil de desempenho/eficiência e interpretação pelo gestor. Lista de membros ou relatório individual não é automaticamente um indicador agregado do processo.
3. Padrões/baselines e contextos comparáveis; corrente crescente em velocidade comparável, com explicação dos limites de carga/regime. Não inventar potência, eficiência, diagnóstico causal ou prognóstico quando faltarem dados.
4. KPIs/desvios rastreáveis, período, método, fontes, qualidade/tempo, cobertura e indisponibilidades compreensíveis.
5. Licença específica e habilitação opcional por equipamento/área, divisão desenvolvedor/gestor, pausa/reativação e ausência de consultas/trabalho analítico quando desativado. CPU-first e multiplataforma, sem GPU/IA generativa obrigatória na base.
6. Selecionar perfil/etapa → gerar conteúdo em Working → editar no canvas existente → salvar/publicar/ativar → localizar e interpretar no Runtime. Conferir se IDs, bindings ou gráficos exigem trabalho manual que a geração guiada deveria resolver.
7. Mesmos contratos na UI e nas APIs: validação, persistência, revisão/escopo/permissão, erros, auditoria e apresentação de resultado antigo. Revisar redundâncias e campos internos expostos.

Roteiro de software com dados sintéticos autorizados: motor/bomba sem vibração; caso com pressão/vazão; etapa com equipamentos; regimes comparáveis/incompatíveis; área/equipamento desativados; gestor restrito. Verificar respostas e dados persistidos junto da UI. Campo e efetividade operacional permanecem na #701; a falta desses dados não impede auditar e corrigir software. O relatório cria as correções específicas necessárias antes de implementá-las.

## 4. Matter: origem, correção declarada e pacote incorporado

Consulta oficial em 26/10/2026: [#1092](https://github.com/matter-js/matterjs-server/issues/1092) fechada; [#1039](https://github.com/matter-js/matterjs-server/issues/1039#issuecomment-6289309911) aberta, com correção declarada na 1.5.0 e validação pendente. A [1.5.5](https://github.com/matter-js/matterjs-server/releases/tag/v1.5.5) é a release não-prerelease mais recente observada. Isso atualiza a avaliação histórica, sem qualificar a integração.

O lockfile do SCADIVO ainda fixa `matter-server@1.5.0-alpha.0-20261013-75a39cb` e Matter.js `0.18.0-alpha.0-20261008-6066b7499`. Comparar os commits, pacotes e correções; não afirmar que o pacote exato já está corrigido ou que uma atualização é necessariamente suficiente. Testes isolados devem cobrir limites PBKDF, maxPathsPerInvoke=0, término limitado, API, bindings, subscription/read/write, fabric cifrada e restart.

Se necessária, a atualização entra em PR próprio, pin exato, integridade/proveniência/SBOM e CI. Manter **Versão em teste**, limites de plataforma/instalação/HA e o escopo atual. A qualificação estável/distribuição após Wave 16 permanece uma etapa distinta. Não conectar dispositivos/rede do proprietário como parte desta pesquisa. Fontes locais: ADR de Matter e package.json/lock do processo auxiliar.

## 5. Help/manual acompanha o produto

- O manual atual descreve o comportamento implementado. Requisitos enfileirados recebem sinalização de revisão; não viram instruções de uma interface inexistente.
- Cada PR corretivo identifica tópicos, campos, exemplos, idiomas e imagens afetados; revisa fonte instalada e manual juntos, e atualiza o roteiro de uso.
- Usar `HELP-REVIEW-AFTER-PRODUCT` com issue, tópico/campo e critério de revisão quando o conteúdo depender de entrega futura. Criar o alvo junto do guia válido; não adicionar marcadores apontando para tópicos inexistentes nem transformar fechamento de issue em validação documental.
- O aceite inclui prova do produto e correspondência da documentação no mesmo candidato. Capturas finais são do sistema real; a auditoria geral #357 e o fechamento #425 conservam o histórico e a versão capturada.
- Testes locais/ponta a ponta e CI no SHA exato indicam o que foi verificado, sem reivindicar campo/L4 ou desempenho de planta. Nenhuma correção nesta fila está entregue pela existência do plano.
