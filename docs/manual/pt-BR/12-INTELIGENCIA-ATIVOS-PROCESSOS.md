# Inteligência de Ativos e Processos

Este capítulo descreve os fluxos disponíveis hoje. Inteligência é opcional: a licença habilita a função, mas você configura os equipamentos, perfis e etapas que pretende analisar. Não é necessário aplicar análises a toda a planta.

**Ajuda contextual:** `/help?topic=intelligence.overview`.


## Comece por uma pergunta pequena

Imagine a Bomba 01. Você já recebe corrente e velocidade no SCADIVO e quer investigar se a corrente mudou quando a bomba trabalha em condições semelhantes. Comece por essa pergunta, sem tentar configurar toda a planta. Uma comparação pode orientar investigação; não comprova, sozinha, eficiência física ou defeito.

Os nomes usados no editor representam partes dessa tarefa:

| Nome | O que significa neste exemplo |
| --- | --- |
| TAG | A variável que fornece corrente ou velocidade. |
| Perfil de medição | A ficha que reúne os sinais escolhidos e o significado de cada um. |
| Associação | A ligação dessa ficha à Bomba 01 cadastrada como Equipamento. |
| Contexto operacional | A condição em que os dados devem ser comparados, como uma faixa de operação definida. |
| Etapa do processo | Um grupo organizado de associações, como os equipamentos do bombeamento. Não programa partidas. |
| Cobertura | A consulta que mostra quais dados utilizáveis existem no período. |

Siga este percurso e aprofunde cada parte nas seções abaixo:

1. Confira os TAGs existentes, suas unidades e os dados gravados do período. Não invente sensores ausentes.
2. Monte o perfil e associe-o à Bomba 01. Os seletores cuidam dos IDs internos.
3. Salve a configuração, escolha onde usar Intelligence, publique/ative a revisão e libere explicitamente o recurso para análise. Licença, configuração e execução autorizada são condições separadas.
4. Consulte primeiro a cobertura. Depois escolha apenas análises cujas exigências seus sinais atendem. Resultado indisponível precisa de verificação; não deve ser lido como zero.
5. Quando precisar de uma visualização para o operador, gere uma tela, revise no editor normal e siga a publicação/ativação. A tela abre no Runtime e as consultas dependem de solicitação explícita; gerar não consulta dados nem coloca a tela em operação automaticamente.

Você pode começar por um equipamento. Contextos e etapas entram quando forem úteis à pergunta. O detalhe sobre horários, papéis dos sinais e limites está adiante para consulta durante a configuração.

## 1. Começar pelo equipamento e pelos TAGs

Escolha o Equipamento já cadastrado e os TAGs de medições úteis, como corrente, tensão, pressão, vazão ou temperatura. Para cada TAG, confira o tipo de valor e a unidade. Qualidade informa a condição da leitura; timestamp é seu horário de origem. Historian é o histórico gravado, que precisa ter dados no período escolhido. Inclua o sinal pela pergunta que ele ajuda a responder, não apenas porque existe.

Para editar, sua conta precisa das permissões `EngineeringModify` e `AssetProcessIntelligenceManage`. A segunda deve ser concedida explicitamente para todos os recursos, ou para o equipamento/local que você vai configurar. Confira também a licença Intelligence. O nome do papel da conta não substitui a permissão efetiva.

## 2. Criar perfil de medição e associação

1. Abra Engineering > Inteligência de ativos e crie um Perfil de medição. Dê um nome que ajude a reconhecer o conjunto, como “Bomba de transferência”.
2. Adicione os TAGs que descrevem o equipamento. Para cada TAG, escreva o que ele mede, marque se é necessário para a análise e complete os detalhes semânticos solicitados. Confira a unidade e a referência real do TAG.
3. Salve a configuração no Working.
4. Na seção de associações, escolha o Equipamento existente e o perfil. Um equipamento pode usar mais de um perfil.
5. Revise referências ausentes e duplicadas antes de salvar e seguir o lifecycle normal do projeto.

