# Telecontrole e Modbus Server

## DNP3: associação, eventos e comandos

Configure a outstation e diferencie identidade, consulta, representação e confirmação.

### Prepare a associação

O SCADIVO atua como mestre/cliente DNP3 TCP. Obtenha com o responsável pela outstation: IP/porta, endereços de enlace, lista de pontos, variações, classes e operações permitidas. Um ponto estático pode chegar por consulta e eventos podem chegar espontaneamente. Não há Browse ou Testar conexão de Engenharia anunciado neste descritor; avalie a associação e o ponto conhecido no Runtime/Diagnóstico após a ativação. Não interprete a falta desse botão como ausência do driver.

### Campos da fonte

Tempos DNP3 usam duração hh:mm:ss, não os milissegundos de outros drivers. Classes de evento não são prioridades de alarme. Preserve o perfil da associação remota ao ajustar opções avançadas.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Transporte DNP3 | Selecione tcp, o transporte implementado neste perfil. Porta serial não pode ser digitada como host para converter este driver em DNP3 serial. | tcp |  |
| Endereço da estação remota | IP ou nome alcançável pelo servidor SCADIVO. O navegador não é quem abre a conexão de protocolo. | 192.0.2.10 |  |
| Porta TCP | Porta do serviço remoto. Padrão 20000; confirme a configuração da estação e o caminho de rede. | 20000 | 1–65535. |
| Endereço DNP3 do mestre | Endereço de enlace local que a outstation deve reconhecer como mestre. Não é IP, porta TCP ou índice de TAG. | 1 | 0–65519; corresponda à associação remota. |
| Endereço DNP3 da outstation | Endereço de enlace da estação remota. Mesmo com IP/porta corretos, um endereço DNP3 incorreto impede a associação esperada. | 10 | 0–65519. |
| Tempo para conectar | Tempo máximo para estabelecer TCP; padrão 5 s. Não é intervalo de leitura. | 00:00:05 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Tempo para resposta DNP3 | Espera por uma resposta de protocolo, padrão 5 s. Aumentar não corrige endereço de enlace ou índice errado. | 00:00:05 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Espera mínima de reconexão | Primeira espera após desconexão, padrão 1 s. Deve ser menor ou igual ao máximo. | 00:00:01 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Espera máxima de reconexão | Teto da espera progressiva, padrão 30 s. Comandos antigos não são guardados para repetição na reconexão. | 00:00:30 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Supervisão de enlace | Padrão 1 minuto para supervisão de enlace. Conexão viva não comprova ponto atualizado. Zero é inválido; limpar um campo não deve ser interpretado automaticamente como desativação do recurso. | 00:01:00 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica de integridade | Padrão 15 minutos. Reconsulta dados para ressincronizar a imagem da estação; não é retenção do Historian nem frequência de todos os eventos. | 00:15:00 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica da classe 1 | Opcional, sem intervalo padrão: consulta eventos dessa classe como complemento ao tráfego espontâneo. A classe vem da configuração da outstation, não da prioridade de alarme no SCADIVO. | 00:01:00 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica da classe 2 | Opcional, sem intervalo padrão: consulta eventos dessa classe como complemento ao tráfego espontâneo. A classe vem da configuração da outstation, não da prioridade de alarme no SCADIVO. | 00:01:00 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica da classe 3 | Opcional, sem intervalo padrão: consulta eventos dessa classe como complemento ao tráfego espontâneo. A classe vem da configuração da outstation, não da prioridade de alarme no SCADIVO. | 00:01:00 | Duração positiva em hh:mm:ss; 00:00:05 = 5 s. |
| Integridade inicial — classe 0 | Inclui esta classe na ressincronização inicial; padrão true. Classe 0 representa dados estáticos; 1–3 representam classes de eventos. Pelo menos uma classe inicial deve permanecer habilitada. | true |  |
| Integridade inicial — classe 1 | Inclui esta classe na ressincronização inicial; padrão true. Classe 0 representa dados estáticos; 1–3 representam classes de eventos. Pelo menos uma classe inicial deve permanecer habilitada. | true |  |
| Integridade inicial — classe 2 | Inclui esta classe na ressincronização inicial; padrão true. Classe 0 representa dados estáticos; 1–3 representam classes de eventos. Pelo menos uma classe inicial deve permanecer habilitada. | true |  |
| Integridade inicial — classe 3 | Inclui esta classe na ressincronização inicial; padrão true. Classe 0 representa dados estáticos; 1–3 representam classes de eventos. Pelo menos uma classe inicial deve permanecer habilitada. | true |  |
| Suspender espontâneos da classe 1 ao iniciar | Padrão true: solicita suspender espontâneos durante a preparação da associação. Não exclui eventos da outstation nem altera sua classe. | true |  |
| Reativar espontâneos da classe 1 | Padrão true: solicita reativação após a integridade inicial. Exige suporte e configuração compatíveis na estação; marcar não faz um ponto gerar eventos. | true |  |
| Buscar eventos disponíveis da classe 1 | Padrão true: reage à indicação de eventos disponíveis consultando a classe. Não muda a frequência de amostragem do sensor nem substitui a fila de eventos remota. | true |  |
| Suspender espontâneos da classe 2 ao iniciar | Padrão true: solicita suspender espontâneos durante a preparação da associação. Não exclui eventos da outstation nem altera sua classe. | true |  |
| Reativar espontâneos da classe 2 | Padrão true: solicita reativação após a integridade inicial. Exige suporte e configuração compatíveis na estação; marcar não faz um ponto gerar eventos. | true |  |
| Buscar eventos disponíveis da classe 2 | Padrão true: reage à indicação de eventos disponíveis consultando a classe. Não muda a frequência de amostragem do sensor nem substitui a fila de eventos remota. | true |  |
| Suspender espontâneos da classe 3 ao iniciar | Padrão true: solicita suspender espontâneos durante a preparação da associação. Não exclui eventos da outstation nem altera sua classe. | true |  |
| Reativar espontâneos da classe 3 | Padrão true: solicita reativação após a integridade inicial. Exige suporte e configuração compatíveis na estação; marcar não faz um ponto gerar eventos. | true |  |
| Buscar eventos disponíveis da classe 3 | Padrão true: reage à indicação de eventos disponíveis consultando a classe. Não muda a frequência de amostragem do sensor nem substitui a fila de eventos remota. | true |  |
| Ressincronizar após estouro de eventos | Padrão true: solicita integridade quando a outstation informa estouro do buffer. Recupera a imagem atual disponível, não eventos já perdidos. | true |  |
| Sincronização do relógio remoto | disabled é o padrão. lan ou nonLan selecionam o método DNP3 compatível com a outstation. Isto pode ajustar o relógio remoto; combine com o responsável da estação. Não define fuso horário nem valida automaticamente sensores. | disabled |  |
| Limite de solicitações pendentes | Padrão 16: limita a fila de solicitações/comandos do usuário para a associação. Não é licença de usuários e não permite reaplicar comandos antigos depois de reconectar. | 16 | 1–1024. |

