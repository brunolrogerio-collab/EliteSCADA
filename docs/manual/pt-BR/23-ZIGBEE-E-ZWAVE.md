# Zigbee e Z-Wave: conexão, identidade e operação

## Zigbee e Z-Wave: escolher o caminho da conexão

Confira quem mantém a rede e qual tipo de fonte representa essa instalação.

### Antes de preencher campos

Este é um checklist, não uma tela nova de configuração. A escolha da fonte depende do coordenador/controlador e da instalação externa que você realmente tem. Um rádio não deve ser aberto simultaneamente pelo SCADIVO e por outro processo que controle a mesma porta.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Caminho da conexão | Zigbee nativo usa o coordenador TI suportado no host SCADIVO. Zigbee2MQTT Bridge usa uma instalação externa Zigbee2MQTT e seu broker. Z-Wave JS Controller usa um controlador Z-Wave serial preparado no host. Não são opções intercambiáveis de um mesmo dispositivo. |  |  |
| Rede e dispositivos preparados | Tenha a rede e os dispositivos já associados pelo procedimento próprio do coordenador/instalação. A descoberta de Engenharia consulta o que existe; não abre inclusão, não faz reset e não cria uma rede nova. |  |  |
| Valor recebido e confirmação | Um dispositivo encontrado pode não ter todas as funções mapeadas. Estado antigo, confirmação de transporte e feedback recente são diferentes. Confira qualidade e atualização antes de usar o TAG em comando, alarme ou tela. |  |  |

### Rede física, TAG e interface têm etapas distintas

1. Identifique se a rede usa coordenador local suportado, Zigbee2MQTT externo ou controlador Z-Wave local.
2. Confirme rede/dispositivos preparados, acesso do servidor e material protegido, sem criar outra rede por engano.
3. Configure a fonte correspondente e confira as limitações de autoria/seleção do guia específico.
4. Com vínculo válido, confira o TAG, tipo, qualidade e feedback; salve/publique/ative pelo fluxo do projeto.
5. Acrescente tela, alarme e comando autorizado somente após conferir uma leitura conhecida.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.radio-connections)

## Zigbee nativo: coordenador e estado On/Off

Prepare a fonte para o coordenador TI suportado e entenda o alcance limitado desta versão.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-ZIGBEE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.zigbee-native-setup; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Instalação necessária

Este driver usa coordenador serial TI zStack/CC2652 e processo auxiliar gerenciado da distribuição para Windows ou Linux x64. No contêiner, o host precisa disponibilizar o recurso físico e o estado persistente. A rede e o dispositivo devem estar preparados; não há descoberta ou assistente de inclusão de Engenharia neste tipo. Não promete todos os dispositivos/clusters Zigbee.

### Campos e limitação da autoria

