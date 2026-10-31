# Alarmes e eventos: configuração e operação

## Alarmes: configurar condição, prioridade e comportamento

Configure um aviso para uma condição conhecida e teste o que o operador verá. O exemplo usa pressão alta sem comandar equipamento.

<!-- HELP-REVIEW-AFTER-PRODUCT-751: https://github.com/brunolrogerio-collab/EliteSCADA/issues/751; topic=alarms.configuration; fields=alarm.activationDelayMilliseconds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-753: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.configuration; fields=alarm.requiresAcknowledgement; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Antes de começar

Comece por uma condição simples: avisar quando a pressão chegar ao limite alto. Em Engenharia > Alarmes, use um TAG de teste cujo valor, tipo, escala e unidade você já conferiu. Um TAG é a variável que fornece a leitura.

O alarme observa essa variável; ele não configura o sensor nem desliga a bomba sozinho. Combine limite e resposta operacional com o responsável pelo processo. Para aprender, use simulação, com permissão e bloqueio de Engenharia aplicáveis, sem atuar em equipamento real.

### Qual tipo escolher

Escolha o tipo pela pergunta que o alarme deve responder:

Digital acompanha um booleano: você escolhe se true ou false significa condição de alarme. High e HighHigh ativam quando o valor é maior ou igual ao limite (≥). Low e LowLow ativam quando é menor ou igual (≤). Para dois limites, crie duas definições e escolha a prioridade de cada uma.

Communication acompanha a qualidade da leitura: ativa quando ela é diferente de Good e retorna com Good. Não usa setpoint, o limite numérico, nem cria um temporizador de perda de comunicação só por ser selecionado. Com qualidade ruim, os alarmes de processo não tratam o número como leitura válida; use Communication para acompanhar essa condição.

System aparece no seletor, mas não tem uma regra genérica de processo neste avaliador. Para limite alto ou estado booleano, escolha o tipo próprio, em vez de usar System como substituto.

### Procedimento: testar um limite alto

Neste teste, 10 bar é apenas um valor didático. ACK significa reconhecimento: o operador confirma que tomou ciência da notificação.

1. Crie um alarme high para o TAG de pressão simulado, com setpoint 10. Confira a unidade bar no TAG.
2. Preencha prioridade, área e mensagem clara. Mantenha Requer reconhecimento marcado neste teste.
3. Execute Preview, leia os erros e aplique ao Workspace, o rascunho de edição. Se editar de novo, valide de novo.
4. Salve, publique e ative a revisão correta. Aplicar ao Workspace não ativa o Runtime sozinho.
5. Com qualidade Good, envie 9: a condição fica Normal. Envie 10: fica Active. A igualdade também ativa.
6. Reconheça no Runtime mantendo 10 no TAG: o estado fica Acknowledged, mas a pressão não mudou. Envie 9: fica Returned e sai da lista ativa porque já teve ACK.
7. Repita a ativação com 10 e volte a 9 sem reconhecer: Returned continua pendente. Reconheça e confira a saída da lista ativa. Consulte o histórico para ver as transições anteriores.

### Identificação e condição

Selecione Novo alarme ou uma definição existente. O caminho identifica qual sinal será avaliado; o nome e a mensagem ajudam o operador a entender o problema.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome | Use uma descrição legível que distinga equipamento e condição. Renomear não muda o TAG observado. | Pressão alta — Bomba 01 |  |
| Caminho do TAG | Informe o caminho de um TAG existente. Confira o valor já escalonado no Monitor de TAGs antes de escolher o limite. | Planta.Bomba01.Pressao | Não é endereço de Driver nem nome de tela. |
| Tipo | Escolhe a regra de comparação; consulte a explicação dos tipos neste artigo. HighHigh é outra definição de limite, não uma prioridade automática. | high |  |
| Setpoint (limite) | Aparece nos tipos analógicos. Informe um número na unidade atual do TAG. High/HighHigh ativam em valor ≥ limite; Low/LowLow em valor ≤ limite. | 10 bar: preencher 10 | A igualdade também ativa. Não há campo de histerese neste editor. |
| Valor digital ativo | Só aparece em Digital. Marcado significa que true é a condição de alarme; desmarcado significa false. Confirme a polaridade real do sinal. | Falha=true / Pronto=false |  |
| Prioridade | Ordena e destaca a atenção: low, medium, high ou critical. Não altera o limite, a permissão nem a lógica de parada. | high |  |
| Classe | Rótulo de classificação para organizar definições. Não instala uma regra de proteção ao digitar um nome. | Processo |  |
| Área | Contexto do processo usado também na autorização por área das ações. Use o nome previsto na política; digitar uma área não concede acesso. | Estacao01 |  |
| Mensagem | Explique condição e local de forma útil para o operador. Uma instrução escrita não executa um comando. | Bomba 01: conferir pressão de descarga |  |