### Campos do ponto e do comando

Em TAGs, selecione a fonte DNP3 e use o assistente de família/índice. Confira o tipo do TAG e aplique o vínculo; os campos avançados descrevem o contrato do ponto e podem exigir a definição ou os arquivos de Engenharia do projeto, pois o assistente visível não oferece todos os perfis avançados. Endereço, família e índice precisam concordar.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço do ponto DNP3 | Formato dnp3:tipo:índice, por exemplo dnp3:analogInput:7. A fonte identifica a outstation; não repita IP ou endereço de enlace no ponto. | dnp3:analogInput:7 |  |
| Família do ponto | binaryInput/binaryOutputStatus usam Boolean; doubleBitBinaryInput usa Enum; analogInput/analogOutputStatus usam tipo numérico compatível; counter/frozenCounter usam Int32 ou Int64. A família e o índice identificam o ponto, não o número do grupo de mensagem. | analogInput |  |
| Índice na família | Índice publicado pela outstation, de 0 a 65535; lacunas são permitidas. Índice 7 de analogInput não é o mesmo ponto que índice 7 de binaryInput. | 7 |  |
| Representação estática | Opcional, formato GxVy. Deve corresponder à família e ao tipo do TAG: G30V5 é analógico Float; G30V6 é Double. Variação 0 pede padrão do dispositivo quando suportado; não é garantia de um tipo arbitrário. | G30V5 |  |
| Representação de evento | Opcional. Exemplo G32V7 é analógico Float com tempo. Representação estática e de evento devem concordar com a família/tipo. A variação não troca a identidade do ponto. | G32V7 |  |
| Classe de evento esperada | Opcional: class1, class2 ou class3 para conferência/diagnóstico. Não reclassifica o ponto remoto nem corresponde à prioridade do alarme SCADIVO. | class1 |  |
| Habilitar comando neste ponto | Padrão false. Somente binaryOutputStatus/analogOutputStatus admitem comando. Também depende de ReadOnly, permissão e vínculo de comando; um input não vira saída ao marcar true. | false |  |
| Seleção antes da operação | selectBeforeOperate é o padrão; directOperate só quando a estação e o procedimento permitirem. Não há opção Direct Operate No Response. Confirmação de protocolo não comprova atuação física. | selectBeforeOperate |  |
| Operação para verdadeiro | Comando binário: latchOn, latchOff, pulseOn ou pulseOff. Padrão latchOn. true é o valor pedido pelo usuário; a operação real depende deste mapeamento. | latchOn |  |
| Operação para falso | Mesmo conjunto de operações; padrão latchOff. Confira com a lógica remota antes de inverter significados, especialmente abertura/fechamento ou partida/parada. | latchOff |  |
| Indicação Trip/Close | none, trip ou close conforme o equipamento. Padrão none. Este qualificador não é intertravamento nem autorização adicional para manobra. | none |  |
| Contagem da operação | Padrão 1; quantidade transportada no comando binário. Não é número de novas tentativas do SCADIVO. Combine a interpretação com a outstation. | 1 | 1–255. |
| Tempo ligado do comando | Duração hh:mm:ss do comando binário, padrão 00:00:00. Para pulso use a duração prevista pelo equipamento; não confunda com timeout da resposta. | 00:00:00 |  |
| Tempo desligado do comando | Duração complementar de comando binário, padrão 00:00:00. Não representa atraso de alarme ou intervalo de aquisição. | 00:00:00 |  |
| Tipo do comando analógico | int16, int32, float32 ou float64; deve corresponder ao TAG Int16, Int32, Float ou Double. A representação de comando G41 é distinta da representação do estado recebido. | float32 |  |

