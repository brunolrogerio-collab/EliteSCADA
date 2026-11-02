# Inteligencia de Activos y Procesos

Este capítulo describe los flujos disponibles actualmente. Inteligencia es opcional: la licencia habilita la función, pero usted configura los equipos, perfiles y etapas que pretende analizar. No necesita aplicar análisis a toda la planta.

**Ayuda contextual:** `/help?topic=intelligence.overview`.


## Empiece por una pregunta pequeña

Imagine Bomba 01. SCADIVO ya recibe corriente y velocidad y usted quiere investigar si cambió la corriente en condiciones de operación similares. Empiece por esa pregunta sin configurar toda la planta. Una comparación orienta la investigación; no demuestra por sí sola eficiencia física ni un defecto.

Los nombres del editor representan partes de esta tarea:

| Nombre | Significado en este ejemplo |
| --- | --- |
| TAG | La variable que proporciona corriente o velocidad. |
| Perfil de medición | La ficha que reúne señales elegidas y significado de cada una. |
| Asociación | El vínculo de esa ficha con Bomba 01 registrada como Equipo. |
| Contexto operacional | La condición en que deben compararse datos, como un intervalo de operación definido. |
| Etapa del proceso | Un grupo ordenado de asociaciones, como los equipos de bombeo. No programa arranques. |
| Cobertura | La consulta que muestra qué datos utilizables hay en el período. |

Siga este recorrido y profundice en las secciones siguientes:

1. Compruebe TAGs existentes, unidades y datos guardados del período. No invente sensores ausentes.
2. Monte el perfil y asócielo a Bomba 01. Los selectores gestionan los IDs internos.
3. Guarde la configuración, elija dónde usar Intelligence, publique/active la revisión y habilite explícitamente el recurso para análisis. Licencia, configuración y ejecución autorizada son condiciones separadas.
4. Consulte cobertura primero. Elija después solo análisis cuyos requisitos cumplen sus señales. Un resultado no disponible requiere comprobación; no debe leerse como cero.
5. Para una vista del operador, genere una pantalla, revísela en el editor normal y siga publicación/activación. Abre en Runtime y las consultas requieren solicitud explícita; generar no consulta datos ni pone automáticamente la pantalla en operación.

Puede empezar por un equipo. Contextos y etapas se añaden cuando sirven a la pregunta. Más adelante encontrará horarios, papeles de señales y límites para consultar al configurar.

## 1. Empezar por el equipo y los TAGs

Elija el Equipo ya registrado y los TAGs de mediciones útiles, como corriente, tensión, presión, caudal o temperatura. En cada TAG compruebe tipo de valor y unidad. Quality informa la condición de la lectura; timestamp es su hora de origen. Historian es el histórico guardado, que debe tener datos del período elegido. Incluya una señal por la pregunta que ayuda a responder, no solo porque exista.

Para editar, su cuenta necesita los permisos `EngineeringModify` y `AssetProcessIntelligenceManage`. El segundo debe concederse explícitamente para todos los recursos o para el equipo/lugar que configurará. Compruebe también la licencia Intelligence. El nombre del rol no sustituye el permiso efectivo.

## 2. Crear un perfil de medición y una asignación

1. Abra Engineering > Inteligencia de activos y cree un Perfil de medición. Asígnele un nombre que ayude a reconocer el conjunto, como “Bomba de transferencia”.
2. Añada los TAGs que describen el equipo. Para cada TAG, explique qué mide, indique si el análisis lo necesita y complete los detalles de medición solicitados. Compruebe la unidad y la referencia real del TAG.
3. Guarde la configuración en Working.
4. En las asignaciones, elija el Equipo existente y el perfil. Un equipo puede usar más de un perfil.
5. Revise las referencias ausentes y duplicadas antes de guardar y seguir el lifecycle normal del proyecto.

El perfil organiza referencias y significado de señales. El sistema crea sus claves internas; no necesita inventar IDs ni códigos. El perfil no crea TAGs, no habilita por sí solo la captura de Historian ni demuestra que los instrumentos estén calibrados.

