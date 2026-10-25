# Alarmes e eventos: configuração e operação

## Alarmes: configurar condição, prioridade e comportamento

Monte um alarme a partir de um TAG e saiba o que cada opção realmente faz.

<!-- HELP-REVIEW-AFTER-PRODUCT-751: https://github.com/brunolrogerio-collab/EliteSCADA/issues/751; topic=alarms.configuration; fields=alarm.activationDelayMilliseconds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-753: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.configuration; fields=alarm.requiresAcknowledgement; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Antes de começar

Abra Engenharia > Alarmes. Tenha um TAG cuja leitura, tipo, escala e unidade já foram conferidos. Um alarme de pressão alta observa o valor do TAG; não configura o sensor e não desliga a bomba sozinho. Defina limites e resposta operacional com o responsável pelo processo. Para aprender, use um TAG de simulação, sem atuar em equipamento real. Edição requer a permissão e o bloqueio de Engenharia aplicáveis.

### Identificação e condição

Selecione Novo alarme ou uma definição existente. O caminho identifica qual sinal será avaliado; o nome e a mensagem ajudam o operador a entender o problema.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome | Use uma descrição legível que distinga equipamento e condição. Renomear não muda o TAG observado. | Pressão alta — Bomba 01 |  |
| Caminho do TAG | Informe o caminho de um TAG existente. Confira o valor já escalonado no Monitor de TAGs antes de escolher o limite. | Planta.Bomba01.Pressao | Não é endereço de Driver nem nome de tela. |
| Tipo | Escolhe a regra de comparação; consulte os tipos abaixo. HighHigh é outra definição de limite, não uma prioridade automática. | high |  |
| Setpoint (limite) | Aparece nos tipos analógicos. Informe um número na unidade atual do TAG. High/HighHigh ativam em valor ≥ limite; Low/LowLow em valor ≤ limite. | 10 bar: preencher 10 | A igualdade também ativa. Não há campo de histerese neste editor. |
| Valor digital ativo | Só aparece em Digital. Marcado significa que true é a condição de alarme; desmarcado significa false. Confirme a polaridade real do sinal. | Falha=true / Pronto=false |  |
| Prioridade | Ordena e destaca a atenção: low, medium, high ou critical. Não altera o limite, a permissão nem a lógica de parada. | high |  |
| Classe | Rótulo de classificação para organizar definições. Não instala uma regra de proteção ao digitar um nome. | Processo |  |
| Área | Contexto do processo usado também na autorização por área das ações. Use o nome previsto na política; digitar uma área não concede acesso. | Estacao01 |  |
| Mensagem | Explique condição e local de forma útil para o operador. Uma instrução escrita não executa um comando. | Bomba 01: conferir pressão de descarga |  |

### Qual tipo escolher

Digital compara o booleano com o valor ativo escolhido. High e HighHigh usam ≥; Low e LowLow usam ≤. Para dois limites, crie duas definições e escolha suas prioridades separadamente. Communication ativa para qualidade diferente de Good e retorna com Good; não depende de setpoint. Com qualidade ruim, os tipos de processo não avaliam um limite numérico como se a leitura fosse válida: use o alarme de comunicação para acompanhar isso. System aparece no seletor, mas não tem regra genérica de processo neste avaliador; não o use como substituto de High ou Digital. Não se presume temporizador de perda de comunicação apenas por selecionar Communication.

### Comportamento e som

Expanda Comportamento do alarme. Algumas opções são salvas na definição, mas têm limitações no Runtime atual; elas estão descritas junto ao campo.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Habilitado | Inclui a definição na avaliação quando a revisão for ativada. Desabilitar não corrige o sinal nem a causa física. | Marcado |  |
| Atraso de ativação (ms) | Representa o atraso desejado. Limitação atual: o valor é salvo, mas não retarda a ativação no Runtime. Não conte com ele para eliminar oscilações. | 1000 ms = 1 s | Campo inteiro; atraso ainda não aplicado. |
| Requer reconhecimento | Indica a intenção de exigir ACK do operador. Limitação atual: desmarcar não elimina a pendência de reconhecimento no Runtime, inclusive após retorno. | Marcado |  |
| Permitir shelving | Autoriza que esta definição possa ser colocada em estado suprimido pela operação disponível. A ação ainda exige permissão AlarmShelve. O Alarm Center descrito aqui não oferece botão de shelving. | Conforme procedimento da planta | Não é Silenciar sons; não se promete expiração automática. |
| Perfil sonoro / prévia | Escolha nenhum, pulso curto, duplo, triplo, ascendente ou alternado e teste a prévia. No Runtime, o operador precisa ativar sons no navegador. Volume, saída de áudio e permissão do navegador influenciam a reprodução. | double | Silenciar no navegador não reconhece o alarme. |

### Procedimento: testar um limite alto

