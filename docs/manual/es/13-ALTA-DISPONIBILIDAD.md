# Alta disponibilidad: configuración y operación

## HA: configurar dos servidores y seguir la operación

Distinga configuración guardada, servidor activo, compañero listo y protección contra ejecución duplicada.

### Qué preparar primero

HA significa Alta Disponibilidad: dos servidores pueden asumir Runtime de forma controlada. Abra Ingeniería > Alta disponibilidad. Requiere licencia HA Runtime y HighAvailabilityObserve; guardar/operar también HighAvailabilityTransfer. Compruebe red entre servicios, autenticación, relojes y almacenamiento de cada nodo. Bases compartidas o separadas necesitan comprobar topología; habilitar HA no crea replicación ni prueba disponibilidad.

### Rellene solo lo que depende de la instalación

La página distingue Este servidor y Servidor compañero. El operador no necesita inventar identidades internas ni transporte automático.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección principal de cada servidor | Endpoint accesible entre servicios. La dirección detectada solo sirve si el compañero la alcanza; localhost en dos hosts no es un servidor. | Direcciones reales de ambos servicios |  |
| Dirección externa alternativa | Abra solo si la red exige otra dirección anunciada. En otro caso conserve automática para evitar destinos inconsistentes. | Sin alternativa en red simple |  |
| Servidor preferente | Preferencia de inicio/failback; no fuerza promoción ni establece preparación. | Este servidor o compañero |  |
| Autenticación entre servidores | Configure secreto compartido mediante flujo protegido en ambos nodos. Se limpia del formulario; compruebe estado sin capturarlo. | Configurar autenticación |  |
| Protección y referencia compartida | Fencing evita dos ejecutores Active. Ambos deben ver la misma referencia externa compartida; dos archivos locales con igual nombre no son autoridad compartida. | Recurso compartido provisionado |  |
| Failover automático | Permite cambio automático elegible y exige protección. No sustituye compañero preparado, datos disponibles ni seguridad. | Habilitar tras comprobar preparación |  |

### Procedimiento de preparación

1. Compruebe modo En ejecución.
2. Prepare HA con dos direcciones y preferencia.
3. Configure autenticación/protección compartida y resuelva preparación.
4. Guarde configuración deseada; Guardado no equivale a En ejecución.
5. Si se exige, planifique reinicios y compruebe configuración cargada.
6. Actualice y revise Active efectivo, compañero, base y protección. Standby no equivale a Ready Standby.
7. Antes de operar haga ensayo manual acordado con responsable; las comprobaciones del manual no lo sustituyen.

### Lea estados antes de operar

Active efectivo está autorizado a ejecutar, no es solo preferido. Synchronizing aún recibe/verifica estado. Ready Standby indica preparación evaluada; Standby no la prueba. Promoting/Demoting son transiciones. Stale es observación antigua; Isolated, Faulted, degradación o autoridad ambigua exigen diagnóstico. Banco disponible difiere de compañero accesible. No oculte fallo de base aumentando tolerancias de red.

### Elija la operación correcta

Switchover transfiere de Active efectivo a compañero elegible. Failback solicita volver al preferente cuando se permite. Recovery reconcilia fallo/bloqueo. Forzar resincronización es recuperación específica sin Active y con autoridad anterior cercada; no declara listo a un compañero. Lea destino, confirmación y resultado; compruebe historial y Active resultante. No repita rechazo sin cambiar su causa.

### Ajustes avanzados: significado y límites

Parta de valores propuestos. Cambiar tiempos afecta protección y respuesta; evalúe red, relojes y operación. Los límites del formulario no son recomendaciones de rendimiento.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Freshness (s) | Edad máxima de observación reciente. | Conservar valor propuesto | 1–120 s |
| Lease (s) | Validez de autoridad de ejecución; no es licencia comercial. | Conservar valor propuesto | 3–120 s |
| Poll (ms) | Intervalo de consulta de referencia; menor aumenta frecuencia. | Conservar valor propuesto | 100–10000 ms |
| Edad máxima de witness (s) | Edad máxima de evidencia de compañero listo; no crea preparación. | Conservar valor propuesto | Al menos Lease; hasta 600 s |
| Margen de reloj (s) | Margen por diferencia de relojes; no sustituye sincronización. | Conservar valor propuesto | 0 hasta floor(Lease/3) |

### Si hay problemas

403: revise permisos. Guardado difiere de ejecución: reinicio pendiente. Compañero inaccesible: direcciones/red/autenticación. Referencia indisponible: almacenamiento externo compartido. Compañero no listo: sincronización/base/diagnóstico sin forzar promoción. Registre hora, acción, resultado y nodo efectivo; nunca secreto.

[Abrir esta guía en Ayuda instalada](/help?topic=ha.configuration-operation)
