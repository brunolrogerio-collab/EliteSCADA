# Engineering, fuentes de datos y TAGs

Este capítulo prepara fuentes de datos y variables del proyecto. Empiece por una lectura conocida y compruébela antes de usarla en pantallas, alarmas o Scripts. Los nombres siguientes explican cómo llega una edición a la versión en ejecución.

## De la lectura a la pantalla y al comando

La **Data Source** proporciona una lectura; el **TAG** identifica su punto en el proyecto. Dirección y ajustes del vínculo indican qué punto de esa fuente leer. **Schema** es la definición de campos aceptados por el Driver; use formulario y Ayuda de la conexión elegida, sin inventar identificadores.

En pantalla, **binding** hace que una propiedad del objeto siga un TAG. Por ejemplo, un valor mostrado representa presión de bomba. Elija recurso existente con selectores u **Object Browser**, el navegador de referencias del proyecto. Dibujar la lectura no crea otra variable de proceso.

**Server Memory** conserva valores pertenecientes al servidor. **Client Memory** sirve al estado local de este cliente Runtime y puede perder valor al terminar el contexto; otro navegador no lo recibe automáticamente. Un comando solicita una acción al servidor, que comprueba autorización. Revise retorno y estado real: mostrar un botón o cambiar su color no prueba ejecución.

## Working, Save, Revision, Publish y Activate

Working es el borrador editable. Save guarda el trabajo; una Revision es una copia identificada que puede comprobar. Publish prepara la revisión para uso. Activate pone explícitamente la revisión publicada en ejecución, como Active. Guardar o ver el cambio en el editor no cambia por sí solo lo que ejecuta Runtime.

**Ayuda relacionada:** `/help?topic=engineering.lifecycle`.

## Data Sources y Drivers

Cree Data Sources usando tipos registrados en el catálogo del producto. Los campos, formatos, límites, valores predeterminados y referencias protegidas proceden del schema del tipo seleccionado.

Los drivers de comunicación son distintos de los proveedores internos de fuentes. Simulation es una herramienta de desarrollo/pruebas y no se cuenta como Driver de producción en el catálogo de Help.

### Procedimiento: configurar una fuente sin inventar parámetros

**Objetivo:** crear/editar un Data Source usando solamente el contrato real del tipo seleccionado.

**Requisitos previos:** acceso de Engineering y un tipo de Data Source disponible en el catálogo.

**Pasos:**
1. Seleccione el tipo de Data Source.
2. Complete solamente los campos expuestos por el editor/schema de ese tipo.
3. Use los ejemplos y formatos proporcionados por la propia superficie cuando estén disponibles.
4. Ejecute Connection Test, Discover, Browse, Import o Reconcile solamente si el tipo declara esa capability.
5. Corrija los errores en el Data Source antes de intentar ocultarlos en un TAG o en la presentación.

**Resultado esperado:** la configuración sigue siendo compatible con el schema registrado.

**Diagnóstico:** no trasplante la sintaxis de otro protocolo. Una respuesta de rechazo del backend debe corregirse en la configuración de la fuente.

**Ayuda relacionada:** `/help?topic=sources.data-sources` y los temas `driver.<type-key>`.

## TAGs

Los TAGs usan identidad estable y un binding compatible con el Data Source. Los valores, quality y timestamps llegan por el pipeline de Runtime bajo la autoridad del backend.

### Direccionamiento

Use solamente los campos y formatos expuestos por el schema TagBinding del Driver. Las direcciones de protocolos distintos no son intercambiables.

**Ayuda relacionada:** `/help?topic=tags.addressing`.

### Scaling / normalization

El scaling cambia el valor de ingeniería antes de su uso operacional y puede afectar la lógica, las alarmas, el histórico y las pantallas. El formato visual controla solamente la presentación. No use el formato para simular scaling.

**Ayuda relacionada:** `/help?topic=tags.scaling-formatting`.

