# ADR-002 — Driver SDK e caminho de dados em tempo real

Status: Aceito

## Decisão

Todos os protocolos de comunicação ativos implementam `ICommunicationDriver` como boundary de Runtime. O Core não depende de nenhum protocolo concreto. Drivers publicam valores exclusivamente pelo caminho comum de TAG/current cache/eventos; Historian, Alarm Engine, Realtime e Gateway não conhecem bibliotecas de protocolo.

O Runtime Web recebe alterações pelo endpoint WebSocket `/ws/tags`. REST permanece disponível para descoberta de recursos do produto, leitura pontual e escrita autorizada. O frontend nunca conversa diretamente com PLCs, brokers ou servidores industriais.

### Runtime e Engineering são superfícies diferentes

A experiência acumulada e as pesquisas de MQTT, OPC UA, BACnet, Siemens S7 e Allen-Bradley mostraram que discovery, browse, connection test, importação de projeto e reconciliação não devem ser métodos obrigatórios do driver ativo.

O Driver SDK separa:

- `ICommunicationDriver` para ciclo de vida e operações do Runtime;
- `ICommunicationDiagnosticsSource` para diagnóstico protocol-neutral opcional de Data Sources externos;
- descriptors e interfaces de Engineering específicas por capacidade, definidas em ADR-009 e `DriverEngineeringContracts.cs`.

Assim, um protocolo implementa apenas discovery/browse/import/reconcile que realmente possui. Resultados dessas ferramentas são candidatos transitórios e entram no modelo canônico somente por validação/Preview/Apply.

### Aquisição não altera o pipeline

Drivers podem adquirir valores por polling, subscription, event-driven ou modelo híbrido. Isso não cria caminhos alternativos para TAGs.

Exemplos esperados:

- Modbus/S7: polling;
- OPC UA: subscriptions/monitored items;
- MQTT: eventos de broker;
- BACnet: COV com polling fallback onde aplicável.

Em todos os casos:

`Driver/Data Source -> TAG current cache -> Event Bus -> consumidores do produto`

### Timestamps e qualidade

`TagValue.Timestamp` é o tempo local de observação/publicação do EliteSCADA. Protocolos que fornecem tempo de origem ou de servidor podem preencher `SourceTimestamp` e `ServerTimestamp` separadamente.

Qualidade é semântica EliteSCADA. Estado de conexão, MQTT QoS, OPC UA StatusCode, BACnet Reliability e outras evidências de protocolo são mapeadas deliberadamente; nenhuma delas substitui automaticamente `TagQuality`.

## Fluxo

`Canonical Engineering -> DriverHost/compiler -> ICommunicationDriver/Data Source -> Current Tag Cache -> Event Bus -> Historian/Alarmas/Realtime/Gateway`

Engineering discovery/browse/import segue uma superfície separada e protegida:

`Driver Engineering adapter -> candidates -> validate -> preview -> apply -> Canonical Engineering`

## Consequências

- Modbus, MQTT, OPC UA, BACnet, S7 e Allen-Bradley podem ser adicionados sem alterar o TAG Engine.
- Bibliotecas concretas de protocolo permanecem substituíveis.
- Historian e Alarm Engine assinam o mesmo evento sem conhecer drivers.
- Web Runtime não consulta banco para obter valor atual.
- Engineering tooling não precisa ativar um Runtime para testar/buscar/importar um equipamento.
- Discovery/browse nunca se tornam uma segunda fonte de verdade.
- Detalhamento normativo complementar: `docs/ADR-009-DRIVER-SDK-ENGINEERING-BOUNDARIES.md` e `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`.
