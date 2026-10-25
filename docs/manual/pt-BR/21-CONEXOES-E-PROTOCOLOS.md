# Conexões, Modbus, MQTT e OPC UA

## Fonte de dados: da conexão ao TAG conferido

Configure uma conexão antes de ligar valores a telas, alarmes e histórico.

### Antes de preencher

Em Engenharia → Fontes de dados, tenha o mapa de endereços/nós/tópicos, parâmetros da rede ou serial e uma leitura conhecida. O servidor SCADIVO executa a comunicação; ter acesso pelo navegador não prova que o servidor consegue alcançar o equipamento. Use primeiro um TAG somente leitura.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Chave da fonte | Identifica a conexão nas referências dos TAGs. Use uma chave legível, diferente das outras fontes; não é o endereço IP. | plc.bomba01 |  |
| Nome | Nome apresentado na lista. Descreva qual equipamento ou serviço esta conexão atende. | Bomba 01 |  |
| Tipo de conexão / driver | Escolhe o protocolo e seus campos. Modbus TCP, Modbus RTU Master, MQTT Raw e OPC UA são conexões diferentes; trocar o tipo exige revisar os vínculos dos TAGs. | modbus.tcp |  |
| Habilitada | Inclui a fonte na configuração operacional quando a revisão é ativada. Habilitar não corrige rede, credenciais ou um TAG mal configurado. | true |  |
| Referências protegidas | Campos de senha/certificado recebem a referência de material provisionado no servidor. Peça ao administrador a referência correta; não cole a senha, chave privada ou conteúdo do certificado no campo comum de configuração. |  |  |

### Preparar, conferir e ativar

1. Crie ou selecione a fonte e escolha o driver correto.
2. Preencha os campos básicos e abra os avançados somente quando precisar alterar seus padrões.
3. Use Testar conexão quando o driver oferecer esse recurso; o teste do rascunho não aplica as alterações.
4. Verifique alterações e leia os diagnósticos; Aplicar ao Workspace atualiza a configuração de trabalho.
5. Configure um TAG com essa fonte, tipo e vínculo do protocolo; teste a leitura quando disponível.
6. Salve/publique e ative a revisão pelo fluxo do projeto para o Runtime usar essa configuração.
7. Confira valor, unidade, qualidade e atualização antes de criar comandos ou alarmes.

### Quando conecta, mas não há valor

Teste de conexão confirma somente sua etapa, não o mapa inteiro nem uma ação física. Verifique fonte habilitada, vínculo do TAG, revisão ativa e diagnósticos. Acesso negado difere de timeout; valor ruim difere de zero. ReadOnly e permissões de escrita continuam válidos mesmo se o protocolo aceitar escrita. Recursos de descoberta/teste dependem do driver; MQTT Raw não anuncia teste de conexão de Engenharia.

[Abrir este guia na Ajuda instalada](/help?topic=sources.connection-workflow)

## Modbus: TCP, serial e mapa de registradores

Prepare o cliente TCP ou mestre RTU e entenda o endereço e a representação de cada valor.

### Escolha cliente ou mestre

Use Modbus TCP para consultar um servidor/controlador de rede e Modbus RTU Master para consultar escravos no barramento serial do host. Este roteiro não configura o SCADIVO como Modbus Server; esse tipo tem outro papel. Tenha o mapa do fabricante e comece por uma variável conhecida, sem escrita.

### Conexão TCP e tempos comuns

Host e porta são do TCP. Unit ID, varredura, timeout e lacuna também se aplicam ao RTU, com seu limite de Unit ID.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host do controlador | Nome DNS ou IP acessível pelo servidor SCADIVO, que executa o driver. localhost é esse servidor ou contêiner, não o computador do navegador. | 192.0.2.10 |  |
| Porta TCP | Porta em que o controlador atende Modbus TCP. Não é a porta da interface web nem a da API SCADIVO. | 502 | 1–65535; padrão 502. |
| Unit ID da fonte | Endereço lógico do dispositivo; em gateway TCP/serial pode selecionar um escravo. Vale para TAGs sem substituição individual. Use o valor do manual do equipamento, não o último número do IP. | 1 | TCP: 0–255. RTU: 0–247. Padrão 1; o alvo precisa responder a leituras. |
| Intervalo de varredura (ms) | Tempo solicitado entre ciclos de leitura. 1000 ms equivale a 1 segundo; reduzir aumenta tráfego e carga. Uma resposta atrasada não fica mais rápida por reduzir este campo. | 1000 | 10–600000; padrão 1000. |
| Tempo máximo da resposta (ms) | Quanto esperar por uma requisição. Diferente da varredura: timeout curto pode provocar falhas em rede ou barramento lento; longo demora mais para detectar ausência. | 3000 | 50–60000; padrão 3000. |
| Lacuna máxima entre endereços | Avançado: permite reunir leituras próximas em um bloco, incluindo o espaço entre elas. Alguns controladores rejeitam blocos que atravessam endereços não implementados; diminua a lacuna nesse caso. Não altera os endereços dos TAGs. | 8 | 0–125; padrão 8. |