1. Crie um alarme high para o TAG de pressão simulado, com setpoint 10 e unidade bar no TAG.
2. Preencha prioridade, área e uma mensagem clara. Para este teste mantenha Requer reconhecimento marcado.
3. Execute Preview, leia erros e aplique ao Workspace. Qualquer nova edição exige outra validação.
4. Salve, publique e ative a revisão correta; aplicar ao Workspace não ativa o Runtime sozinho.
5. Com qualidade Good, envie 9: a condição fica Normal. Envie 10: fica Active, pois a igualdade entra no limite.
6. Reconheça no Runtime: fica Acknowledged e o TAG continua em 10. Envie 9: fica Returned e deixa a lista ativa porque já teve ACK.
7. Em nova ativação, envie 10 e depois 9 sem ACK: fica Returned pendente. Reconheça e confira a saída da lista ativa. Consulte histórico para as transições anteriores.

### Se o resultado não for o esperado

Confira TAG/revisão ativa, escala/unidade, sinal booleano, habilitação e qualidade. Alta prioridade não força ativação. Um limite que oscila não ganha histerese por mudar a mensagem ou a classe. Preview valida o candidato; não comprova leitura de campo. Atraso e dispensa de ACK continuam com as limitações descritas. Caso o projeto mude durante a edição, recarregue e valide de novo em vez de aplicar um candidato antigo.

[Abrir este guia na Ajuda instalada](/help?topic=alarms.configuration)

## Alarmes no Runtime: estados, reconhecimento e sons

Leia a condição atual, reconheça com segurança e diferencie silêncio, retorno e histórico.

<!-- HELP-REVIEW-AFTER-PRODUCT-753-RUNTIME: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.runtime-operation; fields=alarmState.returned; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Onde olhar e como interpretar

Abra a central de alarmes do Runtime. Ela mostra a lista ativa visível para sua sessão, contagens e detalhes do item selecionado. Não é uma lista de todas as ocorrências passadas. Selecione uma linha para ver mensagem/nome, prioridade, área, último valor, ativação, última transição e quem/quando reconheceu. IDs nos detalhes ajudam a identificar exatamente a definição; não é necessário inventar ou digitar um ID para reconhecer.

### Estados que aparecem nos detalhes

A lista ativa inclui Active, Acknowledged e Returned sem ACK. Assim, vazio não significa que nunca houve alarme. Estados fora dela podem ser encontrados em outras consultas apropriadas.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Normal | Condição não ativa no momento; um ciclo anterior pode existir no histórico. | 9 < 10 |  |
| Active | Condição presente e ainda sem reconhecimento desse ciclo. | 10 ≥ 10 |  |
| Acknowledged | ACK registrado, mas a condição continua presente. Não significa que a causa foi resolvida. | ACK com pressão ainda em 10 |  |
| Returned | Condição deixou de estar ativa. Se ainda não houve ACK, permanece pendente na lista ativa. Depois de ACK, sai dessa lista. | 10 → 9 |  |
| Disabled | Definição desabilitada para avaliação. Não indica equipamento saudável. | Habilitado desmarcado |  |
| Shelved | Notificação suprimida pela operação de shelving. O valor e o estado subjacente continuam sendo acompanhados; ao retirar a supressão, reaparece o estado subjacente atual. | Manutenção controlada | A central atual não oferece botão para essa ação. |

### Procedimento: reconhecer uma notificação

1. Selecione a linha e leia condição, valor e horário; confirme o equipamento.
2. Execute a resposta operacional prevista pela planta.
3. Use Reconhecer alarme se a sessão possui permissão AlarmAcknowledge para a área. Uma sessão View Only não ganha essa ação.
4. Aguarde a confirmação do servidor e a releitura. Se o ACK foi aceito mas a releitura falhou, atualize para conferir o resultado antes de repetir ações.
5. Se a condição persiste, o estado reconhecido continua visível. Se ela já retornou, o item deixa a lista ativa após o ACK. Uma nova ativação abre outra pendência.

### Sons, silêncio e indisponibilidade

Ativar sons de alarmes libera o áudio nesta sessão do navegador; depende de suporte, volume e saída de áudio. Perfis configurados repetem enquanto houver alarme pendente, inclusive Returned sem ACK. Silenciar sons é local e não reconhece, desabilita ou coloca em shelving. Reconhecer não envia comando ao processo. Sem acesso: confirme login, classe de sessão e permissão/área; não tente resolver mudando o TAG. Sem dados ou serviço indisponível: releia e confira diagnóstico do Runtime. Para ocorrências passadas, use Histórico e escolha alarm.events com período adequado; não confunda com operational.events ou Audit.

[Abrir este guia na Ajuda instalada](/help?topic=alarms.runtime-operation)

## Eventos operacionais: cadastrar, emitir e consultar

Registre uma ocorrência do processo com contexto, sem confundir definição, ocorrência, alarme e Audit.

### Escolha o recurso certo

