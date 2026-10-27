# Scripting, Alarmes e Histórico

## Comece pela tarefa

Um Script é um pequeno programa da aplicação. Você não precisa começar por todos os comandos: escolha uma tarefa que consiga conferir, como arredondar uma leitura. Depois aprenda como chamar a função e selecionar as variáveis usadas por ela.

Para calcular no servidor sem depender de uma tela aberta, escolha **Server**. Para responder a um clique ou mudar a aparência na tela aberta, escolha **Client Visual**. Cada um oferece funções próprias. Os exemplos indicam qual opção usar; mantenha essa escolha ao adaptá-los.

### Primeiro exercício: arredondar uma leitura

Use uma aplicação de teste. Neste exemplo, `Plant.Pump.Current` é a variável de entrada e `State.Pump.LastCurrent` guarda o resultado na memória do servidor. Substitua as referências pelas que selecionar no seu projeto.

1. Prepare um TAG numérico para a leitura e um TAG numérico de memória do servidor para o resultado. Declare ambos nas referências do Script; o destino deve ser do tipo `ServerMemoryTag`.
2. Crie um Script com escopo **Server** e habilite-o. Cadastre uma entrada **Timer**, por exemplo com intervalo de `1000 ms`. Timer chama a função periodicamente; não é preciso escrever um laço.
3. No campo **Handler**, informe `on_timer`. Handler é o nome da função a chamar. Cole o código abaixo e valide.
4. Faça o teste disponível com um valor conhecido e confira o resultado. Para usá-lo no Runtime, aplique a edição, salve, publique e ative a revisão pelos controles do projeto.

```python
def on_timer():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        rounded = round(current, 2)
        write_server_memory("State.Pump.LastCurrent", rounded)
```

Leia o código em pequenas partes:

- `def on_timer():` define a função; os dois-pontos abrem seu bloco. As linhas com quatro espaços à esquerda pertencem à função.
- `read_tag(...)` lê a referência entre aspas. `current` é a variável local em que o código guarda essa leitura.
- `if current is not None:` verifica se há valor. `None` é ausência, não o número zero. Sem valor, a função termina sem gravar outro resultado.
- `round(current, 2)` arredonda para duas casas e guarda em `rounded`. Com entrada `12.346`, o resultado esperado é `12.35`.
- `write_server_memory(...)` grava o resultado no destino de memória declarado. Isso não envia um comando à bomba.

O exercício verifica o cálculo; a função `read_tag` do Server retorna só o valor, sem certificar qualidade ou horário da leitura. Se não houver nova gravação, a memória conserva seu último valor. A aplicação precisa mostrar separadamente quando o dado está indisponível.

Se nada executar, confira primeiro **Handler**, **Timer**, referências e revisão ativa. Se o editor indicar erro, comece pela primeira linha apontada. Depois do exercício, use as seções seguintes como consulta: limites da linguagem, demais gatilhos, funções, comandos e tratamento de falhas.

## Server Scripts

No Server, o produto chama a função quando ocorre o evento configurado. A função usa as referências que você declarou e os comandos disponíveis neste ambiente. Ele aceita parte da sintaxe Python; as limitações estão detalhadas abaixo. Não permite instalar bibliotecas externas.

Scripts de scope Server habilitados são hospedados pela revisão **Active**. Ao ativar outra revisão, a geração anterior é cancelada; leituras, escritas e emissão de eventos da geração antiga não devem continuar agindo sobre a nova revisão.

### Antes de escrever: conceitos que evitam erros

- **Handler** é a função chamada por um ponto de entrada. O nome configurado no Engineering deve coincidir exatamente com o nome da função.
- **Ponto de entrada/trigger** diz quando chamar o handler: inicialização, alteração de TAG, intervalo de Timer ou evento de Runtime.
- **Dependência** declara qual TAG ou comando o Script pode usar. Colocar o caminho no código não declara acesso.
- **Binding legível** é um caminho como `Plant.Pump.DischargePressure` ligado a uma identidade estável. Quando esse binding está configurado, o código pode usar o caminho; o produto ainda valida a identidade. Um ID estável também é aceito.
- **Server Memory** é memória retentiva do servidor. Seus TAGs são dependências do tipo `ServerMemoryTag`; não confunda com Client Memory, que pertence ao Runtime do cliente.
- **Active** é a revisão que o Runtime está executando. Salvar ou editar código, por si só, não significa que a nova versão já esteja Active.

