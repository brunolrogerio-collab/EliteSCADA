# Base de datos: configuración, migración y recuperación

## Base de datos: configurar, migrar y recuperar

Comprenda la base activa antes de validar otro destino o migrar.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-DATABASE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=database.topology; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Dónde entrar y qué resuelve esta página

Abra Ingeniería > Base de datos. Se requiere SystemAdmin. Local Managed usa almacenamiento local administrado; Remote usa PostgreSQL configurado. Esto elige almacenamiento, no redundancia HA. Lea Estado actual y Próxima acción. Un Local Managed saludable no necesita perfil Remote si satisface su instalación.

### Prepare el destino

Acuerde servidor, base, cuenta, certificados y permisos con el administrador. La dirección debe ser accesible por el servicio SCADIVO, no solo el navegador. En Docker localhost dentro del contenedor es ese contenedor. En Windows/Linux los certificados deben existir en el servidor del servicio. Haga backup y planifique mantenimiento.

### Campos básicos y avanzados

Empiece con host, usuario y secreto. Abra Configuración avanzada solo para cambiar valores propuestos. Los ejemplos son demostrativos, no credenciales.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host / FQDN | DNS/dirección de PostgreSQL accesible al servicio, no la URL web. | db.example.invalid |  |
| Usuario / identidad | Cuenta PostgreSQL de aplicación; no su login de Authority SCADIVO. | Cuenta facilitada por el administrador |  |
| Contraseña / secreto | Se envía sin devolverse en el estado. Limpiarse al enviar no significa perder credencial. No incluya en incidencias ni capturas. | Introducir solo en formulario protegido |  |
| Database | Nombre de base destino, no de proyecto. Inicialmente elitescada; confírmelo. | elitescada |  |
| Puerto | Puerto PostgreSQL. Inicialmente 5432, distinto del HTTPS de aplicación. | 5432 |  |
| Modo TLS | Require exige cifrado. VerifyCa valida cadena CA; VerifyFull también el nombre del servidor. Prefer y Disable no ofrecen igual protección. Use el modo acordado con TI; no desactive TLS para ocultar un error. | Require o VerifyFull con CA válida |  |
| Ruta del certificado CA | Archivo CA legible por el servicio; exigido para VerifyCa/VerifyFull. Una ruta del equipo del navegador no sirve a otro host/contenedor. | Ruta provisionada en servidor |  |
| Timeout (s) | Espera de conexión; inicialmente 15 segundos. Aumentarla no corrige usuario, DNS ni certificado. | 15 s |  |
| Historian usa Primary / dedicado | Marcado, histórico usa base principal. Desmarque solo para destino Historian dedicado y complete su endpoint; también debe superar compatibilidad. | Usar Primary en configuración simple |  |

### Migrar datos a otra base

1. Compruebe base activa y haga backup.
2. Complete Remote y Valide destino: conexión, autenticación, TLS y compatibilidad; accesibilidad no basta.
3. Prepare migración y revise origen, destino y datos. La configuración queda bloqueada.
4. En mantenimiento Inicie migración; escrituras durables pueden pausarse.
5. Espere copia y verificación automática.
6. Tras verificación correcta, Active banco Remote y confirme.
7. Si se exige reinicio, reinicie/reabra el servicio y compruebe topología, salud, proyecto y revisión antes de operar.

### Conectar a base existente no es migrar

Conectar a base existente (sin copia) no copia ni mezcla registros. Úselo solo con destino preparado del mismo proyecto y revisión activa exigidos. Lea confirmación. La incompatibilidad bloquea cambio; no vacíe ni modifique base para evitar comprobación. Para transportar datos actuales use migración.

### Revertir y volver al banco local

Si se exige Recovery/Rollback, revise diagnóstico y Revierta operación cuando esté disponible. Volver a Local Managed valida/reactiva almacenamiento local conservado y puede exigir reinicio. No mezcla automáticamente escrituras posteriores de Remote con el local. Conserva la base anterior; volver no restaura un backup ni elimina Remote. Compruebe datos y revisión activos.

### Errores que requieren acciones diferentes

Sin conexión: revise DNS/puerto/acceso del servicio. Autenticación rechazada: cuenta PostgreSQL/secreto. TLS: CA, nombre y validez. Incompatibilidad: versión PostgreSQL, capacidad TimescaleDB exigida y schema. Fase inválida: actualice y siga Próxima acción; no inicie otra copia. Almacenamiento indisponible puede degradar Runtime; memoria no debe simular persistencia. Para soporte registre hora, fase e ID sin contraseñas.

[Abrir esta guía en Ayuda instalada](/help?topic=database.topology)