### Exemplo de leitura analógica

Exemplo de parâmetros, não pacote completo. A outstation deve publicar ponto analógico 7 nas representações indicadas. Crie TAG Float de leitura; após publicar/ativar o projeto, compare valor, qualidade e origem do tempo com uma medição conhecida. Não invente um valor esperado apenas porque a associação conectou.

```json
{
  "source": {
    "transport": "tcp",
    "host": "192.0.2.10",
    "port": "20000",
    "masterAddress": "1",
    "outstationAddress": "10"
  },
  "address": "dnp3:analogInput:7",
  "binding": {
    "pointKind": "analogInput",
    "index": "7",
    "staticVariation": "G30V5",
    "eventVariation": "G32V7",
    "expectedEventClass": "class1",
    "writable": "false"
  },
  "tagDataType": "Float",
  "readOnly": true
}
```

### Exemplo de saída binária

Define o perfil de comando de uma saída real já acordada com a outstation. Não é autorização para testar manobra em equipamento ativo. O Runtime precisa admitir o comando e a sessão precisa da capacidade correspondente. Comando aceito no protocolo e atualização do estado são evidências distintas; confira o retorno publicado pelo ponto.

```json
{
  "source": {
    "transport": "tcp",
    "host": "192.0.2.10",
    "port": "20000",
    "masterAddress": "1",
    "outstationAddress": "10"
  },
  "address": "dnp3:binaryOutputStatus:1",
  "binding": {
    "pointKind": "binaryOutputStatus",
    "index": "1",
    "writable": "true",
    "commandMode": "selectBeforeOperate",
    "binaryTrueOperation": "latchOn",
    "binaryFalseOperation": "latchOff",
    "tripCloseCode": "none",
    "commandCount": "1",
    "commandOnTime": "00:00:00",
    "commandOffTime": "00:00:00"
  },
  "tagDataType": "Boolean",
  "readOnly": false
}
```

