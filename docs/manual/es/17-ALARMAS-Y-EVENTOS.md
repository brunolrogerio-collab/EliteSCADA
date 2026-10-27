# Alarmas y eventos: configuración y operación

## Alarmas: configurar condición, prioridad y comportamiento

Configure un aviso para una condición conocida y pruebe qué verá el operador. El ejemplo usa presión alta sin comandar equipos.

<!-- HELP-REVIEW-AFTER-PRODUCT-751: https://github.com/brunolrogerio-collab/EliteSCADA/issues/751; topic=alarms.configuration; fields=alarm.activationDelayMilliseconds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-753: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.configuration; fields=alarm.requiresAcknowledgement; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Antes de empezar

Empiece por una condición sencilla: avisar cuando la presión alcance el límite alto. En Ingeniería > Alarmas, use un TAG de prueba con valor, tipo, escala y unidad comprobados. Un TAG es la variable que proporciona la lectura.

La alarma observa esa variable; no configura el sensor ni detiene la bomba sola. Acuerde límite y respuesta operacional con el responsable. Para aprender, use simulación con permiso y bloqueo de Ingeniería aplicables, sin actuar en equipos reales.

### Qué tipo elegir

Elija el tipo según la pregunta que debe responder la alarma:

Digital sigue un booleano: elija si true o false representa alarma. High y HighHigh activan con valor mayor o igual al límite (≥). Low y LowLow activan con menor o igual (≤). Para dos límites, cree dos definiciones y elija la prioridad de cada una.

Communication sigue la calidad: activa cuando difiere de Good y retorna con Good. No usa setpoint, el límite numérico, ni crea un temporizador de pérdida de comunicación solo por seleccionarlo. Con calidad mala, las alarmas de proceso no tratan el número como lectura válida; use Communication para esa condición.

System aparece en el selector, pero no tiene regla genérica de proceso en este evaluador. Para límite alto o estado booleano, elija su tipo propio en vez de usar System como sustituto.

### Procedimiento: probar un límite alto

Aquí, 10 bar es solo un valor didáctico. ACK significa reconocimiento: el operador confirma que ha visto el aviso.

1. Cree una alarma high para el TAG de presión simulado con setpoint 10. Compruebe unidad bar en el TAG.
2. Complete prioridad, área y mensaje claro. Mantenga Requiere reconocimiento marcado para esta prueba.
3. Ejecute Preview, lea errores y aplique al Workspace, el borrador de edición. Si edita de nuevo, valide de nuevo.
4. Guarde, publique y active la revisión correcta. Aplicar al Workspace no activa Runtime por sí solo.
5. Con calidad Good envíe 9: queda Normal. Envíe 10: queda Active. La igualdad también activa.
6. Reconozca en Runtime manteniendo 10 en el TAG: queda Acknowledged, pero la presión no cambió. Envíe 9: queda Returned y sale de la lista activa porque ya tuvo ACK.
7. Active otra vez con 10 y vuelva a 9 sin reconocer: Returned sigue pendiente. Reconozca y compruebe la salida de la lista activa. Consulte histórico para las transiciones anteriores.

### Identidad y condición