O perfil organiza referências e significado dos sinais. As chaves internas são criadas pelo sistema; você não precisa inventar IDs ou códigos. O perfil não cria TAGs, não habilita captura Historian por si só e não comprova que os instrumentos estão calibrados.

## 3. Configurar contexto operacional

Corrente maior pode ser esperada se velocidade ou carga aumentou. Um contexto operacional explicita condições, como bomba ligada e velocidade em uma faixa definida, para selecionar evidências comparáveis. Ele não aprende automaticamente o modo de operação nem transforma correlação em diagnóstico. Defina limites com conhecimento do equipamento.

Escolha a associação do equipamento e dê ao contexto um nome que explique a condição, como “Bomba ligada em vazão normal”. Adicione ao menos uma condição usando um TAG do perfil associado. A chave interna do contexto é criada pelo sistema.

Para número, o intervalo inclui mínimo e máximo e usa a unidade atual do TAG. Para booleano, escolha True ou False. Para String ou Enum, use o valor exato, incluindo maiúsculas quando aplicável.

Todas as condições precisam corresponder a leituras com exatamente o mesmo instante UTC de origem. Leituras apenas próximas no tempo não bastam. Se outra edição alterar o contexto, atualize a tela e revise as condições antes de salvar.

## 4. Organizar etapas do processo

Use **Etapas ordenadas do processo** para montar a sequência que deseja acompanhar. Dê a cada etapa um nome legível, adicione as associações de equipamento/perfil e ajuste a ordem. A chave interna é criada pelo sistema. A etapa organiza membros, mas não infere por si só causalidade, balanço ou um KPI agregado do processo.

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
- **Contexto sem correspondência:** revise as condições, a unidade, o intervalo e se as leituras têm o mesmo horário de origem.
- **Rascunho indisponível:** complete as seleções exigidas pelo tipo de tela e use o editor normal para corrigir o resultado.
- **Resultado diferente após edição:** salve a configuração correta, confira a revisão Active e faça uma nova consulta explícita.

**Ajuda relacionada:** `/help?topic=historian.overview`, `/help?topic=tags.quality`, `/help?topic=tags.timestamps`, `/help?topic=screens.overview`, `/help?topic=engineering.lifecycle`.

## Confira os dados antes de interpretar

Em Engenharia > Inteligência de ativos, localize a associação do equipamento. Em Conferir dados da referência, escolha início e fim de um período conhecido e clique em Consultar referência. Os horários são locais e convertidos para UTC. Cada período pode ter até 31 dias. Leituras aceitas têm a evidência de origem exigida; rejeições explicam por que outras leituras ficaram de fora. A contagem não comprova que o equipamento estava saudável.

Em Conferir condições do contexto, selecione um contexto salvo pelo nome e consulte o período. O SCADIVO usa a versão salva automaticamente. O resultado informa quantas leituras atendem a todas as condições no mesmo instante de origem. Zero leituras compatíveis pode significar condições não atendidas ou dados insuficientes; confira a explicação apresentada. Publique as alterações e ative o equipamento antes de consultar.

## Use o desvio conjunto experimental

Abra Desvio conjunto experimental na associação. Escolha o contexto salvo e de duas a oito medições numéricas; medições que definem as condições do contexto ficam fora da lista de alvos. Informe o intervalo real de aquisição em segundos, de 1 a 86.400. Não invente esse valor: ele descreve a frequência esperada das leituras. Escolha uma referência e um período posterior de avaliação, sem sobreposição, e clique em Calcular desvio conjunto.

O resultado mostra quantos instantes puderam ser avaliados e quantos ficaram em aquecimento. O desvio combina mudanças de nível e da relação entre sinais. Expanda Contribuições das medições para investigar a participação de cada sinal. Esses números não têm unidade física e não diagnosticam causa, falha ou vida útil. A função é experimental, validada somente por simulação e sem limiar de alarme validado em campo.

Use Cancelar consulta para interromper. Mudar período, contexto, sinais ou cadência descarta o resultado anterior; é preciso consultar novamente. Se a referência não puder ser ajustada, confira dados, qualidade, sincronização, contexto e variação dos sinais antes de ampliar períodos.