Evento operacional registra um fato, por exemplo início de lote ou troca de modo. Cada emissão cria uma ocorrência com horário e identidade próprios; não mantém condição ativa nem exige ACK. Alarme acompanha uma condição que pode ativar/retornar. Audit rastreia ações e resultados de segurança/administração. Um clique de objeto visual também se chama evento, mas não é uma ocorrência operacional por si só. Cadastrar a definição abaixo não emite nada automaticamente; associar um TAG só dá contexto, não cria gatilho de mudança.

### Campos da definição

Abra Engenharia > Eventos Operacionais e selecione Novo Evento Operacional. Informe primeiro significado e origem; expanda Contexto opcional somente quando necessário. Os rótulos de tipo/categoria/origem são textos da sua convenção, não configurações de conexão.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome de exibição | Nome legível para pessoas. | Início da simulação | Até 240 caracteres. |
| Identificador | Chave textual única entre definições, sem diferenciar maiúsculas. Não é o ID estável usado pela API Python. | simulation.started | Obrigatório; até 160 caracteres. |
| Tipo | Descreve a natureza do fato; não seleciona o tipo de alarme. | process | Obrigatório; até 120 caracteres. |
| Categoria | Agrupa ocorrências segundo sua convenção para busca e análise. | operation | Obrigatório; até 120 caracteres. |
| Origem | Identifica semanticamente quem produz o fato. Digitar server-script não cria nem vincula um script automaticamente. | server-script | Obrigatório; até 240 caracteres. |
| Habilitado | Define a disponibilidade da definição na revisão de Runtime. Não remove ocorrências históricas. | Marcado |  |
| Mensagem padrão | Texto usado se a emissão não informar outra mensagem. Uma mensagem da emissão pode substituí-lo. | Simulação iniciada | Até 4000 caracteres. |
| Área (opcional) | Localiza o fato na organização do processo; não concede permissão ao usuário. | Estacao01 | Até 240 caracteres. |
| Caminho do equipamento (opcional) | Referência de contexto ao equipamento. Não cria equipamento nem desenho. | Planta.Bomba01 | Até 500 caracteres. |
| TAG (opcional) | Escolha um TAG existente no seletor; o editor guarda a identidade e o caminho. Sem TAG é válido para um fato geral. | Planta.Bomba01.Estado | Associação não monitora mudanças automaticamente. |

### Procedimento: preparar a definição e o produtor

1. Crie a definição com os campos obrigatórios e contexto útil; evite uma chave genérica igual para todos os fatos.
2. Execute Preview e leia os diagnósticos. Corrija chaves duplicadas e referências inválidas.
3. Aplique ao Workspace e depois salve, publique e ative conforme o ciclo do projeto.
4. Configure o produtor: um Server Script pode usar emit_operational_event. Obtenha o ID estável da definição salva nos dados exportados/referências do projeto; não substitua pelo Identificador textual.
5. Associe o handler Python ao gatilho Server apropriado. Evite Timer para registrar apenas uma transição: cada chamada cria outra ocorrência, mesmo que a mensagem seja igual.
6. Ative a revisão que contém tanto definição quanto script.
7. Produza um fato controlado e consulte operational.events no Histórico, com período que inclua a execução.

### Exemplo Python: uma ocorrência ao inicializar

O UUID abaixo é ilustrativo: substitua pelo ID estável da sua definição. No Script Engineering, escolha escopo Server e associe o evento Initialize ao handler initialize; o nome da função sozinho não cria essa associação. O exemplo registra o início da simulação a cada inicialização do script, não a partida física de uma bomba. O argumento event é o contexto do gatilho e pode ficar sem uso. emit_operational_event(definition_id, message=None, context=None) retorna None; não devolve o ID da ocorrência. As chaves e valores do dicionário de contexto são textos: use str(valor) para números calculados.

```python
def initialize(event):
    emit_operational_event(
        "c1900000-0000-0000-0000-000000000002",
        "Simulation started",
        {"mode": "simulation", "batch": "42"}
    )
```

### Consultar e diagnosticar

No Histórico selecione operational.events, ajuste início/fim e os filtros disponíveis; confira horário, origem, mensagem e contexto. alarm.events contém transições de alarmes, e Audit possui outra finalidade. Contexto adicional não muda tipo, categoria, origem ou identidade próprios da definição, mesmo que contenha chaves com esses nomes. Até 128 pares de contexto, chaves de até 160 caracteres e valores de até 4000; mensagens também têm limite de 4000. Não inclua senhas ou segredos. Sem ocorrência: confira revisão ativa, definição habilitada, ID correto, associação do handler, diagnóstico do script, período e permissão de consulta. A emissão deve respeitar a autoridade ativa do Runtime; uma falha não autoriza emitir por caminho alternativo. Nada aparecer após apenas cadastrar a definição é esperado.

[Abrir este guia na Ajuda instalada](/help?topic=operational-events.configuration)
