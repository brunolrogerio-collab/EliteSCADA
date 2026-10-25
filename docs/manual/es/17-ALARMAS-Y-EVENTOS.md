# Alarmas y eventos: configuración y operación

## Alarmas: configurar condición, prioridad y comportamiento

Cree una alarma a partir de un TAG y comprenda qué hace cada opción.

<!-- HELP-REVIEW-AFTER-PRODUCT-751: https://github.com/brunolrogerio-collab/EliteSCADA/issues/751; topic=alarms.configuration; fields=alarm.activationDelayMilliseconds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-753: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.configuration; fields=alarm.requiresAcknowledgement; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Antes de empezar

Abra Ingeniería > Alarmas. Verifique primero lectura, tipo, escala y unidad del TAG. Una alarma de presión alta observa su valor; no configura el sensor ni detiene la bomba por sí sola. Acuerde límites y respuesta con el responsable del proceso. Aprenda con un TAG simulado, sin actuar sobre equipos reales. La edición requiere el permiso y bloqueo de Ingeniería correspondientes.

### Identidad y condición

Seleccione Nueva alarma o una definición existente. La ruta identifica la señal a evaluar; el nombre y mensaje ayudan al operador a entender el problema.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre | Use una descripción legible que distinga equipo y condición. Renombrar no cambia el TAG observado. | Presión alta — Bomba 01 |  |
| Ruta del TAG | Introduzca una ruta de TAG existente. Compruebe su valor ya escalado en el Monitor de TAGs antes de elegir el límite. | Planta.Bomba01.Pressao | No es dirección del Driver ni nombre de pantalla. |
| Tipo | Selecciona la comparación; consulte los tipos siguientes. HighHigh es otra definición de límite, no prioridad automática. | high |  |
| Setpoint (límite) | Aparece para tipos analógicos. Introduzca un número en la unidad actual del TAG. High/HighHigh se activan con valor ≥ límite; Low/LowLow con valor ≤ límite. | 10 bar: introducir 10 | La igualdad también activa. Este editor no tiene campo de histéresis. |
| Valor digital activo | Solo aparece en Digital. Marcado significa alarma con true; desmarcado con false. Confirme la polaridad real. | Fallo=true / Listo=false |  |
| Prioridad | Ordena y destaca la atención: low, medium, high o critical. No cambia límite, permiso ni lógica de parada. | high |  |
| Clase | Etiqueta para organizar definiciones. Escribir un nombre no instala una regla de protección. | Proceso |  |
| Área | Contexto del proceso usado también para autorizar acciones por área. Use el nombre previsto en la política; escribir un área no concede acceso. | Estacao01 |  |
| Mensaje | Explique condición y ubicación de forma útil. Una instrucción escrita no ejecuta un comando. | Bomba 01: comprobar presión de descarga |  |

### Qué tipo elegir

Digital compara el booleano con el valor activo elegido. High y HighHigh usan ≥; Low y LowLow usan ≤. Para dos límites, cree dos definiciones y asigne prioridades por separado. Communication se activa con calidad distinta de Good y retorna con Good; no usa setpoint. Con mala calidad, los tipos de proceso no evalúan el límite numérico como si la lectura fuera válida: supervise esta situación con una alarma de comunicación. System aparece en el selector, pero no tiene regla genérica de proceso en este evaluador; no lo use en lugar de High o Digital. Seleccionar Communication no implica un temporizador de pérdida de comunicación.

### Comportamiento y sonido

Expanda Comportamiento de la alarma. Algunas opciones se guardan en la definición, pero tienen limitaciones actuales en Runtime, indicadas junto al campo.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Habilitada | Incluye la definición en la evaluación al activar la revisión. Deshabilitar no corrige la señal ni la causa física. | Marcada |  |
| Retardo de activación (ms) | Representa el retardo deseado. Limitación actual: se guarda, pero no retrasa la activación en Runtime. No dependa de él para filtrar oscilaciones. | 1000 ms = 1 s | Campo entero; retardo aún no aplicado. |
| Requiere reconocimiento | Indica la intención de exigir ACK del operador. Limitación actual: desmarcar no elimina el reconocimiento pendiente en Runtime, incluso tras el retorno. | Marcada |  |
| Permitir shelving | Permite suprimir esta definición mediante la operación disponible. La acción sigue requiriendo permiso AlarmShelve. El Alarm Center descrito aquí no tiene botón de shelving. | Según el procedimiento de planta | No es Silenciar sonidos; no se promete vencimiento automático. |
| Perfil sonoro / prueba | Elija ninguno, pulso corto, doble, triple, ascendente o alternado y pruebe la vista previa. En Runtime, el operador debe activar sonidos en el navegador. Volumen, salida y permiso del navegador afectan la reproducción. | double | Silenciar en el navegador no reconoce la alarma. |

