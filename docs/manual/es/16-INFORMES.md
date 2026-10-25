# Informes: diseño y operación

## Informes: configurar datos, período y resolución

Empiece por la pregunta que debe responder antes de dibujar la página.

### Prepare datos comprobables

Abra Ingeniería > Informes. Empiece con un TAG de presión con captura existente y período corto de valores conocidos. Un informe consulta datos guardados; no habilita captura ni recupera pasado no almacenado. Necesita permisos de edición y consulta. Histórico de muestras y transiciones de alarmas usan fuentes distintas.

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

Resolución es elección de consulta; no cambia captura ni elimina muestras brutas. Agregar en informe no configura política permanente de la base.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Bruto, intervalo fijo o resumen | Bruto examina muestras; intervalo fijo recupera por pasos; resumen aplica función por intervalo. Un promedio puede ocultar pico: revise bruto. | Bruto para comprobar; resumen horario para visión general |  |
| Intervalo fijo | Elija separación entre posiciones de consulta. No es polling del Driver ni garantiza muestra en cada posición. Los huecos siguen importando. | 5 minutos si responde a la pregunta |  |
| Agregación y agrupar cada | Promedio, mínimo, máximo, suma, conteo, primero y último difieren. Elija intervalo. Sumar caudales no calcula automáticamente volumen; necesitaría integración/unidades adecuadas. | Máximo por hora para buscar picos |  |
| Tabla larga o ancha | Larga organiza muestras por filas; ancha organiza intervalos para comparar variables. En bruto, ancha está deshabilitada. | Larga para inspeccionar muestras |  |

### Primera prueba antes del diseño final

1. Use consulta incorporada historian.samples.
2. Añada TAG conocido.
3. Período corto y Bruto.
4. Ejecute Preview y compruebe filas/horas/valores/calidad disponible.
5. Cambie a Resumen, Máximo e intervalo horario; compare sin esperar igualdad.
6. Después diseñe/valide. Sin filas revise TAG/período/captura/permiso antes de fuentes, anchos o colores.

[Abrir esta guía en Ayuda instalada](/help?topic=reports.data-designer)

## Informes: página, variables, campos y resúmenes

Separe valor consultado, formato y lugar donde se imprime.

### Comprenda la página

Use Diseño para editar y Vista previa para ejecución. Dimensiones en milímetros, no píxeles Runtime. Vertical/Horizontal cambian orientación. Contenido debe caber tras márgenes. Zoom de vista previa no cambia dimensiones impresas.

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

Cabecera/pie de informe son del informe; los de página se repiten. Detalle recibe filas; cabecera/pie de grupo dependen de agrupación. Detalle puede quedar vacío con título visible si no hay filas. Empiece con Etiqueta y Campo, ofrecidos directamente.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Altura de sección | Espacio reservado. Un control más alto puede cortarse; ampliar no crea filas. | Ajustar banda al contenido |  |
| X, Y, ancho y alto (mm) | Posición/tamaño de diseño. Revise sección/ancho útil; no son coordenadas del monitor. | Campo dentro de Detalle |  |
| Texto de etiqueta | Contenido fijo, como título. Use Campo para valores por fila; escribir ruta TAG en etiqueta no vincula. | Lecturas de turno |  |
| Consulta y campo | Consulta elige resultado; Campo debe existir en dataset. Use nombre expuesto, como value o timestamp para muestras, no uno inventado. | Consulta main / campo value |  |
| Recurso de imagen | Una imagen necesita recurso existente/resuelto. Si selector no lo ofrece, no invente ID; use controles editables y compruebe disponibilidad.  | Recurso existente cuando disponible |  |

### Agrupación no es agregación temporal

Agrupar por organiza filas por campo y orden. Un resumen elige campo, función y alcance: grupo o informe entero. Intervalo temporal viene de Resolución, no del nombre de grupo. No sume datos ya agregados sin comprenderlo. Conteo son registros, no horas de funcionamiento. Compruebe grupo pequeño manualmente.

### De vista previa a revisión activa

1. Dibuje título, Detalle y campos reales.
2. Ejecute vista previa y revise páginas/cortes.
3. Valide referencias/estructura.
4. Aplique candidato válido; actualice/valide si cambia proyecto.
5. Guarde/publique/active.
6. Genere en Runtime con iguales parámetros y compare. La vista previa no es definición guardada ni revisión activa.

[Abrir esta guía en Ayuda instalada](/help?topic=reports.layout-variables)

## Informes Runtime: generar, comprobar y exportar

La salida pertenece a una ejecución con período y parámetros propios.

### Encuentre el informe disponible

Abra Informes Runtime y busque nombre. La lista depende de revisión activa y autorización. Acceso a Ingeniería u objeto visual no concede todas las consultas. Si vacía, revise revisión/permisos antes de recrear.

### Período, parámetros y ejecución

1. Seleccione informe.
2. Elija relativo o Desde/Hasta si definición permite. Relativo se resuelve para ejecución; repetir después puede cambiar ventana.
3. En absoluto inicio antes de fin y zona horaria correcta.
4. Rellene parámetros con tipo/opciones exigidos; número/fecha/booleano no admite cualquier texto.
5. Genere y espere; cancelar no elimina definición.
6. Revise hora de generación, ventana y parámetros antes de interpretar.

### Vista previa, PDF, hoja de cálculo e impresión

Use páginas/zoom para revisar; ajustar ancho no cambia definición. PDF/impresión ofrecen presentación paginada; XLSX/CSV permiten trabajar salida. Exportan resultados, no paquetes Engineering ni cambios TAG. Regenere para actualizar; abrir exportación antigua no la hace en vivo. Compruebe unidades/fechas/decimales para destinatario.

### Problemas con comprobaciones distintas

Informe ausente: revisión/autorización. Cabecera sin Detalle: consulta vacía, período/filtros/captura. Campo vacío/error: referencias/tipo/parámetros. Número inesperado: resolución/función/rótulo de unidad/alcance resumen. Cortes: milímetros/ancho/altura. Datos antiguos: hora de ejecución y regenerar. No cambie permisos/unidades a ciegas.

[Abrir esta guía en Ayuda instalada](/help?topic=reports.runtime-output)