## 3. Configurar el contexto operacional

Mayor corriente puede ser esperable si aumenta velocidad o carga. El contexto explicita condiciones, como bomba en marcha y velocidad dentro de un intervalo, para seleccionar evidencia comparable. No aprende automáticamente el modo ni convierte correlación en diagnóstico. Defina límites con conocimiento del equipo.

Elija la asociación del equipo y dé al contexto un nombre que explique la condición, como “Bomba encendida con caudal normal”. Añada al menos una condición usando un TAG del perfil asociado. El sistema crea la clave interna del contexto.

Para números, el intervalo incluye mínimo y máximo en la unidad actual del TAG. Para booleanos, elija True o False. Para String o Enum, use el valor exacto, incluidas mayúsculas cuando corresponda.

Todas las condiciones deben coincidir con lecturas que tengan exactamente la misma hora UTC de origen. Lecturas solo próximas no bastan. Si otra edición cambia el contexto, actualice la pantalla y revise sus condiciones antes de guardar.

## 4. Organizar etapas del proceso

Use **Etapas ordenadas del proceso** para montar la secuencia que desea supervisar. Dé a cada etapa un nombre legible, añada las asignaciones de Equipo/perfil y ajuste el orden. El sistema crea la clave interna de cada etapa. La etapa organiza miembros, pero no infiere por sí sola causalidad, balance ni un KPI agregado del proceso.

Guarde la configuración en Working. Los datos y análisis de Runtime siguen sujetos a la revisión Active y a las autorizaciones vigentes.

## 5. Solicitar análisis

Seleccione el Equipo asignado y elija el análisis disponible, como cobertura del perfil, rendimiento o desviación contextual. Defina el período y solicite explícitamente la consulta. La desviación contextual requiere condiciones operacionales configuradas y evidencia temporal suficiente. El análisis es de solo lectura; no cambia un TAG, no controla equipos ni sustituye Alarmas.

Interprete el resultado junto con cobertura, quality, timestamps y las limitaciones de cada señal. La ausencia, quality deficiente, falta de historial o desalineación temporal no significan cero, operación normal ni falla confirmada. Los análisis actuales no son un diagnóstico de mantenimiento ni una prueba de eficiencia física. Para miembros de una etapa, examine un equipo a la vez; no combine resultados independientes como un score del proceso.

**Ayuda contextual:** `/help?topic=intelligence.process-stage-member-insights`.

## 6. Generar borradores de Pantallas

Guarde primero los cambios pendientes de la configuración de Intelligence. En la parte superior de la página, localice **Crear un panel para Runtime** y elija un perfil de equipo o una etapa del proceso en **Contenido del panel**. La lista muestra nombres ya registrados; no es necesario escribir IDs. El generador básico necesita al menos un TAG numérico resuelto. Si no puede elegir una fuente, complete el perfil, la asignación o la etapa y compruebe la licencia y los permisos.

Pulse **Generar borrador del panel**. SCADIVO abre una Pantalla normal y editable en el editor canónico, con tarjetas de valores, una tendencia y cobertura organizadas a partir de TAGs existentes. Es un borrador: no consulta valores actuales ni Historian, no inventa KPI ni publica la Pantalla.

En el editor, revise nombres, unidades, dimensiones, vínculos y legibilidad. Use **Preview** y **Aplicar al Workspace**; luego guarde, publique y active la revisión del proyecto, y configure/compruebe la navegación de Runtime. Solo entonces los operadores podrán abrir la Pantalla. Las acciones guiadas especializadas aparecen junto a sus análisis y pueden pedir reglas o intervalos adicionales.

## 7. Problemas comunes

### Elegir objetos y análisis del panel

Después de elegir la fuente, abra cada medición en **Objetos de medición**. Elija valor numérico, dial semicircular, anillo, barra horizontal, regla vertical, intervalos o valor con meta. La presentación no cambia el TAG ni el cálculo. Los temas de objetos son claro, oscuro o alto contraste.