Os parâmetros de rede devem corresponder ao coordenador e ao estado já existentes. A autoria do vínculo pelo assistente genérico está incompleta: seus campos extras são recusados pelo Runtime. Os campos abaixo descrevem o que o driver exige; não há um fluxo corrigido de criação pelo assistente atual.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Recurso de coordenador no host | ID de um recurso físico registrado na instalação como ZigbeeCoordinator, disponível e serial, com família TI zStack e modelo CC2652. Não é caminho COM/tty digitado aqui. Peça ao administrador o ID e a disponibilização do dispositivo no Windows/Linux/contêiner. | radio.zigbee1 |  |
| Identidade da rede | Nome/identidade da rede preparada, que precisa coincidir com a evidência registrada no recurso de coordenador. Não renomeia a rede física nem substitui PAN ID ou chave. | zigbee.lab1 |  |
| PAN ID | Identificador da rede existente. Use o valor da instalação; no campo numérico prefira decimal. Mudar este valor não é procedimento de reparação. | 4660 | 1–65534. |
| PAN ID estendido | Identidade de rede com 16 dígitos hexadecimais, conforme o coordenador preparado. Não é o IEEE de um atuador nem uma chave secreta. | 00124b0000000001 |  |
| Canal da rede | Canal Zigbee utilizado pela rede existente. Trocar apenas o número no projeto não migra de forma segura todos os dispositivos da rede. | 15 | 11–26. |
| Velocidade serial | Velocidade da ligação ao coordenador, compatível com seu firmware. Não é frequência de medição ou canal de rádio. | 115200 | 9600–1000000. |
| Referência da chave de rede | Referência de material protegido provisionado no host para esta rede. O administrador fornece a referência/slot; não cole a chave secreta nem a leve no pacote de engenharia. Não substitua uma chave existente por um valor novo para tentar resolver conexão. |  |  |
| IEEE do dispositivo | Identidade de 16 dígitos hexadecimais do dispositivo já associado. Nome amigável e endereço de rede curto não substituem o IEEE. | 00124b0000000001 |  |
| Endpoint | Endpoint da função no dispositivo, obtido do mapeamento real. Não é porta TCP nem número do canal no dashboard. | 1 | 1–240. |
| Cluster suportado | Este driver nativo aceita genOnOff nesta versão; não oferece todos os clusters Zigbee nem medição analógica por este vínculo. | genOnOff |  |
| Atributo suportado | Atributo onOff, com TAG Boolean. Combinar outro atributo ou tipo numérico é recusado. | onOff |  |
| Endereço completo do TAG | Reúne IEEE/endpoint/cluster/atributo. O exemplo é de uma função On/Off; substitua pela identidade existente do dispositivo. | 00124b0000000001/1/genOnOff/onOff |  |
| Autoria do vínculo nesta versão | O assistente genérico atual grava os campos de identidade como ajustes extras, mas este driver exige que estejam apenas no endereço. O Runtime recusa o objeto gerado. A autoria pela interface aguarda correção; não contorne desabilitando validação. |  |  |

### Exemplo de uma função preparada

O exemplo descreve a identidade de uma função booleana já existente. Não é pacote de importação nem abre inclusão. O vínculo do driver guarda a identidade no endereço completo, sem duplicá-la em ajustes extras. ReadOnly ainda controla se a escrita é permitida, junto da autorização da Authority.

```json
{"address":"00124b0000000001/1/genOnOff/onOff","ieeeAddress":"00124b0000000001","endpoint":1,"cluster":"genOnOff","attribute":"onOff","dataType":"Boolean"}
```

### Diagnóstico e preservação da rede

Confira recurso disponível, modelo/família, acesso serial e identidade de rede antes de rever o TAG. Depois confira IEEE/endpoint e Boolean/genOnOff/onOff. Não resolva falha apagando o estado persistente, mudando a chave ou criando outra rede sem o procedimento de manutenção apropriado. Uma escrita seguida de leitura é conferência de estado, não prova de atuação física.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.zigbee-native-setup)

## Z-Wave JS Controller: rede preparada e valores persistidos

Use o controlador registrado no host e preserve a identidade exata de cada valor.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-ZWAVE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.zwave-setup; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### O que este tipo faz

O processo auxiliar gerenciado usa um controlador serial local e comunicação interna restrita ao próprio host. A instalação suporta Windows/Linux x64; em Docker também são necessários dispositivo e diretório de estado persistente. Este driver trabalha com valores persistidos já preparados: não oferece inclusão, exclusão, SmartStart, reset, administração de controlador nem descoberta de Engenharia.

### Fonte e leitura do endereço