### Investigue por sintoma

Sem associação: confira IP/20000, mestre/outstation e rede a partir do servidor. Associado sem valor: confira família/índice e integridade inicial. Eventos ausentes: confira classe, suporte a espontâneos, consultas e configuração remota. Valor incompatível: compare variação e tipo do TAG, sem trocar classe para corrigir tipo. Um evento sem tempo de origem não ganha precisão de relógio por ser recebido agora. Comando recusado: confira saída, tipo, modo, permissões e estado da associação; não repita automaticamente após desconexão.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.dnp3-setup)

## IEC 104: estações, pontos observados e comandos

Configure CA/IOA e tipos sem tratar a observação como uma lista completa de pontos.

### O que preparar e o que Browse comprova

O SCADIVO é cliente/mestre IEC 60870-5-104. Tenha a lista de CA/IOA, tipos, relógio e comandos fornecida pela estação remota. Testar conexão comprova o alcance informado pelo resultado; Browse faz uma observação limitada com interrogação geral e tráfego recebido, não uma varredura garantida de todo o mapa. O resultado é parcial; ausência na lista não prova que um ponto inexista.

### Campos da fonte e do ponto

Os tempos desta fonte usam segundos. CA/IOA é a identidade do ponto; Type ID define interpretação e o perfil de comando é separado. Não acrescente Type ID ao endereço para contornar um conflito de configuração.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço da estação remota | IP ou nome alcançável pelo servidor SCADIVO. O navegador não é quem abre a conexão de protocolo. | 192.0.2.10 |  |
| Porta TCP | Porta do serviço remoto. Padrão 2404; confirme a configuração da estação e o caminho de rede. | 2404 | 1–65535. |
| Estações — Common Addresses | Lista decimal de endereços CA separados por vírgula, por exemplo 1,2. É o endereço lógico da estação, não IP ou IOA de um ponto. O CA do TAG deve pertencer a esta lista. | 1 | CA 0–65535 conforme o perfil remoto. |
| Fuso do relógio da estação | Identificador de fuso reconhecido pelo servidor para interpretar CP56Time2a. UTC só é correto se a estação usar UTC. Se usar horário local, configure seu fuso real; não use o fuso do navegador por conveniência. Confira o identificador na instalação Windows/Linux. | UTC |  |
| Originador da solicitação | Identificador de origem no protocolo; padrão 0. Não é usuário de login nem capacidade da Authority. Use o valor previsto no perfil da estação. | 0 | 0–255. |
| t0 — conexão | Tempo para estabelecer conexão, em segundos. Padrão 30. | 30 | 0.1–3600 s. |
| t1 — supervisão de transmissão | Espera supervisionada por confirmação, padrão 15 s. Deve ser maior que t2. | 15 | 0.1–3600 s. |
| t2 — confirmação de recepção | Limite para confirmação retardada, padrão 10 s; obrigatoriamente menor que t1. Não é atraso do alarme. | 10 | 0.1–3600 s. |
| t3 — supervisão ociosa | Supervisão da sessão ociosa, padrão 20 s. Não é prazo de validade de uma medição. | 20 | 0.1–86400 s. |
| k — janela de envio | Quantidade máxima de unidades de informação sem confirmação. Padrão 12; não é número de TAGs. Ajuste com o perfil remoto. | 12 | 1–32767. |
| w — janela de confirmação | Limiar de recepções para confirmar, padrão 8; deve ser menor ou igual a k. Não aumenta a quantidade de pontos disponíveis. | 8 | 1–32767; w ≤ k. |
| CA do ponto | Common Address que contém o ponto. Use um dos endereços configurados na fonte; dois pontos com mesmo IOA e CA distintos não são o mesmo ponto. | 1 | 0–65535. |
| IOA do ponto | Endereço do objeto de informação, conforme a lista da estação. O assistente gera ca=1;ioa=100 para o exemplo; não é número de linha da tabela. | 100 | 0–16777215. |
| Tipo da informação monitorada | MSpNa1/MSpTb1 usam Boolean; MDpNa1/MDpTb1 usam Enum; MBoNa1/MBoTb1 usam Int32; MMeNb1/MMeTe1 usam Int16; MMeNa1/MMeTd1 e MMeNc1/MMeTf1 usam Float. A família normalizada não é automaticamente uma grandeza em volts ou bar. Tipos com tempo e sem tempo continuam referindo-se ao mesmo CA/IOA compatível. | MMeNc1 |  |
| Tipo do comando | Somente para TAG gravável: CScNa1 Boolean, CDcNa1 Enum, CSeNa1/CSeNc1 Float ou CSeNb1 Int16. Deve ser compatível com o tipo monitorado e o perfil remoto; não transforma uma entrada em saída. | CSeNc1 |  |
| Modo de execução do comando | direct ou sbo, conforme a estação. O assistente inicia em sbo quando não há opção salva; o descritor de configuração declara direct como padrão. Escolha explicitamente o modo pretendido. sbo seleciona antes de operar; não substitui intertravamento de processo. | sbo |  |
| Qualificador de comando | Valor 0–31, padrão 0, conforme a operação remota. Não é prioridade de alarme ou papel de usuário. | 0 |  |

