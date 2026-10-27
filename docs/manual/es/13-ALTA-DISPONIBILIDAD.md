# Alta disponibilidad: configuración y operación

## HA: configurar dos servidores y seguir la operación

Distinga configuración guardada, servidor activo, compañero listo y protección contra ejecución duplicada.

### Qué preparar primero

HA significa Alta Disponibilidad. Se preparan dos servidores para transferir Runtime entre ellos con protección contra ejecución duplicada. En esta guía, nodo significa cada servidor.

Abra Ingeniería > Alta disponibilidad. Observar requiere licencia HA Runtime y permiso para supervisar los servidores. Guardar u operar requiere también permiso de transferencia. Compruebe red entre servicios, autenticación, relojes y almacenamiento de ambos nodos.

La base puede ser compartida o separada según la instalación preparada. Habilitar HA no crea replicación de datos ni garantiza disponibilidad de la base. Compruebe esa parte con el responsable antes de configurar la pareja.

### Rellene solo lo que depende de la instalación

La pantalla llama a los nodos Este servidor y Servidor asociado. Empiece por las direcciones que permiten comunicar sus servicios. Use el selector para el preferido y el flujo protegido de autenticación. El producto gestiona identidades internas y transporte automático; no invente esos valores. La tabla detalla las elecciones.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección principal de cada servidor | Endpoint accesible entre servicios. La dirección detectada solo sirve si el compañero la alcanza; localhost en dos hosts no es un servidor. | Direcciones reales de ambos servicios |  |
| Dirección externa alternativa | Abra solo si la red exige otra dirección anunciada. En otro caso conserve automática para evitar destinos inconsistentes. | Sin alternativa en red simple |  |
| Servidor preferente | Preferencia de inicio/failback; no fuerza promoción ni establece preparación. | Este servidor o compañero |  |
| Autenticación entre servidores | Configure secreto compartido mediante flujo protegido en ambos nodos. Se limpia del formulario; compruebe estado sin capturarlo. | Configurar autenticación |  |
| Protección y referencia compartida | Es la referencia usada para evitar dos servidores Active a la vez, protección llamada fencing. Ambos deben ver el mismo almacenamiento externo compartido. Dos archivos locales con igual nombre no forman una referencia común. | Recurso compartido provisionado |  |
| Failover automático | Permite cambio automático elegible y exige protección. No sustituye compañero preparado, datos disponibles ni seguridad. | Habilitar tras comprobar preparación |  |

### Procedimiento de preparación

1. Compruebe el modo En ejecución antes de editar. Informa la configuración usada ahora por el servicio.
2. Prepare HA, indique las direcciones de ambos servicios y elija el servidor preferido.
3. Configure autenticación y protección compartida. Resuelva los elementos de Preparación para HA.
4. Use Guardar configuración deseada. Guarda la elección, pero no demuestra que ya esté En ejecución.
5. Si falta reiniciar, planifíquelo y compruebe después la configuración realmente cargada.
6. Actualice el estado. Compruebe Active efectivo, preparación del asociado, base y protección. Standby solo no significa Ready Standby.
7. Antes de usar en el proceso, haga el ensayo manual acordado con el responsable. Las comprobaciones de esta guía no sustituyen esa evaluación.

### Lea estados antes de operar

Active efectivo es el servidor autorizado a ejecutar ahora. Puede no ser el preferido. El asociado puede estar solo en Standby, esperando, o en Ready Standby, con preparación evaluada por el servidor. Lea esa diferencia antes de transferir.

Synchronizing indica sincronización en curso. Promoting y Demoting son transiciones: espere el resultado. Stale avisa de observación antigua. Isolated, Faulted, degradación o autoridad ambigua requieren diagnóstico.

Compruebe por separado disponibilidad de base y comunicación con el asociado. Alcanzar el asociado no demuestra que la base funcione. Aumentar tolerancias de red para ocultar una falla de base no la resuelve.

### Elija la operación correcta

Elija la acción según la situación. Switchover transfiere de forma planificada del Active efectivo a un asociado elegible. Failback solicita volver al preferido cuando se permite. Recovery reconcilia fallas o bloqueos.

Forzar resincronización tiene una condición específica: no debe haber Active y la autoridad anterior debe estar cercada, es decir, impedida de seguir ejecutando. La acción no permite declarar un asociado listo por decisión del operador.

Antes de confirmar, lea acción y destino. Después compruebe resultado, histórico y quién quedó Active. Si se rechaza, investigue la causa antes de repetir.

### Ajustes avanzados: significado y límites

Mantenga inicialmente los tiempos indicados. Controlan cuánto tiempo se considera reciente una información y cómo responde la protección a fallas.

Ajuste solo después de evaluar red, relojes y operación. Que un número esté dentro del límite del formulario no lo convierte en recomendación para su instalación. La tabla explica cada tiempo.

Para comprobar permisos en Seguridad: HighAvailabilityObserve permite observar; HighAvailabilityTransfer se exige también para guardar y operar. Son identificadores de referencia para el administrador.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Freshness (s) | Tiempo máximo para considerar reciente una observación. Después puede ser demasiado antigua para orientar operación; no es el intervalo de adquisición de un TAG. | Conservar valor propuesto | 1–120 s |
| Lease (s) | Vigencia de la autorización temporal del servidor para ejecutar como Active. Al vencer, esa autoridad deja de ser válida. Lease no es una licencia comercial. | Conservar valor propuesto | 3–120 s |
| Poll (ms) | Tiempo entre consultas a la referencia compartida. Un valor menor consulta con más frecuencia. Usa milisegundos, no segundos. | Conservar valor propuesto | 100–10000 ms |
| Edad máxima de witness (s) | Edad máxima de evidencia de compañero listo; no crea preparación. | Conservar valor propuesto | Al menos Lease; hasta 600 s |
| Margen de reloj (s) | Margen por diferencia de relojes; no sustituye sincronización. | Conservar valor propuesto | 0 hasta floor(Lease/3) |

### Si hay problemas

Ante respuesta 403, compruebe permiso para observar u operar. Si Guardado difiere de En ejecución, compruebe reinicio pendiente. Si el asociado no está disponible, revise direcciones, red y autenticación entre servicios.

Si falta la referencia de protección, investigue el almacenamiento externo compartido. Si el asociado no está listo, lea sincronización, base y diagnóstico; no fuerce promoción para eludir la condición.

Investigue un problema por vez. Registre hora, acción, resultado y servidor efectivo, sin incluir el secreto de autenticación.

[Abrir esta guía en Ayuda instalada](/help?topic=ha.configuration-operation)
