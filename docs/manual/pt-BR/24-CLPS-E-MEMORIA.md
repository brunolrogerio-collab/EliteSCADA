# CLPs: conexão, símbolos e memória

## Siemens S7: CPU, memória e primeira leitura

Configure ISO-on-TCP sem confundir conexão, acesso ao DB e permissão de escrita.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-S7: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.siemens-setup; fields=communicationBinding,valueOrder; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Prepare o lado do CLP

Escolha uma variável conhecida no programa do CLP e obtenha seu endereço de memória. Este driver S7 ISO-on-TCP lê por endereço absoluto: você precisa saber onde o valor está, não apenas seu nome. Não é a navegação simbólica completa do TIA nem uma conexão OPC UA.

Em S7-1200/1500, confirme com o responsável o acesso externo permitido, PUT/GET quando exigido e um DB com layout absoluto compatível. DB é o bloco de dados; offsets indicam posições dentro dele. Otimizações que mudam essas posições podem tornar o mapa incompatível. O SCADIVO não altera essas opções nem substitui a proteção do CLP.

### Campos da fonte

Escolha RackSlot ou TSAP explícito conforme a instalação. Um teste de conexão negocia a sessão; o acesso a DB e cada ponto ainda precisa ser conferido.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço do CLP | IP ou nome acessível pelo servidor SCADIVO. Não é o IP do navegador nem o endereço do projeto. | 192.0.2.10 |  |
| Porta TCP | Porta do serviço habilitado no CLP ou adaptador. Padrão deste driver: 102. Confira firewall e configuração real antes de mudar. | 102 | 1–65535. |
| Família da CPU | Selecione S7300, S7400, S71200 ou S71500 de acordo com a CPU instalada. Unknown não define um perfil operacional. Isto não habilita automaticamente serviços ou permissões no CLP. | S71200 |  |
| Como localizar a conexão | RackSlot deriva o destino a partir de rack, slot e função. ExplicitTsap usa o TSAP de destino informado pela configuração da instalação. Escolha um modo e preencha os campos correspondentes. | RackSlot |  |
| Rack | Em RackSlot, número do rack onde está a CPU. Não é o número de um equipamento no projeto. | 0 | 0–7; obrigatório em RackSlot. |
| Slot da CPU | Em RackSlot, posição real da CPU. O exemplo 1 não serve para todas as famílias; confira o hardware configurado. | 1 | 0–31. |
| Função da conexão | Em RackSlot: OperatorPanel, ProgrammingDevice ou Basic, conforme a conexão permitida pelo CLP. Não é papel de usuário da Authority. | OperatorPanel |  |
| Habilitar escrita na fonte | Padrão false. Para escrever, também é necessário writable no vínculo, TAG não ReadOnly, destino gravável e permissão. Não habilita PUT/GET nem altera proteção da CPU. | false |  |
| TSAP de origem | Identificador do lado cliente na sessão ISO. Padrão 0x0100. Não é porta TCP; mantenha o valor da configuração de comunicação. | 0x0100 |  |
| TSAP de destino | Obrigatório em ExplicitTsap. Use o identificador configurado para a conexão do CLP; não copie o exemplo sem conferir. | 0x0201 |  |
| Tempo máximo de conexão | Milissegundos permitidos para estabelecer a conexão. | 5000 | 1–300000 ms. |
| Tempo máximo da resposta | Milissegundos permitidos para responder a uma requisição. Aumentar não corrige endereço, rota ou tipo errado. | 3000 | 1–300000 ms. |
| Espera para reconectar | Milissegundos antes de tentar reconexão após falha. Não é intervalo de medição; zero não corrige recusa de acesso. | 1000 | 0–300000 ms. |
| Tamanho de mensagem solicitado | Bytes de PDU solicitados na negociação S7. Padrão 480; o tamanho aceito depende do CLP. Não é quantidade de TAGs nem tamanho de banco de dados. | 480 | 240–960 bytes. |

### Campos do vínculo de TAG

