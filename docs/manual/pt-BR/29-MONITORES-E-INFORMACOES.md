# Monitores e Informações

## Monitor de desenvolvimento: acompanhar referências

Monte uma lista de observação de TAGs, memórias e diagnósticos sem escrever ou forçar valores.

### Escolha este monitor para comparar variáveis

No Engineering, abra Monitoramento. Ele reúne referências que você escolhe em uma tabela somente leitura. O catálogo usa a engenharia aberta, mas valores de TAGs e Memória do Servidor vêm do Runtime acessível; uma definição no Working não cria uma amostra ativa. Memória do Cliente pertence ao contexto local deste navegador. Estados de sistema/driver são diagnósticos, não medições físicas. Para detalhes e histórico de um TAG, use TAG Monitor; para saúde de conexão, use Diagnóstico de comunicação.

### Adicionar e acompanhar

1. Confira o projeto aberto e a aplicação ativa.
2. Em Procurar referências do projeto, busque e expanda as famílias/pastas até a referência desejada; selecione-a para adicionar.
3. Como alternativa, use Adicionar referência exata e pressione Adicionar ou Enter. Esse campo exige referência/caminho exato; não é busca por trecho.
4. Leia Fonte, Tipo, Valor, Qualidade / Estado e Última atualização em conjunto.
5. Acrescente outras referências para comparar e aguarde novas observações.
6. Remova a linha ou use Limpar quando não precisar da lista. Isso não apaga TAGs ou definições do projeto.

### Campos e ações

Use a árvore para evitar digitação e resolver nomes ambíguos. A lista de observação é guardada na sessão do navegador; não é configuração publicada nem garantia de persistência após fechar a sessão.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Adicionar referência exata | Recebe caminho/referência completo. O mesmo rótulo de dois recursos é ambíguo; escolha na árvore. | Plant.Motor.Status | Não cria TAG nem busca parcial; referência inexistente/ambígua gera mensagem. |
| Procurar referências do projeto | Busca e árvore por famílias para escolher TAG, memória, sistema, driver ou asset existente. | Escolher uma referência na árvore | Estar no catálogo não garante valor vivo; assets não se tornam sinais de processo. |
| Nome / Referência | Mostra rótulo e referência da linha. Confira o recurso antes de comparar. | Plant.Motor.Status.03 | Referência de bit não é um novo TAG físico. |
| Fonte | Distingue TAG, Memória do Servidor, Memória do Cliente, Sistema, Driver e Asset. | Memória do Cliente | Memória local de um navegador não é o valor de outro cliente. |
| Valor | Mostra o valor disponível ou um traço quando ausente/não representável. | 8 ou true | Traço não é zero/false; números Int64 devem manter precisão. |
| Tipo | Tipo da referência ou amostra; uma vista de bit é Boolean. | Int16 → bit Boolean | Não converte a definição original ao observar. |
| Qualidade / Estado | TAGs mostram qualidade; referências locais/diagnósticas podem mostrar LocalSession, Available ou Unavailable. | Good / LocalSession | São conceitos distintos; estado local não prova aquisição física Good. |
| Última atualização | Mostra horário de origem quando disponível; caso contrário, horário da observação. | Horário da leitura observada | Memória local não ganha um timestamp de sensor; confira a origem. |
| Remover | Tira uma referência da lista de observação. | Remover uma linha | Não exclui recurso, amostra nem histórico. |
| Limpar | Esvazia a lista observada desta sessão. | Tabela sem itens | Não apaga aplicação nem interrompe aquisição. |

### Exemplo: observar o bit de uma palavra

Uma palavra Int16 de valor 8 tem o bit de índice 3 ligado: 8 = 2³. O sufixo .03 seleciona esse bit, contando a partir de zero; .00 é o primeiro bit. Adicione a palavra e sua vista para comparar. O tipo base deve permitir seleção de bit, dentro de sua largura. Não interprete .03 como casa decimal, endereço de bobina ou campo independente. O exemplo abaixo usa nomes fictícios; só funciona se o TAG existir e tiver amostra inteira válida. Observar o bit não escreve na palavra.

```json
{
  "reference": "Plant.Motor.Status",
  "dataType": "Int16",
  "value": 8,
  "bitReference": "Plant.Motor.Status.03",
  "expectedBit": true
}
```

### Falta de valor ou atualização

Referência não encontrada: confira caminho e projeto; nomes ambíguos exigem seleção explícita. Definição só no Working: confirme o Active antes de esperar valores. Unavailable/sem amostra: confira permissões, servidor, fonte e qualidade; não coloque zero para esconder a ausência. A tabela combina atualização agrupada e realtime; uma desconexão não permite afirmar que o último valor está atual. Client Memory observa o contexto local, não outro Runtime. Use o diagnóstico do recurso para aprofundar e anote referência, estado e horário. Não há comando de escrita/force neste monitor.

