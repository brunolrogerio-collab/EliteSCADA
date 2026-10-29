# Base de datos: configuración, migración y recuperación

## Base de datos: configurar, migrar y recuperar

Comprenda la base activa antes de validar otro destino o migrar.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-DATABASE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=database.topology; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Dónde entrar y qué resuelve esta página

Esta pantalla define dónde guarda datos SCADIVO. Abra Ingeniería > Base de datos con permiso para administrar el sistema (SystemAdmin).

Local Managed es la base local administrada por la instalación. Remote es un PostgreSQL preparado en otro destino. Si la base local está sana y cubre sus necesidades, puede seguir usándola sin completar Remote.

Antes de cambiar algo, lea Estado actual y Próxima acción. Elegir una base define almacenamiento; no configura por sí solo redundancia HA entre dos servidores.

### Uso y retención del Historian

En **Uso y retención del Historian**, revise los tamaños medidos en la base utilizada por el Historian. **Base del Historian** incluye todos los datos de esa base; **Historian total** reúne las muestras sin procesar y los agregados que el provider pudo medir. Si la topología utiliza bases separadas, esta pantalla mide solo la base del Historian; no suma la base principal ni representa el espacio libre del volumen del servidor. El espacio libre y el crecimiento diario solo aparecen si existe una medición confiable. Si la pantalla indica que no se informaron, no significa que sean cero.

**Sin expiración automática** significa que SCADIVO no programó cuándo eliminar las muestras sin procesar. No significa que el disco sea ilimitado. Para conservarlas durante un período, desmarque la opción e indique los días. Una política más corta puede eliminar muestras antiguas; marque la confirmación solo después de revisar el período y el impacto. TimescaleDB ejecuta esa política, por lo que la eliminación no tiene que ocurrir en el instante de guardar.

En la primera aplicación, si no hay una política de SCADIVO registrada, el formulario pide confirmar antes de asumir las políticas Timescale de retención y actualización de agregados. Se desactivarán los niveles de agregación sin configuración registrada y se perderán las reglas externas no registradas. Consulte con quien administra la base antes de continuar.

#### Definir un límite por tamaño

El límite en **GiB** controla solo el espacio medido de los datos sin procesar del Historian, incluidos los bloques, índices y datos auxiliares de TimescaleDB. No limita agregados, alarmas, eventos, Auditoría, la base principal ni el espacio libre del servidor. Los agregados mantienen su propia retención.

En **Al alcanzar el límite**, elija **Solo avisar** para recibir el aviso sin borrar datos. **Eliminar bloques completos más antiguos** exige confirmar que las muestras sin procesar pueden perderse permanentemente. Cada minuto, el mantenimiento puede eliminar hasta cuatro intervalos antiguos completos y siempre conserva el intervalo más reciente. Como los bloques tienen su propio tamaño y duración, el límite es una meta: el uso puede superarlo hasta la siguiente ejecución y seguir por encima cuando solo quede el intervalo protegido. No es un tope instantáneo ni garantiza espacio libre en el volumen del servidor.

El límite por tamaño y la retención por días pueden coexistir; se aplica primero la condición que elimine las muestras. Revise el valor medido después del mantenimiento y planifique la capacidad con margen. **El archivado y la restauración verificada todavía no están disponibles en esta pantalla.**

### Prepare el destino

Pida al administrador los datos del destino: dirección, base, cuenta, permisos y certificados. Haga backup y acuerde una ventana de mantenimiento antes de migrar. La migración puede pausar escrituras.

La conexión parte del servicio SCADIVO. Abrir una dirección en el navegador no demuestra que el servicio la alcance. En Docker, localhost es el propio contenedor; use un nombre o dirección accesible realmente por el servicio.

El certificado también debe existir en el servidor que ejecuta el servicio, sea Windows, Linux o contenedor. Una ruta disponible solo en el equipo del navegador no cumple ese requisito.

### Migrar datos a otra base

1. Compruebe qué base está activa y tenga un backup disponible.
2. Complete Remote y use Validar destino. Se comprueban conexión, autenticación, protección TLS y compatibilidad; conectar no basta para permitir el cambio.
3. Prepare la migración y revise origen, destino y datos incluidos. Después queda bloqueada la configuración.
4. En la ventana de mantenimiento use Iniciar migración. Las escrituras durables pueden pausarse durante la operación.
5. Espere la copia y verificación automática. Lea el resultado antes de continuar.
6. Solo tras verificar correctamente, use Activar base Remote y confirme el destino.
7. Si se pide reinicio, reinicie o reabra el servicio según la instalación. Antes de retomar, compruebe base activa, salud, proyecto y revisión.

