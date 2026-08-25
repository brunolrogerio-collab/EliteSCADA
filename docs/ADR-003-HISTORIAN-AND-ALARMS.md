# ADR-003 — Historian assíncrono e Alarm Engine desacoplado

## Status
Aceito para o marco 0.1-dev.

## Decisão
O histórico é consumido a partir do Event Bus e exposto ao restante do sistema pela interface `IHistorian`. A primeira implementação é `BufferedInMemoryHistorian`, com `Channel<T>` para desacoplar aquisição e persistência. PostgreSQL/TimescaleDB será uma implementação posterior da mesma abstração.

Alarmes são avaliados por um `IAlarmEngine` independente do frontend e dos drivers. O motor consome `TagValueChanged`, mantém estado de alarme e publica `AlarmStateChanged`.

## Consequências
- Um banco lento não deve bloquear diretamente o scan de um driver.
- A troca do armazenamento do histórico não altera o Tag Engine.
- Alarmes podem futuramente ser persistidos, transmitidos por WebSocket e auditados sem acoplamento ao protocolo de origem.
- O histórico em memória desta etapa é deliberadamente transitório e serve apenas para validar fluxo e API.
