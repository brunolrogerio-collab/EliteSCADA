# Scripting, Alarmes e Histórico

## Server Scripts

Server Scripts habilitados e de scope Server são hospedados pela revisão Active.

A superfície pública documentada neste build contém exatamente:

- `read_tag(tag_id)`
- `read_server_memory(tag_id)`
- `write_tag(tag_id, value)`
- `write_server_memory(tag_id, value)`
- `publish_server_memory_sample(tag_id, value, quality)`
- `emit_operational_event(definition_id, message=None, context=None)`

TAGs acessados devem ser dependências declaradas. Funções de Server Memory aceitam referências de ServerMemoryTag.

### Triggers de Runtime

O host atual despacha:

- `Initialize`
- `Dispose`
- `TagChanged`
- `Timer`
- `ServerRuntimeEvent`

Quando Active muda, a geração anterior é cancelada. Acesso a TAG e emissão de evento usam gates de revisão para impedir execução obsoleta contra a nova revisão.

### Política de falha e isolamento

A política padrão registrada no produto usa timeout de handler de 250 ms, fila limitada a 128 eventos, Timer mínimo de 50 ms e cooldown após 5 falhas consecutivas. Ao fim do cooldown configurado, um evento pode atuar como probe de recuperação.

Esses limites isolam a instância do script; não concedem fallback de Authority.

### Sandbox

A superfície de Server Script não oferece acesso arbitrário a:

- filesystem;
- sistema operacional;
- shell/processos;
- rede arbitrária;
- banco de dados direto;
- Drivers industriais diretamente;
- secrets;
- DOM do navegador;
- browser storage.

O preflight de editor ajuda a rejeitar imports/calls obviamente proibidos, mas o enforcement de segurança não depende apenas de análise textual.

### Procedimento: escrever um Script seguro que reage a TAG

**Objetivo:** ler dados declarados e escrever somente uma saída autorizada.

**Pré-requisitos:** referências de TAG estáveis e declaradas; destino gravável e autorizado.

**Passos:**
1. Declare as dependências necessárias.
2. Leia o TAG usando `read_tag`.
3. Calcule o resultado sem acessar recursos externos proibidos.
4. Escreva somente TAG autorizado com `write_tag`.
5. Valide sintaxe/referências na superfície de Script Engineering.
6. Salve/publique/ative pelo lifecycle normal.

**Resultado esperado:** o script roda somente na revisão Active e permanece limitado às APIs públicas.

**Troubleshooting:** erro de referência, sandbox ou autorização deve ser corrigido na origem; não substitua por acesso direto ao Driver.

**Ajuda relacionada:** `/help?topic=scripts.server`.

## Client Memory

Client Memory é memória não retentiva pertencente ao cliente Runtime. Server Memory é retentiva e pertencente ao servidor. Ambas são source providers internos e não são Drivers de comunicação.

**Ajuda relacionada:** `/help?topic=sources.internal-memory`.

## Alarmes, Eventos Operacionais e Audit

Os três domínios são distintos:

- **Alarm**: condição operacional configurada de alarme.
- **Operational Event**: ocorrência operacional explícita definida pelo produto/aplicação.
- **Audit**: rastreabilidade de ações de segurança/Engineering/operação.

Não use Alarm como log genérico e não use Operational Event como substituto de Audit.

**Ajuda relacionada:** `/help?topic=alarms.overview`, `/help?topic=operational-events.overview`, `/help?topic=audit.overview`.

## Historian e Trends

Historian recebe valor, quality e timestamp pelo pipeline do produto. Trends apresentam dados atuais ou históricos autorizados; não redefinem quality, timestamp, scaling ou persistência.

**Ajuda relacionada:** `/help?topic=historian.overview`, `/help?topic=trends.overview`.

## Historical Time Range e Playback

A estrutura desses capítulos já está reservada, mas os procedimentos finais estão bloqueados até a aceitação de #383 e do workstream de Playback subsequente. Não use este manual como indicação de que a UX final desses recursos já está congelada.