O caminho do TAG no SCADIVO identifica a variável da aplicação; área, DB, byte e bit localizam a memória do CLP. Endereço portátil e campos do vínculo devem concordar.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Área da memória | Input: entradas; Output: saídas; Merker: memória M; DataBlock: DB. Use a área do endereço real do programa. A leitura de entradas não torna essa área gravável. | DataBlock |  |
| Número do DB | Em DataBlock, número não zero do bloco. Fora dessa área use zero. O número não é offset em bytes. | 1 | 1–65535 em DB; zero nas demais áreas. |
| Posição inicial em bytes | Posição base zero dentro da área/DB. DB1.DBD0 começa no byte 0; Float32 ocupa quatro bytes. Não use 32 por confundir bits com bytes. | 0 | 0–2097151; todo o valor deve caber. |
| Bit para Boolean | Somente Boolean usa bit 0–7 dentro do byte. Para outros tipos mantenha zero; não use o seletor genérico de bits para substituir esse campo. | 0 |  |
| Tipo físico no CLP | Escolha o armazenamento real: Boolean, Byte, SInt, UInt16, Int16, UInt32, Int32, Int64, Float32, Float64, String, WString, Date ou DateTime. Float32 corresponde a TAG Float; Float64 a Double; texto a String; datas a DateTime. Um nome de TAG não converte o conteúdo da memória. | Float32 |  |
| Capacidade do texto | Somente String/WString: capacidade declarada de 1–254 caracteres; não é tamanho da leitura numérica. Nos demais tipos use zero. Inclua espaço real para o cabeçalho do formato no DB. | 0 |  |
| Permitir escrita neste ponto | Comece com false para conferir a leitura. true só habilita um ponto que também seja gravável na memória/protocolo; ReadOnly do TAG, permissões da sessão e demais bloqueios continuam valendo. | false |  |
| Ordem dos bytes e palavras | O descritor ainda declara Normal, ByteSwap, WordSwap e ByteAndWordSwap, mas o vínculo atual não aceita esse campo nos ajustes nem order no endereço. A ordem é representada pela transformação física compartilhada de bytes/palavras. Essa divergência faz parte da correção de autoria pendente; não copie o campo do catálogo para o vínculo como solução manual. | Normal |  |
| Autoria do vínculo nesta versão | O catálogo atual informa ao assistente genérico uma identidade de vínculo que difere da exigida pelo Runtime S7. A criação por esse caminho é recusada; aguarda correção. Os demais campos e o exemplo descrevem o vínculo S7 correto, não um fluxo de autoria já corrigido. Não altere IDs internos para contornar a validação. O campo valueOrder também precisa ser projetado para a transformação física, em vez de ser salvo como ajuste extra. |  |  |

### Da fonte até uma leitura conferida

A criação manual pelo assistente atual encontra a limitação de vínculo descrita acima. O exemplo demonstra os parâmetros de um vínculo válido para revisão, não instrui a forçar uma identidade interna na tela. Para um projeto com vínculo S7 já válido, confira fonte, leitura conhecida e qualidade após publicar/ativar pelo fluxo do projeto. Não marque leitura ou escrita como testada apenas porque o teste de conexão passou.

### Exemplo: REAL no início do DB1