### Primeira leitura de ponto conhecido

1. Crie/salve a fonte e teste a conexão a partir do servidor.
2. Use Browse para conferir pontos realmente observados, ou a lista técnica remota para autoria manual. Revise os candidatos antes de aplicar.
3. Em TAGs, selecione a fonte e abra o assistente IEC 104; informe CA, IOA e tipo monitorado compatível. Para começar, mantenha o TAG somente leitura.
4. Publique/ative pelo fluxo do projeto e confira valor, qualidade e tempo no Monitor de TAGs. A estação precisa realmente fornecer o ponto do exemplo; os parâmetros não geram uma medição fictícia.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "2404",
    "commonAddresses": "1",
    "stationTimeZone": "UTC"
  },
  "address": "ca=1;ioa=100",
  "binding": {
    "iec104.typeId": "MMeNc1"
  },
  "tagDataType": "Float",
  "readOnly": true
}
```

### Perfil de comando compatível

Este exemplo muda o mesmo ponto para um setpoint Float por seleção antes de operação. Só use se a estação definir esse CA/IOA para o comando. Permissão, admissão do Runtime, confirmação de protocolo e retorno de processo são verificações distintas. Não habilite escrita só para eliminar uma mensagem de tipo incompatível.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "2404",
    "commonAddresses": "1",
    "stationTimeZone": "UTC"
  },
  "address": "ca=1;ioa=100",
  "binding": {
    "iec104.typeId": "MMeNc1",
    "iec104.commandTypeId": "CSeNc1",
    "iec104.commandMode": "sbo",
    "iec104.qualifier": "0"
  },
  "tagDataType": "Float",
  "readOnly": false
}
```

### Diagnóstico por camada

Sem sessão: confira IP/2404 e parâmetros APCI, incluindo t2 < t1 e w ≤ k. Sessão sem pontos: confira CA solicitado, conclusão da interrogação geral, janela de observação e lista remota. Valor incompatível: confira IOA e família do Type ID; normalizado, inteiro escalado e Float não são intercambiáveis. Tempo deslocado: confira relógio da estação, fuso configurado e validade do CP56Time2a, sem corrigir o dado alterando apenas o relógio do navegador. Comando recusado/ambíguo: confira tipo, modo, qualificador e estado da sessão; timeout não comprova que a estação deixou de operar.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.iec104-setup)

