# Home/Building: dispositivos e conexões

## Home/Building: incorporar um dispositivo

Transforme descoberta em Equipamento, TAGs e comandos, conferindo o que realmente será criado.

### Fonte, dispositivo e Local são diferentes

A Fonte de dados guarda como comunicar; Equipamento representa o dispositivo; Local organiza onde ele está. Capacidades funcionais como OnOff, Temperature ou Cover descrevem funções do equipamento, não permissões da Authority. Um dispositivo descoberto não ganha autorização para comandar pelo simples fato de ser incorporado.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Fonte de integração | Fonte já configurada em Fontes de dados. O tipo precisa oferecer descoberta para aparecer neste fluxo; uma lista vazia não prova que a rede inteira está vazia. |  |  |
| Dispositivo encontrado | Escolha um candidato e confira TAGs, comandos, capacidades e avisos. Encontrado não significa incorporado; candidatos sem materialização não podem ser incorporados por este formulário. |  |  |
| Nome do dispositivo | Nome legível para o Equipamento que será criado. Não renomeia o dispositivo físico nem sua identidade de origem. | Luz da sala |  |
| Caminho do dispositivo | Organiza o Equipamento no projeto. Confira conflitos na prévia; não é IP, NodeId nem tópico. | Home/Sala/Luz |  |
| Local | Opcional: selecione um Local existente para situar o dispositivo. Cadastre os Locais na área correspondente antes de associar. Não cria permissão de acesso. |  |  |

### Da descoberta à revisão ativa

1. Configure e aplique a fonte em Engenharia → Fontes de dados, com materiais protegidos disponíveis no servidor.
2. Abra Home/Building → Dispositivos, selecione a fonte e descubra os candidatos quando o driver oferecer descoberta.
3. Escolha um dispositivo e leia os avisos e a lista de TAGs/comandos/capacidades.
4. Ajuste nome, caminho e Local e confira a prévia; isso ainda não altera a configuração.
5. Aplique somente uma prévia válida; se o Workspace mudar, refaça a prévia antes de tentar aplicar.
6. Salve/publique/ative pelo fluxo do projeto e confira estado, qualidade, unidade e comandos autorizados.

### Sem candidato ou sem a função esperada

Confira fonte salva, comunicação a partir do servidor e recursos anunciados pelo driver. KNX/IP Secure não anuncia descoberta de Engenharia; Home Assistant tem a limitação de seleção descrita em seu guia. Um recurso não mapeado não deve ser tratado como entregue: veja avisos e campos realmente gerados. Um gateway ou bridge pode oferecer um dispositivo sem que o SCADIVO tenha seu protocolo nativo. Não importe repetidamente para tentar corrigir rede ou permissões.

[Abrir este guia na Ajuda instalada](/help?topic=home-building.device-workflow)

## BACnet/IP: dispositivo, objetos e propriedades

Configure a rede UDP e selecione a identidade do objeto; nome e endereço de rede não substituem o vínculo.

### Antes de conectar

Este driver é BACnet/IP sobre UDP; não anuncia BACnet Secure Connect. Tenha instância do dispositivo, rota/IP/porta e acesso à rede a partir do servidor SCADIVO. Em Docker confira a rede e o UDP; descoberta por broadcast depende da topologia, não do navegador. Comece por um objeto somente leitura conhecido.

### Campos da conexão e do vínculo