As referências S0/S2 são opcionais no formato da fonte, mas os dispositivos seguros precisam do material correspondente à rede. Região RF e identidade NVM pertencem ao recurso do host. A autoria de vínculos novos pela interface está pendente: o descritor sem campos adicionais não produz o vínculo exigido pelo Runtime.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Recurso de controlador no host | ID de recurso ZWaveController serial disponível, com região RF e identidade NVM da rede registradas pelo administrador. Não digite caminho serial ou parâmetros RF nesta fonte. Regiões e identidades precisam corresponder ao controlador já preparado. | radio.zwave1 |  |
| Chave S0 Legacy: referência | Referência de material protegido provisionado no host para esta rede. O administrador fornece a referência/slot; não cole a chave secreta nem a leve no pacote de engenharia. Não substitua uma chave existente por um valor novo para tentar resolver conexão. |  |  |
| Chave S2 Access Control: referência | Referência de material protegido provisionado no host para esta rede. O administrador fornece a referência/slot; não cole a chave secreta nem a leve no pacote de engenharia. Não substitua uma chave existente por um valor novo para tentar resolver conexão. |  |  |
| Chave S2 Authenticated: referência | Referência de material protegido provisionado no host para esta rede. O administrador fornece a referência/slot; não cole a chave secreta nem a leve no pacote de engenharia. Não substitua uma chave existente por um valor novo para tentar resolver conexão. |  |  |
| Chave S2 Unauthenticated: referência | Referência de material protegido provisionado no host para esta rede. O administrador fornece a referência/slot; não cole a chave secreta nem a leve no pacote de engenharia. Não substitua uma chave existente por um valor novo para tentar resolver conexão. |  |  |
| Endereço persistido do valor | Identifica nó, endpoint, Command Class, propriedade e chave opcional, preservando tipo string/number. Não é o nome mostrado ao operador. O endereço deve corresponder ao inventário preparado do controlador. | node/5/endpoint/0/cc/37/property/string/currentValue/key/none/- |  |
| Nó | Número atribuído ao dispositivo na rede Z-Wave existente; não é endereço IP. A exclusão/reinclusão pode exigir revisar este vínculo. | 5 | 1–232. |
| Endpoint | Endpoint do valor no dispositivo, incluindo 0 quando o valor é do nó raiz. Não é porta serial/TCP. | 0 | 0–127. |
| Command Class | Código da classe a que o valor pertence. O exemplo 37 é de Switch Binary; o driver admite um conjunto limitado de valores persistidos e recusa classes fora desse escopo. Não é uma permissão da Authority. | 37 |  |
| Propriedade e seu tipo | Use string/currentValue para a propriedade textual do exemplo. Propriedade number/1 e string/1 são identidades diferentes; não remova o tipo. Não presuma que currentValue seja gravável. | string/currentValue |  |
| Chave opcional e seu tipo | key/none/- significa sem chave adicional. Quando existe uma chave, mantenha seu tipo number ou string e o conteúdo escapado do endereço; não omita partes do formato. | none/- |  |
| Autoria de um vínculo novo | Este driver exige vínculo de comunicação versionado, além do Address. O assistente genérico não cria um vínculo novo quando o descritor não tem campos adicionais, como neste caso. Digitar só o endereço de um TAG novo não basta; a correção da autoria está pendente. Um vínculo válido já recebido por pacote/API é outra situação. |  |  |

### Exemplo: ler estado de uma chave binária

O exemplo é uma leitura Boolean de currentValue do nó 5. Use os valores reais da instalação. A propriedade pode não ser gravável; para comandar, o valor precisa ser anunciado como gravável pelo controlador e autorizado no SCADIVO. O exemplo não é pacote de importação.

```json
{"address":"node/5/endpoint/0/cc/37/property/string/currentValue/key/none/-","nodeId":5,"endpoint":0,"commandClass":37,"property":"currentValue","dataType":"Boolean","readOnly":true}
```

### Conferência e falhas comuns

Os TAGs admitidos são Boolean, Int16/Int32/Int64, Float/Double e String, dentro das propriedades persistidas aceitas. Confira recurso, região, identidade NVM e materiais de segurança; depois endereço completo e metadados reais do valor. Valor inexistente, tipo incompatível ou escrita não anunciada não se resolvem removendo validação. Eventos transitórios não se tornam automaticamente TAGs persistidos. Confirme o retorno antes de considerar um comando concluído.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.zwave-setup)