Exemplo de configuração, não de pacote completo. O programa do CLP deve realmente guardar um REAL em DB1.DBD0. O resultado esperado é a leitura Float desse valor, com escrita desabilitada; não um valor de teste criado pelo driver.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "102",
    "cpuFamily": "S71200",
    "connectionMode": "RackSlot",
    "rack": "0",
    "slot": "1",
    "connectionRole": "OperatorPanel",
    "writeEnabled": "false"
  },
  "address": "s7iso:v1;area=DataBlock;db=1;byte=0;bit=0;type=Float32;string=0;writable=false",
  "binding": {
    "area": "DataBlock",
    "dbNumber": "1",
    "byteOffset": "0",
    "bitOffset": "0",
    "valueType": "Float32",
    "stringLength": "0",
    "writable": "false"
  },
  "tagDataType": "Float"
}
```

### Importação TIA e diagnóstico

A importação anunciada aceita exportações compatíveis de PLC TAGs em XLSX, XML e SDF; não é importação completa de projeto TIA nem TIA Openness. Confira candidatos, tipos e endereços antes de aplicar. Sem conexão: confira IP, porta 102, modo, rack/slot/TSAP e acesso permitido. Conectado sem leitura: confira número de DB, layout absoluto, offset e tamanho. Valor incoerente: compare tipo, bytes e ordem com o programa. Escrita recusada: confira writeEnabled, writable, ReadOnly e permissão; não relaxe a proteção do CLP para testar.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.siemens-setup)

## Allen-Bradley Logix: rota, símbolos e leitura

Use acesso simbólico ControlLogix/CompactLogix e confira os limites antes de importar ou escrever.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-LOGIX: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.logix-setup; fields=communicationBinding,bitIndex; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Qual conexão está sendo configurada

Comece por um símbolo conhecido de ControlLogix ou CompactLogix. Símbolo é a referência nomeada usada pelo programa. Tenha o endereço IP, perfil e rota reais e confira se o símbolo permite acesso externo.

Este driver usa mensagens explícitas EtherNet/IP para esses símbolos. Ele não é um scanner de entradas/saídas cíclicas de todos os dispositivos EtherNet/IP nem um driver genérico para SLC, PLC-5 ou MicroLogix. Confira a família antes de preparar a fonte.

### Campos da fonte

Uma rota CIP é diferente da porta TCP. Identificar o controlador não comprova a leitura de um símbolo nem a escrita.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço do CLP | IP ou nome acessível pelo servidor SCADIVO. Não é o IP do navegador nem o endereço do projeto. | 192.0.2.10 |  |
| Porta TCP | Porta do serviço habilitado no CLP ou adaptador. Padrão deste driver: 44818. Confira firewall e configuração real antes de mudar. | 44818 | 1–65535. |
| Perfil do controlador | ControlLogix ou CompactLogix conforme o equipamento. O perfil não descobre automaticamente o slot ou configura o CLP. | CompactLogix |  |
| Rota CIP até a CPU | Sequência ordenada de saltos porta,link separados por /. 1,0 representa um salto específico, não porta TCP 1. Use a rota documentada; um destino direto pode usar rota vazia conforme o perfil. Não acrescente saltos por tentativa. | 1,0 |  |
| Intervalo de leitura | Intervalo solicitado em milissegundos entre ciclos de aquisição. 1000 significa aproximadamente um ciclo por segundo, sujeito ao tempo de rede e processamento. Não é garantia de atualização do programa do CLP. | 1000 | 50–3600000 ms. |
| Tempo máximo da resposta | Milissegundos permitidos para responder a uma requisição. Aumentar não corrige endereço, rota ou tipo errado. | 3000 | 1–60000 ms. |
| Espera mínima de reconexão | Primeira espera após falha de conexão, em milissegundos. Deve ser menor ou igual à espera máxima. | 500 | 1–60000 ms. |
| Espera máxima de reconexão | Teto da espera progressiva, em milissegundos. Não é validade do dado nem tempo de confirmação de comando. | 15000 | 1–300000 ms. |
| Pontos por lote | Limita o agrupamento de requisições. Padrão 16; aumentar pode elevar carga e tamanho de mensagens. Não altera quantos TAGs estão licenciados. | 16 | 1–64. |
| Exigência de segurança do transporte | Unsecured é a implementação disponível. CipSecurityRequired é uma exigência explícita que esta versão recusa; não ativa CIP Security nem aceita downgrade silencioso. Se a instalação exigir CIP Security, este caminho não atende ao requisito. | Unsecured |  |

### Campos do vínculo simbólico

A identidade inclui escopo, programa quando aplicável, caminho e tipo nativo. Não copie um endereço portátil e deixe campos apontando a outro símbolo. Bool é legível; escrita nativa de Bool não está habilitada nesta versão.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Localização do símbolo | Controller para símbolo global; Program para símbolo de um programa, com programName. Isto não é escopo de autorização da Authority. | Controller |  |
| Programa do CLP | Preencha para escopo Program com o nome real do programa. Não é o nome da aplicação SCADIVO nem da rotina. |  |  |
| Caminho do símbolo | Nome real e caminho suportado, incluindo membro/índice quando aplicável. O exemplo TankLevel deve existir no CLP como REAL. Renomear o TAG SCADIVO não renomeia o símbolo. | TankLevel |  |
| Tipo nativo do símbolo | Leitura nesta versão: Bool, Sint, Int, Dint, Lint e Real. Mapeiam para Boolean, Int16, Int16, Int32, Int64 e Float. Lreal/String podem aparecer no catálogo/importação, mas não estão habilitados para leitura Runtime. Estruturas inteiras não são um escalar. | Real |  |
| Acesso externo definido no CLP | ReadOnly permite leitura; ReadWrite também permite escrita compatível; None bloqueia acesso. Unknown não comprova permissão para escrever. Este campo descreve o símbolo; não muda a opção no CLP. | ReadOnly |  |
| Símbolo constante | true identifica uma constante do CLP e impede escrita. Não use false para tentar tornar uma constante gravável. | false |  |
| Bit físico de um inteiro | Opcional para projetar um bit de Sint/Int/Dint/Lint como Boolean: limites 7/15/31/63. Não se aplica a Real ou Bool. Não transforma a escrita em operação bit a bit segura; confira o suporte antes de comandar. O vínculo aceito usa o seletor físico compartilhado, não um ajuste extra chamado bitIndex; a projeção pelo assistente também está pendente. |  |  |
| Autoria do vínculo nesta versão | O Runtime guarda identidade simbólica no endereço portátil e exige ajustes extras vazios. O assistente genérico atual copia os campos desta tabela para ajustes extras e o vínculo gerado é recusado. A criação por esse caminho aguarda correção; o exemplo mostra o contrato de um ponto válido, não um assistente corrigido. |  |  |

### Da fonte até uma leitura conferida

A criação manual pelo assistente atual encontra a limitação de vínculo descrita acima. O exemplo mostra uma identidade de leitura válida para revisão. Em projeto com vínculo Logix já válido, confira fonte, símbolo, tipo e qualidade após publicar/ativar. Os serviços de Browse/importação/reconciliação anunciados precisam ter seus candidatos e vínculos conferidos antes de aplicar; não declare a importação bem-sucedida só porque o arquivo abriu.

### Exemplo: símbolo REAL de leitura

TankLevel deve existir no escopo Controller como REAL. O TAG usa Float e ReadOnly para a primeira verificação. O exemplo não configura uma rota de rack por você.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "44818",
    "profile": "CompactLogix",
    "securityMode": "Unsecured",
    "scanIntervalMs": "1000"
  },
  "address": "logix:v1;scope=controller;symbol=TankLevel;native=real;access=readonly;constant=false",
  "binding": {},
  "tagDataType": "Float"
}
```