A identidade do vínculo contém dispositivo, tipo/instância do objeto e propriedade, com índice opcional. Configuração de COV e prioridade não altera essa identidade.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Instância do dispositivo | Identidade BACnet do equipamento, não seu IP nem a instância de um objeto. A instância do vínculo do TAG precisa corresponder à fonte. Confirme no controlador ou na descoberta. | 1201 | 0–4194302. |
| Endereço manual do alvo | Opcional: IPv4 do equipamento com porta UDP. Útil quando o alvo é conhecido; não substitui a identidade deviceInstance. | 192.0.2.20:47808 |  |
| Porta UDP local | Porta em que o processo SCADIVO atende BACnet/IP. Outra aplicação ou contêiner pode já ocupá-la; confira firewall e exposição UDP no ambiente do servidor. | 47808 | 1–65535. |
| Intervalo de leitura (ms) | Ritmo solicitado de varredura para pontos consultados. Não é a frequência garantida dos eventos COV. | 1000 | 50–600000. |
| Timeout BACnet (ms) | Tempo de espera de uma solicitação BACnet. Diferente da janela de descoberta e da varredura. | 3000 | 100–60000. |
| Janela de descoberta (ms) | Quanto aguardar respostas durante a descoberta. Aumentar pode ajudar numa rede lenta, mas não faz broadcasts atravessarem uma sub-rede bloqueada. | 1500 | 100–30000. |
| Endereço do BBMD | Opcional: ponto de distribuição BACnet configurado pelo administrador para o cenário de rede. Não é necessário inventar um BBMD numa rede local simples. |  |  |
| Registro Foreign Device: validade (s) | Opcional: duração do registro no BBMD. Exige bbmdAddress; não é validade do valor do TAG nem retenção histórica. | 600 | 30–32767. |
| Tipo do objeto | Código numérico do objeto BACnet. No exemplo, 0 identifica Analog Input; use o tipo mostrado pelo navegador ou pelo mapa do fabricante, não o tipo Double do TAG. | 0 |  |
| Instância do objeto | Número do objeto dentro do dispositivo. Analog Input 37 é diferente de outro tipo de objeto com instância 37. | 37 | 0–4194303. |
| Propriedade | Código da propriedade a ler. O exemplo 85 é Present Value. Nome, descrição e valor são propriedades diferentes; prefira seleção no navegador para evitar códigos errados. | 85 |  |
| Índice do array | Preencha apenas quando a propriedade for um array e você precisar de um elemento específico. Vazio preserva a propriedade inteira; não acrescente índice a um valor escalar. |  |  |
| Usar COV quando disponível | Solicita notificações por mudança em vez de depender apenas de varredura. Depende do servidor/objeto e há verificação por leitura no driver; ausência de mudança não significa conexão perdida. | true |  |
| Prioridade de escrita BACnet | Opcional, para objeto gravável que aceite prioridade. Não é prioridade de alarme nem permissão SCADIVO. Uma escrita aceita pode não alterar o valor efetivo se outro comando tiver precedência; confira feedback e a política do equipamento. | 8 | 1–16. |

### Exemplo: valor de uma entrada analógica

O exemplo identifica Present Value do Analog Input 37 no dispositivo 1201. É uma descrição de vínculo, não um pacote completo para importar. Use a descoberta/navegação e confirme o tipo e a unidade antes de ativar; substitua as identidades pelas do seu equipamento.

```json
{"deviceInstance":1201,"objectType":0,"objectInstance":37,"propertyIdentifier":85,"useCov":true}
```

### Conferência e diagnóstico

Teste a conexão, descubra/navegue os objetos e revise a prévia antes de aplicar; ative pelo fluxo do projeto. Sem resposta: confira UDP, instância, alvo e topologia/BBMD. Objeto encontrado mas valor inválido: confira propriedade, índice e tipo. COV não suportado pede revisar as notificações e a leitura de verificação; não interprete ausência de mudança como valor zero. Escrita recusada pede acesso, objeto gravável e prioridade, além da autorização SCADIVO.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.bacnet-setup)

## KNX/IP Secure: túnel, DPT e estado confirmado

Use os endereços do projeto KNX e mantenha comando e feedback separados.

<!-- HELP-REVIEW-AFTER-PRODUCT-786: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.knx-setup; fields=writeGroupAddress; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Requisitos e alcance

A implementação usa um processo auxiliar gerenciado XKNX para túnel KNX IP Secure TCP e Data Secure. A instalação precisa incluir seu runtime compatível para Windows ou Linux x64 e o material protegido provisionado no host; um contêiner também precisa desses recursos. Este tipo não é uma promessa de túnel IP sem segurança, roteamento multicast ou descoberta automática de Engenharia. Obtenha parâmetros e endereços do projeto KNX/administrador.