## Entenda as contagens e as condições

As análises são consultadas sob demanda: editar perfis ou ativar um escopo não inicia análise contínua. Na referência, leia os motivos ao lado de Não disponível. Um sinal obrigatório de texto, opção ou verdadeiro/falso não entra na coorte numérica obrigatória; a contagem dos sinais numéricos do perfil continua sendo apresentada separadamente. Contagem zero de um tipo não consultado não prova ausência de histórico.

Na consulta de contexto, veja primeiro Condições do contexto: leituras aceitas e quantas atenderam a cada condição. Uma condição de modo pode ser textual e ainda selecionar os instantes válidos para corrente ou vazão. Depois confira as medições nas condições selecionadas. Essas contagens não comprovam saúde ou eficiência.

## Compare dois períodos e cancele consultas

Nas consultas de referência e condições do contexto, marque Comparar com outro período se quiser conferir a evidência em duas janelas. Preencha o início e o fim da comparação. Cada janela pode ter até 31 dias, e o início da comparação deve ser estritamente posterior ao fim da referência: não use o mesmo instante nos dois limites. A consulta fica indisponível enquanto houver campos vazios, período invertido ou sobreposição.

Consulte e leia as contagens de cada período. A comparação de contagens não é, por si só, um indicador de eficiência. Desmarcar a comparação ou alterar qualquer limite remove o resultado anterior. Durante a leitura, use Cancelar consulta: uma resposta atrasada não deve reaparecer na tela. Para consultar novamente, confira os parâmetros e clique no botão de consulta.

<!-- installed-system-guides -->

## Intelligence: habilitar, pausar e resolver bloqueios

Escolha quais equipamentos analisar, prepare a configuração e ative o uso. Ter a licença não inicia análises automaticamente.

### Siga a sequência completa

Comece com um equipamento, como Bomba 01. Abra Engenharia > Inteligência de ativos. Você vai reunir as variáveis da bomba, dizer a qual equipamento pertencem e escolher onde usar a análise.

O perfil é a lista de sinais e seus significados. A associação liga esse perfil ao Equipamento cadastrado. Configure contextos e etapas somente quando fizerem sentido para sua pergunta. Depois salve, publique e ative a revisão do projeto.

Há ainda uma decisão separada: ativar explicitamente o recurso selecionado para permitir as análises. Criar o perfil, marcar uma caixa ou possuir a licença não faz essa ativação por você.

### Permissões e seleção de recursos

Sua conta precisa de permissão para editar a Engenharia e para gerenciar Intelligence nos equipamentos ou locais escolhidos. Na tela Segurança, essas permissões aparecem como EngineeringModify e AssetProcessIntelligenceManage. A segunda precisa de uma concessão explícita, global ou com alcance no recurso. O nome do papel, como Developer ou SystemAdmin, sozinho não comprova a concessão.

Para consultar resultados, também são necessários TrendUse e licença Intelligence. Se um controle estiver bloqueado, confira as capacidades efetivas da sessão nessa tela. Selecione os recursos pelos seletores; não é necessário decorar ou inventar IDs.

### Ativar apenas onde for útil

1. Em Escopos elegíveis, selecione apenas os equipamentos ou locais que deseja analisar. Escopo, aqui, é essa seleção de recursos.
2. Salve os escopos e a configuração preparada.
3. Publique e ative a revisão do projeto. Resolva alterações pendentes antes de liberar a execução.
4. Na linha do recurso, use Ativar e confira se a permissão de execução ficou armada, isto é, liberada para esse recurso.
5. Abra a consulta da associação, escolha o período e solicite a análise. A ativação permite consultar; não faz a consulta automaticamente.
6. Para interromper as análises, use Pausar. O trabalho pendente é cancelado, mas TAGs e histórico permanecem. Antes de retirar um recurso da seleção, pause-o.

### Ativo configurado não significa permissão armada

Se uma permissão foi revogada, o recurso pode continuar marcado como Ativo na configuração e, mesmo assim, estar impedido de executar. São duas informações diferentes: o que foi configurado e o que está autorizado agora.