### Serial no Windows, Linux e Docker

Use os parâmetros reais do equipamento. O barramento é administrado pelo host; configurações compartilhadas precisam ser compatíveis. Se a porta não abrir, confira dispositivo, permissão do processo/contêiner e outro programa ocupando a porta antes de alterar o mapa de TAGs.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Porta serial do servidor | Dispositivo visível ao servidor/DriverHost: por exemplo COM3 no Windows ou /dev/ttyUSB0 no Linux. Em Docker, o host deve disponibilizar o dispositivo ao contêiner e conceder acesso. A porta do navegador não é usada; a configuração pode ser preparada offline. |  |  |
| Velocidade (baud) | Deve coincidir com os dispositivos do mesmo barramento serial. Uma velocidade maior só funciona se o equipamento e a instalação a suportarem. | 9600 | 300–4000000; padrão 9600. |
| Bits de dados | Parte do formato serial. Copie o ajuste do equipamento; não é o tamanho do tipo numérico do TAG. | 8 | 5–8; padrão 8. |
| Paridade | Verificação do formato serial; todos os dispositivos que compartilham a linha precisam de configuração compatível. | None | None / Odd / Even / Mark / Space; padrão None. |
| Bits de parada | Completa o formato serial junto com baud, bits de dados e paridade. Deve coincidir com o equipamento. | One | One / OnePointFive / Two; padrão One. |

### Vínculo do TAG e conversão

O endereço escolhe onde ler; o tipo no equipamento escolhe como interpretar. São decisões diferentes. discrete/input são de leitura; habilitar escrita no TAG não transforma a área em gravável. Tipo do TAG deve comportar o resultado da conversão.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Área e endereço do TAG | Use coil para saída booleana, discrete para entrada booleana, holding para registrador de leitura/escrita e input para registrador de entrada. Endereço canônico começa em zero: holding:0. Se o fabricante chama o primeiro holding de 40001, confirme a convenção antes de converter; não digite 40001 automaticamente como offset. | holding:0 | Offset 0–65535; o valor completo deve caber na área. |
| Unit ID individual | Substitui o Unit ID da fonte apenas neste TAG. Deixe vazio para herdar; no RTU confirme também os limites do escravo/barramento. | 1 |  |
| Tipo no equipamento | Define como os bits do equipamento são interpretados. Sem ajuste, é inferido do tipo do TAG. Int16/UInt16 usam 1 registrador, Int32/UInt32/Float32 usam 2, Int64/UInt64/Float64 usam 4; confirme o mapa do fabricante e compatibilidade do tipo do TAG. | Int16 |  |
| Ordem de palavras | Só afeta valores com vários registradores: HighWordFirst coloca primeiro a palavra mais significativa; LowWordFirst, a menos significativa. Não é a ordem de bytes dentro de cada palavra; não troque aleatoriamente para esconder endereço/tipo errado. | HighWordFirst |  |
| Multiplicador | Converte o valor lido: valor apresentado = bruto × multiplicador + offset. Para décimos de bar, 253 × 0.1 + 0 = 25.3 bar. Evite aplicar a mesma conversão novamente em outra camada. | 0.1 | Finito e diferente de zero; padrão 1. |
| Deslocamento | Soma aplicada depois do multiplicador. Use somente quando o mapa do equipamento exigir correção de origem. | 0 | Finito; padrão 0. |

### Exemplo e diagnóstico por camadas

1. Configure a fonte TCP com o host real, porta 502, Unit ID do controlador e os tempos iniciais de 1000/3000 ms.
2. Crie TAG Double, somente leitura, holding:0 e tipo no equipamento Int16 para um registrador que represente décimos de bar.
3. Use multiplicador 0.1, offset 0; se o bruto conhecido for 253, espere 25.3 bar.
4. Teste a leitura e confira antes de ativar a revisão. Timeout pede conferir caminho/Unit ID; endereço ilegal pede conferir área e base; valor absurdo pede conferir tipo/ordem/conversão. Não use uma escrita como teste de leitura.

```json
{"address":"holding:0","wireType":"Int16","raw":253,"scale":0.1,"offset":0,"expected":25.3}
```