Seleccione Nueva alarma o una definición existente. La ruta identifica la señal a evaluar; el nombre y mensaje ayudan al operador a entender el problema.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre | Use una descripción legible que distinga equipo y condición. Renombrar no cambia el TAG observado. | Presión alta — Bomba 01 |  |
| Ruta del TAG | Introduzca una ruta de TAG existente. Compruebe su valor ya escalado en el Monitor de TAGs antes de elegir el límite. | Planta.Bomba01.Pressao | No es dirección del Driver ni nombre de pantalla. |
| Tipo | Elige la regla de comparación; consulte la explicación de tipos en este artículo. HighHigh es otra definición de límite, no una prioridad automática. | high |  |
| Setpoint (límite) | Aparece para tipos analógicos. Introduzca un número en la unidad actual del TAG. High/HighHigh se activan con valor ≥ límite; Low/LowLow con valor ≤ límite. | 10 bar: introducir 10 | La igualdad también activa. Este editor no tiene campo de histéresis. |
| Valor digital activo | Solo aparece en Digital. Marcado significa alarma con true; desmarcado con false. Confirme la polaridad real. | Fallo=true / Listo=false |  |
| Prioridad | Ordena y destaca la atención: low, medium, high o critical. No cambia límite, permiso ni lógica de parada. | high |  |
| Clase | Etiqueta para organizar definiciones. Escribir un nombre no instala una regla de protección. | Proceso |  |
| Área | Contexto del proceso usado también para autorizar acciones por área. Use el nombre previsto en la política; escribir un área no concede acceso. | Estacao01 |  |
| Mensaje | Explique condición y ubicación de forma útil. Una instrucción escrita no ejecuta un comando. | Bomba 01: comprobar presión de descarga |  |

### Comportamiento y sonido

Expanda Comportamiento de la alarma. Algunas opciones se guardan en la definición, pero tienen limitaciones actuales en Runtime, indicadas junto al campo.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Habilitada | Incluye la definición en la evaluación al activar la revisión. Deshabilitar no corrige la señal ni la causa física. | Marcada |  |
| Retardo de activación (ms) | Representa el retardo deseado. Limitación actual: se guarda, pero no retrasa la activación en Runtime. No dependa de él para filtrar oscilaciones. | 1000 ms = 1 s | Campo entero; retardo aún no aplicado. |
| Requiere reconocimiento | Indica la intención de exigir ACK del operador. Limitación actual: desmarcar no elimina el reconocimiento pendiente en Runtime, incluso tras el retorno. | Marcada |  |
| Permitir shelving | Permite suprimir esta definición mediante la operación disponible. La acción sigue requiriendo permiso AlarmShelve. El Alarm Center descrito aquí no tiene botón de shelving. | Según el procedimiento de planta | No es Silenciar sonidos; no se promete vencimiento automático. |
| Perfil sonoro / prueba | Elija ninguno, pulso corto, doble, triple, ascendente o alternado y pruebe la vista previa. En Runtime, el operador debe activar sonidos en el navegador. Volumen, salida y permiso del navegador afectan la reproducción. | double | Silenciar en el navegador no reconoce la alarma. |

### Si el resultado es distinto

Compruebe TAG/revisión activa, escala/unidad, polaridad, habilitación y calidad. La prioridad alta no fuerza activación. Cambiar mensaje o clase no añade histéresis a un límite oscilante. Preview valida un candidato; no demuestra adquisición de campo. Retardo y exención de ACK conservan las limitaciones descritas. Si el proyecto cambia durante la edición, recargue y valide de nuevo en lugar de aplicar un candidato antiguo.

[Abrir esta guía en Ayuda instalada](/help?topic=alarms.configuration)

## Alarmas en Runtime: estados, reconocimiento y sonidos

Lea la condición actual, reconozca y distinga silencio, retorno e histórico.

<!-- HELP-REVIEW-AFTER-PRODUCT-753-RUNTIME: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.runtime-operation; fields=alarmState.returned; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Dónde mirar y cómo interpretar

La central de alarmas del Runtime ayuda a decidir qué necesita atención ahora. Muestra la lista activa permitida para su sesión, recuentos y detalles de la fila elegida. No es el histórico de todo lo ocurrido.

Seleccione una fila y compruebe equipo, mensaje, prioridad, área, valor y horarios. Los detalles indican también quién reconoció y cuándo. Los IDs identifican la definición exacta para consulta o soporte; reconocer no requiere inventar ni escribir un ID.

### Estados de los detalles

ACK confirma que el operador ha visto la alarma. Es distinto de la condición física; una alarma reconocida puede seguir activa.