## Modbus Server: publicar TAGs por TCP ou RTU

Monte um mapa limitado e distinga cliente externo, escrita interna e valor atualizado.

### Servidor é diferente de mestre

No perfil Server o SCADIVO atende outro mestre/cliente e publica seus TAGs mapeados em Holding Registers. Ele não consulta um CLP remoto por essa fonte. Para publicar um valor de outro driver, crie o TAG de destino Server e uma rota Gateway compatível; um TAG não passa a ter duas fontes. TCP escuta uma interface/porta; RTU responde pelo dispositivo serial do servidor.

### Campos da fonte e do mapa

Os campos de rede pertencem ao TCP; os de linha, ao RTU. Faixas e vínculos definem o mesmo modelo de mapa. O editor de faixas não converte automaticamente rótulos 40001 de ferramentas externas.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Interface local do servidor TCP | Endereço local de escuta, não IP de um CLP remoto. Padrão 0.0.0.0 atende interfaces IPv4; 127.0.0.1 restringe ao próprio host. No Docker confira também portas publicadas e rede do contêiner. Dentro de um contêiner, 127.0.0.1 é o loopback do contêiner, não o do computador host. | 127.0.0.1 |  |
| Porta de escuta TCP | Padrão 502. O exemplo usa 1502 para uma instalação de teste; o cliente externo deve usar a mesma porta acessível. Outra fonte/processo não pode ocupar o mesmo endpoint. | 1502 | 1–65535. |
| Unit ID servido | Identificador aceito pelo servidor, padrão 1. TCP aceita 0–247; RTU usa 1–247. O Unit ID do vínculo deve coincidir com a fonte. Não é endereço IP. | 1 |  |
| Faixas de Holding Registers | Use o editor estruturado de início/fim. Intervalos inclusivos, base zero, sem sobreposição: v1:0-9 representa dez registradores. Todos os registradores de um valor devem caber na mesma faixa; ela não cria TAGs automaticamente. | v1:0-9 | Até 32 faixas e 16384 registradores no total; endereços 0–65535. |
| Clientes TCP simultâneos | Somente TCP: máximo de conexões, padrão 16. Não é quantidade de Views de licença ou autorização de usuário. | 16 | 1–128. |
| Tempo de cliente TCP ocioso | Somente TCP: espera máxima pela próxima requisição, padrão 30000 ms. Não é taxa de atualização de um TAG. | 30000 | 1000–600000 ms. |
| Porta RTU no servidor | Somente RTU: dispositivo acessível ao serviço no Windows/Linux/contêiner, como COM4 ou /dev/ttyUSB0. A porta fica exclusiva enquanto o servidor estiver ativo; não compartilhe com um mestre que controle a mesma porta física. | /dev/ttyUSB0 |  |
| Velocidade da linha RTU | Bits por segundo iguais aos do mestre e demais dispositivos. Padrão 9600; não é polling do servidor. | 9600 | 300–4000000. |
| Bits de dados RTU | Padrão 8; deve corresponder à linha real. O intervalo do campo não garante que toda combinação seja aceita pelo adaptador/host. | 8 | 5–8; confira a instalação. |
| Paridade RTU | None, Odd, Even, Mark ou Space; padrão None. Deve coincidir com o mestre e ser suportada pela porta. | None |  |
| Bits de parada RTU | One, OnePointFive ou Two; padrão One. Use a combinação válida para a linha e o host, não o número de registradores. | One |  |
| Tempo para completar quadro RTU | Padrão 1000 ms para receber o restante do quadro após início do tráfego. Não é intervalo de medição. | 1000 | 50–10000 ms. |
| Endereço do TAG no mapa | Neste servidor use holding:N, base zero. holding:0 costuma ser chamado 40001 por ferramentas que usam rótulo de referência; confirme a convenção do cliente. Coils/Input Registers não fazem parte deste perfil. | holding:0 |  |
| Unit ID no vínculo | Opcional: sem valor usa a fonte. Em Server, uma substituição precisa ser igual ao Unit ID servido; não cria várias estações no mesmo mapa. | 1 |  |
| Tipo nos registradores | Boolean/Int16/UInt16 usam um registrador; Int32/UInt32/Float32 usam dois; Int64/UInt64/Float64 usam quatro. Tipo físico, tipo do TAG e largura precisam ser compatíveis. O exemplo usa Int16 físico e TAG Int16 sem conversão. | Int16 |  |
| Ordem de palavras | HighWordFirst é padrão; LowWordFirst inverte palavras de valores com múltiplos registradores. Use a mesma ordem no cliente. Em Int16 de uma palavra não há duas palavras para inverter. | HighWordFirst |  |
| Multiplicador do valor | Padrão 1: valor de Engenharia = bruto × scale + offset. Deve ser finito e diferente de zero. Na publicação para registradores o adaptador aplica a conversão inversa; não aplique escala duplicada no cliente. | 1 |  |
| Deslocamento do valor | Parcela somada após multiplicar, padrão 0. Não é offset do endereço e não altera holding:0. | 0 |  |
| Escrita por cliente externo | ReadOnly é o padrão; ReadWrite habilita a escrita externa nesse TAG mapeado. É um controle separado de ReadOnly para escritas internas do SCADIVO. Modbus não pede login SCADIVO a cada requisição; planeje acesso de rede e mapa autorizado antes de abrir ReadWrite. | ReadOnly |  |