### Campos do gateway e do ponto

O assistente genérico de TAG ainda produz um vínculo incompatível com o contrato KNX/IP. A correção da autoria pela interface está pendente; os campos abaixo explicam o contrato, mas não há um roteiro validado para concluir esse vínculo pelo assistente atual. Não tente resolver alterando DPTs ou desabilitando segurança.

Configure a fonte e o vínculo usando o DPT real. Senhas e chaveiro são referências protegidas, não arquivos secretos dentro da aplicação.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host do gateway KNX | IP ou nome do gateway KNX/IP Secure acessível pelo servidor SCADIVO. Não é endereço de grupo nem endereço individual. | 192.0.2.40 |  |
| Porta do túnel Secure | Porta do endpoint Secure TCP configurado no gateway. Não presuma que um gateway de túnel IP não seguro oferece este serviço. | 3671 | 1–65535. |
| Endereço individual | Endereço atribuído ao cliente KNX, na notação área.linha.dispositivo. Obtenha no projeto KNX/administrador para evitar duplicidade. | 1.1.250 |  |
| Usuário do túnel Secure | Identificador de usuário Secure do gateway, não usuário de login SCADIVO. Deve corresponder ao material de autenticação provisionado. | 1 | 1–127. |
| Referência da senha Secure | Referência do material protegido provisionado no servidor para esta integração. Peça a referência ao administrador; não cole senha, token ou chave secreta em configurações comuns nem no manual da aplicação. |  |  |
| Referência de autenticação do dispositivo | Referência do material protegido provisionado no servidor para esta integração. Peça a referência ao administrador; não cole senha, token ou chave secreta em configurações comuns nem no manual da aplicação. |  |  |
| Referência do chaveiro Data Secure | Referência do material protegido provisionado no servidor para esta integração. Peça a referência ao administrador; não cole senha, token ou chave secreta em configurações comuns nem no manual da aplicação. |  |  |
| Grupo de comando / endereço do vínculo | Endereço em principal/intermediário/subgrupo para a escrita. No vínculo portátil este é o Address do TAG; para TAG somente leitura, confira o mapeamento do projeto KNX. O assistente genérico de TAG ainda produz um vínculo incompatível com o contrato KNX/IP. A correção da autoria pela interface está pendente; os campos abaixo explicam o contrato, mas não há um roteiro validado para concluir esse vínculo pelo assistente atual. Não tente resolver alterando DPTs ou desabilitando segurança. | 1/2/3 |  |
| Grupo do estado confirmado | Origem do feedback. Para TAG gravável deve ser diferente do grupo de comando; o driver não considera o envio do comando como confirmação de estado. | 1/2/4 |  |
| Tipo de ponto KNX (DPT) | Define como interpretar o telegrama. 1.001/1.002 usam TAG Boolean; 5.001 e 9.001/9.004/9.007/9.024 usam Float ou Double neste driver. O exemplo usa 9.001 para temperatura. Não escolha DPT apenas pelo tamanho aparente do valor. | 9.001 |  |

### Exemplo: comando e retorno distintos

O exemplo descreve um TAG Double DPT 9.001 com grupo de comando 1/2/3 e retorno 1/2/4. Substitua pelos grupos existentes no projeto KNX. Não é pacote de importação e não contém material de segurança. Para ponto gravável, grupos iguais são rejeitados; o retorno precisa existir no equipamento.

```json
{"writeGroupAddress":"1/2/3","stateGroupAddress":"1/2/4","dpt":"9.001","tagType":"Double"}
```

### Verifique por etapas

Confira primeiro disponibilidade do processo auxiliar e referências protegidas, depois host/porta/usuário/endereço individual e grupos/DPT/tipo do TAG. A autoria pelo assistente atual precisa da correção indicada acima. Com vínculo corretamente validado, salve/publique/ative pelo projeto e confira o retorno antes de escrever. Um envio aceito sem retorno não deve atualizar artificialmente o estado. Este driver não anuncia descoberta de Engenharia.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.knx-setup)

## Home Assistant Bridge: conectar e escolher entidades

