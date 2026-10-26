# Monitores e Información

## Monitor de desarrollo: observar referencias

Prepare una lista de TAGs, memorias y diagnósticos sin escribir ni forzar valores.

### Elija este monitor para comparar variables

En Ingeniería abra Monitor de Desarrollo. Reúne referencias elegidas en una tabla de solo lectura. El catálogo usa la ingeniería abierta, pero valores de TAG y Memoria del Servidor vienen del Runtime accesible; definir en Working no crea muestra activa. Memoria del Cliente pertenece a este navegador. Estados de sistema/driver son diagnósticos, no medidas físicas. Use TAG Monitor para detalles/historia del punto y Diagnóstico de comunicación para salud de conexión.

### Añadir y observar

1. Compruebe proyecto abierto y aplicación activa.
2. En Explorar referencias del proyecto busque/expanda familias y carpetas y seleccione para añadir.
3. Alternativamente use Agregar referencia exacta y Agregar o Enter. Exige referencia/path exacto, no búsqueda parcial.
4. Lea Fuente, Tipo, Valor, Calidad / Estado y Última actualización juntos.
5. Añada otras referencias y espere observaciones nuevas.
6. Elimine una fila o Limpie la lista al terminar. No elimina TAGs ni definiciones del proyecto.

### Campos y acciones

Use el árbol para evitar errores y resolver nombres ambiguos. La lista se guarda en la sesión del navegador; no es configuración publicada ni persistencia garantizada después de cerrarla.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Agregar referencia exacta | Recibe path/referencia completa. Etiqueta compartida es ambigua; elija en el árbol. | Plant.Motor.Status | No crea TAG ni busca fragmentos; ausente/ambigua produce mensaje. |
| Explorar referencias del proyecto | Búsqueda/árbol de familias para TAG, memoria, sistema, driver o asset existente. | Elegir en el árbol | Presencia en catálogo no garantiza valor vivo; assets no se convierten en señales de proceso. |
| Nombre / Referencia | Etiqueta y referencia de la fila. Verifique antes de comparar. | Plant.Motor.Status.03 | Una vista de bit no es nuevo TAG físico. |
| Fuente | Distingue TAG, Memoria del Servidor, Memoria del Cliente, Sistema, Driver y Asset. | Memoria del Cliente | Memoria local no es valor de otro navegador. |
| Valor | Valor disponible o guion si falta/no es representable. | 8 o true | Guion no es cero/false; Int64 debe conservar precisión. |
| Tipo | Tipo de referencia/muestra; una vista de bit es Boolean. | Int16 → bit Boolean | Observar no convierte la definición original. |
| Calidad / Estado | Calidad de TAG; referencias locales/diagnósticas pueden mostrar LocalSession, Available o Unavailable. | Good / LocalSession | Son conceptos distintos; estado local no demuestra adquisición física Good. |
| Última actualización | Hora de origen si existe; si no, hora de observación. | Hora de lectura observada | Memoria local no obtiene timestamp de sensor; compruebe origen. |
| Eliminar | Quita una referencia de la lista. | Eliminar una fila | No borra recurso, muestra ni historia. |
| Limpiar | Vacía la lista de esta sesión. | Tabla sin elementos | No elimina aplicación ni detiene adquisición. |

### Ejemplo: observar un bit de palabra

Una palabra Int16 de valor 8 tiene encendido el bit de índice 3: 8 = 2³. El sufijo .03 elige ese bit contando desde cero; .00 es el primero. Añada palabra y vista para comparar. El tipo base debe permitir bits dentro de su anchura. .03 no es decimal, dirección de bobina ni campo independiente. Nombres ficticios: el TAG debe existir con muestra entera válida. Observar no escribe en la palabra.

```json
{
  "reference": "Plant.Motor.Status",
  "dataType": "Int16",
  "value": 8,
  "bitReference": "Plant.Motor.Status.03",
  "expectedBit": true
}
```

### Sin valor o actualización

Referencia no encontrada: revise path/proyecto; nombre ambiguo exige selección explícita. Definición solo Working: compruebe Active antes de esperar valores. Unavailable/sin muestra: revise permiso, servidor, fuente y calidad; no oculte ausencia con cero. La tabla combina actualización agrupada y realtime; desconexión no demuestra actualidad del último valor. Client Memory observa contexto local, no otro Runtime. Revise diagnóstico del recurso y registre referencia, estado y hora. No hay comando de escritura/force aquí.

