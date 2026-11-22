# Informes: diseño y operación

## Informes: configurar datos, período y resolución

Empiece por la pregunta que debe responder antes de dibujar la página.

### Prepare datos comprobables

Empiece por la pregunta: ¿qué lecturas deben aparecer y de qué período? En Ingeniería > Informes use primero un TAG de presión con histórico existente y un intervalo corto de valores conocidos. Así comprueba datos antes de diseñar la página.

El informe consulta registros guardados. No habilita captura ni recupera un pasado no grabado. Su cuenta necesita autorización para editar y consultar la fuente. Para valores use muestras; para transiciones de alarma, el conjunto de alarmas.

### Primera prueba antes del diseño final

1. Elija consulta incorporada, propia del informe, e historian.samples, el conjunto de muestras.
2. Añada un TAG conocido con el selector de referencias.
3. Elija período corto y resolución Bruto.
4. Ejecute Preview. Compruebe filas, horas, valores y calidad disponibles.
5. Cambie a Resumen con Máximo e intervalo horario. Compare con datos brutos: el máximo de un intervalo no es la lista de todas las lecturas.
6. Después organice la página y valide. Sin filas en la vista previa, compruebe TAG, período, captura y permiso antes de cambiar colores o anchos.

### Identificación y origen de consulta

Cree o seleccione informe. Use selectores en lugar de inventar IDs. Estos campos configuran definición; ejecutar vista previa es otra acción.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre, identificador y descripción | Nombre aparece en lista; identificador conserva referencias; descripción explica objetivo/contexto. Renombrar no crea datos. | Presión de bomba — turno |  |
| Data Query guardada o consulta incorporada | Data Query reutiliza definición existente; incorporada pertenece al informe. Compruebe selección antes del diseño. El editor no admite SQL libre. | Consulta incorporada para primer ejemplo |  |
| Fuente de datos / dataset | historian.samples consulta muestras; alarm.events ocurrencias/transiciones de alarmas. Los campos dependen de elección; cambiar dataset puede invalidar campos. | historian.samples |  |
| Período predeterminado y de vista previa | Predeterminado pertenece a definición; período de vista previa elige ventana actual. Use unidad del editor. Una hora son 3600 segundos; no es número de filas. | Última hora para comprobar pocas muestras |  |
| Datos del informe / variables | Añada TAGs desde navegador de referencias. Compruebe ruta/tipo/unidad; configure presentación por variable. | TAG de presión Bomba 01 |  |
| Filas por página | Tamaño de página solicitado; compruebe cantidad devuelta. No define retención de Historian. | 50 | 1–200 |

### Elija cómo presentar datos

Resolución define cómo recuperar y resumir registros en esta consulta. Bruto muestra muestras guardadas; otras opciones organizan o resumen la salida. Compruebe valores brutos antes de usar un resumen.

No cambia los datos capturados, elimina registros brutos ni configura una política permanente de agregación de la base.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Bruto, intervalo fijo o resumen | Bruto examina muestras; intervalo fijo recupera por pasos; resumen aplica función por intervalo. Un promedio puede ocultar pico: revise bruto. | Bruto para comprobar; resumen horario para visión general |  |
| Intervalo fijo | Elija separación entre posiciones de consulta. No es polling del Driver ni garantiza muestra en cada posición. Los huecos siguen importando. | 5 minutos si responde a la pregunta |  |
| Agregación y agrupar cada | Promedio, mínimo, máximo, suma, conteo, primero y último difieren. Elija intervalo. Sumar caudales no calcula automáticamente volumen; necesitaría integración/unidades adecuadas. | Máximo por hora para buscar picos |  |
| Tabla larga o ancha | Larga organiza muestras por filas; ancha organiza intervalos para comparar variables. En bruto, ancha está deshabilitada. | Larga para inspeccionar muestras |  |

[Abrir esta guía en Ayuda instalada](/help?topic=reports.data-designer)

## Informes: página, variables, campos y resúmenes

Separe valor consultado, formato y lugar donde se imprime.

### Comprenda la página

En el Designer, Proyecto/Design permite montar la página; Vista previa/Preview muestra una ejecución con datos. Empiece por un título y un campo de lectura antes de añadir grupos y totales.

Las dimensiones usan milímetros, como una página impresa, no píxeles de Runtime. Vertical es retrato; horizontal es paisaje. Reserve márgenes y ajuste contenido al ancho restante. El zoom cambia la visualización, no el tamaño impreso.

### Cómo dar formato a una variable

La variable sigue vinculada al TAG. Estos campos cambian presentación, no calibración ni conversión por rótulo.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Mostrar columna y orden | Elija visibilidad y Mover arriba/abajo. Quitar variable no elimina TAG. | Presión antes de corriente |  |
| Rótulo visible | Cabecera legible, distinta de identidad TAG. | Presión de descarga |  |
| Unidad automática, oculta o rótulo propio | Automática usa unidad TAG; Oculta no muestra; rótulo propio solo cambia texto. Etiquetar psi un valor bar no convierte. | Automática si la unidad es correcta |  |
| Decimales y formato numérico | Elija estándar, fijo o científico. Decimales afectan presentación, no exactitud/valor guardado. No oculte variación relevante. | Una posición para décimas | 0–15 posiciones en editor |
| Fecha/hora y textos booleanos | Elija fecha/hora local, fecha, hora o ISO 8601. Rótulos Verdadero/Falso deben tener sentido; falta de datos no es Falso. | Marcha / Paro solo si corresponde |  |

