# Gateway e arquivos CSV/Excel

## Gateway de TAGs: configurar e testar uma rota

Transfira valores entre TAGs do servidor, com qualidade, conversão e diagnóstico explícitos.

### Quando usar

Abra Engenharia > Gateway. Uma rota lê um TAG de origem e solicita escrita em um TAG de destino pelo Runtime. Permite ligar fontes de protocolos diferentes já configuradas, mas não cria Data Sources, endereços nem conexão por si só. É transferência em uma direção, não sincronização bidirecional automática. Não monte um circuito de rotas de volta sem analisar o efeito. Para aprender, use dois TAGs Server Memory sem conexão física.

### Identidade e pontas da rota

Os seletores oferecem TAGs com fonte habilitada e propriedade do servidor. Client Memory pertence ao navegador e não é ponta de rota. Simulation pode servir de origem, mas não de destino. O destino precisa permitir escrita; origem e destino devem ser diferentes. IDs mantêm identidade interna, caminhos ajudam a conferir o significado.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Identificador da rota | Chave para reconhecer a rota no projeto e nos diagnósticos. O formulário atual mantém o nome da rota junto desse identificador; no arquivo tabular Key e Name aparecem separados. | copy-speed | Obrigatório, sem espaços. |
| Habilitada | Define se a rota participa da revisão ativada. Não equivale a corrigir uma falha de destino. | Sim |  |
| TAG de origem | Selecione o valor que deve alimentar o destino. Confira tipo, qualidade e escala no Monitor de TAGs. | Server.Source |  |
| TAG de destino | Selecione o TAG gravável, já configurado para seu equipamento ou Server Memory. A rota não muda automaticamente sua permissão de escrita. | Server.Destination |  |

### Disparo, partida e conversão

GoodOnly é a política de qualidade atual: valor ruim não é transferido como leitura válida. Os campos condicionais mudam conforme o modo e a conversão escolhidos. Períodos são milissegundos; não alteram o ciclo de aquisição do Driver.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| OnChange / Periodic | OnChange reage às atualizações aceitáveis e considera deadband/intervalo mínimo. Periodic verifica o valor corrente no período definido; não fabrica qualidade Good quando a fonte está ruim. | OnChange |  |
| Transferência inicial | Sincronizar usa o primeiro valor aceitável disponível na partida. Aguardar próximo valor evita essa sincronização inicial e aguarda a condição de transferência seguinte. Avalie o efeito antes de usar destino físico. | WaitForNextAcceptableValue |  |
| Deadband (OnChange) | Diferença absoluta na unidade da origem, comparada ao último valor de origem transferido com sucesso. Diferença menor é ignorada; igualdade com o deadband permite prosseguir. Não é porcentagem nem deadband de alarme. | 0.1 | Número finito ≥ 0; apenas OnChange. |
| Intervalo mínimo (OnChange, ms) | Limita frequência de tentativas no modo de mudança. Atualizações rápidas podem ser reunidas e o valor atual é relido antes de escrever; não é fila garantida de cada amostra. | 1000 ms = 1 s | Se configurado: 100 a 86400000 ms. |
| Período (Periodic, ms) | Intervalo de transferência periódica. Não combine com deadband ou intervalo mínimo de OnChange no arquivo. | 1000 | Obrigatório em Periodic: 100 a 86400000 ms. |
| Exact / CheckedNumeric | Exact exige compatibilidade direta sem ganho/offset. CheckedNumeric explicita transformação numérica e valida o tipo destino. Estouro de faixa ou fração que seria perdida em inteiro causa falha; não há truncamento silencioso. | CheckedNumeric |  |
| Gain (ganho) | Multiplicador da fórmula destino = origem × gain + offset. Ausente usa 1. Não é o ajuste de escala do Driver. | 2 |  |
| Offset | Parcela somada após o ganho; ausente usa 0. Confirme unidade/faixa do destino antes de publicar. | 5 |  |

### Procedimento: 11 vira 27 sem equipamento real

1. Crie dois TAGs graváveis Server Memory: Server.Source Int16 e Server.Destination Int32, sem destino físico.
2. Crie copy-speed com essas pontas, OnChange, Aguardar próximo valor, CheckedNumeric, gain 2 e offset 5. Deixe deadband e intervalo mínimo vazios neste primeiro teste.
3. Execute Preview da configuração; corrija erros e aplique ao Workspace. Salve, publique e ative.
4. No ambiente de teste, atualize a origem para 11 com qualidade Good. Confira 27 no destino: 11 × 2 + 5.
5. Confira diagnóstico de transferência e último erro; uma solicitação aceita não prova que equipamento físico atuou.
6. Só depois avalie faixa, unidade, qualidade, partida e ritmo necessários para trocar as pontas por TAGs reais. Não use uma rota para contornar Authority, licença ou autoridade HA.

### CSV equivalente e diagnóstico

Este CSV usa caminhos legíveis, sem IDs. Os dois TAGs e sua fonte precisam existir antes do Preview/Apply. Em Import/Export, selecione Rotas Gateway e CSV; o arquivo não cria os TAGs. O diagnóstico de Runtime é da revisão ativa, não do rascunho. Aguardando origem pede conferir fonte/qualidade; Degraded e último erro pedem conferir conversão, faixa e destino. Contadores de ignoradas/coalescidas não significam automaticamente defeito: consulte modo e limites. Importar uma rota não executa uma transferência até o lifecycle apropriado.

