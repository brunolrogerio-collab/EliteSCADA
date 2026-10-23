# Scripting, alarmas e histórico

## Scripts de servidor

Un Server Script es una automatización que se ejecuta en el servidor. Piense en él como una función pequeña invocada por un evento del producto: lee dependencias declaradas, calcula algo y usa las APIs permitidas. Aunque su sintaxis se parece a Python, el Runtime ejecuta un subconjunto determinista: no es Python general ni un lugar para instalar bibliotecas.

Los Scripts habilitados con alcance Server son alojados por la revisión **Active**. Al activar otra revisión, la generación anterior se cancela; las lecturas, escrituras y emisiones de eventos de la generación antigua no deben seguir actuando sobre la nueva revisión.

### Antes de escribir: conceptos que evitan errores

- **Handler** es la función llamada por un punto de entrada. El nombre configurado en Engineering debe coincidir exactamente con el nombre de la función.
- **Punto de entrada/trigger** indica cuándo llamar al handler: inicialización, cambio de TAG, intervalo de Timer o evento del Runtime.
- **Dependencia** declara qué TAG o comando puede usar el Script. Escribir una ruta en el código no declara acceso.
- **Binding legible** es una ruta como `Plant.Pump.DischargePressure` vinculada a una identidad estable. Cuando se configura este binding, el código puede usar la ruta; el producto sigue validando la identidad. También se acepta un ID estable.
- **Server Memory** es memoria retentiva del servidor. Sus TAG deben declararse como dependencias `ServerMemoryTag`; no la confunda con Client Memory, que pertenece al Runtime del cliente.
- **Active** es la revisión que ejecuta el Runtime. Guardar o editar código no hace que la nueva versión quede Active por sí solo.

### Cómo escribir un handler

En el nivel principal del archivo, declare funciones. No coloque lecturas, escrituras ni cálculos sueltos fuera de una función. Un handler común puede no recibir argumentos. `ServerRuntimeEvent` proporciona un diccionario de evento y, en ese caso, el handler recibe un argumento:

```python
def on_start():
    pressure = read_tag("Plant.Pump.DischargePressure")

def on_device_event(event):
    action = event["canonicalEvent"]["payload"]["action"]
```

El segundo ejemplo muestra la estructura del evento canónico que usa el host; use solamente campos presentes en el evento real configurado para su proyecto. Acceder a una clave inexistente hace fallar el handler.

### Triggers disponibles

| Trigger | Cuándo usarlo | Observación |
| --- | --- | --- |
| `Initialize` | Inicializar el estado de la instancia cuando comienza | No suponga que el código se ejecuta una sola vez para siempre; una nueva generación puede inicializarse otra vez. |
| `Dispose` | Finalizar o limpiar el estado de la instancia | Puede que la generación ya se esté cancelando; mantenga el handler breve. |
| `TagChanged` | Recalcular una regla cuando cambia el TAG asociado | Declare como dependencias todos los TAG leídos y escritos. |
| `Timer` | Ejecutar una comprobación periódica sin depender de una pantalla abierta | Configure un intervalo de al menos 50 ms o el mínimo indicado por el producto. No cree bucles ni espere dentro del Script. El Timer no está sincronizado con el ciclo de adquisición del Driver. |
| `ServerRuntimeEvent` | Reaccionar a una ocurrencia de Transient Event canónico | Asocie el evento real en el grafo Active; los targets genéricos no invocan el handler. |

### Sintaxis: qué funciona y qué no

El subconjunto actual acepta:

- `def` sin decorators, valores predeterminados ni parámetros solo posicionales (positional-only) o solo por nombre (keyword-only);
- asignación local y `+=`, `-=`, `*=`;
- `if`/`else`, `return` y `pass`;
- listas, tuplas, diccionarios y acceso por índice, por ejemplo `event["payload"]["action"]`;
- `+`, `-`, `*`, `/`, `//`, `%`, `**`; comparaciones `==`, `!=`, `<`, `<=`, `>`, `>=`, `is`, `is not`;
- `not`, `and`, `or`, llamadas posicionales a funciones del propio Script y `min`, `max`, `abs`, `round`, `int`, `float`, `bool`, `str`, `len`.