Los objetos con escala requieren mínimo y máximo reales en la unidad del TAG. Se rellena una escala válida ya registrada; de lo contrario, indique límites reales. Los intervalos necesitan dos límites ordenados dentro de la escala y la meta debe estar dentro de sus límites. Los rangos incompletos o inválidos bloquean la generación. Use Valor numérico si no conoce una escala adecuada.

En **Análisis del panel**, rendimiento y desviación contextual quedan disponibles cuando sus selecciones en la asignación estén completas. Use **Configurar análisis de la asignación** para acceder a los controles existentes. Cada análisis puede incluirse por separado. Las vistas generadas reciben botones de navegación entre Pantallas. Un análisis no disponible no bloquea las otras vistas; lea el motivo antes de cambiar el significado de las señales.

Los paneles generados usan un fondo oscuro propio para conservar el contraste en ambos temas del sistema. Puede cambiarlo en Fondo, en el editor de Pantallas; compruebe también los colores de textos y valores al elegir otro fondo.

- **Recurso bloqueado:** compruebe licencia, `EngineeringModify`, `AssetProcessIntelligenceManage` y scope.
- **Perfil/asignación incompletos:** compruebe equipo, perfil, TAGs y calificadores obligatorios.
- **Sin cobertura/historial:** confirme que el TAG recibía muestras útiles con quality y timestamps y que había captura en el período. Historian no completa datos retroactivamente.
- **Sin coincidencia de contexto:** revise las condiciones, la unidad, el intervalo y si las lecturas tienen la misma hora de origen.
- **Borrador no disponible:** complete las selecciones requeridas por ese tipo de Pantalla y refine el resultado en el editor normal.
- **El resultado cambia tras editar:** guarde la configuración correcta, compruebe la revisión Active y solicite explícitamente una consulta nueva.

**Ayuda relacionada:** `/help?topic=historian.overview`, `/help?topic=tags.quality`, `/help?topic=tags.timestamps`, `/help?topic=screens.overview`, `/help?topic=engineering.lifecycle`.

## Revise los datos antes de interpretar

En Ingeniería > Inteligencia de activos, localice la asignación del equipo. En Revisar datos de referencia, elija inicio y fin de un período conocido y pulse Consultar referencia. Las horas son locales y se convierten a UTC. Cada período admite hasta 31 días. Las lecturas aceptadas tienen la evidencia de origen exigida; los rechazos explican por qué se excluyeron otras lecturas. El conteo no demuestra que el equipo estuviera sano.

En Revisar condiciones del contexto, seleccione un contexto guardado por su nombre y consulte el período. SCADIVO usa su versión guardada automáticamente. El resultado indica cuántas lecturas cumplen todas las condiciones en el mismo instante de origen. Cero lecturas compatibles puede indicar condiciones no cumplidas o datos insuficientes; lea la explicación. Publique los cambios y active el equipo antes de consultar.

## Use la desviación conjunta experimental

Abra Desviación conjunta experimental en la asignación. Elija el contexto guardado y de dos a ocho mediciones numéricas; las mediciones que definen las condiciones del contexto se excluyen de los objetivos. Indique el intervalo real de adquisición en segundos, de 1 a 86.400. No invente ese valor: describe la frecuencia esperada de las lecturas. Elija una referencia y un período posterior de evaluación sin superposición, y pulse Calcular desviación conjunta.

El resultado muestra instantes evaluados y en calentamiento. La desviación combina cambios de nivel y de la relación entre señales. Expanda Contribuciones de las mediciones para investigar la participación de cada señal. Los números no tienen unidad física ni diagnostican causas, fallos o vida útil. La función es experimental, validada solo por simulación y sin umbral de alarma validado en campo.

Use Cancelar consulta para interrumpir. Cambiar períodos, contexto, señales o cadencia descarta el resultado anterior; consulte de nuevo. Si no se puede ajustar la referencia, revise datos, calidad, sincronización, contexto y variación de señales antes de ampliar períodos.