### Como escrever um handler

No nível principal do arquivo, declare funções. Não coloque leituras, escritas ou cálculos soltos fora de uma função. Um handler comum pode não receber argumentos. `ServerRuntimeEvent` fornece um dicionário de evento e, nesse caso, o handler recebe um argumento:

```python
def on_start():
    pressure = read_tag("Plant.Pump.DischargePressure")

def on_device_event(event):
    action = event["canonicalEvent"]["payload"]["action"]
```

O segundo exemplo mostra o formato de um evento canônico usado pelo host; só use campos presentes no evento real configurado para o seu projeto. Acesso por chave ausente falha o handler.

### Triggers disponíveis

| Trigger | Quando usar | Observação |
| --- | --- | --- |
| `Initialize` | Inicializar o estado da instância quando ela começa | Não presuma que o código roda uma vez para sempre; nova geração pode inicializar novamente. |
| `Dispose` | Encerrar/limpar o estado da instância | A geração pode já estar sendo cancelada; mantenha o handler curto. |
| `TagChanged` | Recalcular uma regra quando o TAG associado mudar | Declare como dependências todos os TAGs lidos e escritos. |
| `Timer` | Fazer uma verificação periódica independente de uma tela aberta | Configure intervalo de pelo menos 50 ms ou o mínimo indicado pelo produto. Não crie laço nem espere dentro do Script. O Timer não sincroniza com o ciclo de aquisição do Driver. |
| `ServerRuntimeEvent` | Reagir a uma ocorrência canônica de Transient Event | Associe o evento real no grafo Active; targets genéricos não acionam o handler. |

### Sintaxe: o que funciona e o que não funciona

O subconjunto atual aceita:

- `def` sem decorators, valores padrão, parâmetros positional-only ou keyword-only;
- atribuição local e `+=`, `-=`, `*=`;
- `if`/`else`, `return` e `pass`;
- listas, tuplas, dicionários e acesso por índice, por exemplo `event["payload"]["action"]`;
- `+`, `-`, `*`, `/`, `//`, `%`, `**`; comparações `==`, `!=`, `<`, `<=`, `>`, `>=`, `is`, `is not`;
- `not`, `and`, `or`, chamadas posicionais a funções do próprio Script e a `min`, `max`, `abs`, `round`, `int`, `float`, `bool`, `str`, `len`.

Não aceita `import`, código/variáveis no nível principal, `for`, `while`, comprehensions, `class`, `try/except`, `lambda`, f-strings, acesso a atributos, decorators, argumentos nomeados ou bibliotecas Python externas. Mesmo código sintaticamente aceito pode falhar por tipo incompatível, divisão por zero ou chave/índice inexistente.

**Diferença importante:** neste interpretador, `and` e `or` avaliam todos os operandos; não têm o curto-circuito do Python normal. Evite usá-los para proteger uma operação que pode falhar. Escreva `if` aninhado:

```python
def evaluate_pressure():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    if pressure is not None:
        if high_limit is not None:
            above_limit = pressure >= high_limit
```

### API de TAG, Server Memory e Operational Event

Estas são as seis funções públicas documentadas para TAG, memória e evento. Todas as referências precisam ser dependências declaradas; para caminho legível, o binding precisa corresponder à identidade estável. Em falha de referência, corrija a dependência/binding — não tente contornar pelo Driver.