### Publicar e conferir uma leitura

1. Crie a fonte Server TCP ou RTU e configure a interface/linha, Unit ID e faixas.
2. Testar conexão TCP tenta reservar temporariamente a interface/porta local e a libera; RTU testa abertura/reserva exclusiva da linha e a libera. Nenhum desses testes comprova leitura por um cliente remoto nem deixa o servidor ativo.
3. Em TAGs, selecione a fonte Server e o assistente Modbus. Escolha Holding Register, endereço base zero e tipo; mantenha o acesso externo ReadOnly inicialmente. Permita escrita interna no TAG se ele receber dados por Gateway ou comandos internos autorizados.
4. Publique/ative. Alimente um valor conhecido no TAG e confira sua atualização no Monitor. No cliente externo leia o Unit ID e endereço correspondentes; confirme tipo e ordem.

```json
{
  "source": {
    "bindAddress": "127.0.0.1",
    "port": "1502",
    "unitId": "1",
    "holdingRanges": "v1:0-9"
  },
  "address": "holding:0",
  "metadata": {
    "modbus.valueType": "Int16",
    "modbus.wordOrder": "HighWordFirst",
    "modbus.scale": "1",
    "modbus.offset": "0",
    "modbus.server.clientAccess": "ReadOnly"
  },
  "tagDataType": "Int16",
  "readOnly": false,
  "internalValue": 123
}
```

### O que esperar do exemplo e de um reinício

Depois de uma escrita interna autorizada de 123, o cliente deve ler 123 em holding:0, Unit ID 1, porta 1502. O valor não aparece automaticamente por constar no exemplo. O mapa em memória nasce zerado: uma leitura zero não comprova que o TAG recebeu uma amostra atual, e os registradores não carregam automaticamente a qualidade/tempo do TAG. Após reinício confira a alimentação do mapa; ele não é o Historian nem garantia de memória retentiva.

### Escrita externa e diagnóstico

Faixa permitida não significa escrita livre: cada registrador escrito deve pertencer a TAG mapeado e ReadWrite externo. O servidor recusa escrever parte de um valor de múltiplos registradores, atravessar lacunas não mapeadas, sobrepor TAGs ou ultrapassar faixas. Leituras FC03 aceitam 1–125 registradores; FC06 escreve um registrador e FC16 escreve 1–123 registradores; uma operação deve respeitar cada valor completo. Sem escuta: confira porta ocupada, interface, firewall e Docker. Leitura errada: confira Unit ID, base zero, tipo/ordem e valor realmente alimentado. Escrita interna recusada: confira ReadOnly/permissão; escrita externa recusada: confira clientAccess e mapa completo.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.modbus-server-setup)