[Abrir este guia na Ajuda instalada](/help?topic=drivers.modbus-setup)

## MQTT: tópicos, JSON e atualidade dos valores

Conecte ao broker e transforme mensagens em valores tipados, separando leitura e publicação.

### Broker não é sensor

MQTT Raw recebe mensagens quando publicadas. Não faz varredura Modbus nem descobre automaticamente TAGs a partir de qualquer JSON. Tenha host, porta, TLS, autenticação, tópicos e exemplos reais de payload. Para começar, use um TAG Double somente leitura.

### Conexão, sessão e recursos

Os campos de sessão diferem entre MQTT 3.1.1 e 5. Confirme a versão antes de abrir os ajustes avançados. Limites de tamanho/fila contêm o uso de memória; não constituem um histórico.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host do broker | Servidor que recebe/publica as mensagens, acessível a partir do servidor SCADIVO. Não é o tópico nem uma URL com caminho. | mqtt.example.invalid |  |
| Porta do broker | Use a porta realmente configurada no broker; deve corresponder ao uso de TLS. | 8883 | 1–65535; padrão 8883. |
| Conexão TLS | Ativa transporte protegido. Desabilitar não muda automaticamente a porta nem resolve certificado inválido. Use o modo exigido pelo broker e sua cadeia de confiança. | true |  |
| ID do cliente MQTT | Identifica esta conexão no broker. Escolha valor único por cliente ativo; reutilizar o mesmo ID em outra conexão pode derrubar a sessão anterior. Não é o login. | scadivo-pump01 |  |
| Versão MQTT | mqtt5 usa Clean Start e expiração da sessão; mqtt311 usa Clean Session. Selecione a versão atendida pelo broker, sem misturar suas opções. | mqtt5 |  |
| Usuário do broker | Conta autorizada pelo broker para os tópicos usados; não é a conta de acesso ao SCADIVO. |  |  |
| Referência da senha do broker | Referência protegida resolvida pelo servidor. Sem material provisionado ou sem permissão no broker, a configuração não autentica; não digite uma senha literal no lugar da referência. |  |  |
| Keep Alive (segundos) | Intervalo de supervisão da conexão com o broker; não solicita novas medições ao dispositivo. | 30 | 1–65535; padrão 30. |
| Tempo para conectar (ms) | Prazo de uma tentativa de conexão, diferente do tempo de validade de um valor do TAG. | 10000 | 100–300000. |
| Espera inicial de reconexão (ms) | Espera mínima entre tentativas após perda de conexão. Ajuste com o limite máximo para evitar tentativas contínuas. | 1000 | 100–300000. |
| Espera máxima de reconexão (ms) | Limita o crescimento da espera. Deve ser maior ou igual à espera mínima. | 30000 | 100–3600000. |
| MQTT 3.1.1: limpar sessão | false solicita continuidade da sessão no broker com o mesmo Client ID. true solicita sessão limpa. Não use esta opção no modo MQTT 5. | false |  |
| MQTT 5: início limpo | true inicia sem reaproveitar a sessão anterior; false solicita continuidade. É independente do tempo de expiração posterior à desconexão. | false |  |
| MQTT 5: expiração da sessão (s) | Quanto o broker mantém a sessão após desconectar. Com Clean Start false precisa ser maior que zero. Não mantém valores sempre atuais nem substitui histórico. | 3600 | 0–4294967295. |
| Máximo por mensagem (bytes) | Limite de memória por payload recebido; mensagens maiores são rejeitadas. Dimensione para o documento real, não para o número de TAGs. | 1048576 | 1–67108864. |
| Limite de falhas de conexão | Limite de falhas consecutivas usado pela supervisão de prontidão. Elevar não corrige credenciais ou broker indisponível. | 5 | 1–1000. |
| Máximo de mensagens no buffer | Limita a fila de mensagens do transporte. Buffer maior consome mais memória; não é arquivamento nem garantia de guardar tudo durante uma falha. | 4096 | 1–1000000. |

### Leitura, qualidade e escrita