| Função | O que recebe e retorna | Regras principais |
| --- | --- | --- |
| `read_tag(tag_reference)` | Caminho vinculado ou ID estável; retorna o valor corrente. | Não retorna quality nem timestamp da origem. Só lê dependência declarada. |
| `read_server_memory(tag_reference)` | Caminho vinculado ou ID estável; retorna o valor corrente de Server Memory. | Exige dependência declarada do tipo `ServerMemoryTag`. |
| `write_tag(tag_reference, value)` | TAG declarado e valor compatível com seu tipo; retorna `None`. | Exige TAG gravável e autorização no escopo correspondente. Não muda diretamente um objeto visual. |
| `write_server_memory(tag_reference, value)` | ServerMemoryTag declarado e valor serializável; retorna `None`. | Grava memória do servidor, não um TAG de processo. |
| `publish_server_memory_sample(tag_reference, value, quality)` | ServerMemoryTag, valor serializável e nome canônico de `TagQuality`; retorna `None`. | A quality deve ser exatamente um valor suportado, por exemplo `Good`, `Uncertain`, `Bad`, `BadCommunication`, `BadConfiguration`, `BadDevice`, `Stale`, `Disabled` ou `Unavailable`. O Script informa a quality; a função não a descobre na origem. |
| `emit_operational_event(definition_id, message=None, context=None)` | ID estável da definição; texto opcional; dicionário opcional com valores de texto; retorna `None`. | A definição precisa existir. Entradas têm limites de quantidade/tamanho. Operational Event não substitui Audit nem Alarm. |

### Exemplo 1: derivar um estado visual

Use este padrão quando quiser avaliar um valor e escrever o resultado em um TAG gravável que já esteja ligado à propriedade visual. Declare pressão, limite e saída como dependências; configure a saída com tipo compatível (neste exemplo, booleano).

```python
def update_pressure_indicator():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    if pressure is not None:
        if high_limit is not None:
            write_tag("Plant.Screen.PumpPressureHigh", pressure >= high_limit)
```

Se qualquer entrada não estiver disponível, o handler não sobrescreve a saída com `False`; isso evita transformar dado ausente em indicação de condição normal. O Server Script não altera diretamente o objeto da tela: o TAG de saída precisa estar vinculado à propriedade visual.

### Exemplo 2: histerese e Operational Event sem repetição contínua

Use um estado retentivo para registrar se a condição já foi sinalizada. Configure `TagChanged` para o TAG de pressão e declare os dois limites, o `ServerMemoryTag` de estado e a definição de evento como dependências aplicáveis. O limite de retorno deve ser menor que o limite de disparo.

```python
def on_pressure_change():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    reset_limit = read_tag("Limits.Pump.ResetPressure")
    event_sent = read_server_memory("State.Pump.HighPressure")

    if pressure is not None:
        if high_limit is not None:
            if reset_limit is not None:
                if event_sent is not True:
                    if pressure >= high_limit:
                        write_server_memory("State.Pump.HighPressure", True)
                        emit_operational_event(
                            "<stable-event-definition-id>",
                            "Pump discharge pressure crossed the high limit",
                            {"pressure": str(pressure)}
                        )
                else:
                    if pressure <= reset_limit:
                        write_server_memory("State.Pump.HighPressure", False)
```

Substitua o placeholder pelo ID estável real. O estado evita emitir o mesmo evento em cada mudança enquanto a pressão permanece acima do limite. Isso é lógica de aplicação, não substitui configuração de Alarm quando o requisito é um alarme.

### Exemplo 3: amostra calculada e qualidade explícita

`read_tag` não expõe se a fonte está `Good`, `Stale` ou qual é o timestamp de origem. Portanto, verificar apenas `value is not None` **não** prova que a amostra é boa. Se a aplicação não tem como confirmar quality e atualidade, publique uma quality conservadora apropriada ou não publique:

```python
def publish_unverified_sample():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        publish_server_memory_sample(
            "State.Pump.LastCurrent",
            current,
            "Uncertain"
        )
```

Use `"Good"` apenas quando uma regra validada do projeto estabelecer a confiabilidade do valor e não depender de informações de quality/timestamp que o Script não recebe. Nunca substitua valor ausente por zero para forçar uma amostra.

### Exemplo 4: Timer sem laço

Configure o ponto de entrada `Timer` no Engineering. O host dispara o handler; o código não fica rodando continuamente:

