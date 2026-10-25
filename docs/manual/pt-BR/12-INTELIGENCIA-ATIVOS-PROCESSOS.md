# Inteligência de Ativos e Processos

Este capítulo descreve os fluxos disponíveis hoje. Inteligência é opcional: a licença habilita a função, mas você configura os equipamentos, perfis e etapas que pretende analisar. Não é necessário aplicar análises a toda a planta.

**Ajuda contextual:** `/help?topic=intelligence.overview`.

## 1. Começar pelo equipamento e pelos TAGs

Antes de criar um perfil, identifique o recurso estável de Equipamento e os TAGs que representam medições úteis: por exemplo, corrente, tensão, pressão, vazão ou temperatura. Confira unidade, tipo, quality, timestamp e a captura Historian para o período que será analisado. Não inclua um sinal apenas porque está disponível; explique qual pergunta operacional ele ajuda a responder.

Para editar, a conta precisa de `EngineeringModify` e de um grant explícito `AssetProcessIntelligenceManage`, global ou que abranja o recurso. Recursos de Intelligence também dependem da licença correspondente.

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

<!-- installed-system-guides -->

## Intelligence: habilitar, pausar e resolver bloqueios

Licença disponível, recurso selecionado e execução ativa são três condições diferentes.

### Siga a sequência completa

Abra Engenharia > Inteligência de ativos. Primeiro crie Equipamento e TAGs. Depois configure perfil de sinais, associação Equipamento/perfil, contextos e etapas que fizerem sentido. Salve a configuração e os escopos escolhidos; publique/ative a revisão. Só então ative explicitamente o recurso para executar análises. Criar um perfil, marcar uma caixa ou possuir licença não arma a execução automaticamente.

### Permissões e seleção de recursos

EngineeringModify permite configurar; AssetProcessIntelligenceManage precisa ser um grant explícito, global ou com alcance no Equipamento/Local correspondente. O nome Developer ou SystemAdmin por si só não substitui esse grant. Consultas também exigem TrendUse e licença Intelligence. Conceda pela tela Segurança e confira as capacidades efetivas da sessão. Não exponha IDs para o operador decorar: escolha os recursos pelo seletor e preserve sua identidade.

### Ativar apenas onde for útil

1. Em Escopos elegíveis, selecione os equipamentos/locais desejados.
2. Salve os escopos e a configuração.
3. Publique/ative a revisão; resolva alterações pendentes antes de armar execução.
4. Na linha do recurso use Ativar e confira a permissão de processo armada.
5. Abra a consulta de uma associação e escolha o período.
6. Para deixar de analisar, use Pausar; o trabalho pendente é cancelado. A pausa não apaga TAGs nem histórico. Para retirar um recurso da seleção, pause-o primeiro.

### Ativo configurado não significa permissão armada

Se a autorização foi revogada, a permissão de execução pode ficar fechada mesmo com Ativo na configuração. Restaurar o grant não rearma o trabalho silenciosamente: confira o estado e use Ativar novamente, inclusive após reinício quando a tela exigir. Isso evita reiniciar análise sem decisão do responsável. Não confunda esse estado com erro do Historian ou ausência de dados.

### Exemplo de implantação pequena

Comece por Bomba 01 em uma área. Configure corrente e velocidade se existirem; não invente vibração ausente. Depois adicione medições e análises cujo catálogo aceite seus sinais. Deixe outros recursos pausados se não serão utilizados. A configuração seletiva controla o que está autorizado a executar; não é uma promessa de consumo fixo de CPU/RAM.

[Abrir este guia na Ajuda instalada](/help?topic=intelligence.activation)

## Intelligence: significado dos sinais e unidades

Diga o que foi medido, não apenas como o TAG foi chamado.

### Por que o nome não basta

Um TAG chamado Potência pode representar potência ativa, aparente ou um valor calculado. Uma pressão pode ser sucção, descarga ou diferencial. As regras não devem adivinhar isso pelo texto. Abra o perfil e selecione o papel semântico controlado quando a análise o exigir; complete os qualificadores mostrados para esse papel. O texto de papel legado é apenas descritivo.

### Campos e escolhas