### Browse, importação e reconciliação

O Browse online enumera símbolos no nível Controller, com paginação; não promete navegar programas completos. A importação compatível L5X/L5K pode trazer escopos de programa. Revise candidatos suportados, arrays, membros e External Access; um símbolo visível pode ser incompatível. Reconcile compara identidades, não altera automaticamente o programa do CLP. Para falha de conexão, confira IP/44818 e rota. Para símbolo ausente, confira escopo, programa e nome. Para escrita recusada, confira tipo suportado, constante, External Access, ReadOnly e permissão.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.logix-setup)

## Mitsubishi MELSEC: perfil, rota e dispositivos

Configure MC/SLMP e confira base numérica, largura do valor e limites da CPU.

### Preparação do serviço MC

Peça ao responsável pelo CLP o perfil, a porta e uma posição de memória conhecida para testar. O serviço MC/SLMP TCP compatível precisa estar habilitado no equipamento.

Este perfil usa quadros binários 3E, o formato de mensagem previsto para a conexão. Não presuma suporte a todo modelo Mitsubishi, mensagens ASCII, UDP ou toda a memória do CLP. Compare o perfil com o equipamento real antes de preencher os endereços.

### Campos da fonte

Comece pelos campos básicos. Roteamento e agrupamento avançados devem corresponder à instalação; os valores padrão não são uma descoberta automática da topologia.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço do CLP | IP ou nome acessível pelo servidor SCADIVO. Não é o IP do navegador nem o endereço do projeto. | 192.0.2.10 |  |
| Porta TCP | Porta do serviço habilitado no CLP ou adaptador. Padrão deste driver: 5007. Confira firewall e configuração real antes de mudar. | 5007 | 1–65535. |
| Perfil de família Mitsubishi | Fx5U32MtDs ou IqR04EnCpu: limites de memória específicos, não certificação de todas as CPUs MELSEC. Se o modelo não corresponder, confira a compatibilidade antes de usar. | Fx5U32MtDs |  |
| Intervalo de leitura | Intervalo solicitado em milissegundos entre ciclos de aquisição. 1000 significa aproximadamente um ciclo por segundo, sujeito ao tempo de rede e processamento. Não é garantia de atualização do programa do CLP. | 1000 | 10–600000 ms. |
| Tempo máximo de conexão | Milissegundos permitidos para estabelecer a conexão. | 5000 | 100–60000 ms. |
| Tempo máximo da resposta | Milissegundos permitidos para responder a uma requisição. Aumentar não corrige endereço, rota ou tipo errado. | 6000 | 250–60000 ms. |
| Temporizador de supervisão MC | Tempo pedido no quadro MC, em unidades de 250 ms. Padrão 20 equivale a 5 s. É diferente do timeout local de resposta, que deve permitir esse tempo e a rede. | 20 | 1–65535 unidades. |
| Número da rede MC | Roteamento MC da instalação; padrão 0 para o destino configurado diretamente. Não é número de interface Ethernet do computador. | 0 | 0–239. |
| Estação de destino MC | Campo de estação do quadro. Padrão 255 (FF). Use o valor da rota MC, não o endereço Unit ID de Modbus. | 255 | 1–255. |
| Número de I/O do módulo | Campo de roteamento em hexadecimal, padrão 03FF. Não é endereço de uma saída Y; preserve o valor da instalação. | 03FF |  |
| Estação multidrop | Complemento da rota MC; padrão 0. Preencha conforme o caminho real, sem confundir com IP ou estação do quadro. | 0 | 0–31. |
| Palavras por requisição | Limite de agrupamento de palavras, padrão 64. Um valor de 32 bits ocupa duas palavras; não confunda quantidade de palavras com número de TAGs. | 64 | 2–960. |
| Bits por requisição | Limite para leitura de dispositivos de bit, padrão 64. Ajuste conforme a capacidade do CLP e a carga medida. | 64 | 1–7168. |
| Pontos dispersos por requisição | Limita agrupamento de endereços não contíguos, padrão 16. Não cria endereços inexistentes nem altera o mapa. | 16 | 1–192. |
| Blocos por requisição | Limita blocos agrupados, padrão 8. Mais blocos não garantem leitura mais rápida em redes ou CPUs saturadas. | 8 | 1–120. |
| Habilitar limite explícito de R | Opcional. Sem valor, área R fica desabilitada. Informe o máximo validado para a memória instalada; o parser atual ainda limita R a 32767. O máximo amplo do campo não amplia esse limite do perfil. |  |  |