La lista activa incluye Active, Acknowledged y Returned aún sin ACK. Puede quedar vacía tras retorno y reconocimiento, aunque las transiciones anteriores sigan en el histórico. La tabla explica los estados de los detalles.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Normal | La condición no está activa ahora; puede existir un ciclo anterior en el histórico. | 9 < 10 |  |
| Active | Condición presente y ciclo aún sin reconocimiento. | 10 ≥ 10 |  |
| Acknowledged | ACK registrado, pero la condición sigue presente. No significa causa resuelta. | ACK con presión aún en 10 |  |
| Returned | La condición dejó de estar activa. Sin ACK queda pendiente en la lista activa. Tras ACK sale de esa lista. | 10 → 9 |  |
| Disabled | Definición deshabilitada para evaluación. No indica equipo sano. | Habilitada desmarcada |  |
| Shelved | Notificación suprimida mediante shelving. Se sigue observando valor y estado subyacente; retirar la supresión restaura el estado subyacente actual. | Mantenimiento controlado | La central actual no ofrece botón para esta acción. |

### Procedimiento: reconocer una notificación

1. Seleccione la fila. Lea condición, valor y hora y confirme el equipo.
2. Siga la respuesta operacional prevista por la planta para esa condición.
3. Use Reconocer alarma si la sesión lo permite en esa área. El permiso técnico es AlarmAcknowledge; una sesión View Only sigue sin esa acción.
4. Espere confirmación y actualización. Si el servidor aceptó ACK pero falló la relectura, actualice para comprobar antes de repetir acciones.
5. Si persiste la condición, la alarma reconocida sigue visible. Si ya retornó, sale de la lista activa tras ACK. Una nueva activación crea otra pendiente de reconocimiento.

### Sonidos, silencio e indisponibilidad

Para oír avisos, use Activar sonidos de alarmas en esta sesión del navegador. Compruebe soporte, volumen y salida de audio. Los perfiles se repiten mientras haya alarma pendiente, incluso Returned sin ACK.

Silenciar es local: no reconoce, deshabilita ni aplica shelving. Shelving suprime la notificación. Reconocer tampoco envía un comando al proceso. Son efectos distintos.

Sin acceso, compruebe login, clase de sesión y permiso del área. Sin datos o servicio, actualice y consulte diagnóstico de Runtime; cambiar el TAG no resuelve una denegación. Para el pasado, abra Histórico y elija alarm.events en el período. Ese conjunto es de alarmas; operational.events y Audit tienen otras finalidades.

[Abrir esta guía en Ayuda instalada](/help?topic=alarms.runtime-operation)

## Eventos operacionales: definir, emitir y consultar

Registre una ocurrencia del proceso con contexto, distinguiendo definición, ocurrencia, alarma y Audit.

### Elija el recurso adecuado

Use un evento operacional para registrar un hecho, como inicio de lote o cambio de modo. Cada emisión crea una ocurrencia con hora e identidad propias. No mantiene condición activa ni pide ACK.

Elija alarma para seguir una condición que activa y retorna. Audit registra acciones y resultados de seguridad o administración. Un clic en pantalla también se llama evento, pero no se convierte solo en ocurrencia operacional.

Primero registre la definición del hecho; después configure quién lo emite. Registrar no emite automáticamente. Asociar un TAG solo aporta contexto, no crea disparador de cambio.

### Procedimiento: preparar definición y productor

1. Cree la definición con campos obligatorios y nombre/clave que distingan el hecho. Añada contexto útil.
2. Ejecute Preview y corrija claves duplicadas o referencias inválidas de los diagnósticos.
3. Aplique al Workspace; guarde, publique y active la revisión con los controles del proyecto.
4. Prepare quién emitirá la ocurrencia. Un Server Script puede llamar emit_operational_event. Obtenga el ID estable de la definición guardada de referencias o datos exportados; no lo sustituya por el identificador textual.
5. Asocie el handler, la función Python, al disparador Server adecuado. Evite Timer para una sola transición: cada llamada crea otra ocurrencia, incluso con igual mensaje.
6. Active la revisión que contiene definición y Script.
7. Produzca un hecho de prueba controlado y consulte operational.events en Histórico, incluyendo la ejecución en el período.