```csv
Key,Name,SourceTagPath,DestinationTagPath,TransferMode,QualityPolicy,ConversionPolicy,InitialTransferPolicy,Gain,Offset,Enabled
copy-speed,Copy speed,Server.Source,Server.Destination,OnChange,GoodOnly,CheckedNumeric,WaitForNextAcceptableValue,2,5,true
```

[Abrir este guia na Ajuda instalada](/help?topic=gateway.configure-test)

## CSV e Excel: exportar, editar e importar sem perder referências

Prepare uma tabela por entidade, valide o candidato e confira o que será criado ou atualizado.

### Qual arquivo usar

Na área Import/Export/Gerenciamento do projeto, use a seção CSV/Excel para TAGs, Alarmes, Data Sources, Rotas Gateway ou Eventos Operacionais. Escolha uma entidade por arquivo. Para Telas, Popups, Templates, scripts e dependências compostas, use os mecanismos de projeto/biblioteca adequados; não transforme sua estrutura em colunas inventadas. CSV/XLSX de engenharia não é exportação dos valores históricos nem backup de Authority/banco. Comece exportando o modelo atual para preservar nomes de colunas e convenções.

### Controles do fluxo

O arquivo e a escolha de entidade/formato/modo identificam o candidato. Qualquer alteração deve passar novamente pelo Preview. O resumo informa criações, atualizações, ignorados e erros; ignorado não significa que a linha foi aplicada.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Entidade | Escolha o tipo de objetos contidos nas linhas. Uma planilha de rotas não pode ser lida como TAGs. | gateway-routes |  |
| CSV / XLSX | CSV usa vírgula entre campos e aspas para textos com vírgula/aspas/quebra. XLSX aceita uma única aba com o nome exportado para a entidade. Arquivo .xls antigo não vira XLSX por renomear. | GatewayRoutes no XLSX |  |
| Modo de mesclagem | CreateOnly cria os novos e ignora existentes; UpdateExisting atualiza existentes e ignora novos; CreateAndUpdate faz ambos. A correspondência segue a identidade da entidade, não a posição da linha. Não é substituir tudo nem excluir ausentes. | CreateAndUpdate |  |
| Arquivo e confirmação | Selecione o arquivo salvo, execute Preview e leia os diagnósticos antes de confirmar Apply. O conteúdo deve ser o mesmo validado; se o Workspace mudar, recarregue/revalide. | Exportar → editar → Preview → Apply |  |
| Chaves e caminhos de referência | Exportações atuais usam caminhos/chaves legíveis, ocultando IDs internos. Preserve o caminho de TAG ou a chave da entidade ao atualizar; uma chave nova pode criar outro objeto. Referências precisam existir no projeto/candidato adequado. | SourceTagPath=Server.Source |  |

### Regras para editar no Excel ou editor de texto

Mantenha os cabeçalhos técnicos exportados, mesmo que a interface esteja traduzida. Coluna desconhecida ou repetida pode impedir a importação. Use ponto decimal (2.5) e booleanos true/false; não acrescente unidade ao número. Campos JSON exigem JSON válido: não são Python nem lista separada por ponto e vírgula. Preserve caminhos/chaves como texto para evitar conversão automática em data/número. Fórmulas XLSX são recusadas, mesmo com resultado calculado: cole valores antes de salvar. A única aba deve ser Tags, Alarms, DataSources, GatewayRoutes ou OperationalEvents conforme a entidade. Não inclua usuários, password hashes, credenciais de login ou segredos; referências protegidas de Data Source não são os segredos em si. Campos complexos devem partir de exportação real, não de um exemplo simplificado.

### Exemplo: dois TAGs para o teste de Gateway

A fonte Server Memory com chave memory.server deve existir. Este CSV mínimo cria os TAGs do exemplo de Gateway; não cria a fonte. Em um projeto de teste, selecione TAGs/CSV e Preview, confira os dois itens e aplique pelo fluxo protegido. Depois importe a rota do guia Gateway. Uma tabela com poucas colunas é para criação guiada: para atualizar objetos completos, preserve a exportação completa, pois colunas omitidas podem assumir valores padrão e não significam necessariamente manter o valor anterior.

```csv
Path,Name,DataType,DataSourceKey,ReadOnly,InitialValueDataType,InitialValueJson
Server.Source,Source,int16,memory.server,false,int16,0
Server.Destination,Destination,int32,memory.server,false,int32,0
```

### Procedimento completo e recuperação de erro

1. Exporte a entidade atual e guarde uma cópia antes de editar.
2. Edite uma pequena seleção; preserve referências e tipos. Para expansão, use novos caminhos/chaves inequívocos e crie Data Sources antes de TAGs, depois alarmes/rotas/eventos dependentes.
3. Salve apenas valores no formato escolhido e selecione novamente o arquivo.
4. Escolha modo e Preview; confira contagens e linhas com erro. Preview não modifica Working.
5. Confirme Apply do candidato validado; só então confira os objetos no editor.
6. Salve, publique e ative para mudar o Runtime, conforme o ciclo do projeto.
7. Se houver erro de linha, corrija a origem do erro no arquivo, sem apagar linhas para esconder dependências; repita Preview. Se o resultado atualizado perdeu uma opção, compare colunas omitidas/defaults com o arquivo completo original. Exportação é uma cópia de configuração, não reversão automática de uma ativação.

[Abrir este guia na Ajuda instalada](/help?topic=engineering.tabular-workflow)