## Zigbee2MQTT Bridge: inventário, seleção e atualidade

Conecte a uma instalação externa e diferencie inventário, incorporação e relatório recente.

<!-- HELP-REVIEW-AFTER-PRODUCT-785-Z2M: https://github.com/brunolrogerio-collab/EliteSCADA/issues/785; topic=drivers.zigbee2mqtt-setup; fields=selectedIeeeAddresses; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-787: https://github.com/brunolrogerio-collab/EliteSCADA/issues/787; topic=drivers.zigbee2mqtt-setup; fields=inventoryFormat; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Instalação externa e fluxo disponível

Zigbee2MQTT e broker são administrados fora do SCADIVO. Dispositivos devem estar associados à rede externa; este conector não abre inclusão, não administra o coordenador e não substitui o driver Zigbee nativo. Teste de conexão, descoberta e teste de leitura são anunciados, mas descoberta sem seleção lista somente inventário. A interface atual não envia a seleção necessária para incorporar; o fluxo aguarda correção.

O formato externo põe as funções em definition.exposes; esta versão do parser procura exposes na raiz. Assim, um dispositivo pode ser listado sem gerar TAGs. A correção de compatibilidade está pendente, separadamente da seleção na interface.

### Campos da fonte

Host, tópico base e credenciais precisam corresponder ao broker realmente usado pela instalação. Confirme a rede a partir do servidor SCADIVO, não apenas do navegador.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host do broker MQTT | Nome ou IP do broker usado pela instalação externa Zigbee2MQTT, acessível a partir do servidor SCADIVO. Não é a URL da interface Zigbee2MQTT nem um tópico. | mqtt.example.invalid |  |
| Porta do broker | Porta realmente configurada no broker. O padrão do formulário é 1883; se usar TLS, confira a porta correspondente em vez de supor troca automática. | 1883 | 1–65535. |
| Usar TLS | Ativa transporte protegido se o broker o oferecer. Não instala certificados nem substitui a autenticação do broker. | false |  |
| Tópico base | Prefixo configurado no Zigbee2MQTT externo. O driver usa, por exemplo, baseTopic/bridge/state e baseTopic/bridge/devices. Não coloque o friendly name de um dispositivo aqui. | zigbee2mqtt |  |
| ID do cliente MQTT | Opcional; vazio gera um ID estável a partir da identidade da fonte. Se definir, não reutilize o mesmo ID em outro cliente ativo. Não é usuário ou senha. |  |  |
| Versão MQTT | mqtt5 ou mqtt311 conforme o broker suportar. O driver usa seus próprios padrões de sessão; não copie todos os ajustes avançados de MQTT Raw para esta fonte. | mqtt5 |  |
| Usuário do broker | Conta permitida nos tópicos da instalação externa. Diferente do login SCADIVO. |  |  |
| Referência da senha do broker | Referência de material protegido provisionado no host para esta rede. O administrador fornece a referência/slot; não cole a chave secreta nem a leve no pacote de engenharia. Não substitua uma chave existente por um valor novo para tentar resolver conexão. |  |  |
| Keep Alive (s) | Supervisão da conexão com o broker. Não solicita medição ao sensor nem acorda dispositivo a bateria. | 30 | 1–65535. |
| Timeout de conexão (ms) | Prazo para estabelecer conexão com o broker, separado do tempo de uma requisição ou confirmação de escrita. | 10000 | 100–300000. |
| Timeout de requisição (ms) | Prazo para a operação de consulta. Não é intervalo de varredura de todos os sensores. | 5000 | 100–30000. |
| Espera pela confirmação da escrita (ms) | Prazo para um relatório de estado compatível e suficientemente recente após o comando. Aceitação MQTT sem feedback não confirma o estado do equipamento. | 10000 | 100–60000. |
| Espera mínima de reconexão (ms) | Espera inicial após perda da conexão. Reduzir não corrige broker offline nem autenticação recusada. | 1000 | 100–300000. |
| Espera máxima de reconexão (ms) | Limita a espera progressiva e deve ser maior ou igual à mínima. | 30000 | 100–3600000. |
| Tamanho máximo por mensagem (bytes) | Limita payload recebido, inclusive inventários; aumente apenas se o documento real exigir e houver recursos. Não é capacidade de histórico. | 1048576 | 1–67108864. |
| Limite de falhas de conexão | Limite de falhas consecutivas usado pela supervisão de prontidão. Não concede operação com uma origem inacessível. | 10 | 1–1000. |
| Máximo de mensagens em fila | Limita o buffer do transporte. Mais mensagens usam mais memória; não equivale a armazenamento persistente. | 2048 | 1–65535. |
| Máximo de dispositivos no inventário | Limite de inventário/seleção para esta fonte. Não é promessa de capacidade de rádio e não remove as cotas de licença. | 256 | 1–4096. |
| Máximo de funções por dispositivo | Limita as descrições de propriedades processadas por dispositivo. Expose é uma função/propriedade anunciada pelo Zigbee2MQTT, não um TAG automaticamente incorporado. | 256 | 1–2048. |