```python
def on_timer():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        write_server_memory("State.Pump.LastCurrent", current)
```

Declare ambos os TAGs e confirme que o destino é `ServerMemoryTag`. Se precisa detectar mudanças, considere `TagChanged`; não reduza o intervalo do Timer sem necessidade, porque timers rápidos geram trabalho adicional no servidor.

### Rich Command: operação explicitamente autorizada

Além das seis APIs acima, a integração do host disponibiliza `invoke_rich_command` para uma dependência Rich Command declarada. Isso **não** é acesso direto a Driver. Configure o binding real do comando, associe um `ServerRuntimeEvent` a um Transient Event real e conceda `CommandExecute` no escopo exato. Os parâmetros devem ser valores escalares compatíveis com o schema do comando.

O resultado distingue `Rejected`, `Accepted`, `Completed`, `Failed`, `TimedOut` e `Unknown`. `Accepted` significa que a solicitação foi aceita, não que o equipamento concluiu a ação. `Unknown` é ambíguo: não repita automaticamente, pois o comando pode ter sido executado apesar da resposta perdida. Faça reconciliação por feedback independente do processo antes de decidir uma nova ação.

```python
def on_device_event(event):
    action = event["canonicalEvent"]["payload"]["action"]
    if action == "single":
        result = invoke_rich_command(
            "<stable-rich-command-guid>",
            {"onTime": "00:00:00.2500000"}
        )
        if result["outcome"] == "Completed":
            write_server_memory("State.Command.LastOutcome", 1)
        else:
            write_server_memory("State.Command.LastOutcome", -1)
```

Este exemplo registra sucesso apenas para `Completed`; qualquer outro resultado fica marcado como não concluído/pendente de diagnóstico. Não use o valor `-1` como prova de falha física: para distinguir os resultados, armazene ou publique o estado segundo o modelo de dados da aplicação. Substitua comando, parâmetro e campos do evento pelos objetos reais. A operação ainda passa pelas validações e autorizações normais do produto.

### Fluxo recomendado no Engineering

1. Defina o objetivo e o efeito permitido: apenas calcular, gravar memória, gravar um TAG de processo ou solicitar Rich Command.
2. Crie/configure o Script e escolha scope `Server`.
3. Declare cada TAG, ServerMemoryTag, evento ou comando necessário. Confira tipo, binding, identidade estável, gravação permitida e escopo de Authority.
4. Crie um handler e vincule seu nome exato ao trigger. Para `ServerRuntimeEvent`, confira o evento canônico e os campos disponíveis.
5. Escreva primeiro a leitura e os casos de dado ausente; depois o cálculo; por último a escrita/emissão.
6. Valide a sintaxe e as dependências no editor. Preflight é feedback; a autorização e o sandbox continuam sendo aplicados no runtime.
7. Teste com valores normais, limites, valores ausentes, tipos inesperados e falha/timeout de comando. Confirme cada efeito escrito e cada evento.
8. Salve, gere a revisão e siga o fluxo normal de publicação/ativação. Verifique que a revisão esperada se tornou `Active`.

### Execução, falhas e sandbox

A política padrão documentada usa timeout de handler de 250 ms, fila limitada a 128 eventos, Timer mínimo de 50 ms e cooldown após 5 falhas consecutivas. Depois do cooldown configurável, um evento pode ser aceito como probe de recuperação. Mantenha cada handler curto; o orçamento determinístico também limita o número de passos.

Server Script não fornece acesso arbitrário a filesystem, sistema operacional, shell/processos, rede, banco de dados, Drivers, secrets, DOM ou browser storage. O preflight ajuda a detectar alguns erros cedo, mas não é a fronteira de segurança: o enforcement é do runtime.

### Problemas comuns