Restaurar a concessão não reinicia o trabalho silenciosamente. Confira o estado e use Ativar novamente, inclusive após reinício quando a tela pedir. Isso exige uma decisão explícita do responsável. Verifique essa condição antes de investigar falta de dados no histórico gravado, chamado Historian.

### Exemplo de implantação pequena

Para Bomba 01, comece com corrente e velocidade se essas medições existirem. Escolha análises que aceitem esses sinais. Quando houver novas medições, você poderá ampliar o perfil e usar outras análises compatíveis.

Deixe pausados os recursos que não pretende usar. Não invente um sinal de vibração ausente. A seleção reduz o que está autorizado a executar, mas não garante um consumo fixo de processador ou memória.

[Abrir este guia na Ajuda instalada](/help?topic=intelligence.activation)

## Intelligence: significado dos sinais e unidades

Diga o que foi medido, não apenas como o TAG foi chamado.

### Por que o nome não basta

Antes de calcular, explique o que cada variável mede. Por exemplo, um TAG chamado Potência pode ser potência ativa, aparente ou um cálculo; Pressão pode medir a sucção, a descarga ou a diferença entre elas. O nome sozinho não permite escolher a fórmula correta.

No perfil, selecione o papel semântico pedido: é a opção que identifica o significado do sinal para a análise. Depois preencha os qualificadores exibidos, os detalhes que tornam essa escolha precisa. O texto de papel legado continua sendo uma descrição; não substitui essa seleção.

### Campos e escolhas

Use esta tabela quando surgir dúvida no perfil. Os seletores mostram as opções aceitas pelo catálogo atual. Escolha a que corresponde ao instrumento e ao processo, mesmo que outra opção liberasse um cálculo.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Obrigatório | Marca sinais cuja ausência compromete a cobertura pedida. Não transforme um sinal essencial em opcional apenas para obter resultado. | Corrente necessária à comparação |  |
| Papel semântico | Escolha o que o sinal representa para o cálculo, por exemplo qual grandeza e qual função na máquina. A análise usa essa opção controlada; não adivinha pelo nome do TAG. | Potência ativa de entrada, se medida |  |
| Base de corrente | Informe a base de corrente que o instrumento fornece e que o papel admite, como RMS quando apropriado. Se a etiqueta diz apenas corrente, confirme a medição antes de escolher. | Selecionar a opção documentada do instrumento |  |
| Tipo de sinal | Diferencia medição, referência e função exigida pelo papel. Um setpoint não comprova o valor medido. | Medição real quando a regra exigir |  |
| Referência de pressão | Diga qual pressão foi medida. A diferença entre saída e entrada é diferente da pressão de descarga sozinha; escolha a base exigida pelo papel e confirmada pelo instrumento. | Diferencial medido saída − entrada |  |
| Direção do fluxo | Diz em que sentido a vazão representa a transferência. Sentido oposto pode invalidar uma regra. | Bomba para processo, quando corresponder |  |
| Referência de frequência | Explica qual frequência o sinal representa conforme o catálogo; não confunda referência elétrica e velocidade física. | Conferir a origem do instrumento |  |
| TAG e unidade | Selecione o TAG real e confira sua unidade de engenharia controlada. Alterar apenas rótulo no perfil não converte os dados. Alguns cálculos exigem versões/unidades específicas do catálogo. | kW para potência, se esse for o dado real |  |

### Exemplo: não confundir proxy com eficiência

Suponha que a corrente da bomba aumentou enquanto a velocidade permaneceu igual. Isso pode orientar uma investigação, mas a corrente, sozinha, não calcula a eficiência física do motor. É um indicador substituto, também chamado proxy.

Outras regras podem usar potência elétrica de entrada, vazão e diferença de pressão. Elas só ficam disponíveis quando os sinais têm os papéis, unidades e alinhamento exigidos. Se faltar uma medição ou um detalhe necessário, leia o motivo da indisponibilidade; não preencha valores fictícios para liberar a regra.

[Abrir este guia na Ajuda instalada](/help?topic=intelligence.semantics)

## Intelligence: consultar resultados e montar dashboards