### Vínculos gerados e seleção ainda pendente

Os campos técnicos abaixo são produzidos pelo serviço de incorporação, a partir da seleção e do inventário. Não são uma lista de IDs para o desenvolvedor inventar. Enquanto a tela não transmitir a seleção, listar um dispositivo não garante Equipamento/TAGs prontos. As funções escalares mapeadas são binary e numeric; capacidades funcionais dependem das propriedades suportadas.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| IEEE, endpoint e propriedade | Endereço estável produzido a partir do IEEE, endpoint opcional e propriedade. Friendly name pode mudar e não deve virar a identidade do vínculo. | ieee:0x00124b0024cafe01/endpoint:/property:temperature |  |
| Identidade interna da fonte — automática | Campo técnico gerado pelo serviço de incorporação. Relaciona ao ID estável da fonte salva. Não invente nem copie IDs entre fontes. |  |  |
| Chave da fonte — automática | Campo técnico gerado pelo serviço de incorporação. Relaciona à chave legível da fonte. Não invente nem copie IDs entre fontes. |  |  |
| IEEE — automático | Campo técnico gerado pelo serviço de incorporação. IEEE normalizado do dispositivo selecionado, não nome amigável. Não invente nem copie IDs entre fontes. | 0x00124b0024cafe01 |  |
| Endpoint — automático | Campo técnico gerado pelo serviço de incorporação. Endpoint anunciado pela função; pode estar ausente. Não invente nem copie IDs entre fontes. |  |  |
| Propriedade — automática | Campo técnico gerado pelo serviço de incorporação. Nome da propriedade anunciada, como temperature ou state. Não invente nem copie IDs entre fontes. | temperature |  |
| Tipo da função — automático | Campo técnico gerado pelo serviço de incorporação. binary produz TAG Boolean; numeric produz TAG Double. Outros tipos não viram TAG escalar por este fluxo. Não invente nem copie IDs entre fontes. | numeric |  |
| Acesso anunciado — automático | Campo técnico gerado pelo serviço de incorporação. Bits combináveis: 1 estado, 2 escrita, 4 leitura por requisição. Precisa incluir 1; 5 significa estado e leitura por requisição. Não concede autorização de segurança. Não invente nem copie IDs entre fontes. | 5 |  |
| Unidade — automática | Campo técnico gerado pelo serviço de incorporação. Precisa coincidir com a unidade do TAG; não é conversão arbitrária de escala. Não invente nem copie IDs entre fontes. | °C |  |
| Mínimo — automático | Campo técnico gerado pelo serviço de incorporação. Limite numérico anunciado pela função, quando houver. Não invente nem copie IDs entre fontes. |  |  |
| Máximo — automático | Campo técnico gerado pelo serviço de incorporação. Limite numérico superior anunciado, quando houver. Não invente nem copie IDs entre fontes. |  |  |
| Passo — automático | Campo técnico gerado pelo serviço de incorporação. Passo numérico positivo, quando anunciado; não define frequência de leitura. Não invente nem copie IDs entre fontes. |  |  |
| Valor ligado — automático | Campo técnico gerado pelo serviço de incorporação. Valor JSON explícito para verdadeiro de uma função binária. Texto ON e Boolean true são tipos diferentes. Não invente nem copie IDs entre fontes. | "ON" |  |
| Valor desligado — automático | Campo técnico gerado pelo serviço de incorporação. Valor JSON explícito e diferente do valor ligado para a função binária. Não invente nem copie IDs entre fontes. | "OFF" |  |
| Identidade do ponto — automática | Campo técnico gerado pelo serviço de incorporação. Combina fonte, IEEE, endpoint e propriedade; copiar de outra fonte é inválido. Não invente nem copie IDs entre fontes. |  |  |
| Função do Equipamento — automática | Campo técnico gerado pelo serviço de incorporação. Exemplo Temperature ou OnOff; não é uma capability de segurança concedida ao usuário. Não invente nem copie IDs entre fontes. | Temperature |  |
| Contrato de confirmação — automático | Campo técnico gerado pelo serviço de incorporação. Quando suportado, exige relatório de estado não retido compatível após o comando, com evidência de atualidade da origem. Não é confirmação de atuação física. Não invente nem copie IDs entre fontes. | non-retained-state-match |  |
| Seleção explícita para incorporar | O serviço aceita seleção de IEEE, mas a tela atual Home/Building não a envia. Sem seleção, a descoberta lista inventário sem conteúdo para incorporar; escolher uma linha na tela não refaz a solicitação com o IEEE. O fluxo de incorporação pela interface aguarda correção. |  |  |
| Formato do inventário — limitação atual | O formato externo põe as funções em definition.exposes; esta versão do parser procura exposes na raiz. Assim, um dispositivo pode ser listado sem gerar TAGs. A correção de compatibilidade está pendente, separadamente da seleção na interface. | definition.exposes |  |