| Sintoma | Verifique |
| --- | --- |
| TAG “não declarado” ou caminho rejeitado | A dependência existe? O binding legível está configurado e aponta para a mesma identidade estável? O ID está correto? |
| Handler não é chamado | O nome de função coincide exatamente com o ponto de entrada? O Script está habilitado e scope Server? A revisão esperada está Active? O trigger está ligado ao objeto/evento correto? |
| Falha ao ler/escrever Server Memory | A dependência é realmente `ServerMemoryTag`? Escrita e valor correspondem ao contrato? |
| Saída visual não muda | O destino é TAG gravável e autorizado? Ele está ligado à propriedade do objeto? Server Script não altera objeto visual diretamente. |
| Script compila, mas handler falha | Confira tipos, `None`, chave/índice do evento, divisão por zero e limites; lembre que `and/or` não fazem curto-circuito. |
| Comando retorna `Accepted`, `TimedOut` ou `Unknown` | Não presuma conclusão física nem repita automaticamente. Consulte feedback independente, estado do equipamento e auditoria. |
| Script entra em cooldown | Corrija a causa da falha, examine diagnostics e teste com dados representativos antes de reativar. |

**Ajuda relacionada:** `/help?topic=scripts.server`, `/help?topic=sources.internal-memory`, `/help?topic=operational-events.overview`, `/help?topic=alarms.overview`.

## Client Visual Scripts

No Client Visual, o evento e a amostra retornada pelas funções do produto são objetos estruturados: leia campos com ponto, como `event.visualObjectId`, `sample.quality` e `sample.value`. Não use acesso de dicionário nesses objetos. Dicionários criados no próprio Python e os eventos do Server seguem seus respectivos contratos.

Client Visual Script é Python executado no Runtime do cliente para interação com uma Screen/Popup aberta e para apresentação local. Ele tem escopo diferente de Server Script: não substitui automação do backend e não continua trabalhando sem o cliente. Server Script, revisão Active, triggers de backend e suas seis APIs estão descritos na seção anterior.

### Criar e ligar uma interação

1. Em Script Engineering, crie um Script e selecione escopo `Client Visual` e linguagem Python.
2. Declare um entry point `objectInteraction` com nome de handler, por exemplo `on_click`.
3. No painel Events do objeto da Screen/Popup, selecione `Click`, o Script e o handler.
4. Declare nas dependências do Script cada TAG lido ou escrito. O caminho legível usado no código precisa estar ligado à identidade estável do TAG.
5. Valide, salve no Working e use o fluxo normal de revisão/publicação/ativação antes de conferir no Runtime.

Os eventos de interação aceitos incluem `click`, `pointerenter`, `pointermove` e `pointerleave`. O handler recebe um dicionário de evento. Para uma interação visual, ele inclui chaves como `eventKey`, `visualDefinitionId`, `visualObjectId` e `pointer`.

### API disponível

Importe as funções fornecidas pelo módulo `elite_scada`:

| Função | Uso |
| --- | --- |
| `tag_read(reference)` | Lê a TAG permitida e retorna dados como `value`, `quality`, `timestamp` e `source`. |
| `tag_write(reference, value)` | Solicita escrita de valor Boolean, número finito ou texto. Exige TAG gravável, dependência e autorização Runtime. |
| `client_memory_read(reference)` / `client_memory_write(reference, value)` | Lê ou altera memória não retentiva do cliente. Não é Historian e não sincroniza clientes. |
| `visual_property_read(target, property)` / `visual_property_write(target, property, value)` | Lê ou solicita override de propriedade registrada no objeto visual do contexto atual. |
| `visual_property_clear(target, property)` | Remove o override temporário de Script para aquela propriedade. |
| `visual_tween_request(arguments)` | Solicita animação pela API visual permitida, sujeita à validação do Runtime. |

As chamadas de bridge são assíncronas. Use `async def` e `await`. A API não libera acesso direto a Drivers, banco, arquivos, sistema operacional, credenciais, DOM ou rede arbitrária. A escrita em TAG segue as permissões da sessão e o serviço normal do Runtime; existir botão ou Script não concede autorização.

### Exemplo: ler TAG e respeitar qualidade

Associe `on_click` ao evento Click e declare `Plant.Pump.DischargePressure` como dependência. Use a leitura somente quando a qualidade for `Good`; dado ausente ou quality diferente não é zero nem comprova normalidade.