[Abrir este guia na Ajuda instalada](/help?topic=engineering.development-monitor)

## TAG Monitor: conferir valor, qualidade e histórico

Compare Working e Active e investigue um TAG no Runtime real, sem confundir edição com dados em execução.

### Identifique o que está sendo observado

Abra Engineering → Diagnósticos → TAG Monitor. O topo identifica a engenharia aberta e a aplicação ativa; o Inspector abaixo consulta TAGs visíveis do Active Runtime. Working serve de contexto e não substitui a origem dos valores. Se houver simulação/demo ativa, essa é a origem informada; não é prova de comunicação com equipamento físico. O monitor é somente leitura: mesmo um TAG marcado Gravável não pode ser escrito por esse painel.

### Conferir um TAG passo a passo

1. Compare Contexto Engineering, Revisão Working e Identidade ativa. Se Working difere do Active, não assuma que a edição já está em execução.
2. Em Buscar TAGs, digite parte do path, nome, tipo, unidade, origem ou valor.
3. Use Qualidade e Acesso para reduzir a lista; limpe filtros se faltar uma TAG esperada.
4. Selecione a TAG. Confira valor, tipo, unidade, qualidade, origem, horários e Atualização do valor.
5. Leia Histórico recente; use Atualizar histórico para consultar novamente.
6. Confira se há permissão, captura e amostras no intervalo antes de concluir que histórico vazio é defeito. Para busca completa, períodos/Playback e exportação, use os recursos de Histórico correspondentes.

### Contexto e filtros

O indicador Working vs Active compara projeto, revisão e alterações não salvas. É uma conferência de contexto; não publica nem ativa o projeto.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Contexto Engineering | Projeto aberto para edição. | Projeto da estação | Não garante que esse projeto esteja ativo. |
| Revisão Working | Revisão base aberta na engenharia; alterações não salvas podem diferir dela. | Revisão 12 | Não é automaticamente a revisão em execução. |
| Identidade ativa | Projeto/revisão do Runtime observado; ou indicação de simulação/ausência de Active. | Projeto · rev 11 | Sem identidade disponível, não invente uma comparação confirmada. |
| Working vs Active | Alinhado ou diferente orienta a comparação; leia os avisos de modo e indisponibilidade. | Working difere do Active | Salvar não implica Publicar/Ativar. |
| Ativado em | Momento informado de ativação da revisão. | Horário de ativação | Não é horário de amostra. |
| Buscar TAGs | Filtra os metadados/valor dos TAGs visíveis. | Parte do path ou unidade | Uma TAG pode existir mas não estar visível à sua conta. |
| Qualidade | Todos, Good, Atenção, Bad ou Sem amostra para localizar a condição desejada. | Sem amostra | Não altera a qualidade; ausência de resultado pode ser filtro. |
| Acesso | Todos, Somente leitura ou Gravável conforme o atributo do TAG. | Gravável | Não é permissão efetiva de escrita; painel continua somente leitura. |
| Atualizar | Reconsulta a lista. Observe também o estado realtime/periódico. | Nova consulta | Não reinicia aquisição nem limpa histórico. |

### Detalhes e histórico recente

Leia qualidade e atualização com o valor. Há diferença entre um valor antigo, uma consulta recente que voltou válida e um novo valor de processo.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Valor atual | Valor exibido com a unidade quando existe. | Pressão medida | Sem amostra não é zero; aparência do valor não comprova saúde. |
| Qualidade | Condição informada da amostra, como Good ou BadCommunication. | BadCommunication | Good não comprova calibração do sensor nem atualização recente sozinho. |
| Tipo e unidade | Tipo do dado e unidade configurada. | Double · bar | Não converte a física do sensor pelo texto da unidade. |
| Origem / Data Source | Fonte do ponto. Compare com o equipamento esperado. | Fonte configurada | Não substituir pelo nome da tela. |
| Timestamp SCADIVO | Horário associado à amostra apresentada pelo Runtime. | Horário da amostra | Compare com os demais horários; não é hora de abrir a página. |
| Timestamp da origem | Horário da origem quando ela o fornece. | Horário informado pelo dispositivo | Pode estar ausente; relógios/driver precisam de contexto. |
| Timestamp do servidor | Horário do servidor quando disponível na amostra. | Horário de servidor | Não significa que o sensor mediu nesse instante. |
| Atualização do valor | Atualizada, envelhecendo, desatualizada ou sem timestamp, com Motivo. | Dados desatualizados | Não confundir sucesso de requisição com uma amostra nova. |
| Última solicitação / resposta válida | Ajuda a separar falha de consulta de ausência de avanço da amostra. | Resposta recebida agora | Pode retornar valor já existente; confira timestamp do valor. |
| Path, ID estável e descrição | Identificam o recurso para comparação/diagnóstico. | Path legível da TAG | Não há ID a preencher aqui; não edite identidade para resolver falha. |
| Histórico recente | Consulta limitada do ponto selecionado; janela padrão de 15 minutos e até 120 amostras solicitadas. | Valores do intervalo recente | Não é todo o histórico nem configura retenção/captura; não é controle de janela editável neste painel. |
| Atualizar histórico | Reconsulta o ponto e seu histórico recente. | Nova consulta do intervalo | Não cria amostras ausentes e não força gravação. |