### Conectar a base existente no es migrar

Use Conectar a base existente (sin copia) solo si el destino ya está preparado con el mismo proyecto y revisión activa exigidos por la instalación. No copia ni combina registros. Para llevar los datos actuales al nuevo destino, use migración.

Lea la confirmación antes de cambiar. Si proyecto o revisión son incompatibles, se bloquea la operación. No vacíe ni altere la base para eludir la comprobación.

### Revertir y volver al banco local

Si aparece Recovery/Rollback necesario, la operación necesita recuperación o reversión. Lea el diagnóstico y use Revertir operación cuando esté disponible.

Volver a Local Managed verifica y reactiva la base local preservada; puede pedir reinicio. No combina automáticamente los datos escritos en Remote después del cambio. Se conserva la base anterior, pero no equivale a restaurar un backup. Tampoco elimina Remote.

Al volver, compruebe qué datos, proyecto y revisión están realmente activos antes de retomar la operación.

### Campos básicos y avanzados

Para preparar Remote, empiece por dirección, usuario y contraseña dados por el administrador. Los demás campos pueden conservar sus valores cuando sean adecuados a la instalación. Abra Configuración avanzada para consultar o cambiar detalles. La tabla explica cada elección; los ejemplos no son credenciales reales.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host / FQDN | DNS/dirección de PostgreSQL accesible al servicio, no la URL web. | db.example.invalid |  |
| Usuario / identidad | Cuenta PostgreSQL de aplicación; no su login de Authority SCADIVO. | Cuenta facilitada por el administrador |  |
| Contraseña / secreto | Se envía sin devolverse en el estado. Limpiarse al enviar no significa perder credencial. No incluya en incidencias ni capturas. | Introducir solo en formulario protegido |  |
| Database | Nombre de base destino, no de proyecto. Inicialmente elitescada; confírmelo. | elitescada |  |
| Puerto | Puerto PostgreSQL. Inicialmente 5432, distinto del HTTPS de aplicación. | 5432 |  |
| Modo TLS | TLS protege la conexión. Require exige cifrado; VerifyCa también verifica la cadena de la autoridad certificadora; VerifyFull comprueba además el nombre del servidor. Prefer y Disable no garantizan la misma protección. Use el modo acordado con TI; no desactive TLS solo para ocultar un error. | Require o VerifyFull con CA válida |  |
| Ruta del certificado CA | Archivo CA legible por el servicio; exigido para VerifyCa/VerifyFull. Una ruta del equipo del navegador no sirve a otro host/contenedor. | Ruta provisionada en servidor |  |
| Timeout (s) | Espera de conexión; inicialmente 15 segundos. Aumentarla no corrige usuario, DNS ni certificado. | 15 s |  |
| Historian usa Primary / dedicado | Marcada, el histórico guardado, llamado Historian, usa la base principal. Desmarque solo si preparó una base dedicada al histórico. Complete entonces sus datos de conexión y valide también la compatibilidad de ese destino. | Usar Primary en configuración simple |  |

### Errores que requieren acciones diferentes

Empiece por el mensaje recibido. Sin conexión, compruebe nombre/dirección, puerto y acceso desde el servicio. Si se rechaza autenticación, revise cuenta PostgreSQL y contraseña. Ante error TLS, compruebe autoridad certificadora, nombre del servidor y vigencia del certificado.

En incompatibilidad, lea requisitos de versión PostgreSQL, TimescaleDB cuando corresponda y schema, la estructura de datos esperada. En fase inválida, actualice el estado y siga Próxima acción; no inicie otra copia mientras haya una en curso.

Con base no disponible, Runtime puede quedar degradado. El producto no debe presentar escrituras en memoria como persistidas en la base. Para soporte registre hora, fase e ID de operación, sin contraseña.

Para retención de muestras, archivado programado, rotación y restauración del Historian, consulte [Captura y consulta del Historian](07-HISTORIAN-CAPTURA-CONSULTA.md). Los archivos del Historian se administran desde la API y no forman parte de la copia de seguridad de la configuración del proyecto.

[Abrir esta guía en Ayuda instalada](/help?topic=database.topology)