```client-visual-python
from elite_scada import tag_read

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample.quality == "Good":
        pressure = sample.value
```

### Exemplo: guardar um valor na Client Memory

Configure a referência `UI.LastPressure` no provider Client Memory do projeto. Esse valor é local ao Runtime cliente e pode desaparecer quando a sessão/cliente termina.

```client-visual-python
from elite_scada import tag_read, client_memory_write

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample.quality == "Good":
        await client_memory_write("UI.LastPressure", sample.value)
```

### Exemplo: identificar a interação

Declare `on_pointer_enter` como handler `objectInteraction` e associe-o a `Pointer enters object` no painel Events.

```client-visual-python
async def on_pointer_enter(event):
    object_id = event.visualObjectId
    event_name = event.eventKey
```

Use pointer handlers para resposta breve. `pointermove` pode disparar repetidamente durante o movimento; não faça processamento demorado nem espere ativamente em cada chamada. Cada execução tem limite de tempo e falhas são isoladas do Runtime restante.

### Problemas comuns

| Sintoma | Verifique |
| --- | --- |
| `ImportError` | Use `elite_scada` e confira o nome exato da função oferecida ao Client Visual. Bibliotecas gerais e módulos arbitrários não fazem parte dessa API. |
| TAG não declarada ou caminho rejeitado | Adicione a dependência estável ao Script; confira binding legível e identidade do TAG. |
| Leitura existe, mas não deve atualizar a tela | Confira `quality` e `timestamp`; não trate leitura indisponível como zero/estado normal. |
| Escrita rejeitada | Confira se a TAG permite escrita, a capacidade da sessão, o valor compatível e o feedback do Runtime. |
| Handler não chamado | Confira escopo `Client Visual`, entry point `objectInteraction`, nome exato e associação Script/evento/objeto. |
| Interface lenta em interação | Reduza trabalho em `pointermove`, elimine loops/espera ativa e mantenha o handler curto. |

**Ajuda relacionada:** `/help?topic=scripts.client-visual`, `/help?topic=scripts.engineering`, `/help?topic=scripts.python-validation`, `/help?topic=visual.events`.

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

## Histórico: período e Playback

No Runtime, Histórico consulta registros de TAG, alarmes ou eventos. A consulta é somente leitura: não reconhece alarmes, não altera valores e não cria amostras retroativas. Para valores, Historian precisa ter capturado dados no período; alarmes e eventos também precisam ter sido registrados. A autorização da sessão continua valendo.

### Consultar um período

1. Abra **Histórico** no Runtime e escolha **Valores de TAGs**, **Alarmes** ou **Eventos**.
2. Selecione um período relativo ou absoluto. Use um atalho ou informe quantidade e unidade; no período absoluto, preencha **De** e **Até** no fuso indicado.
3. Confirme que o início antecede o fim. A janela aceita no máximo 31 dias. Se o horário local ficar ambíguo por mudança de fuso/horário de verão, escolha outro horário.
4. Selecione **Consultar**. Depois da primeira consulta, use busca, ordenação e filtros somente quando o conjunto oferecer esses campos; alguns aparecem apenas depois que o Runtime descobre o formato dos resultados.
5. Para investigar uma consulta vazia, confira conjunto, período, autorização, captura anterior ao intervalo, quality e timestamps.

### Rever a tela no Playback

Playback é diferente da consulta tabular do Histórico. Escolha o período e o instante na superfície operacional e selecione **Entrar no Playback**. O Runtime permanece Live até essa confirmação. No Playback, a apresentação acompanha o instante histórico e as ações operacionais ficam bloqueadas; selecione **Voltar ao Live** antes de retomar o controle atual. Playback não transforma valores antigos em comandos nem preenche dados que não foram capturados.

**Resultado esperado:** Histórico lista somente registros disponíveis; Playback mostra explicitamente o contexto histórico e permite voltar ao Live.

**Ajuda relacionada:** `/help?topic=runtime.history`, `/help?topic=historian.overview`, `/help?topic=trends.overview`.
