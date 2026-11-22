# Sesiones Runtime y diagnóstico

## Sesión Runtime: visualizar, interactuar y finalizar

Entienda cuándo puede observar la aplicación y cuándo puede operar sus controles.

### Observar u operar

Elija Visualización para seguir valores/estados. Solicite Interactiva para usar controles de proceso. Es una solicitud: compruebe siempre Concedida. La cuenta también necesita permiso para la acción. Interactiva no da acceso a equipos no autorizados.

### Comprobar sesión

1. Entre y abra Runtime.
2. Abra menú de cuenta y Clase de sesión Runtime.
3. Elija Visualización para observar o Interactiva para operar.
4. Espere: Solicitada muestra pedido y Concedida la respuesta efectiva.
5. Si pidió Interactiva y recibió Visualización, lea aviso antes de comandar. Puede faltar capacidad interactiva. Consulte al responsable si lo necesita.

### Ejemplo: ve la bomba pero no puede comandarla

Compruebe Concedida. Visualización permite observar, no operar. Si indica Interactiva, revise permiso para esa bomba/acción. Si ambos son correctos, consulte mensaje de comando y diagnóstico de comunicación. Vaya por partes: clase no corrige vínculo de TAG ni avería de equipo.

### Cambiar o finalizar

Elija otra clase en el panel. La anterior se libera antes del nuevo pedido. Si falla, puede tener que solicitar otra sesión; no se conserva automáticamente la anterior. Para terminar elija Finalizar sesión y espere confirmación. Cerrar menú solo oculta panel; salir de cuenta es otra acción. Si falla conexión, confirme liberación; cerrar ventana no es confirmación.

### Consultar controles y avisos

Servidor proporciona resultados; no se escriben números/IDs de sesión. Abra detalles técnicos solo para explicar un rechazo al responsable.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Visualización | Solicita observar la aplicación sin comandos ni escrituras operativas. | Ver el estado de la bomba | No es acceso anónimo ni cambia permisos de cuenta. |
| Interactiva | Solicita capacidad interactiva al servidor. Compruebe la clase concedida antes de operar. | Operar con cuenta autorizada | Puede rechazarse o reducirse a Solo visualización; no garantiza la acción. |
| Solicitada | Clase pedida por este cliente. | Interactiva | No demuestra la clase efectiva. |
| Concedida | Clase efectiva de la sesión admitida. Es el resultado que debe comprobar. | Solo visualización | Si difiere de Solicitada, lea el aviso; la alternativa no es éxito interactivo. |
| Motivo de admisión | Detalle técnico de la decisión. Abra Detalles técnicos al investigar. | Código del servidor | Diagnóstico, no campo editable ni forma de eludir la decisión. |
| Motivo de capacidad | Explica disponibilidad/límites de sesiones. Revise licencia y sesiones ocupadas tras una denegación de capacidad. | Capacidad interactiva no disponible | Capacidad no equivale a permiso de Authority. |
| Finalizar sesión | Libera la sesión Runtime actual tras confirmación del servidor. | Sin sesión Runtime activa | No es Salir de la cuenta; cerrar el menú tampoco finaliza la sesión. |
| Error o aviso | Lea el mensaje de solicitud, finalización o reducción antes de repetir. | Solicitud rechazada | Un fallo de red no confirma liberación ni nueva concesión. |

### Si se rechaza la sesión

Anote mensaje, hora y acción. Cambiar permisos/restaurar puede exigir entrar y solicitar sesión otra vez. No copie IDs/cabeceras para obtener acceso. En permisos ProcessValueWrite es escritura de valor y CommandExecute ejecución de comando: autorizaciones distintas. Licencia limita recursos; permisos de cuenta definen acciones.

[Abrir esta guía en Ayuda instalada](/help?topic=runtime.session-classes)

## Diagnóstico: localizar la primera causa del fallo

Interprete salud del host, comunicación, red y calidad del TAG antes de cambiar una pantalla o driver.

### Elija la capa correcta

Una pantalla detenida puede deberse a sesión, revisión activa, comunicación, TAG o presentación. Diagnóstico reúne evidencias; abrir el panel no reinicia una fuente ni cambia el proceso. En Ingeniería, abra Diagnóstico de comunicación. Observa Runtime / Active Revision: una fuente creada solo en Working aún puede no aparecer. Empiece por el host API/DriverHost; después fuente, red/protocolo, muestra y pantalla. Corrija la primera divergencia de origen antes de cambiar colores o insertar valores fijos.