Uma fonte conecta ao Home Assistant gerenciado pelo usuário; somente entidades selecionadas entram no projeto.

<!-- HELP-REVIEW-AFTER-PRODUCT-785: https://github.com/brunolrogerio-collab/EliteSCADA/issues/785; topic=drivers.homeassistant-setup; fields=selectedEntityIds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Bridge local, com identidade explícita

HAB significa Home Assistant Bridge; HA no SCADIVO continua significando Alta Disponibilidade. Use Home Assistant alcançável pelo servidor e token autorizado, provisionado como material protegido. A integração não gerencia contas Home Assistant, não copia sua base interna e não transforma todas as suas integrações em drivers nativos SCADIVO.

### Conexão, identidade e limite atual da seleção

Teste de conexão confirma transporte/autenticação, não incorporação. Na versão atual, a tela genérica de descoberta não fornece seleção de entidades; uma seleção vazia não significa necessariamente falha de rede. Para um TAG conhecido, configure sua fonte e vínculo explicitamente no editor de TAGs e confira a leitura antes de ativar. Não há importação automática da instância inteira.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host Home Assistant | Nome DNS ou IP acessível pelo servidor SCADIVO. Use o equipamento ou serviço de origem, sem caminho de URL; localhost é o servidor/contêiner, não o navegador. | 192.0.2.30 |  |
| Porta | Porta do serviço de origem, diferente das portas da interface e API SCADIVO. Ao mudar TLS, confirme também a porta real do serviço. | 8123 | 1–65535. |
| Usar TLS | Protege o transporte quando o serviço oferecer TLS. Não instala certificados nem muda a porta automaticamente. | false |  |
| Caminho base | Opcional: prefixo de caminho quando Home Assistant está publicado por proxy. O cliente acrescenta /api/websocket. Sem prefixo, deixe vazio; não repita o endpoint completo. |  |  |
| Referência do token Home Assistant | Referência do material protegido provisionado no servidor para esta integração. Peça a referência ao administrador; não cole senha, token ou chave secreta em configurações comuns nem no manual da aplicação. |  |  |
| Tempo para conectar (ms) | Prazo de estabelecimento da conexão WebSocket. Separado do timeout de cada solicitação. | 10000 | 100–300000. |
| Timeout de resposta (ms) | Prazo para a resposta de uma solicitação; não é o período de medição nem a prova de atuação física. Um valor pequeno demais pode recusar uma origem lenta. | 10000 | 100–300000. |
| Espera mínima para reconectar (ms) | Pausa mínima após perda de conexão; evite tentativas contínuas. Não resolve autenticação recusada. | 1000 | 100–300000. |
| Espera máxima para reconectar (ms) | Limite da espera progressiva; precisa ser maior ou igual à espera mínima. | 30000 | 100–3600000. |
| Entidade e campo | Use entidade exata e campo: entity:sensor.temperature:state para estado; entity:light.room:attribute:brightness para atributo. Nome de exibição não é entity_id. Renomear entity_id no Home Assistant exige revisar o vínculo. | entity:sensor.temperature:state |  |
| Seleção de entidades para incorporar | O driver exige uma seleção explícita, mas a tela Home/Building desta versão ainda não permite escolher as entidades. A descoberta pode devolver seleção vazia sem dispositivo para incorporar, mesmo com conexão válida. Não há seletor pronto nessa tela; a correção está pendente. |  |  |

### Exemplos de endereço

Use state para o estado da entidade e attribute para um atributo existente. O exemplo mostra uma leitura de sensor e brilho de uma luz; não é arquivo de importação. Confira tipo e unidade: leitura numérica não é texto de apresentação, e os comandos de brilho usam a conversão suportada pelo driver. Renomear a entidade na origem exige revisar vínculos.

```json
{"state":"entity:sensor.temperature:state","attribute":"entity:light.room:attribute:brightness"}
```

### Comandos e indisponibilidade

O suporte é limitado aos vínculos implementados: switch/light state, brilho e cor de light e posição de cover, conforme o equipamento. Não existe chamada arbitrária de qualquer serviço Home Assistant por este formulário. unknown e unavailable não equivalem a zero ou desligado. Após comando, confira o estado autoritativo; sucesso da solicitação não comprova atuação física.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.homeassistant-setup)