[Abrir esta guía en Ayuda instalada](/help?topic=engineering.development-monitor)

## TAG Monitor: comprobar valor, calidad e historia

Compare Working y Active e investigue un TAG del Runtime real sin confundir edición y ejecución.

### Identifique la fuente observada

Abra Ingeniería → Diagnósticos → TAG Monitor. Arriba se identifica ingeniería y aplicación activa; el Inspector consulta TAGs visibles del Active Runtime. Working es contexto, no origen sustituto de valores. Si está activa simulación/demo, ese es el origen informado, no prueba de comunicación física. Solo lectura: incluso un TAG Grabable no puede escribirse desde este panel.

### Comprobar paso a paso

1. Compare Contexto Engineering, Revisión Working e Identidad activa. Si Working difiere, no suponga que la edición corre.
2. En Buscar TAGs escriba parte de path, nombre, tipo, unidad, fuente o valor.
3. Filtre Calidad/Acceso; limpie si falta un punto esperado.
4. Seleccione y revise valor, tipo, unidad, calidad, fuente, horas y Actualización del valor.
5. Consulte Histórico reciente y Actualizar histórico.
6. Compruebe permiso, captura y muestras del intervalo antes de declarar defecto por historia vacía. Use Histórico completo para períodos/Playback y exportación.

### Contexto y filtros

Working vs Active compara proyecto, revisión y cambios sin guardar. Es comprobación de contexto; no publica ni activa.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Contexto Engineering | Proyecto abierto para editar. | Proyecto de estación | No garantiza que esté activo. |
| Revisión Working | Revisión base cargada; cambios sin guardar pueden diferir. | Revisión 12 | No es automáticamente la ejecutada. |
| Identidad activa | Proyecto/revisión observados, simulación o ausencia de Active. | Proyecto · rev 11 | Identidad no disponible no es comparación confirmada. |
| Working vs Active | Alineado/diferente orienta comparación; lea avisos de modo/disponibilidad. | Working difiere del Active | Guardar no implica Publicar/Activar. |
| Activado en | Hora informada de activación. | Hora de activación | No es hora de muestra. |
| Buscar TAGs | Filtra metadatos/valor de TAGs visibles. | Parte del path o unidad | Puede existir sin ser visible a su cuenta. |
| Calidad | Todos, Good, Atención, Bad o Sin muestra localizan la condición. | Sin muestra | No cambia calidad; ausencia puede ser filtro. |
| Acceso | Todos, Solo lectura o Grabable según atributo del TAG. | Grabable | No es permiso efectivo de escritura; panel sigue solo lectura. |
| Actualizar | Consulta de nuevo la lista y observa estado realtime/periódico. | Nueva consulta | No reinicia adquisición ni borra historia. |

### Detalles e historia reciente

Lea calidad y actualización junto al valor. Valor antiguo, consulta reciente válida y nueva muestra de proceso son cosas distintas.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Valor actual | Valor mostrado con unidad si existe. | Presión medida | Sin muestra no es cero; aspecto no demuestra salud. |
| Calidad | Condición informada como Good o BadCommunication. | BadCommunication | Good solo no demuestra calibración ni actualización reciente. |
| Tipo y unidad | Tipo de dato y unidad configurada. | Double · bar | Texto de unidad no convierte la física del sensor. |
| Origen / Data Source | Fuente del punto, a comparar con el equipo esperado. | Fuente configurada | No es el nombre de pantalla. |
| Timestamp SCADIVO | Hora asociada a muestra mostrada por Runtime. | Hora de muestra | Compare horas; no es apertura de página. |
| Timestamp de origen | Hora de origen cuando la proporciona. | Hora del dispositivo | Puede faltar; relojes/driver requieren contexto. |
| Timestamp del servidor | Hora del servidor cuando consta en muestra. | Hora del servidor | No significa medición del sensor en ese instante. |
| Actualización del valor | Actualizado, envejeciendo, desactualizado o sin timestamp, con Motivo. | Datos desactualizados | Consulta correcta no equivale a muestra nueva. |
| Última solicitud / respuesta válida | Separa fallo de consulta de muestra que no avanza. | Respuesta recibida ahora | Puede devolver valor existente; revise hora de muestra. |
| Path, ID estable y descripción | Identifica recurso para comparar/diagnosticar. | Path legible | No hay ID que completar; no cambie identidad para resolver fallos. |
| Histórico reciente | Consulta limitada del punto; ventana predeterminada de 15 minutos, hasta 120 muestras solicitadas. | Valores recientes | No es toda la historia ni configura retención/captura; no hay control editable de ventana aquí. |
| Actualizar histórico | Consulta de nuevo punto e historia reciente. | Nueva consulta | No crea muestras ausentes ni fuerza grabación. |