No acepta `import`, código/variables en el nivel principal, `for`, `while`, comprensiones de listas/iterables (comprehensions), `class`, `try/except`, `lambda`, f-strings, acceso a atributos, decorators, argumentos nominales ni bibliotecas externas de Python. Incluso el código sintácticamente aceptado puede fallar por tipos incompatibles, división por cero o claves/índices inexistentes.

**Diferencia importante:** en este intérprete, `and` y `or` evalúan todos los operandos; no tienen el cortocircuito de Python normal. No los use para proteger una operación que pueda fallar. Escriba `if` anidados:

```python
def evaluate_pressure():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    if pressure is not None:
        if high_limit is not None:
            above_limit = pressure >= high_limit
```

### API de TAG, Server Memory y Operational Event

Estas son las seis funciones públicas documentadas para TAG, memoria y eventos. Cada referencia debe ser una dependencia declarada; para una ruta legible, el binding debe coincidir con la identidad estable. Si falla una referencia, corrija la dependencia/el binding; no intente eludirlo a través del Driver.

| Función | Qué recibe y devuelve | Reglas principales |
| --- | --- | --- |
| `read_tag(tag_reference)` | Ruta vinculada o ID estable; devuelve el valor actual. | No devuelve quality ni timestamp de origen. Solo lee dependencias declaradas. |
| `read_server_memory(tag_reference)` | Ruta vinculada o ID estable; devuelve el valor actual de Server Memory. | Requiere una dependencia declarada `ServerMemoryTag`. |
| `write_tag(tag_reference, value)` | TAG declarado y valor compatible con su tipo; devuelve `None`. | Requiere TAG escribible y autorización en el alcance correspondiente. No cambia directamente un objeto visual. |
| `write_server_memory(tag_reference, value)` | ServerMemoryTag declarado y valor serializable; devuelve `None`. | Escribe memoria del servidor, no un TAG de proceso. |
| `publish_server_memory_sample(tag_reference, value, quality)` | ServerMemoryTag, valor serializable y nombre canónico de `TagQuality`; devuelve `None`. | Quality debe ser exactamente un valor admitido, por ejemplo `Good`, `Uncertain`, `Bad`, `BadCommunication`, `BadConfiguration`, `BadDevice`, `Stale`, `Disabled` o `Unavailable`. El Script proporciona la quality; la función no la descubre en el origen. |
| `emit_operational_event(definition_id, message=None, context=None)` | ID estable de la definición; texto opcional; diccionario opcional con valores de texto; devuelve `None`. | La definición debe existir. Las entradas tienen límites de cantidad/tamaño. Operational Event no sustituye Audit ni Alarm. |

### Ejemplo 1: derivar un estado visual

Use este patrón para evaluar un valor y escribir el resultado en un TAG escribible ya vinculado a una propiedad visual. Declare la presión, el límite y la salida como dependencias; configure la salida con un tipo compatible (booleano en este ejemplo).

```python
def update_pressure_indicator():
    pressure = read_tag("Plant.Pump.DischargePressure")
    high_limit = read_tag("Limits.Pump.HighPressure")
    if pressure is not None:
        if high_limit is not None:
            write_tag("Plant.Screen.PumpPressureHigh", pressure >= high_limit)
```

Si alguna entrada no está disponible, el handler no sobrescribe la salida con `False`; así se evita convertir datos ausentes en una indicación de condición normal. Server Script no cambia directamente el objeto de pantalla: el TAG de salida debe estar vinculado a la propiedad visual.

### Ejemplo 2: histéresis y Operational Event sin repetición continua

Use un estado retentivo para registrar si la condición ya fue señalada. Configure `TagChanged` para el TAG de presión y declare ambos límites, el `ServerMemoryTag` de estado y la definición del evento como dependencias aplicables. El límite de retorno debe ser menor que el límite de disparo.

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

Sustituya el placeholder por el ID estable real. El estado evita emitir el mismo evento en cada cambio mientras la presión permanezca por encima del límite. Esto es lógica de aplicación, no sustituye la configuración de Alarm cuando el requisito es una alarma.