### Procedimiento: probar un límite alto

1. Cree una alarma high para el TAG de presión simulado con setpoint 10 y unidad bar en el TAG.
2. Complete prioridad, área y un mensaje claro. Mantenga Requiere reconocimiento marcado para esta prueba.
3. Ejecute Preview, lea errores y aplique al Workspace. Otra edición requiere validar de nuevo.
4. Guarde, publique y active la revisión correcta; aplicar al Workspace no activa Runtime por sí solo.
5. Con calidad Good, envíe 9: queda Normal. Envíe 10: pasa a Active porque la igualdad alcanza el límite.
6. Reconozca en Runtime: pasa a Acknowledged y el TAG sigue en 10. Envíe 9: pasa a Returned y sale de la lista activa porque ya tiene ACK.
7. En otra activación, envíe 10 y luego 9 sin ACK: queda Returned pendiente. Reconozca y compruebe la salida de la lista activa. Consulte el histórico para transiciones anteriores.

### Si el resultado es distinto

Compruebe TAG/revisión activa, escala/unidad, polaridad, habilitación y calidad. La prioridad alta no fuerza activación. Cambiar mensaje o clase no añade histéresis a un límite oscilante. Preview valida un candidato; no demuestra adquisición de campo. Retardo y exención de ACK conservan las limitaciones descritas. Si el proyecto cambia durante la edición, recargue y valide de nuevo en lugar de aplicar un candidato antiguo.

[Abrir esta guía en Ayuda instalada](/help?topic=alarms.configuration)

## Alarmas en Runtime: estados, reconocimiento y sonidos

Lea la condición actual, reconozca y distinga silencio, retorno e histórico.

<!-- HELP-REVIEW-AFTER-PRODUCT-753-RUNTIME: https://github.com/brunolrogerio-collab/EliteSCADA/issues/753; topic=alarms.runtime-operation; fields=alarmState.returned; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Dónde mirar y cómo interpretar

Abra la central de alarmas de Runtime. Muestra la lista activa visible para su sesión, contadores y detalles del elemento seleccionado. No es una lista de todas las ocurrencias pasadas. Seleccione una fila para ver mensaje/nombre, prioridad, área, último valor, activación, última transición y quién/cuándo reconoció. Los IDs identifican la definición exacta; no necesita inventar ni escribir un ID para reconocer.

### Estados de los detalles

La lista activa incluye Active, Acknowledged y Returned sin ACK. Una lista vacía no significa que nunca hubo una alarma. Los estados fuera de ella pueden encontrarse en otras consultas apropiadas.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Normal | La condición no está activa ahora; puede existir un ciclo anterior en el histórico. | 9 < 10 |  |
| Active | Condición presente y ciclo aún sin reconocimiento. | 10 ≥ 10 |  |
| Acknowledged | ACK registrado, pero la condición sigue presente. No significa causa resuelta. | ACK con presión aún en 10 |  |
| Returned | La condición dejó de estar activa. Sin ACK queda pendiente en la lista activa. Tras ACK sale de esa lista. | 10 → 9 |  |
| Disabled | Definición deshabilitada para evaluación. No indica equipo sano. | Habilitada desmarcada |  |
| Shelved | Notificación suprimida mediante shelving. Se sigue observando valor y estado subyacente; retirar la supresión restaura el estado subyacente actual. | Mantenimiento controlado | La central actual no ofrece botón para esta acción. |

### Procedimiento: reconocer una notificación

1. Seleccione la fila y lea condición, valor y hora; confirme el equipo.
2. Siga la respuesta operativa de la planta.
3. Use Reconocer alarma si la sesión tiene permiso AlarmAcknowledge para el área. Una sesión View Only no adquiere esta acción.
4. Espere confirmación del servidor y actualización. Si el ACK se aceptó pero falló la relectura, actualice para comprobarlo antes de repetir acciones.
5. Si la condición persiste, sigue visible reconocida. Si ya retornó, sale de la lista activa tras ACK. Otra activación genera otro reconocimiento pendiente.