## Entienda los conteos y las condiciones

Los análisis se consultan bajo demanda: editar perfiles o activar un alcance no inicia análisis continuo. En la referencia, lea los motivos junto a No disponible. Una señal obligatoria de texto, opción o verdadero/falso no entra en la cohorte numérica obligatoria; los conteos numéricos del perfil se muestran por separado. Un conteo cero de un tipo no consultado no demuestra ausencia de histórico.

En las consultas de contexto, revise primero Condiciones del contexto: lecturas aceptadas y cuántas cumplieron cada condición. Un modo textual puede seleccionar instantes válidos de corriente o caudal. Después revise las mediciones en las condiciones seleccionadas. Estos conteos no demuestran salud ni eficiencia.

## Compare dos períodos y cancele consultas

En las consultas de referencia y condiciones del contexto, marque Comparar con otro período para revisar evidencia en dos ventanas. Indique inicio y fin de la comparación. Cada ventana admite hasta 31 días, y la comparación debe comenzar estrictamente después del fin de referencia: no use el mismo instante en ambos límites. La consulta permanece deshabilitada con campos vacíos, fechas invertidas o superposición.

Consulte y lea los conteos de cada período. Comparar conteos no es, por sí solo, un indicador de eficiencia. Desmarcar la comparación o cambiar cualquier límite elimina el resultado anterior. Durante la lectura, use Cancelar consulta: una respuesta tardía no debe reaparecer. Para consultar de nuevo, revise los parámetros y pulse el botón de consulta.

<!-- installed-system-guides -->

## Intelligence: habilitar, pausar y resolver bloqueos

Elija qué equipos analizar, prepare la configuración y active su uso. Tener la licencia no inicia análisis automáticamente.

### Siga la secuencia completa

Empiece con un equipo, como Bomba 01. Abra Ingeniería > Inteligencia de activos. Reunirá sus variables, indicará a qué equipo pertenecen y elegirá dónde usar el análisis.

El perfil reúne señales y significados. La asociación vincula ese perfil al Equipo registrado. Configure contextos y etapas solo cuando sirvan a su pregunta. Después guarde, publique y active la revisión del proyecto.

Queda una decisión separada: activar explícitamente el recurso elegido para permitir análisis. Crear el perfil, marcar una casilla o tener la licencia no hace esa activación por usted.

### Permisos y selección de recursos

Su cuenta necesita permiso para editar Ingeniería y gestionar Intelligence en los equipos o lugares elegidos. En Seguridad aparecen como EngineeringModify y AssetProcessIntelligenceManage. El segundo requiere una concesión explícita, global o con alcance en el recurso. El nombre del rol, como Developer o SystemAdmin, no demuestra por sí solo la concesión.

Las consultas también requieren TrendUse y licencia Intelligence. Si un control está bloqueado, compruebe las capacidades efectivas de la sesión. Elija recursos con los selectores; no necesita memorizar ni inventar IDs.

### Activar solo donde sea útil

1. En Alcances elegibles, seleccione solo equipos o lugares que desea analizar. Aquí, alcance significa esa selección de recursos.
2. Guarde los alcances y la configuración preparada.
3. Publique y active la revisión del proyecto. Resuelva cambios pendientes antes de permitir ejecución.
4. En la fila del recurso use Activar y compruebe que el permiso queda armado, es decir, habilitado para ese recurso.
5. Abra la consulta de la asociación, elija el período y solicite el análisis. Activar permite consultar; no hace la consulta automáticamente.
6. Use Pausar para interrumpir análisis. Se cancela el trabajo pendiente, pero quedan TAGs e histórico. Pause un recurso antes de retirarlo de la selección.

### Activo configurado no significa habilitado

Si se revocó un permiso, el recurso puede seguir marcado Activo en la configuración y tener la ejecución bloqueada. Son datos distintos: lo configurado y lo autorizado ahora.