Cada TAG usa tópico exato e um tipo. Um mesmo documento pode alimentar TAGs distintos com Pointers diferentes. QoS, retenção e freshness têm funções diferentes; o valor pode existir e estar Stale.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Tópico de leitura | Tópico exato que alimenta este TAG, respeitando maiúsculas e minúsculas. Não use + ou # para criar identidade de TAG; separe os tópicos e campos em TAGs próprios. | plant/pump01/state |  |
| Formato do conteúdo | utf8Scalar lê um único valor textual, como 12.5 ou true. json lê JSON e pode extrair um campo com JSON Pointer. O resultado precisa ser compatível com o tipo do TAG. | json |  |
| Campo do valor no JSON | Caminho JSON Pointer, não expressão Python nem JSONPath: /current lê current na raiz; /motor/current lê dentro de motor; /values/0 lê o primeiro item. ~1 representa / dentro do nome; ~0 representa ~. Vazio usa a raiz, que precisa ser um valor escalar compatível. | /current |  |
| Campo do instante de medição | Campo JSON que informa quando a origem mediu o valor. Exemplo UTC com Z; não confunda horário de medição com horário de chegada ao broker. | /timestamp |  |
| Exigir instante de origem | Com true, configure também o Pointer do timestamp e publique um instante válido; sem ele a amostra é rejeitada. | true |  |
| Validade sem nova amostra (ms) | Opcional: após esse intervalo sem amostra mais recente, o valor passa a Stale. Não solicita publicação ao dispositivo. Escolha uma margem em relação ao período normal de envio. | 5000 | 1–2147483647 quando preenchido. |
| Valor retido pelo broker | staleWithoutSourceTimestamp marca como Stale o retido sem timestamp de origem. acceptAsCurrent aceita-o como atual; escolha apenas se a aplicação puder assumir isso. Mensagem retida não comprova equipamento conectado agora. | staleWithoutSourceTimestamp |  |
| QoS de leitura | 0: no máximo uma entrega; 1: pelo menos uma, podendo repetir; 2: entrega MQTT exatamente uma vez na sessão. QoS não confirma atuação física nem validade do sensor. | 1 |  |
| Tópico de escrita | Necessário para ponto gravável e deve ser exato. Use tópico de comando do equipamento, separado do feedback quando o contrato exigir. Não publique acidentalmente no tópico de medição. |  |  |
| QoS de escrita | Qualidade de entrega solicitada ao publicar, com os mesmos valores 0/1/2. Confirmação do broker não é confirmação do processo. | 1 |  |
| Reter publicação | false por padrão. true pede ao broker que guarde o último payload publicado; avalie o risco de um novo assinante receber um comando antigo. Só se aplica a ponto gravável. | false |  |

### Exemplo de payload: corrente do motor

Publique este exemplo no tópico plant/pump01/state do broker de teste. No TAG Double use formato json, Pointer /current, timestamp /timestamp e timestamp obrigatório. O valor decodificado esperado é 12.5. O instante é ilustrativo: em teste ao vivo use um timestamp atual e verifique qualidade/idade, não apenas o número. Não cole este JSON como configuração da fonte.

```json
{"current":12.5,"timestamp":"2026-10-25T12:00:00Z"}
```

### Limites da publicação e diagnóstico

Extrair /current para leitura não cria um modelo de documento de comando. A escrita JSON de um campo interno, com Pointer não vazio, é rejeitada: o produto não reconstrói o envelope. Para escrita, use contrato escalar ou JSON na raiz compatível com o equipamento, TAG gravável, tópico de publicação e autorização. Se conectar sem valor, confira tópico exato, publicação, Pointer, tipo e timestamp. Para Stale, confira retenção, instante de origem e intervalo de freshness; não masque falta de medição aceitando tudo como atual.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.mqtt-setup)

## OPC UA: endpoint, certificados e importação de nós

Selecione o servidor, aprove sua identidade e associe variáveis usando os nós corretos.

### Descoberta não é ativação

Na fonte OPC UA, use o assistente de descoberta para conhecer endpoints e modos oferecidos. Escolher um candidato preenche parâmetros; não cria credenciais protegidas nem aprova automaticamente certificados. O teste/navegação de Engenharia é temporário e não muda a revisão ativa.

### Conexão e identidades