### Ejemplo 3: muestra calculada y quality explícita

`read_tag` no informa si la fuente está `Good`, `Stale` ni cuál es el timestamp de origen. Por tanto, comprobar solo `value is not None` **no** demuestra que la muestra sea buena. Si la aplicación no puede confirmar quality y actualidad, publique una quality conservadora adecuada o no publique:

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

Use `"Good"` solo cuando una regla validada del proyecto establezca la confiabilidad del valor y no dependa de información de quality/timestamp que el Script no recibe. Nunca reemplace un valor ausente por cero para forzar una muestra.

### Ejemplo 4: Timer sin bucle

Configure el punto de entrada `Timer` en Engineering. El host invoca el handler; el código no permanece ejecutándose continuamente:

```python
def on_timer():
    current = read_tag("Plant.Pump.Current")
    if current is not None:
        write_server_memory("State.Pump.LastCurrent", current)
```

Declare ambos TAG y confirme que el destino es un `ServerMemoryTag`. Si necesita detectar cambios, considere `TagChanged`; no reduzca el intervalo del Timer sin necesidad, porque los timers rápidos generan trabajo adicional en el servidor.

### Rich Command: operación autorizada explícitamente

Además de las seis APIs anteriores, la integración del host proporciona `invoke_rich_command` para una dependencia Rich Command declarada. Esto **no** es acceso directo al Driver. Configure el binding real del comando, asocie un `ServerRuntimeEvent` a un Transient Event real y conceda `CommandExecute` en el alcance exacto. Los parámetros deben ser valores escalares compatibles con el esquema del comando.

El resultado distingue `Rejected`, `Accepted`, `Completed`, `Failed`, `TimedOut` y `Unknown`. `Accepted` significa que la solicitud fue aceptada, no que el equipo haya completado la acción. `Unknown` es ambiguo: no repita el comando automáticamente, ya que puede haberse ejecutado aunque se haya perdido la respuesta. Reconcilie mediante feedback independiente del proceso antes de decidir otra acción.

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

Este ejemplo registra el éxito solo para `Completed`; cualquier otro resultado queda marcado como no completado/pendiente de diagnóstico. No use `-1` como prueba de fallo físico: para distinguir resultados, almacene o publique el estado según el modelo de datos de la aplicación. Sustituya el comando, parámetro y campos del evento por objetos reales del proyecto. La operación sigue pasando por las validaciones y autorizaciones normales del producto.

### Flujo recomendado en Engineering

1. Defina el objetivo y el efecto permitido: solo calcular, escribir memoria, escribir un TAG de proceso o solicitar un Rich Command.
2. Cree/configure el Script y seleccione alcance `Server`.
3. Declare cada TAG, ServerMemoryTag, evento o comando necesario. Compruebe tipo, binding, identidad estable, permiso de escritura y alcance de Authority.
4. Cree un handler y vincule su nombre exacto al trigger. Para `ServerRuntimeEvent`, compruebe el evento canónico y los campos disponibles.
5. Escriba primero las lecturas y los casos de datos ausentes; después el cálculo; por último las escrituras/emisiones.
6. Valide la sintaxis y las dependencias en el editor. Preflight es información de ayuda; el Runtime sigue aplicando la autorización y el sandbox.
7. Pruebe valores normales, límites, valores ausentes, tipos inesperados y fallos/timeouts de comandos. Confirme cada escritura y evento.
8. Guarde, genere la revisión y siga el flujo normal de publicación/activación. Verifique que la revisión esperada quedó `Active`.

### Ejecución, fallos y sandbox

La política predeterminada documentada usa un timeout de handler de 250 ms, una cola limitada a 128 eventos, un intervalo mínimo de Timer de 50 ms y cooldown después de 5 fallos consecutivos. Después del cooldown configurable, un evento puede aceptarse como prueba de recuperación. Mantenga breve cada handler; el presupuesto determinista también limita la cantidad de pasos.

Server Script no proporciona acceso arbitrario al sistema de archivos, sistema operativo, shell/procesos, red, base de datos, Drivers, secretos, DOM ni almacenamiento del navegador. Preflight ayuda a detectar algunos errores pronto, pero no es el límite de seguridad: la aplicación de reglas corresponde al Runtime.