### Comportamento e som

Expanda Comportamento do alarme. O atraso começa quando a condição é observada e só ativa o alarme depois do intervalo configurado. O campo Requer reconhecimento ainda tem a limitação indicada junto a ele.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Habilitado | Inclui a definição na avaliação quando a revisão for ativada. Desabilitar não corrige o sinal nem a causa física. | Marcado |  |
| Atraso de ativação (ms) | Depois que a condição fica ativa, o Runtime espera este intervalo antes de ativar o alarme. Se a condição voltar ao normal ou a qualidade ficar diferente de Good antes do fim, a contagem é cancelada. Para alarmes de comunicação, qualidade diferente de Good é a condição ativa. | 1000 ms = 1 s | Campo inteiro não negativo; 0 ou vazio ativa imediatamente. A contagem pendente recomeça após reinício do Runtime. |
| Requer reconhecimento | Indica a intenção de exigir ACK do operador. Limitação atual: desmarcar não elimina a pendência de reconhecimento no Runtime, inclusive após retorno. | Marcado |  |
| Permitir shelving | Autoriza que esta definição possa ser colocada em estado suprimido pela operação disponível. A ação ainda exige permissão AlarmShelve. O Alarm Center descrito aqui não oferece botão de shelving. | Conforme procedimento da planta | Não é Silenciar sons; não se promete expiração automática. |
| Perfil sonoro / prévia | Escolha nenhum, pulso curto, duplo, triplo, ascendente ou alternado e teste a prévia. No Runtime, o operador precisa ativar sons no navegador. Volume, saída de áudio e permissão do navegador influenciam a reprodução. | double | Silenciar no navegador não reconhece o alarme. |

### Se o resultado não for o esperado

Confira TAG/revisão ativa, escala/unidade, sinal booleano, habilitação e qualidade. Alta prioridade não força ativação. Uma condição que volta ao normal antes do fim cancela a contagem; isso não cria histerese para um limite que oscila. Alarmes de comunicação consideram qualidade diferente de Good como condição ativa. A opção Requer reconhecimento ainda tem a limitação indicada no campo. Preview valida o candidato; não comprova leitura de campo. Caso o projeto mude durante a edição, recarregue e valide de novo em vez de aplicar um candidato antigo.

[Abrir este guia na Ajuda instalada](/help?topic=alarms.configuration)

## Alarmes no Runtime: estados, reconhecimento e sons

Leia a condição atual, reconheça com segurança e diferencie silêncio, retorno e histórico.

<!-- HELP-REVIEW-AFTER-PRODUCT-753-RUNTIME: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.runtime-operation; fields=alarmState.returned; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Onde olhar e como interpretar

A central de alarmes do Runtime ajuda a decidir o que precisa de atenção agora. Ela mostra a lista ativa permitida para sua sessão, contagens e detalhes da linha selecionada. Não é o histórico de tudo que já aconteceu.

Selecione uma linha e confira equipamento, mensagem, prioridade, área, valor e horários. Os detalhes também informam quem reconheceu e quando. IDs identificam a definição exata para consulta ou suporte; reconhecer não exige inventar nem digitar um ID.

### Estados que aparecem nos detalhes

ACK é a confirmação de que o operador tomou ciência do alarme. Ela é separada da condição física. Por isso, um alarme pode já estar reconhecido e continuar ativo.

A lista ativa inclui Active, Acknowledged e Returned ainda sem ACK. Ela pode ficar vazia depois de resolver e reconhecer os itens, embora as transições anteriores continuem no histórico. A tabela explica os estados que aparecem nos detalhes.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Normal | Condição não ativa no momento; um ciclo anterior pode existir no histórico. | 9 < 10 |  |
| Active | Condição presente e ainda sem reconhecimento desse ciclo. | 10 ≥ 10 |  |
| Acknowledged | ACK registrado, mas a condição continua presente. Não significa que a causa foi resolvida. | ACK com pressão ainda em 10 |  |
| Returned | Condição deixou de estar ativa. Se ainda não houve ACK, permanece pendente na lista ativa. Depois de ACK, sai dessa lista. | 10 → 9 |  |
| Disabled | Definição desabilitada para avaliação. Não indica equipamento saudável. | Habilitado desmarcado |  |
| Shelved | Notificação suprimida pela operação de shelving. O valor e o estado subjacente continuam sendo acompanhados; ao retirar a supressão, reaparece o estado subjacente atual. | Manutenção controlada | A central atual não oferece botão para essa ação. |

### Procedimento: reconhecer uma notificação