Canal seguro e autenticação do usuário são configurações distintas. O cliente precisa ser aceito pelo servidor, e o SCADIVO precisa reconhecer o servidor aprovado. Peça ao administrador o provisionamento e as referências; o arquivo de engenharia não deve virar cofre de segredos.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Endpoint do servidor OPC UA | URL opc.tcp completa, acessível pelo servidor SCADIVO. Descubra os endpoints e escolha um que o servidor realmente ofereça. | opc.tcp://192.0.2.20:4840 |  |
| Modo de segurança | None não protege o canal; Sign assina; SignAndEncrypt assina e cifra. O modo deve existir no endpoint escolhido e corresponder à política; não reduza apenas para esconder uma falha de certificado. | SignAndEncrypt |  |
| URI da política de segurança | Use a URI completa fornecida pelo endpoint, não apenas o nome curto. None exige política terminada em #None; um modo protegido não aceita essa política. | http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256 |  |
| ApplicationUri aprovada | Identidade da aplicação servidora, quando fixada. Não é o endereço de conexão. Compare com a identidade aprovada pelo administrador. |  |  |
| SHA-256 do certificado aprovado | Runtime protegido exige aprovação explícita do certificado do servidor. Confira a impressão digital por um canal conhecido antes de registrar; mudança do certificado exige nova conferência. Não é uma chave privada. |  |  |
| Autenticação no servidor OPC UA | Anonymous, UserName ou Certificate conforme o servidor permitir. Anonymous não remove proteção do canal; é diferente do login do SCADIVO. | Anonymous |  |
| Usuário OPC UA | Preencha no modo UserName com a conta autorizada no servidor de origem. |  |  |
| Referência da senha OPC UA | No modo UserName, referência protegida da senha provisionada no host; não é a senha em texto. |  |  |
| Referência do certificado cliente | Identidade da aplicação cliente para o canal protegido. Deve estar provisionada no host e ser aceita pelo servidor. Não confunda com o certificado de usuário. |  |  |
| Referência do certificado de usuário | No modo Certificate, identifica o usuário, separadamente do certificado da aplicação cliente. O administrador fornece a referência protegida. |  |  |
| Timeout da sessão | Duração solicitada para a sessão; não é intervalo de amostragem. Formato horas:minutos:segundos. | 00:01:00 | 00:00:05–01:00:00; padrão 00:01:00. |
| Intervalo de publicação | Ritmo solicitado para entrega das notificações da assinatura. Diferente da coleta de cada nó; o servidor pode negociar valores suportados. | 00:00:01 | 00:00:00.050–00:05:00. |
| Confiança temporária em Engenharia | Somente para sessão temporária de Engenharia. Não aprova o certificado para Runtime: ativação com auto-trust é rejeitada. Após inspecionar, registre a identidade aprovada e mantenha esta opção desabilitada na configuração operacional. | false |  |

### Navegar e vincular TAGs

No navegador OPC UA, selecione as variáveis adequadas, confira tipo e acesso e revise a prévia de importação. Só aplique o candidato depois de conferir conflitos. O vínculo guarda a identidade do nó; renomear o TAG não deve significar apontar para outro nó.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| NodeId | Identificador do nó no servidor; por exemplo ns=2;s=Pump.Current. Não é o nome exibido nem o caminho do TAG SCADIVO. Prefira selecionar no navegador OPC UA para evitar erros de digitação. | ns=2;s=Pump.Current |  |
| URI do namespace | Identidade estável do namespace. Quando disponível, permite reconhecer o mesmo nó mesmo se seu índice ns= mudar. Use a URI do servidor, não invente uma a partir do nome do equipamento. | urn:example:plant |  |
| Intervalo de amostragem | Ritmo solicitado para observar este nó. Formato horas:minutos:segundos; use uma frequência compatível com o processo e confirme o comportamento negociado pelo servidor. | 00:00:01 |  |
| Tamanho da fila do nó | Número de amostras que a fila do item pode reter entre entregas. Maior usa mais recursos; não substitui Historian. | 1 | 1–10000. |
| Descartar a mais antiga | Quando a fila enche, true prioriza as amostras novas; false solicita preservar as antigas em vez de substituir a mais antiga. Confira a política de fila do servidor. | true |  |

### Exemplo de identidade, não arquivo de importação

Este exemplo mostra dois campos do vínculo: NodeId ns=2;s=Pump.Current e namespaceUri urn:example:plant. Substitua pelos dados do seu servidor ou use o navegador. Se o namespace mudar de índice 2 para 4 mantendo a URI e o identificador s=Pump.Current, a identidade estável continua a mesma. Não use esse nome de exemplo para adivinhar um nó existente.

```json
{"nodeId":"ns=2;s=Pump.Current","namespaceUri":"urn:example:plant"}
```

### Conferir e resolver falhas

1. Descubra e escolha endpoint/modo/política compatíveis.
2. Confira identidade e certificado do servidor e provisione os materiais do cliente conforme a autenticação.
3. Teste a conexão e navegue até uma variável conhecida.
4. Revise/aplique a importação de pelo menos um TAG e confira a leitura.
5. Remova a confiança temporária e confirme a configuração aprovada para Runtime.
6. Salve/publique/ative pelo fluxo do projeto e confira qualidade e timestamps. Certificado rejeitado exige conferir confiança/identidade; nó não encontrado exige NodeId/namespace; acesso negado exige conta/permissão; tipo incompatível exige revisar o TAG. Escrita aceita não prova atuação: confira o feedback do equipamento.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.opcua-setup)