### Comprobar una fuente paso a paso

1. Registre TAG/fuente, hora, usuario y acción fallida; confirme revisión activa.
2. Compruebe Host API / DriverHost y Observado. Host no disponible/desactualizado no confirma estados actuales.
3. Limpie la búsqueda o introduzca parte de fuente, driver o endpoint. Empiece con Todos o Requiere atención.
4. Seleccione la fuente. Lea estado, Último error, último éxito/fallo y edad de datos.
5. Compare calidad y contadores en dos observaciones; un total acumulado no fecha cada fallo.
6. Use alcance de red, Connection Test o Test Read solo si están disponibles y son necesarios.
7. Vuelva al TAG y confirme valor, calidad, hora y actualización; después revise la dinámica de pantalla.

### Búsqueda, actualización y confianza

Los filtros solo cambian la lista mostrada. Detener la actualización del panel no desactiva el driver ni la adquisición.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Buscar Data Source, driver o endpoint | Filtra por parte de nombre/clave de fuente, driver o endpoint. | Parte del nombre de la bomba | No crea fuentes; limpie la búsqueda si parece no haber datos. |
| Filtro | Todos muestra las fuentes disponibles; Requiere atención incluye estados no saludables; Solo saludables reduce a los confirmados. | Requiere atención | Sin coincidencias puede ser solo el filtro. Inactiva no es lo mismo que filtrada. |
| Actualización automática | Actualiza observaciones cada 3 segundos mientras esté habilitada. | Habilitada durante la investigación | No cambia scan, retención ni adquisición. |
| Actualizar ahora | Solicita una nueva lectura del diagnóstico. Espere antes de comparar. | Dos observaciones consecutivas | No reinicia ni reconecta manualmente el driver. |
| Host API / DriverHost — Observado | Compruebe si la observación de salud del proceso sigue reciente. | Hora de la observación | Host desactualizado/no disponible deja sin confirmar los estados de fuentes. |
| Totales del panel | Fuentes, saludables, atención, fallo y TAGs Good orientan la revisión inicial. | 2 fuentes, 1 requiere atención | No sustituyen errores individuales ni demuestran salud de todos los puntos. |

### Leer estado operativo y contadores

La ausencia de datos aparece como guion en varios indicadores; no significa cero ni éxito. Instancia Runtime distingue ejecuciones: reiniciar/recrear puede volver incomparables los totales. Ciclos cuentan ejecuciones de adquisición; requests son solicitudes de protocolo. Éxitos/fallos cuentan resultados de operaciones; fallos consecutivos destacan la secuencia reciente sin recuperación. Timeouts son operaciones que no terminaron en plazo. Conexiones, desconexiones y reconexiones ayudan a observar estabilidad del enlace. Lecturas/escrituras cuentan operaciones, no necesariamente TAGs distintos. Actualizaciones publicadas indican valores enviados a Runtime, no persistencia garantizada en Historian. Compare con el driver y el mismo período/instancia; no todos los protocolos cuentan igual.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Estado | Saludable, degradado, reconectando, fallo o detenido orienta la investigación; lea también Último error. | Reconectando | Endpoint accesible no sustituye el estado del driver. |
| Endpoint | Destino de la fuente seleccionada. Compruebe que sea el equipo esperado. | Destino configurado | Puede ser serie o puente local; no todo endpoint es host/puerto TCP. |
| Último éxito | Cuándo hubo comunicación correcta. Compare con ahora y cadencia esperada. | Éxito hace pocos segundos | Éxito antiguo no demuestra actualidad del punto. |
| Último fallo | Sitúa temporalmente un fallo informado. Compare con un éxito posterior. | Fallo anterior al último éxito | Solo no indica si el fallo continúa. |
| Cambio de estado | Cuándo cambió el estado informado. | Empezó a reconectar | No necesariamente es la hora de una muestra de proceso. |
| Edad de los datos | Ayuda a detectar falta de actualización. Compare con comportamiento/scan esperados. | Edad creciente | No toda fuente es cíclica; revise adquisición antes de declarar defecto. |
| Scan configurado | Cadencia configurada cuando corresponde. | Intervalo del driver | No se edita aquí; las actualizaciones por evento pueden diferir. |
| Latencia y último ciclo | Media/última latencia y duración de ciclo ayudan a separar lentitud de falta de actualización. | Mayor de lo habitual | Duración de operación no es retraso completo sensor-píxel. |
| Tasa reciente de fallo | Observe la proporción reciente de operaciones fallidas. | Tendencia creciente | No es eficiencia del equipo ni porcentaje de TAGs malos. |
| Actividad y contadores | Compare ciclos, requests, éxitos, fallos consecutivos, timeouts, conexiones/reconexiones, lecturas, escrituras y actualizaciones publicadas. | Fallos aumentan entre observaciones | Los acumulados y cambios de instancia requieren contexto; no reinicie el sistema para interpretarlos. |
| Calidad de los TAGs | Separe Good, BadCommunication, Otras y Sin muestra. Revise el TAG afectado aunque otros sean Good. | 1 punto sin muestra | Sin muestra no es cero; Good no demuestra exactitud física del sensor. |
| Detalles del protocolo | Lea lo que proporciona ese driver y comunique el mensaje exacto. | Error propio del protocolo | Ausencia de detalles no permite inventar diagnóstico; varían entre drivers. |