## ESPHome: API nativa, entidades e suspensão

Conecte diretamente ao dispositivo e incorpore somente as funções que o driver mapeia.

### O que precisa estar disponível

O firmware precisa oferecer a API nativa ESPHome. O driver se conecta pela rede local sem exigir Home Assistant, Dashboard ESPHome ou processo Python auxiliar. No modo Noise, a chave da API deve estar provisionada no servidor; o navegador não conversa diretamente com o microcontrolador.

### Conexão e vínculo

Prepare a fonte, teste a conexão e use a descoberta para obter as chaves reais das entidades. Os campos avançados não corrigem uma API desabilitada ou chave incompatível.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host ESPHome | Nome DNS ou IP acessível pelo servidor SCADIVO. Use o equipamento ou serviço de origem, sem caminho de URL; localhost é o servidor/contêiner, não o navegador. | 192.0.2.30 |  |
| Porta | Porta do serviço de origem, diferente das portas da interface e API SCADIVO. Ao mudar TLS, confirme também a porta real do serviço. | 6053 | 1–65535. |
| Criptografia da API | noise é o modo preferido com chave provisionada. plaintext é opção explícita para compatibilidade em rede confiável e precisa corresponder ao firmware. Não confunda a chave Noise com senha Wi-Fi. | noise |  |
| Referência da chave Noise | Referência do material protegido provisionado no servidor para esta integração. Peça a referência ao administrador; não cole senha, token ou chave secreta em configurações comuns nem no manual da aplicação. |  |  |
| Timeout de resposta (ms) | Prazo para a resposta de uma solicitação; não é o período de medição nem a prova de atuação física. Um valor pequeno demais pode recusar uma origem lenta. | 5000 | 100–300000. |
| Prazo de confirmação da escrita (ms) | Quanto aguardar o estado autoritativo após comando. Aceitação do envio não é feedback; aumentar o prazo não garante movimento/atuação. | 3000 | 100–300000. |
| Espera mínima para reconectar (ms) | Pausa mínima após perda de conexão; evite tentativas contínuas. Não resolve autenticação recusada. | 500 | 100–300000. |
| Espera máxima para reconectar (ms) | Limite da espera progressiva; precisa ser maior ou igual à espera mínima. | 30000 | 100–3600000. |
| Keep Alive (ms) | Intervalo de supervisão da conexão; não aumenta a frequência de medição do sensor. | 30000 | 1000–300000. |
| Política de suspensão | normal para dispositivos contínuos; expected somente para nós que dormem de propósito. Muda os padrões de reconexão: mínimo 500→10000 ms, máximo 30000→60000 ms. Ausência continua indisponível, nunca saudável. | normal |  |
| Endereço da entidade | Formato tipo:device-id:chave-hex.campo. A descoberta fornece a chave de 8 dígitos hexadecimais; não use o nome de exibição como chave. Exemplo switch:0:00000010.state. | switch:0:00000010.state |  |
| Tipo de escrita | Define uma escrita suportada para a entidade, não um serviço arbitrário: switchstate, lightstate, lightbrightness, coverposition, fanstate, fanspeedlevel, numberstate ou selectstate. Prefira o vínculo gerado pela descoberta. O tipo de entidade/valor e ReadOnly continuam sendo conferidos. | switchstate |  |

### Exemplo de entidade

Este endereço representa state de uma entidade Switch, device-id 0, chave hexadecimal 00000010. Use a chave retornada pelo seu dispositivo; não invente IDs. Um TAG Boolean gravável exige o tipo de escrita compatível. Não é pacote de importação.

```json
{"address":"switch:0:00000010.state","writeKind":"switchstate"}
```

### Mapa de funções e diagnóstico