### Exemplo: Working novo e Runtime antigo

Você edita a revisão 12, mas Identidade ativa mostra revisão 11. O valor observado pertence ao Runtime ativo, não à nova definição do Working. Confira o que mudou, valide e use Salvar → Publicar → Ativar pelo lifecycle quando estiver pronto; depois confira identidade e amostra novamente. Não force o monitor a mostrar a revisão em edição. Em simulação, identifique a origem de teste no topo antes de usar números como evidência de campo.

### Falhas e vazios

Lista vazia difere de nenhum resultado por filtro. Permissão negada exige conferir conta e recurso; não é prova de TAG inexistente. TAG removida da revisão ativa pode deixar detalhes indisponíveis. Realtime conectado informa transporte, não saúde do sensor; atualização periódica pode continuar quando realtime falha. Histórico vazio pode decorrer de captura não habilitada, ausência no intervalo, permissão ou serviço indisponível. Confira a mensagem e o Historian antes de alterar dados. Uma resposta antiga visível após falha não deve ser considerada nova amostra.

[Abrir este guia na Ajuda instalada](/help?topic=engineering.tag-monitor)

## Informações: identificar produto e projeto

Saiba qual versão está instalada e diferencie versão do produto, esquema e revisão da aplicação.

### Quando usar

No Engineering, abra Informações para identificar o produto e o contexto de engenharia em atendimento, comparação de versões ou análise de pacote. É uma página de consulta. Não ativa revisão, atualiza software, muda schema ou revela consumo de licença; esses assuntos têm fluxos próprios.

### Consultar e interpretar

1. Confira Versão do produto. Se estiver indisponível, registre essa condição; não deduza versão pelo nome do projeto.
2. Abra Detalhes técnicos quando precisar comparar pacote/contexto ou informar suporte.
3. Confira Projeto, Revisão base e Snapshot carregado.
4. Consulte o Active em TAG Monitor/Runtime quando a dúvida for o que está executando.
5. Ao reportar problema, leve versão, projeto/revisão, horário e mensagem; evite publicar detalhes privados da aplicação.

### O que cada informação significa

Os dados abaixo são apresentados pelo sistema; não são campos que o usuário precisa preencher.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Versão do produto | Versão do SCADIVO instalado, distinta da revisão do projeto. | Versão exibida pelo produto | Não identifica sozinha a aplicação e a revisão ativas. |
| Schema de Engineering | Identifica formato e versão da estrutura do pacote de engenharia. | Identificação do formato | Não é versão do PostgreSQL nem número da revisão do projeto. |
| Revisão base | Revisão na qual o Working carregado se baseia; pode indicar não salvo. | Revisão base exibida | Edições posteriores podem não estar salvas/publicadas/ativas. |
| Snapshot carregado | Data/hora do retrato de engenharia carregado. | Horário exibido do snapshot | Não é timestamp de sensor, backup completo ou prova de ativação. |
| Projeto | Nome/chave que identifica o contexto de engenharia aberto. | Nome do projeto | Confirme Active em seu painel próprio; nome legível não é chave de licença. |
| Commit do build | Identificação técnica da compilação, quando fornecida. | Identificação exibida | Pode não aparecer; não invente um identificador nem o confunda com revisão da aplicação. |

### Exemplo de comparação

Duas estações podem ter a mesma versão do produto e projetos/revisões diferentes. Também podem ter o mesmo projeto com versões de produto diferentes. Compare cada informação no seu papel e confirme o Active antes de atribuir uma diferença à tela, ao driver ou à licença.

### Se faltar informação

Versão indisponível ou commit ausente não deve ser preenchido manualmente pelo operador. Registre a condição e confira a instalação com o responsável. Se a dúvida é consumo/capacidade, abra Licenciamento; se é valor/qualidade, use TAG Monitor; se é comunicação, use Diagnóstico. Não conclua defeito de processo a partir de um campo de identificação.

[Abrir este guia na Ajuda instalada](/help?topic=engineering.information)