1. Selecione a linha. Leia condição, valor e horário e confirme o equipamento.
2. Siga a resposta operacional prevista pela planta para aquela condição.
3. Use Reconhecer alarme se sua sessão permitir a ação na área correspondente. A permissão técnica é AlarmAcknowledge; uma sessão View Only continua sem essa ação.
4. Aguarde confirmação e atualização da lista. Se o servidor aceitou o ACK, mas a releitura falhou, atualize para conferir antes de repetir ações.
5. Se a condição ainda existe, o alarme reconhecido permanece visível. Se já retornou, o item deixa a lista ativa após o ACK. Uma nova ativação cria outra pendência de reconhecimento.

### Sons, silêncio e indisponibilidade

Para ouvir avisos, use Ativar sons de alarmes nesta sessão do navegador. Confira suporte, volume e saída de áudio. Os perfis repetem enquanto houver alarme pendente, inclusive Returned sem ACK.

Silenciar sons é local: não reconhece, desabilita ou coloca em shelving. Shelving é a supressão da notificação. Reconhecer também não envia comando ao processo. São efeitos diferentes.

Sem acesso, confira login, classe de sessão e permissão para a área. Sem dados ou serviço disponível, atualize e consulte o diagnóstico do Runtime; mudar o TAG não resolve uma recusa de autorização. Para o passado, abra Histórico e escolha alarm.events no período desejado. Esse conjunto é de alarmes; operational.events e Audit têm outras finalidades.

[Abrir este guia na Ajuda instalada](/help?topic=alarms.runtime-operation)

## Eventos operacionais: cadastrar, emitir e consultar

Registre uma ocorrência do processo com contexto, sem confundir definição, ocorrência, alarme e Audit.

### Escolha o recurso certo

Use um evento operacional para registrar um fato, como início de lote ou troca de modo. Cada emissão cria uma ocorrência com horário e identidade próprios. Ela não mantém uma condição ativa nem pede ACK.

Escolha alarme quando precisar acompanhar uma condição que ativa e retorna. Audit serve para rastrear ações e resultados de segurança ou administração. Um clique na tela também é chamado evento, mas não vira ocorrência operacional sozinho.

Primeiro você cadastra a definição do fato; depois configura quem o emite. Cadastrar não emite automaticamente. Associar um TAG só dá contexto, não cria gatilho de mudança.

### Procedimento: preparar a definição e o produtor

1. Crie a definição com os campos obrigatórios e um nome/chave que distingam o fato. Acrescente contexto útil.
2. Execute Preview e corrija chaves duplicadas ou referências inválidas indicadas nos diagnósticos.
3. Aplique ao Workspace e depois salve, publique e ative a revisão pelos controles do projeto.
4. Prepare quem emitirá a ocorrência. Um Server Script pode chamar emit_operational_event. Obtenha o ID estável da definição salva nas referências ou dados exportados do projeto; não substitua pelo Identificador textual.
5. Associe o handler, a função Python, ao gatilho Server adequado. Evite Timer para registrar uma única transição: cada chamada cria outra ocorrência, mesmo com mensagem igual.
6. Ative a revisão que contém a definição e o Script.
7. Produza um fato de teste controlado e consulte operational.events no Histórico, incluindo o horário da execução no período.

### Exemplo Python: uma ocorrência ao inicializar

Este exemplo registra o início da simulação, não a partida física de uma bomba. O UUID ilustrativo é o ID da definição: substitua pelo ID estável da sua definição salva.

No editor de Scripts, escolha Server e associe Initialize ao handler initialize. Escrever a função não cria essa associação. A função executa a cada inicialização do Script; o argumento event traz o contexto do gatilho e pode ficar sem uso.

A chamada emit_operational_event(definition_id, message=None, context=None) retorna None, não o ID da ocorrência. No dicionário context, chaves e valores são textos. Use str(valor) para transformar um número calculado em texto antes de incluí-lo.

```python
def initialize(event):
    emit_operational_event(
        "c1900000-0000-0000-0000-000000000002",
        "Simulation started",
        {"mode": "simulation", "batch": "42"}
    )
```

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

### Consultar e diagnosticar

No Histórico, escolha operational.events, o conjunto de ocorrências operacionais. Ajuste início/fim e filtros e confira horário, origem, mensagem e contexto. alarm.events contém transições de alarmes; Audit registra outro tipo de ação.

O contexto adicional não muda tipo, categoria, origem ou identidade da definição, mesmo que use chaves com esses nomes. São permitidos até 128 pares, com chaves de até 160 caracteres e valores de até 4000; a mensagem também tem limite de 4000. Não inclua senhas ou segredos.

Se não apareceu ocorrência, confira revisão ativa, definição habilitada, ID, ligação do handler, diagnóstico do Script, período e permissão. Somente cadastrar a definição não produz registros. Uma falha de emissão não autoriza outro caminho: a emissão precisa respeitar a autoridade ativa do Runtime.

[Abrir este guia na Ajuda instalada](/help?topic=operational-events.configuration)