### Sonidos, silencio e indisponibilidad

Activar sonidos de alarmas habilita el audio en esta sesión del navegador; depende de soporte, volumen y salida. Los perfiles configurados se repiten mientras haya alarmas sin ACK, incluso Returned sin ACK. Silenciar sonidos es local y no reconoce, deshabilita ni suprime mediante shelving. Reconocer no envía comandos al proceso. Sin acceso: compruebe login, clase de sesión y permiso/área; no intente corregirlo cambiando el TAG. Sin datos o servicio disponible: actualice y consulte diagnóstico de Runtime. Para transiciones anteriores, abra Histórico y elija alarm.events con un período adecuado; no lo confunda con operational.events ni Audit.

[Abrir esta guía en Ayuda instalada](/help?topic=alarms.runtime-operation)

## Eventos operacionales: definir, emitir y consultar

Registre una ocurrencia del proceso con contexto, distinguiendo definición, ocurrencia, alarma y Audit.

### Elija el recurso adecuado

Un evento operacional registra un hecho, como inicio de lote o cambio de modo. Cada emisión crea una ocurrencia con hora e identidad propias; no mantiene condición activa ni exige ACK. Una alarma sigue una condición que se activa/retorna. Audit rastrea acciones y resultados de seguridad/administración. Un clic visual también se llama evento, pero no es por sí solo una ocurrencia operacional. Crear la definición siguiente no emite nada automáticamente; asociar un TAG aporta contexto, no un disparador de cambio.

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

### Procedimiento: preparar definición y productor

1. Cree la definición con los campos obligatorios y contexto útil; evite una misma clave genérica para todos los hechos.
2. Ejecute Preview y lea los diagnósticos. Corrija claves duplicadas y referencias inválidas.
3. Aplique al Workspace; después guarde, publique y active según el ciclo del proyecto.
4. Configure el productor: un Server Script puede llamar emit_operational_event. Obtenga el ID estable de la definición guardada en datos exportados/referencias del proyecto; no lo sustituya por el Identificador textual.
5. Vincule el handler Python al disparador Server adecuado. Evite Timer para registrar una sola transición: cada llamada crea otra ocurrencia aunque el mensaje sea igual.
6. Active la revisión con definición y script.
7. Produzca un hecho controlado y consulte operational.events en Histórico con un período que incluya la ejecución.

### Ejemplo Python: una ocurrencia al inicializar

El UUID siguiente es ilustrativo: sustitúyalo por el ID estable de su definición. En Script Engineering elija ámbito Server y vincule Initialize al handler initialize; el nombre de la función no crea el vínculo. El ejemplo registra inicio de simulación en cada inicialización del script, no arranque físico de bomba. El argumento event es contexto del disparador y puede quedar sin uso. emit_operational_event(definition_id, message=None, context=None) devuelve None, no el ID de la ocurrencia. Claves y valores del contexto son textos: use str(valor) para números calculados.

```python
def initialize(event):
    emit_operational_event(
        "c1900000-0000-0000-0000-000000000002",
        "Simulation started",
        {"mode": "simulation", "batch": "42"}
    )
```

### Consultar y diagnosticar

En Histórico seleccione operational.events, ajuste inicio/fin y filtros disponibles; compruebe hora, origen, mensaje y contexto. alarm.events contiene transiciones de alarmas, y Audit tiene otra finalidad. El contexto adicional no cambia tipo, categoría, origen ni identidad propios de la definición, aunque tenga claves con esos nombres. El contexto admite hasta 128 pares, claves de hasta 160 caracteres y valores de hasta 4000; los mensajes también tienen límite de 4000. No incluya contraseñas ni secretos. Sin ocurrencia: compruebe revisión activa, definición habilitada, ID correcto, vínculo del handler, diagnóstico del script, período y permiso de consulta. La emisión debe respetar la autoridad activa del Runtime; un fallo no autoriza una vía alternativa. Que no aparezca nada tras solo crear la definición es normal.

[Abrir esta guía en Ayuda instalada](/help?topic=operational-events.configuration)
