# Inteligência de Ativos e Processos

Este capítulo descreve os fluxos disponíveis hoje. Inteligência é opcional: a licença habilita a função, mas você configura os equipamentos, perfis e etapas que pretende analisar. Não é necessário aplicar análises a toda a planta.

**Ajuda contextual:** `/help?topic=intelligence.overview`.

## 1. Começar pelo equipamento e pelos TAGs

Antes de criar um perfil, identifique o recurso estável de Equipamento e os TAGs que representam medições úteis: por exemplo, corrente, tensão, pressão, vazão ou temperatura. Confira unidade, tipo, quality, timestamp e a captura Historian para o período que será analisado. Não inclua um sinal apenas porque está disponível; explique qual pergunta operacional ele ajuda a responder.

Para editar, a conta precisa de `EngineeringModify` e `AssetProcessIntelligenceManage` no escopo do recurso. Recursos de Intelligence também dependem da licença correspondente.

## 2. Criar perfil de medição e associação

1. Abra Engineering > Inteligência de ativos e crie um Perfil de medição com nome/chave identificáveis.
2. Adicione os TAGs que descrevem o equipamento. Marque cada sinal como obrigatório ou opcional e complete os qualificadores semânticos pedidos; um rótulo não substitui a unidade nem a referência real do TAG.
3. Salve a configuração no Working.
4. Na seção de associações, escolha o Equipamento existente e o perfil. Uma identidade de equipamento pode usar mais de um perfil.
5. Revise referências ausentes e duplicadas antes de salvar e seguir o lifecycle normal do projeto.

O perfil organiza referências e significado dos sinais. Ele não cria TAGs, não habilita captura Historian por si só e não comprova que os instrumentos estão calibrados.

## 3. Configurar contexto operacional

Contexto diferencia condições de operação que não devem ser comparadas como se fossem iguais — por exemplo, faixas de vazão ou modos indicados por TAG. Crie o contexto associado à associação de equipamento correta e adicione predicados para os TAGs do perfil.

Predicados podem representar intervalo numérico inclusivo ou igualdade booleana, de string ou enum. Os limites numéricos são registrados na unidade atual de Engineering; ao mudar a unidade, revise e confirme novamente os limites. Para que a consulta considere o contexto, os predicados devem corresponder à evidência temporal exigida na mesma marca UTC de origem, sincronizada e exata. Se não houver correspondência, a análise pode ficar sem contexto elegível; não enfraqueça o critério para forçar um resultado.

## 4. Organizar etapas do processo

Use **Etapas ordenadas do processo** para montar a sequência que deseja acompanhar. Crie cada etapa com uma chave estável e um nome legível; adicione as associações de equipamento/perfil e ajuste a ordem explicitamente. A etapa organiza membros, mas não infere por si só causalidade, balanço ou um KPI agregado do processo.

Salve a configuração no Working. Dados e análises de Runtime continuam sujeitos à revisão Active e às autorizações vigentes.

## 5. Consultar análises

Selecione o equipamento associado e escolha a análise disponível — como cobertura de perfil, desempenho ou desvio contextual. Defina o período e solicite a consulta explicitamente. Desvio contextual exige condições operacionais configuradas e evidência temporal suficiente. A análise é somente leitura; não altera TAG, não comanda equipamento e não substitui Alarmes.

Interprete o resultado junto com a cobertura, quality, timestamps e limitações de cada sinal. Ausência, quality ruim, falta de histórico ou desalinhamento temporal não significam zero, operação normal ou falha confirmada. As análises atuais não são diagnóstico de manutenção nem comprovação de eficiência física. Para membros de uma etapa, consulte um equipamento por vez; não some resultados independentes como um score do processo.

**Ajuda contextual:** `/help?topic=intelligence.process-stage-member-insights`.

## 6. Gerar rascunhos de telas

Escolha um perfil ou uma etapa e gere a tela disponível. A geração usa metadados dos TAGs para criar uma Screen editável; não consulta valor ao vivo nem Historian e não inventa KPIs. Analise o rascunho no editor, execute Preview e use Apply ao Workspace. Depois, salve, publique e ative pelas etapas explícitas do lifecycle. A tela gerada não é ativada automaticamente.

## 7. Problemas comuns

- **Recurso bloqueado:** confira licença, `EngineeringModify`, `AssetProcessIntelligenceManage` e escopo.
- **Perfil ou associação incompleta:** confira equipamento, perfil, TAGs e qualificadores obrigatórios.
- **Sem cobertura/histórico:** confirme se o TAG recebia amostras com quality e timestamps úteis e se a captura existia no período. Historian não preenche dados retroativamente.
- **Contexto sem correspondência:** revise predicados, unidade, intervalo e sincronização/igualdade temporal requerida.
- **Rascunho indisponível:** complete as seleções exigidas pelo tipo de tela e use o editor normal para corrigir o resultado.
- **Resultado diferente após edição:** salve a configuração correta, confira a revisão Active e faça uma nova consulta explícita.

**Ajuda relacionada:** `/help?topic=historian.overview`, `/help?topic=tags.quality`, `/help?topic=tags.timestamps`, `/help?topic=screens.overview`, `/help?topic=engineering.lifecycle`.