### Ejemplo: Working nuevo y Runtime anterior

Edita revisión 12, pero Identidad activa muestra 11. Los valores pertenecen al Runtime activo, no a la nueva definición Working. Revise, valide y siga Guardar → Publicar → Activar cuando esté listo; luego compruebe identidad/muestra. No fuerce el monitor a mostrar la revisión editada. En simulación confirme origen de prueba antes de usar números como evidencia de campo.

### Fallos y resultados vacíos

Lista vacía difiere de ningún resultado filtrado. Permiso denegado exige revisar cuenta/recurso, no prueba inexistencia. TAG eliminado de Active puede dejar detalles no disponibles. Realtime conectado describe transporte, no salud del sensor; actualización periódica puede seguir si falla realtime. Historia vacía puede ser captura deshabilitada, sin muestras en intervalo, permiso o servicio no disponible. Lea mensaje y revise Historian antes de cambiar datos. Respuesta antigua visible tras fallo no es muestra nueva.

[Abrir esta guía en Ayuda instalada](/help?topic=engineering.tag-monitor)

## Información: identificar producto y proyecto

Compruebe versión instalada y distinga versión del producto, esquema y revisión de aplicación.

### Cuándo usarlo

En Ingeniería abra Información para identificar producto y contexto en soporte, comparación o análisis de paquete. Es consulta: no activa revisión, actualiza software, cambia esquema ni revela consumo de licencia; tienen sus propios flujos.

### Consultar e interpretar

1. Compruebe Versión del producto. Si falta, registre la condición, no deduzca del nombre de proyecto.
2. Abra Detalles técnicos para comparar paquete/contexto o informar soporte.
3. Compruebe Proyecto, Revisión base y Snapshot cargado.
4. Consulte Active en TAG Monitor/Runtime para saber qué ejecuta.
5. Informe versión, proyecto/revisión, hora y mensaje evitando publicar detalles privados.

### Qué significa cada dato

Los presenta el sistema; no son campos para rellenar.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Versión del producto | Versión SCADIVO instalada, distinta de revisión de proyecto. | Versión mostrada | Por sí sola no identifica la aplicación y la revisión activas. |
| Schema de Engineering | Formato/versión de estructura del paquete. | Identificación del formato | No es versión PostgreSQL ni revisión de proyecto. |
| Revisión base | Base del Working cargado; puede no estar guardado. | Revisión mostrada | Cambios posteriores pueden no estar guardados/publicados/activos. |
| Snapshot cargado | Hora del retrato de ingeniería cargado. | Hora mostrada | No es sensor, backup completo ni prueba de activación. |
| Proyecto | Nombre/clave del contexto Engineering abierto. | Nombre del proyecto | Compruebe Active aparte; nombre no es clave de licencia. |
| Commit de build | Identidad técnica de compilación si está disponible. | Identificación mostrada | Puede faltar; no invente un identificador ni confunda con revisión de aplicación. |

### Ejemplo de comparación

Dos estaciones pueden compartir versión del producto y tener proyectos/revisiones diferentes; o compartir proyecto con distintas versiones de producto. Compare cada dato en su función y confirme Active antes de atribuir diferencia a pantalla, driver o licencia.

### Si falta información

El operador no rellena manualmente versión no disponible o commit ausente. Registre y consulte al responsable de instalación. Para consumo/capacidad abra Licenciamiento; para valor/calidad use TAG Monitor; para comunicación use Diagnóstico. Un dato de identidad no demuestra defecto de proceso.

[Abrir esta guía en Ayuda instalada](/help?topic=engineering.information)