### Problemas comunes

| Síntoma | Qué comprobar |
| --- | --- |
| TAG “no declarado” o ruta rechazada | ¿Existe la dependencia? ¿El binding legible está configurado y vinculado a la misma identidad estable? ¿El ID es correcto? |
| El handler no se invoca | ¿El nombre de la función coincide exactamente con el punto de entrada? ¿El Script está habilitado y tiene alcance Server? ¿La revisión esperada está Active? ¿El trigger está vinculado al objeto/evento correcto? |
| Falla la lectura/escritura de Server Memory | ¿La dependencia es realmente `ServerMemoryTag`? ¿La escritura y el valor cumplen el contrato? |
| La salida visual no cambia | ¿El TAG de destino es escribible y autorizado? ¿Está vinculado a la propiedad visual? Server Script no cambia directamente objetos visuales. |
| El Script valida pero falla el handler | Compruebe tipos, `None`, claves/índices del evento, división por cero y límites; recuerde que `and/or` no hacen cortocircuito. |
| El comando devuelve `Accepted`, `TimedOut` o `Unknown` | No suponga que la acción física terminó ni reintente automáticamente. Consulte feedback independiente, estado del equipo y auditoría. |
| El Script entra en cooldown | Corrija la causa, examine los diagnostics y pruebe con datos representativos antes de volver a habilitarlo. |

**Ayuda relacionada:** `/help?topic=scripts.server`, `/help?topic=sources.internal-memory`, `/help?topic=operational-events.overview`, `/help?topic=alarms.overview`.

## Scripts Client Visual

Un Client Visual Script es Python ejecutado en el Runtime del cliente para interactuar con una Screen/Popup abierta y para la presentación local. Su alcance difiere del Server Script: no sustituye la automatización del backend ni sigue trabajando cuando el cliente no está en ejecución. Los Server Scripts, la revisión Active, los triggers del backend y sus seis APIs se describen arriba.

### Crear y vincular una interacción

1. En Script Engineering, cree un Script y seleccione el alcance `Client Visual` y el lenguaje Python.
2. Declare un punto de entrada `objectInteraction` con un nombre de handler, por ejemplo `on_click`.
3. En el panel Events del objeto Screen/Popup, seleccione `Click`, el Script y el handler.
4. Declare como dependencias cada TAG que el Script lea o escriba. La ruta legible usada en el código debe estar vinculada a la identidad estable del TAG.
5. Valide, guarde en Working y siga el flujo normal de revisión/publicación/activación antes de comprobarlo en Runtime.

Los eventos de interacción admitidos incluyen `click`, `pointerenter`, `pointermove` y `pointerleave`. El handler recibe un diccionario de evento. Para una interacción visual incluye claves como `eventKey`, `visualDefinitionId`, `visualObjectId` y `pointer`.

### API disponible

Importe las funciones proporcionadas por el módulo `elite_scada`:

| Función | Uso |
| --- | --- |
| `tag_read(reference)` | Lee un TAG permitido y devuelve datos como `value`, `quality`, `timestamp` y `source`. |
| `tag_write(reference, value)` | Solicita escribir un booleano, número finito o texto. Requiere TAG escribible, dependencia y autorización de Runtime. |
| `client_memory_read(reference)` / `client_memory_write(reference, value)` | Lee o cambia memoria no retentiva del cliente. No es Historian ni se sincroniza entre clientes. |
| `visual_property_read(target, property)` / `visual_property_write(target, property, value)` | Lee o solicita un override de una propiedad registrada en el objeto visual del contexto actual. |
| `visual_property_clear(target, property)` | Elimina el override temporal del Script para esa propiedad. |
| `visual_tween_request(arguments)` | Solicita animación mediante la API visual permitida, sujeto a validación de Runtime. |

Las llamadas bridge son asíncronas. Use `async def` y `await`. La API no permite acceso directo a Drivers, bases de datos, archivos, sistema operativo, credenciales, DOM ni red arbitraria. Las escrituras de TAG siguen los permisos de la sesión y el servicio normal de Runtime; tener un botón o Script no concede autorización.