### Ejemplo Python: una ocurrencia al inicializar

El ejemplo registra el inicio de la simulación, no el arranque físico de una bomba. El UUID ilustrativo es un ID de definición: sustitúyalo por el ID estable de su definición guardada.

En el editor de Scripts elija Server y asocie Initialize al handler initialize. Escribir la función no crea la asociación. Se ejecuta en cada inicialización del Script; event aporta contexto del disparador y puede quedar sin uso.

emit_operational_event(definition_id, message=None, context=None) devuelve None, no el ID de ocurrencia. En el diccionario context, claves y valores son textos. Use str(valor) para convertir un número calculado en texto antes de incluirlo.

```python
def initialize(event):
    emit_operational_event(
        "c1900000-0000-0000-0000-000000000002",
        "Simulation started",
        {"mode": "simulation", "batch": "42"}
    )
```

### Campos de la definición

Abra Ingeniería > Eventos Operacionales y seleccione Nuevo Evento Operacional. Defina significado y origen primero; expanda Contexto opcional cuando sea necesario. Tipo/categoría/origen son textos de su convención, no configuraciones de conexión.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre visible | Nombre legible para personas. | Inicio de simulación | Hasta 240 caracteres. |
| Identificador | Clave textual única entre definiciones, sin distinguir mayúsculas. No es el ID estable de la API Python. | simulation.started | Obligatorio; hasta 160 caracteres. |
| Tipo | Describe la naturaleza del hecho; no selecciona tipo de alarma. | process | Obligatorio; hasta 120 caracteres. |
| Categoría | Agrupa ocurrencias según su convención para búsqueda y análisis. | operation | Obligatorio; hasta 120 caracteres. |
| Origen | Identifica semánticamente quién produce el hecho. Escribir server-script no crea ni vincula automáticamente un script. | server-script | Obligatorio; hasta 240 caracteres. |
| Habilitado | Controla disponibilidad de la definición en la revisión Runtime. No elimina ocurrencias históricas. | Marcado |  |
| Mensaje predeterminado | Texto usado si la emisión no aporta otro mensaje. El mensaje de emisión puede sustituirlo. | Simulación iniciada | Hasta 4000 caracteres. |
| Área (opcional) | Sitúa el hecho en la organización del proceso; no concede permisos. | Estacao01 | Hasta 240 caracteres. |
| Ruta del equipo (opcional) | Referencia contextual del equipo. No crea equipo ni dibujo. | Planta.Bomba01 | Hasta 500 caracteres. |
| TAG (opcional) | Elija un TAG existente; el editor guarda identidad y ruta. Sin TAG es válido para un hecho general. | Planta.Bomba01.Estado | La asociación no supervisa cambios automáticamente. |

### Consultar y diagnosticar

En Histórico elija operational.events, el conjunto de ocurrencias operacionales. Ajuste inicio/fin y filtros y compruebe hora, origen, mensaje y contexto. alarm.events contiene transiciones de alarmas; Audit registra otro tipo de acción.

El contexto adicional no cambia tipo, categoría, origen ni identidad de la definición, incluso con claves de esos nombres. Se permiten hasta 128 pares, claves de hasta 160 caracteres y valores de hasta 4000; el mensaje tiene también límite de 4000. No incluya contraseñas ni secretos.

Si falta una ocurrencia, compruebe revisión activa, definición habilitada, ID, conexión del handler, diagnóstico del Script, período y permiso. Solo registrar la definición no produce registros. Un fallo no autoriza otra vía: la emisión debe respetar la autoridad activa de Runtime.

[Abrir esta guía en Ayuda instalada](/help?topic=operational-events.configuration)
