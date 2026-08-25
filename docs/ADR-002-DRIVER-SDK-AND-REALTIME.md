# ADR-002 — Driver SDK e caminho de dados em tempo real

Status: Aceito

## Decisão

Todos os protocolos implementam `ICommunicationDriver`. O Core não depende de nenhum protocolo concreto. Drivers registram TAGs no `ITagRegistry` e publicam valores exclusivamente por `ICurrentTagCache`, que dispara `TagValueChanged` no barramento interno.

O Runtime Web recebe alterações pelo endpoint WebSocket `/ws/tags`. REST permanece disponível para descoberta, leitura pontual e escrita autorizada.

O primeiro driver é `SimulationDriver`, usado para desenvolvimento, testes e demonstrações sem hardware.

## Fluxo

Driver -> Tag Registry/Current Tag Cache -> Event Bus -> WebSocket/consumidores futuros.

## Consequências

- Modbus, MQTT e OPC UA poderão ser adicionados sem alterar o Tag Engine.
- Historian e Alarm Engine poderão assinar o mesmo evento sem conhecer drivers.
- O Web Runtime não consulta banco para obter valor atual.