### Ejemplo: leer un TAG y respetar quality

Vincule `on_click` al evento Click y declare `Plant.Pump.DischargePressure` como dependencia. Use la lectura solo cuando quality sea `Good`; un dato ausente o una quality distinta no equivale a cero ni demuestra normalidad.

```client-visual-python
from elite_scada import tag_read

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample["quality"] == "Good":
        pressure = sample["value"]
```

### Ejemplo: guardar un valor en Client Memory

Configure la referencia `UI.LastPressure` en el provider Client Memory del proyecto. Este valor es local al Runtime del cliente y puede desaparecer cuando finaliza la sesión/el cliente.

```client-visual-python
from elite_scada import tag_read, client_memory_write

async def on_click(event):
    sample = await tag_read("Plant.Pump.DischargePressure")
    if sample["quality"] == "Good":
        await client_memory_write("UI.LastPressure", sample["value"])
```

### Ejemplo: identificar la interacción

Declare `on_pointer_enter` como handler `objectInteraction` y vincúlelo a `Pointer enters object` en el panel Events.

```client-visual-python
async def on_pointer_enter(event):
    object_id = event["visualObjectId"]
    event_name = event["eventKey"]
```

Use handlers de puntero para respuestas breves. `pointermove` puede dispararse repetidamente al mover el puntero; no realice trabajo prolongado ni espere activamente en cada llamada. Cada ejecución tiene un límite de tiempo y los fallos se aíslan del resto de Runtime.

### Problemas comunes

| Síntoma | Qué comprobar |
| --- | --- |
| `ImportError` | Use `elite_scada` y compruebe el nombre exacto de la función ofrecida a Client Visual. Las bibliotecas generales y los módulos arbitrarios no forman parte de esta API. |
| TAG no declarado o ruta rechazada | Añada la dependencia estable al Script; compruebe el binding legible y la identidad del TAG. |
| La lectura existe, pero no debe actualizar la pantalla | Compruebe `quality` y `timestamp`; no trate una lectura no disponible como cero/estado normal. |
| Escritura rechazada | Compruebe si el TAG admite escritura, la capacidad de la sesión, el valor compatible y el feedback de Runtime. |
| El handler no se invoca | Compruebe alcance `Client Visual`, punto de entrada `objectInteraction`, nombre exacto y asociación Script/evento/objeto. |
| La interfaz se ralentiza durante la interacción | Reduzca el trabajo en `pointermove`, elimine bucles/espera activa y mantenga breve el handler. |

**Ayuda relacionada:** `/help?topic=scripts.client-visual`, `/help?topic=scripts.engineering`, `/help?topic=scripts.python-validation`, `/help?topic=visual.events`.

## Client Memory

Client Memory es memoria no retentiva propiedad del Runtime del cliente. Server Memory es retentiva y pertenece al servidor. Ambos son providers de origen internos, no Drivers de comunicación.

**Ayuda relacionada:** `/help?topic=sources.internal-memory`.

## Alarmas, Operational Events y Audit

Son tres dominios distintos:

- **Alarm**: condición operativa de alarma configurada.
- **Operational Event**: ocurrencia operativa explícita definida por el producto/la aplicación.
- **Audit**: trazabilidad de acciones de seguridad, Engineering y operación.

No use Alarm como registro genérico ni Operational Event como sustituto de Audit.

**Ayuda relacionada:** `/help?topic=alarms.overview`, `/help?topic=operational-events.overview`, `/help?topic=audit.overview`.

## Historian y Trends

Historian recibe valor, quality y timestamp mediante el pipeline del producto. Trends presentan datos actuales o históricos autorizados; no redefinen quality, timestamp, scaling ni persistencia.

**Ayuda relacionada:** `/help?topic=historian.overview`, `/help?topic=trends.overview`.

## Historical Time Range y Playback

La estructura de estos capítulos está reservada, pero los procedimientos finales están bloqueados hasta que se acepten #383 y el workstream posterior de Playback. No use este manual como indicación de que la UX final de estas funciones esté congelada.