A materialização inclui entidades de processo suportadas, como sensor, binary sensor, switch, light, cover, fan, number e select. Entidades de configuração/diagnóstico e funções não mapeadas podem ser omitidas com aviso; encontrar climate, lock ou button no inventário não significa controle completo dessas funções. Primeiro confira API, modo/chave e conexão; depois endereço/tipo e avisos da descoberta. Para nó em suspensão, expected evita tratar o ciclo como conexão contínua, mas continua mostrando ausência como indisponível.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.esphome-setup)

## Shelly Gen2+: componentes, medição e comandos

Use RPC local e revise os canais e campos efetivamente oferecidos pelo dispositivo.

### Conexão local, com geração compatível

Este driver usa RPC de Shelly Gen2+ por HTTP/WebSocket; não é API de nuvem e não promete compatibilidade com todo Shelly Gen1. Tenha o host local, porta e autenticação do dispositivo. Um componente representa um canal/função do dispositivo, não um usuário ou Local do SCADIVO.

### Campos da fonte e do TAG

Teste a conexão e descubra o dispositivo para obter os componentes reais. A prévia mostra somente as medições e comandos mapeados; nome amigável não substitui component key.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Host do Shelly | Nome DNS ou IP acessível pelo servidor SCADIVO. Use o equipamento ou serviço de origem, sem caminho de URL; localhost é o servidor/contêiner, não o navegador. | 192.0.2.30 |  |
| Porta | Porta do serviço de origem, diferente das portas da interface e API SCADIVO. Ao mudar TLS, confirme também a porta real do serviço. | 80 | 1–65535. |
| Usar TLS | Protege o transporte quando o serviço oferecer TLS. Não instala certificados nem muda a porta automaticamente. | false |  |
| Usuário do dispositivo | Conta de autenticação RPC do Shelly; padrão admin. Não é o usuário do SCADIVO nem a conta de nuvem Shelly. | admin |  |
| Referência da senha Shelly | Referência do material protegido provisionado no servidor para esta integração. Peça a referência ao administrador; não cole senha, token ou chave secreta em configurações comuns nem no manual da aplicação. |  |  |
| Timeout de resposta (ms) | Prazo para a resposta de uma solicitação; não é o período de medição nem a prova de atuação física. Um valor pequeno demais pode recusar uma origem lenta. | 5000 | 100–300000. |
| Espera mínima para reconectar (ms) | Pausa mínima após perda de conexão; evite tentativas contínuas. Não resolve autenticação recusada. | 1000 | 100–300000. |
| Espera máxima para reconectar (ms) | Limite da espera progressiva; precisa ser maior ou igual à espera mínima. | 30000 | 100–3600000. |
| Componente e campo | Formato componente.campo, por exemplo switch:0.output ou switch:0.aenergy.total. O número identifica o canal; não use o nome amigável como endereço. | switch:0.output |  |
| Método RPC de escrita | Para vínculo gravável, método suportado pelo equipamento e pelo driver; no exemplo Switch.Set. Prefira o vínculo gerado pela descoberta, sem assumir que qualquer método pode virar um comando. | Switch.Set |  |
| Parâmetro RPC de escrita | Campo que recebe o valor da escrita; Switch.Set usa on para o Boolean. Exige TAG gravável, método compatível e autorização. O estado confirmado é lido separadamente. | on |  |

### Exemplo: saída do canal zero

O exemplo vincula output de switch:0 a um TAG Boolean; a escrita compatível usa Switch.Set com parâmetro on. Confira o canal real antes de criar comandos. O JSON ilustra o vínculo, não é requisição para enviar diretamente nem pacote de importação.

```json
{"address":"switch:0.output","writeMethod":"Switch.Set","writeParameter":"on"}
```

### Conferir medição e feedback

Os campos dependem do modelo: por exemplo output, potência apower, tensão voltage, corrente current e energia aenergy.total quando presentes. O driver também mapeia funções limitadas de light/cover e sensores suportados. Campo ausente não vira leitura zero; veja avisos e tipo/unidade. Na escrita, confira resposta e estado confirmado separadamente. Se falhar a autenticação, confira a referência protegida e a conta do dispositivo, sem trocar para credenciais de nuvem.

[Abrir este guia na Ajuda instalada](/help?topic=drivers.shelly-setup)