Escolha somente o significado sustentado pelo instrumento e pelo processo. Os seletores oferecem as opções aceitas pelo catálogo atual.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Obrigatório | Marca sinais cuja ausência compromete a cobertura pedida. Não transforme um sinal essencial em opcional apenas para obter resultado. | Corrente necessária à comparação |  |
| Papel semântico | Grandeza e função controladas; a análise compara esse papel, não a semelhança do nome do TAG. | Potência ativa de entrada, se medida |  |
| Base de corrente | Distingue a base admitida pelo papel, como corrente RMS apropriada ao instrumento; não deduza de uma etiqueta genérica. | Selecionar a opção documentada do instrumento |  |
| Tipo de sinal | Diferencia medição, referência e função exigida pelo papel. Um setpoint não comprova o valor medido. | Medição real quando a regra exigir |  |
| Referência de pressão | Identifica base/origem exigida; pressão diferencial de saída menos entrada é diferente de pressão de descarga isolada. | Diferencial medido saída − entrada |  |
| Direção do fluxo | Diz em que sentido a vazão representa a transferência. Sentido oposto pode invalidar uma regra. | Bomba para processo, quando corresponder |  |
| Referência de frequência | Explica qual frequência o sinal representa conforme o catálogo; não confunda referência elétrica e velocidade física. | Conferir a origem do instrumento |  |
| TAG e unidade | Selecione o TAG real e confira sua unidade de engenharia controlada. Alterar apenas rótulo no perfil não converte os dados. Alguns cálculos exigem versões/unidades específicas do catálogo. | kW para potência, se esse for o dado real |  |

### Exemplo: não confundir proxy com eficiência

Corrente aumentando na mesma velocidade pode justificar investigação, mas não é cálculo de eficiência física do motor. Potência elétrica de entrada, vazão e diferencial de pressão podem habilitar outras regras se os papéis, unidades e alinhamento exigidos existirem. Sem instrumento ou qualificador necessário, leia a razão de regra indisponível em vez de preencher um valor fictício.

[Abrir este guia na Ajuda instalada](/help?topic=intelligence.semantics)

## Intelligence: consultar resultados e montar dashboards

Escolha a pergunta, confirme os dados disponíveis e só então interprete o indicador.

### Comece pela cobertura

Com associação salva e execução autorizada, abra a consulta de cobertura do perfil. Informe início e fim e solicite a consulta explicitamente. Os horários do formulário são locais e a consulta identifica sua janela UTC. O fim deve ser posterior ao início. Confira sinais obrigatórios com dados, amostras aceitas/rejeitadas, qualidade e alinhamento temporal antes de ler mínimos, máximos ou médias. Contagens de amostras por si só não provam cadência, completude nem aquisição sincronizada.

### Desempenho: escolha uma regra elegível

Abra Desempenho e selecione a regra do catálogo. Uma opção desabilitada explica o sinal, papel ou unidade ausente. Não escolha outra grandeza apenas por produzir um número. Preencha De/Até, consulte e leia status, unidade, janela e limitações do resultado. Resultado indisponível não equivale a zero; um indicador substituto não deve ser chamado de eficiência física. Mudar associação, revisão ou parâmetros exige nova consulta, não reaproveitar o número anterior.

### Dois tipos de contexto, com limites diferentes

O contexto operacional salvo no perfil usa intervalo numérico inclusivo na unidade de engenharia atual e exige evidência no mesmo timestamp UTC de origem exato. O gerador de tela de desvio contextual tem seleções próprias: 2 a 8 sinais-alvo e dois sinais numéricos de contexto distintos, também distintos dos alvos. São pelo menos quatro sinais numéricos utilizáveis. As faixas desse gerador usam a unidade controlada exibida e a regra limite inferior ≤ valor < limite superior. Não copie automaticamente limites do perfil operacional: unidade e extremidade superior podem ser diferentes.

### Gere a tela e confira a cadeia completa

1. Escolha a associação ou etapa e o tipo de tela.
2. Complete as seleções exigidas; se o botão estiver bloqueado, leia o motivo junto do campo.
3. Gere o rascunho. Esse passo organiza objetos e referências, não consulta o Historian nem ativa a tela.
4. No editor, revise nome, dimensões, objetos, vínculos e navegação. Execute Preview e Apply.
5. Salve/publique/ative a revisão e abra a tela pelo Runtime.
6. Configure/execute a consulta do objeto e confira janela e resultado. Uma tela de etapa reúne membros; não cria automaticamente um KPI agregado nem prova causalidade entre equipamentos.

### Roteiro para investigar resultado vazio

Confira nesta ordem: licença e permissões; escopo salvo e permissão armada; projeto/revisão ativa; associação e TAGs; período; captura Historian; qualidade; papéis/unidades; alinhamento/contexto. Corrija a primeira condição comprovadamente ausente e refaça a consulta. Não amplie limites nem altere o significado do sinal apenas para fazer o resultado aparecer. As análises disponíveis ajudam a investigar; não confirmam defeito nem antecipam falhas com validade de campo ainda não demonstrada.

[Abrir este guia na Ajuda instalada](/help?topic=intelligence.analysis-guide)