### Secciones y controles del diseño

Etiqueta es texto fijo, como Presión de descarga. Campo muestra un dato de la consulta. Son los controles ofrecidos directamente para insertar; empiece por ellos.

Coloque el título en el encabezado del informe y campos de registros en Detalle, sección que recibe filas de la consulta. Encabezado/pie de página repiten en cada página; los de informe pertenecen al informe. Los de grupo dependen del agrupamiento.

Sin filas, puede aparecer el título y quedar vacío Detalle. Compruebe primero los datos en vez de intentar resolverlo aumentando la altura de sección.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Altura de sección | Espacio reservado. Un control más alto puede cortarse; ampliar no crea filas. | Ajustar banda al contenido |  |
| X, Y, ancho y alto (mm) | Posición/tamaño de diseño. Revise sección/ancho útil; no son coordenadas del monitor. | Campo dentro de Detalle |  |
| Texto de etiqueta | Contenido fijo, como título. Use Campo para valores por fila; escribir ruta TAG en etiqueta no vincula. | Lecturas de turno |  |
| Consulta y campo | Consulta elige resultado; Campo debe existir en dataset. Use nombre expuesto, como value o timestamp para muestras, no uno inventado. | Consulta main / campo value |  |
| Recurso de imagen | Una imagen necesita recurso existente/resuelto. Si selector no lo ofrece, no invente ID; use controles editables y compruebe disponibilidad.  | Recurso existente cuando disponible |  |

### Agrupación no es agregación temporal

Agrupar por reúne filas según el campo elegido y permite ordenarlas. Resolución define intervalos de tiempo de la consulta. Son elecciones distintas: nombrar un grupo no cambia el intervalo.

Para un resumen elija campo, función y alcance: solo el grupo o todo el informe. Compruebe si la consulta ya resumió datos antes de sumar otra vez. Recuento indica cantidad de registros, no horas de funcionamiento. Pruebe un grupo pequeño y compruebe el total a mano.

### De vista previa a revisión activa

1. Monte título, sección Detalle y campos vinculados a datos reales.
2. Ejecute vista previa y compruebe datos, páginas y cortes.
3. Use Validar y corrija referencias o estructura inválidas.
4. Aplique solo el candidato válido. Si el proyecto cambió mientras editaba, actualice y valide de nuevo.
5. Guarde, publique y active con los controles del proyecto.
6. Genere en Runtime con iguales parámetros y compare. La vista previa muestra una ejecución; no guarda la definición ni demuestra que esté activa la nueva revisión.

[Abrir esta guía en Ayuda instalada](/help?topic=reports.layout-variables)

## Informes Runtime: generar, comprobar y exportar

La salida pertenece a una ejecución con período y parámetros propios.

### Encuentre el informe disponible

Abra Informes en Runtime y busque por nombre. La lista usa la revisión activa y los permisos de su sesión. Si falta un informe, compruebe esas condiciones antes de recrearlo.

Acceder a Ingeniería o ver un objeto de informe no concede acceso a todos los datos. Quien genera necesita también permiso de consulta de la fuente elegida.

### Período, parámetros y ejecución

1. Seleccione el informe que desea generar.
2. Elija período relativo o Desde/Hasta entre modos permitidos. Relativo se calcula al ejecutar: repetir después puede usar otra ventana.
3. Para intervalo absoluto indique inicio anterior al fin y compruebe el huso mostrado.
4. Complete parámetros con tipos y opciones pedidos. Número, fecha y booleano no aceptan cualquier texto.
5. Use Generar informe y espere. Cancelar interrumpe la solicitud; no elimina la definición.
6. Antes de interpretar compruebe hora de generación, período consultado y parámetros usados.

### Vista previa, PDF, hoja de cálculo e impresión

Compruebe el resultado con página anterior/siguiente y zoom. Ajustar al ancho cambia solo la visualización. Use PDF o impresión para páginas; XLSX y CSV permiten trabajar con datos de salida.

Son exportaciones del resultado, no paquetes para importar proyectos o cambiar TAGs. Un archivo representa esa ejecución. Para actualizar datos genere de nuevo; abrirlo después no consulta en vivo.

Antes de compartir planilla o CSV, compruebe unidades, fechas y decimales esperados por el destinatario.

### Problemas con comprobaciones distintas

Si falta el informe, revise publicación/activación y autorización. Con título sin filas, compruebe período, filtros y captura. Ante campo vacío o error, confirme campo/consulta referenciados, tipo y parámetros.

Un número inesperado requiere revisar resolución, función, unidad y alcance del resumen. Cambiar rótulo no convierte unidades. Ante cortes, compruebe milímetros, ancho útil y altura de sección.

Si parecen datos antiguos, compruebe hora de generación y genere de nuevo. No cambie permisos o unidades a ciegas para intentar corregir la salida.

[Abrir esta guía en Ayuda instalada](/help?topic=reports.runtime-output)