Restaurar la concesión no reinicia el trabajo silenciosamente. Compruebe el estado y use Activar de nuevo, incluso tras reiniciar cuando lo pida la pantalla. Requiere una decisión explícita del responsable. Compruebe esto antes de investigar datos ausentes en el histórico guardado, llamado Historian.

### Ejemplo: empezar pequeño

Para Bomba 01, empiece con corriente y velocidad si existen esas mediciones. Elija análisis que las acepten. Con nuevas mediciones podrá ampliar el perfil y usar otros análisis compatibles.

Mantenga pausados los recursos que no usará. No invente una señal de vibración ausente. La selección limita lo autorizado a ejecutar, pero no garantiza un consumo fijo de procesador o memoria.

[Abrir esta guía en Ayuda instalada](/help?topic=intelligence.activation)

## Intelligence: significado de señales y unidades

Describa qué se midió, no solo el nombre del TAG.

### Por qué no basta el nombre

Antes de calcular, indique qué mide cada variable. Un TAG llamado Potencia puede ser potencia activa, aparente o un cálculo; Presión puede medir aspiración, descarga o su diferencia. El nombre no basta para elegir la fórmula correcta.

En el perfil seleccione el papel semántico solicitado: la opción que identifica el significado de la señal para el análisis. Complete después los calificadores mostrados, los detalles que precisan esa elección. El texto de papel heredado sigue siendo descriptivo; no sustituye esa selección.

### Campos y elecciones

Consulte esta tabla si tiene dudas en el perfil. Los selectores muestran las opciones aceptadas por el catálogo actual. Elija la correspondiente al instrumento y al proceso, aunque otra opción habilitara un cálculo.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Obligatorio | Marca señales cuya ausencia compromete cobertura. No haga opcional una señal esencial solo para obtener resultado. | Corriente necesaria para comparación |  |
| Papel semántico | Elija qué representa la señal en el cálculo: magnitud y función en la máquina. El análisis usa esa opción controlada; no lo adivina por el nombre del TAG. | Potencia activa de entrada, si se mide |  |
| Base de corriente | Indique la base de corriente del instrumento admitida por el papel, como RMS cuando corresponda. Si la etiqueta solo dice corriente, confirme la medición antes de elegir. | Seleccione opción documentada del instrumento |  |
| Tipo de señal | Distingue medición, referencia y función exigida. Una consigna no prueba valor medido. | Medición real cuando se exija |  |
| Referencia de presión | Indique qué presión se midió. La diferencia salida menos entrada no es la presión de descarga sola; elija la base exigida por el papel y confirmada por el instrumento. | Diferencial medido salida − entrada |  |
| Dirección del flujo | Identifica sentido de transferencia; el opuesto puede invalidar regla. | Bomba hacia proceso, si corresponde |  |
| Referencia de frecuencia | Explica frecuencia según catálogo; distinga referencia eléctrica y velocidad física. | Compruebe origen del instrumento |  |
| TAG y unidad | Seleccione TAG real y unidad controlada. Cambiar rótulo no convierte datos. Algunos cálculos exigen unidades/versiones específicas. | kW para potencia si corresponde al dato real |  |

### Ejemplo: no confundir indicador con eficiencia

Suponga que aumenta la corriente de la bomba mientras la velocidad permanece igual. Puede orientar una investigación, pero la corriente por sí sola no calcula la eficiencia física del motor. Es un indicador sustituto, también llamado proxy.

Otras reglas pueden usar potencia eléctrica de entrada, caudal y diferencia de presión. Solo están disponibles cuando las señales cumplen los papeles, unidades y alineación exigidos. Si falta una medición o detalle necesario, lea el motivo de indisponibilidad; no introduzca valores ficticios para habilitar la regla.

[Abrir esta guía en Ayuda instalada](/help?topic=intelligence.semantics)

## Intelligence: consultar resultados y crear dashboards