### Probar alcance de red

Alcance de red (origen: servidor) intenta TCP e ICMP sobre un destino. El origen es el host servidor, no el navegador; localhost/127.0.0.1 significa ese entorno, incluido el contenedor cuando corresponda. Pruebe solo el destino necesario y autorizado. El panel usa timeout de 3 segundos por intento; TCP/ICMP son resultados separados y el total puede tardar más. Requiere permiso de modificar Engineering y puede limitarse por frecuencia.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host o IP | Un nombre DNS o IP sin protocolo, ruta, contraseña ni rango. | 127.0.0.1 en un ensayo local | Sin http://, URLs ni barridos. Compruebe el origen. |
| Puerto único | Puerto TCP del servicio esperado. Solo cambia el destino del ensayo, no la configuración del producto. | 502 para un servicio Modbus TCP conocido | Entero de 1 a 65535; no es prueba UDP ni serie. |
| TCP | Connected significa conexión aceptada; Refused indica rechazo; Timeout significa que no terminó en plazo. | Connected | No autentica, negocia protocolo ni lee registros. |
| ICMP | Success confirma respuesta ICMP. Timeout/Unavailable puede deberse a red o política del host. | Unavailable con TCP Connected | ICMP bloqueado no demuestra TCP no disponible; revise cada resultado. |
| Duración y hora del ensayo | Sitúe la observación y repita de forma dirigida después de corregir la causa. | Observado ahora | No garantiza disponibilidad futura ni latencia completa de proceso. |

### Ejemplo de interpretación

Una fuente Modbus muestra timeouts y un TAG sin muestra. TCP Connected con ICMP Unavailable solo demuestra que un puerto aceptó la conexión, no unidad Modbus, registro ni valor. Revise host/puerto/unidad, permisos del destino y vínculo. Si el host API/DriverHost está desactualizado, recupere esa observación antes de afirmar salud del driver. Tras corregir, compare muestras nuevas y retorno del TAG. El fragmento es una petición técnica de ensayo local, no script de barrido ni cambio de puerto del producto.

```json
{
  "host": "127.0.0.1",
  "port": 502,
  "timeoutMilliseconds": 3000
}
```

### Otros fallos y evidencia para soporte

Ante permiso denegado, revise cuenta y acción exacta; no cambie usuario para ocultarlo. Ante revisión divergente, compare Working, Published y Active. Ante error de Script, lea su diagnóstico y tipos/valores; red saludable no valida sintaxis. Si pantalla y TAG difieren, compruebe vínculo, escala, unidad y calidad. Registre hora, proyecto/revisión activa, fuente/TAG, mensaje, dos observaciones y reproducción. Excluya contraseñas, tokens y claves de capturas/soporte. Un síntoma documentado es evidencia, no causa confirmada.

[Abrir esta guía en Ayuda instalada](/help?topic=diagnostics.overview)