### Memória e campos do vínculo

Dispositivos de bit e de palavra têm armazenamento diferente. Confira o formato usado por este driver mesmo que outro software escreva o endereço de outra forma.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço de dispositivo | X/Y/B/W usam números hexadecimais; M/L/D/R usam decimais. Exemplo D100 é palavra decimal; X10 é hexadecimal 10, não a convenção octal de outro software. O perfil limita o intervalo permitido. | D100 |  |
| Tipo físico | Bit para área de bits; Int16, UInt16, Int32, UInt32 ou Float32 para palavras. Int32/UInt32/Float32 ocupam duas palavras consecutivas. Selecione TAG compatível e preserve a ordem usada pelo CLP; UInt32 usa TAG Int64. No SCADIVO, Int16/UInt16 físicos usam TAG Int32; Int32/UInt32 físicos usam Int64; Float32 usa Float; Bit usa Boolean. | Int16 |  |
| Permitir escrita neste ponto | Comece com false para conferir a leitura. true só habilita um ponto que também seja gravável na memória/protocolo; ReadOnly do TAG, permissões da sessão e demais bloqueios continuam valendo. | false |  |

### Da fonte até uma leitura conferida

1. Em Engenharia > Fontes de dados, crie a fonte correspondente e preencha os parâmetros da instalação.
2. Salve e use Testar conexão quando disponível. Uma conexão bem-sucedida não comprova todos os pontos.
3. Em TAGs, selecione essa fonte, escolha tipo compatível e preencha endereço e vínculo pelo assistente. Na autoria manual, endereço e campos devem representar o mesmo ponto; os exemplos não são um pacote completo de importação.
4. Confira uma leitura conhecida pelo teste de ponto, quando disponível, ou após publicar/ativar pelo fluxo do projeto. Verifique valor, qualidade e atualização no Monitor de TAGs.
5. Somente depois configure tela, alarme ou escrita autorizada. Compare o resultado com o programa e a condição real do equipamento.