Empiece comprobando los datos del período. Después elija un cálculo y, si lo necesita, genere una pantalla para consultar en Runtime.

### Empiece por cobertura

Cobertura responde la primera pregunta: ¿hay datos utilizables para este análisis? Con la asociación guardada y la ejecución autorizada, abra la consulta de cobertura del perfil. Elija inicio y fin y solicite la consulta.

El formulario muestra horas locales; la consulta identifica también la ventana UTC, referencia de tiempo común para comparar datos. El fin debe ser posterior al inicio.

Compruebe señales obligatorias con datos, muestras aceptadas y rechazadas, calidad y alineación horaria. Solo después interprete mínimo, máximo o media. Muchas muestras no demuestran por sí solas ausencia de huecos ni que las señales se midieron juntas.

### Rendimiento: elija regla elegible

En Rendimiento, elija la regla de cálculo del catálogo. Si una opción está deshabilitada, lea qué señal, significado o unidad falta. Elegir otra magnitud solo para obtener un número cambiaría la pregunta.

Complete Desde/Hasta y consulte. Lea estado, unidad, período efectivo y limitaciones junto al resultado. Indisponible no es cero. Un indicador sustituto, como comparar corrientes, no debe presentarse como eficiencia física.

Si cambia asociación, revisión o parámetros, consulte de nuevo. El resultado anterior pertenece a las elecciones anteriores.

### Dos tipos de contexto con límites distintos

El contexto describe una condición de operación para comparar situaciones similares. Por ejemplo, estudiar la bomba en un intervalo de caudal puede ser más útil que mezclar todas las condiciones. Hay dos configuraciones distintas:

En el perfil operacional guardado, el intervalo numérico incluye ambos límites en la unidad de ingeniería actual. Las señales necesitan evidencia con el mismo timestamp UTC de origen exacto. Lecturas solo próximas en el tiempo no cumplen ese requisito.

En el generador de pantalla de desviación contextual, elija de 2 a 8 señales objetivo y dos señales numéricas de contexto. Los contextos deben ser distintos entre sí y de los objetivos: hacen falta al menos cuatro señales numéricas utilizables. Los intervalos usan la unidad controlada mostrada e incluyen el límite inferior, pero excluyen el superior: inferior ≤ valor < superior.

Compruebe qué configuración usa antes de copiar límites. La unidad y la inclusión del límite superior pueden ser diferentes.

### Genere pantalla y compruebe la cadena completa

1. Guarde la configuración actual. En Crear un panel para Runtime, elija un perfil de equipo o una etapa del proceso en Contenido del panel.
2. Seleccione Generar borrador del panel. El botón queda deshabilitado si la configuración sigue sin guardar, falta licencia o permiso, o la fuente no tiene un TAG numérico válido.
3. En el editor de Pantallas, compruebe título, dimensiones, objetos, vínculos y legibilidad. Ejecute Preview y Aplicar al Workspace para llevar la edición al proyecto.
4. Guarde, publique y active la revisión. Configure o compruebe la navegación y abra la Pantalla en Runtime.
5. En el objeto de análisis, configure el período y solicite la consulta. Compruebe la ventana y el resultado. Una Pantalla de etapa agrupa sus equipos; no crea un indicador agregado ni demuestra que un equipo causó el resultado de otro.

### Cómo investigar resultado vacío

Si no hay resultado, busque la primera condición que falta:

Compruebe licencia y permisos; después, selección guardada del recurso y permiso de ejecución armado. Compruebe proyecto y revisión activos. Revise luego asociación, TAGs y período.

Si son correctos, examine captura en Historian, calidad, papeles/unidades y exigencias de alineación/contexto. Corrija la condición demostrablemente ausente y consulte de nuevo.

No cambie el significado de una señal ni amplíe límites solo para obtener un resultado. Los análisis ayudan a investigar. No confirman defectos ni anticipan fallos con una validez de campo aún no demostrada.

[Abrir esta guía en Ayuda instalada](/help?topic=intelligence.analysis-guide)