Comece conferindo os dados do período. Depois escolha um cálculo e, se precisar, gere uma tela para consultar no Runtime.

### Comece pela cobertura

Cobertura responde à primeira pergunta: há dados utilizáveis para esta análise? Com a associação salva e a execução autorizada, abra a consulta de cobertura do perfil. Escolha início e fim e solicite a consulta.

O formulário mostra horários locais; a consulta identifica também a janela UTC, a referência de tempo comum usada para comparar os dados. O fim deve ser posterior ao início.

Confira quais sinais obrigatórios têm dados, quantas amostras foram aceitas ou rejeitadas, a qualidade e o alinhamento dos horários. Só depois interprete mínimo, máximo ou média. Muitas amostras, por si só, não provam que não há lacunas nem que todos os sinais foram medidos juntos.

### Desempenho: escolha uma regra elegível

Em Desempenho, escolha a regra de cálculo do catálogo. Se uma opção estiver desabilitada, leia qual sinal, significado ou unidade está faltando. Escolher outra grandeza apenas para obter um número mudaria a pergunta.

Preencha De/Até e consulte. Leia o estado, a unidade, o período efetivo e as limitações junto do resultado. Indisponível não é zero. Um indicador substituto, como uma comparação de corrente, não deve ser apresentado como eficiência física.

Se mudar a associação, a revisão ou os parâmetros, faça outra consulta. O resultado anterior pertence às escolhas anteriores.

### Dois tipos de contexto, com limites diferentes

Contexto descreve uma condição de operação para comparar situações semelhantes. Por exemplo, analisar a bomba numa faixa de vazão pode ser mais útil que misturar todas as condições. Existem duas configurações diferentes:

No perfil operacional salvo, o intervalo numérico inclui os dois limites, na unidade de engenharia atual. Os sinais precisam de evidência com o mesmo timestamp UTC de origem exato. Leituras apenas próximas no tempo não atendem esse requisito.

No gerador de tela de desvio contextual, escolha 2 a 8 sinais-alvo e dois sinais numéricos de contexto. Os dois contextos devem ser distintos entre si e dos alvos: são pelo menos quatro sinais numéricos utilizáveis. As faixas usam a unidade controlada mostrada e incluem o limite inferior, mas excluem o superior: limite inferior ≤ valor < limite superior.

Confira qual configuração está usando antes de copiar limites. A unidade e a inclusão do limite superior podem ser diferentes.

### Gere a tela e confira a cadeia completa

1. Escolha a associação do equipamento ou a etapa do processo e o tipo de tela desejado.
2. Complete os sinais e opções pedidos. Se o botão estiver bloqueado, leia o motivo junto do campo.
3. Gere o rascunho. Esse passo organiza objetos e referências; ainda não lê dados do Historian nem ativa a tela.
4. No editor, confira título, dimensões, objetos, vínculos e forma de abrir a tela. Execute Pré-visualização e Aplicar para levar a edição ao projeto.
5. Salve a revisão, publique e ative. Abra a tela no Runtime pela navegação configurada.
6. No objeto de análise, configure o período e solicite a consulta. Confira a janela e o resultado. Uma tela de etapa reúne seus equipamentos; isso não cria um indicador agregado nem demonstra que um equipamento causou o resultado de outro.

### Roteiro para investigar resultado vazio

Se não há resultado, procure a primeira condição que falta:

Confira licença e permissões; depois, a seleção salva do recurso e a permissão de execução armada. Confira se o projeto e a revisão ativos são os esperados. Em seguida, revise associação, TAGs e período.

Se esses itens estão corretos, examine a captura no Historian, a qualidade dos dados, os papéis e unidades dos sinais e as exigências de alinhamento e contexto. Corrija a condição comprovadamente ausente e consulte de novo.

Não mude o significado de um sinal nem amplie limites só para fazer aparecer um resultado. As análises ajudam a investigar. Elas não confirmam defeito nem antecipam falhas com validade de campo ainda não demonstrada.

[Abrir este guia na Ajuda instalada](/help?topic=intelligence.analysis-guide)