### Exemplo: inteiro em D100

D100 deve conter um inteiro com sinal de 16 bits no CLP. Um valor 123 deve aparecer como 123 no TAG Int32; não aplique conversão de unidade antes de conferir o dado bruto.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "5007",
    "familyProfile": "Fx5U32MtDs",
    "scanIntervalMilliseconds": "1000"
  },
  "address": "D100",
  "binding": {
    "address": "D100",
    "physicalDataType": "Int16",
    "writable": "false"
  },
  "tagDataType": "Int32"
}
```

### Diagnóstico dirigido

Sem conexão: confira serviço TCP, porta e rota MC. Endereço inválido: confira área, decimal/hexadecimal e limite da família; R exige limite explícito. Valor deslocado: confira tipo e palavras ocupadas. Requisições lentas: compare tempo MC de 250 ms por unidade, timeout local, lotes e carga; não aumente todos juntos. O teste de ponto não é Browse completo nem certifica escrita ou atuação física.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.melsec-setup)

## Panasonic MEWTOCOL: TCP ou serial e mapa de memória

Escolha o transporte real e interprete palavras e contatos sem misturar outros protocolos.

### Escolha o perfil e o transporte

Escolha a conexão conforme a instalação: MEWTOCOL-COM TCP usa um serviço ou adaptador TCP compatível; Host Serial usa a porta física disponível no servidor SCADIVO. São fontes distintas, com configurações próprias.

Para serial, confira cabo, padrão elétrico RS-232/RS-485 e parâmetros da linha. Tenha o mapa de memória do equipamento para escolher uma leitura de teste. Um conversor Ethernet genérico não se torna, por esta configuração, servidor de todos os protocolos Panasonic.

### Campos da fonte e do vínculo

No perfil TCP preencha host/porta/timeout de conexão; no serial preencha porta e formato da linha. Estação, família, modo de quadro e leitura se aplicam a ambos. Os três últimos campos descrevem o ponto.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endereço do CLP | IP ou nome acessível pelo servidor SCADIVO. Não é o IP do navegador nem o endereço do projeto. | 192.0.2.10 |  |
| Porta TCP do adaptador | Obrigatória no perfil TCP, sem porta padrão presumida. Use a porta configurada para MEWTOCOL-COM no CLP/adaptador; não copie a porta MC de Mitsubishi. | 9094 | 1–65535; exemplo, não padrão. |
| Tempo máximo de conexão | Milissegundos permitidos para estabelecer a conexão. | 5000 | 100–60000 ms. |
| Porta serial no servidor | Somente Host Serial: dispositivo acessível ao serviço SCADIVO, por exemplo COM4 no Windows ou /dev/ttyUSB0 no Linux. No Docker o dispositivo e as permissões pertencem ao contêiner/host; não ao navegador. | /dev/ttyUSB0 |  |
| Velocidade serial | Somente serial: bits por segundo iguais aos do CLP. Padrão 9600; não é intervalo de leitura. | 9600 | 300–230400. |
| Bits de dados | Somente serial: 7 ou 8, conforme a instalação. Padrão 8. Junto de paridade e stop bits define o enquadramento elétrico. | 8 |  |
| Paridade | Somente serial: None, Odd ou Even, exatamente como no CLP. Padrão None. Paridade incorreta pode causar timeout sem resposta compreensível. | None |  |
| Bits de parada | Somente serial: One ou Two conforme o CLP. Padrão One; não use nomes de outro driver que não estejam nesta lista. | One |  |
| Espera de troca de direção | Somente serial: pausa em milissegundos para a linha/adaptador após transação. Padrão 0; ajuste pela necessidade real, não para corrigir estação incorreta. | 0 | 0–1000 ms. |
| Estação MEWTOCOL | Número configurado no CLP, obrigatório de 1 a 99. Não é slot de rack, IP nem Unit ID herdado de outro protocolo. | 1 |  |
| Perfil da família Panasonic | Fp0rF32, FpXhCommon ou Fp7RClassicCom. Cada um limita áreas, endereços e escrita. FP7 aqui é o subconjunto classic COM, não toda a memória/funcionalidade do FP7. | Fp0rF32 |  |
| Formato de quadro | Standard é o padrão. Expanded amplia capacidade apenas nos perfis que permitem; Fp0rF32 o recusa. Não habilita MEWTOCOL7 nem estende áreas de memória. | Standard |  |
| Intervalo de leitura | Intervalo solicitado em milissegundos entre ciclos de aquisição. 1000 significa aproximadamente um ciclo por segundo, sujeito ao tempo de rede e processamento. Não é garantia de atualização do programa do CLP. | 1000 | 10–600000 ms. |
| Tempo máximo da resposta | Milissegundos permitidos para responder a uma requisição. Aumentar não corrige endereço, rota ou tipo errado. | 3000 | 250–60000 ms. |
| Endereço Panasonic | Use área e número admitidos pelo perfil: contatos X/Y/R/L/T/C ou palavras WX/WY/WR/WL/DT/LD. DT100 é palavra decimal; em contatos X/Y/R/L o último dígito representa bit hexadecimal 0–F do grupo decimal. Não troque a base inteira por hexadecimal. | DT100 |  |
| Tipo físico | Boolean para contato; UInt16 ou Int16 para palavra, com TAG do tipo correspondente. Este driver não oferece REAL de 32 bits ou string como se fossem uma palavra de 16 bits. Neste driver, tanto Int16 quanto UInt16 físicos usam TAG Int32. Boolean usa TAG Boolean. | Int16 |  |
| Permitir escrita neste ponto | Comece com false para conferir a leitura. true só habilita um ponto que também seja gravável na memória/protocolo; ReadOnly do TAG, permissões da sessão e demais bloqueios continuam valendo. | false |  |

### Da fonte até uma leitura conferida

1. Em Engenharia > Fontes de dados, crie a fonte correspondente e preencha os parâmetros da instalação.
2. Salve e use Testar conexão quando disponível. Uma conexão bem-sucedida não comprova todos os pontos.
3. Em TAGs, selecione essa fonte, escolha tipo compatível e preencha endereço e vínculo pelo assistente. Na autoria manual, endereço e campos devem representar o mesmo ponto; os exemplos não são um pacote completo de importação.
4. Confira uma leitura conhecida pelo teste de ponto, quando disponível, ou após publicar/ativar pelo fluxo do projeto. Verifique valor, qualidade e atualização no Monitor de TAGs.
5. Somente depois configure tela, alarme ou escrita autorizada. Compare o resultado com o programa e a condição real do equipamento.

### Exemplo: palavra DT100 por TCP

A porta 9094 é apenas exemplo de um adaptador configurado, não um padrão universal Panasonic. DT100 deve conter um inteiro de 16 bits com sinal; o TAG Int32 deve mostrar o mesmo número. Para Host Serial substitua os campos de rede pelos campos seriais da instalação.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "9094",
    "station": "1",
    "familyProfile": "Fp0rF32",
    "frameMode": "Standard",
    "scanIntervalMilliseconds": "1000"
  },
  "address": "DT100",
  "binding": {
    "address": "DT100",
    "physicalDataType": "Int16",
    "writable": "false"
  },
  "tagDataType": "Int32"
}
```

### Diagnóstico sem mudar o mapa por tentativa

Sem resposta TCP: confira porta do serviço e estação. Sem resposta serial: confira acesso ao dispositivo no servidor, ligação elétrica, baud/paridade/stop bits e estação. Erro de endereço: compare área e limite do perfil, incluindo dígito de contato. Valor incorreto: confira Int16 versus UInt16 e palavra versus contato. Escrita recusada em X/T/C ou outra área protegida não se resolve mudando apenas ReadOnly. Conexão e teste de ponto não comprovam toda a memória nem atuação física.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.panasonic-setup)