### Quality y timestamps

Quality representa la confianza/estado de una muestra y debe mantenerse fiel al pipeline. Los datos fallidos, ausentes o stale no deben presentarse como Good solo para estabilizar la UI.

Conserve los timestamps proporcionados por la fuente/Runtime conforme al contrato; no sustituya un timestamp industrial por el reloj del navegador al persistir.

**Ayuda relacionada:** `/help?topic=tags.quality`, `/help?topic=tags.timestamps`.

## Escrituras y comandos

Un control visible no hace que un TAG sea escribible. La escritura depende del contrato de la fuente/TAG y de las capabilities efectivas, aplicadas por el servidor. Una sesión View Only o la ausencia de capability no concede autoridad de escritura.

**Ayuda relacionada:** `/help?topic=tags.writeability`, `/help?topic=bindings-commands.overview`.

## Prueba/commissioning de TAGs

La UI final de commissioning todavía está sujeta a la revisión visual, pero la secuencia de diagnóstico es estable:

1. confirme el backend y la autenticación;
2. confirme la revisión Active cuando la lectura sea operacional;
3. valide el Data Source;
4. valide la configuración/conexión del Driver;
5. valide el binding/direccionamiento;
6. observe quality y timestamp;
7. solo entonces investigue la presentación.

## Copy / Paste / Duplicate

La duplicación copia la configuración de Engineering y crea una nueva identidad estable. Los datos transitorios de Runtime, como el valor actual, quality, timestamp, histórico, historial de alarmas y diagnósticos, no forman parte de la copia.

### Procedimiento: duplicar TAGs con Preview

**Objetivo:** crear copias sin colisiones y sin mutar Working antes de la validación.

**Requisitos previos:** uno o más TAGs seleccionados.

**Pasos:**
1. Seleccione un TAG o active la selección múltiple.
2. Use Copy/Paste o Duplicate.
3. Revise los borradores generados.
4. Corrija las colisiones de ID, path y nombre.
5. Ejecute Preview.
6. Aplique solamente el candidato validado; si Working cambió, vuelva a ejecutar Preview.

**Resultado esperado:** los TAGs nuevos reciben sus propios IDs y Working solo se modifica mediante el Apply validado.

**Diagnóstico:** las colisiones bloquean Preview. No reutilice la identidad estable del TAG de origen.

## Generación secuencial de TAGs

La superficie actual de generación secuencial acepta de 1 a 200 TAGs por operación y exige patrones de nombre/path que contengan `{n}`. El paso numérico del sufijo y el paso de la dirección deben ser enteros distintos de cero.

La generación automática de direcciones en esta superficie es específica de las direcciones Modbus canónicas `coil`, `discrete`, `holding` o `input`, con una referencia entre 0 y 65535. La validación detecta colisiones de dirección en el mismo Data Source antes de Preview.

### Procedimiento: generar una secuencia Modbus

**Requisitos previos:** exactamente un TAG de origen vinculado a un Data Source `modbus.tcp` y a una dirección canónica.

**Pasos:**
1. Abra la generación secuencial para el TAG seleccionado.
2. Defina la cantidad, los patrones de nombre/path, el inicio/paso del sufijo y el paso de la dirección.
3. Genere los borradores.
4. Revise el nombre, path y dirección de cada elemento.
5. Resuelva todas las colisiones.
6. Ejecute Preview.
7. Aplique el candidato validado.

**Resultado esperado:** los TAGs se crean solamente después de Preview/Apply, con identidades nuevas y direcciones derivadas del patrón configurado.

**Límite:** no convierta automáticamente direcciones opacas de otros protocolos en una secuencia Modbus.

## Perfil de Historian

La asociación de un TAG con un perfil de captura es configuración de Engineering y debe preservar la referencia estable del perfil. La política completa de captura sigue dependiendo de la finalización del workstream de Historian; no extrapole comportamientos aún no integrados.