### Exemplo de inventário anunciado

O exemplo abaixo preserva o formato externo. No parser atual, suas funções não são extraídas; ele demonstra a limitação, não uma importação concluída. Não reescreva as mensagens do broker como solução operacional.

```json
[
  {
    "ieee_address": "0x00124b0024cafe01",
    "friendly_name": "sensor_sala",
    "supported": true,
    "type": "EndDevice",
    "definition": {
      "model": "Example",
      "vendor": "Example",
      "exposes": [
        {
          "type": "numeric",
          "property": "temperature",
          "access": 5,
          "unit": "°C"
        },
        {
          "type": "binary",
          "property": "state",
          "access": 7,
          "value_on": "ON",
          "value_off": "OFF"
        }
      ]
    }
  }
]
```

### Exemplo de estado e qualidade

Uma mensagem retida é Stale neste conector. Sem last_seen de origem mais recente que a época de conexão/bridge, o relatório pode ser Uncertain mesmo se retain for false. Para confirmar uma operação, precisa também ser posterior ao envio. Configure a informação de medição na instalação externa e confira os instantes; receber agora não prova que o dispositivo mediu agora. O instante abaixo é ilustrativo: em teste ao vivo use uma medição atual.

```json
{"temperature":21.5,"state":"OFF","last_seen":"2026-10-25T12:00:01Z"}
```

### Resolver por camadas

Sem conexão: confira host/porta/TLS/autenticação. Conectado sem inventário: confira baseTopic e bridge/state/devices. Inventário sem incorporação: confira a limitação de seleção da interface, não recadastre toda a rede. Valor Uncertain/Stale: confira retenção, last_seen e reconexão. Escrita sem confirmação: confira acesso anunciado, vínculo de confirmação, disponibilidade e relatório recente; não marque o TAG como confirmado só porque o broker aceitou a publicação.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.zigbee2mqtt-setup)
