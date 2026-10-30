# Backup y recuperación

## Backup y recuperación: Authority, aplicación y datos

Separe archivos, valide antes de aplicar y entienda lo que restaura cada etapa.

### Elija qué recuperar

Antes de restaurar identifique qué perdió y qué copias tiene. La aplicación .escadapkg lleva ingeniería: TAGs, pantallas, Scripts y otros objetos portables. Security Authority guarda usuarios, contraseñas y política en otro backup protegido. Licencia es archivo separado.

Base, histórico Historian y claves/material protegido de drivers tienen procedimientos propios. El paquete no es copia completa de instalación ni contiene usuarios, hashes o credenciales de login. Solo .escadapkg no garantiza recuperar acceso, histórico o claves.

### Preparar y comprobar backups

Exporte la aplicación por el flujo disponible y compruebe proyecto, revisión y destino. Resuelva cambios sin guardar antes de considerar recuperable la copia. Espere fin de descarga.

Cambiar la aplicación ofrece backups Application y Authority antes de detach, la desvinculación. Puede exportar y volver sin cambiar. No haga detach solo para probar backup.

Nueva contraseña del backup protege el archivo Authority; no es contraseña de login. Guárdela por medio protegido separado del archivo. Prepare copias/recuperación de base/Historian y claves de drivers. Este flujo no define la retención ni incluye archivos del Historian. Para archivar y restaurar muestras sin procesar, use Archivar y restaurar muestras, explicado en [Captura y consulta del Historian](07-HISTORIAN-CAPTURA-CONSULTA.md); la API administra esos archivos por separado.

### Recuperar Authority y aplicación en instalación neutra

1. Confirme instalación neutra elegible y servidor destino correcto. No haga detach solo para abrir el flujo.
2. En configuración inicial elija Restaurar backup y seleccione aplicación, Authority, contraseña y licencia opcional.
3. Use Validar backups. Ante rechazo corrija archivo, compatibilidad o credencial. La extensión no sustituye validación.
4. Con archivos válidos use Restaurar Authority.
5. Entre con Administrador restaurado habilitado y permisos efectivos exigidos. El nombre developer solo no basta.
6. Continúe. El servidor valida de nuevo la aplicación con Authority restaurada.
7. Cuando se permita use Importar aplicación y espere resultado completo. Restaurar Authority y recuperar aplicación son etapas distintas, no promesa de transacción única entre archivos.

### Recuperar solo aplicación con Authority existente

Si no hay proyecto persistido pero sí Authority, entre con Administrador compatible y elija Importar aplicación. Seleccione .escadapkg, Valide, lea el resultado y después importe. Exige paquete compatible, catálogo/estado de destino apropiados y autorización; no es sobreescritura genérica de cualquier aplicación activa. Para modificar una aplicación instalada use ingeniería/portabilidad correspondientes o el procedimiento de cambio. No elimine usuarios/base para que aparezca la opción.

### Entienda los campos de archivos

En instalación neutra elegible, Restaurar backup prepara Authority y aplicación. Si Authority existe y solo falta la aplicación, use Importar aplicación con Authority actual; no fuerce la configuración inicial para eludir el estado existente.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Paquete de aplicación (.escadapkg) | Elija la aplicación exportada a recuperar. Compruebe identidad/compatibilidad al validar. | Archivo de aplicación exportada | Seleccionar no importa ni activa; nombre/extensión no demuestran validez. |
| Backup de Authority (.json) | En bootstrap seleccione el backup protegido de Authority. | Backup separado de Authority | No sustituya por JSON editado ni paquete de aplicación. |
| Contraseña del backup de Authority | Use la contraseña elegida al exportar. Abre el archivo, no una sesión de usuario. | Contraseña guardada separadamente | De 12 a 1024 caracteres. Sin contraseña de ejemplo ni recuperación editando archivos. Revise origen/credencial si se rechaza. |
| Licencia (opcional) | Seleccione para intentar instalar después de recuperar la aplicación. | Archivo de licencia compatible | Opcional; puede fallar quedando la aplicación recuperada sin la licencia deseada. |
| Validar backups | Comprueba Authority y paquete antes de permitir restaurar Authority. | Backups válidos | Preview no restaura. Cambiar archivo/contraseña exige validar otra vez. |
| Restaurar Authority | Aplica Authority validada si bootstrap está permitido. Después entre con Administrador restaurado. | Authority restaurada | Acceso restaurado no equivale a aplicación importada; no reutilice sesiones antiguas. |
| Validar aplicación | Comprueba el paquete contra estado/Authority actuales, incluso después de restaurar Authority. | Paquete válido compatible | La validación anterior a restaurar no sustituye la nueva. |
| Importar aplicación | Ejecuta recuperación aceptada: sustituir Working, persistir revisión raíz, publicar y activar por lifecycle. | Application importada, publicada y activada | El éxito completo exige todas las etapas; un estado intermedio no es recuperación integral. |
| Continuar recuperación | Retoma tras login con archivos conservados por el navegador. | Archivos ya seleccionados | Si se perdieron, seleccione y valide otra vez; nombres/tamaños no sustituyen comprobación del servidor. |
| Continuar sin licencia | Prosigue si aplicación recuperada pero licencia opcional falló. | Aviso de licencia separado | No demuestra capacidad/licencia para operar; compruebe Licenciamiento y sesión Runtime después. |

### Interrupción, fallo parcial y licencia

Restaurar Authority no significa que la aplicación ya esté recuperada. Si el navegador conserva archivos seleccionados, continuar mantiene la selección e impide cambiarlos silenciosamente. Si los perdió, selecciónelos de nuevo y haga otra previa antes de Apply. Recordar selección no es backup de archivos.

Ante etapa/error intermedio registre resultado y compruebe estado guardado antes de repetir. El flujo pasa por revisión, publicación y activación; no presuma deshecha una etapa terminada.

Si se recuperó aplicación y solo falló licencia opcional, lea el aviso. Use Continuar sin licencia cuando corresponda y trate licencia después, sin repetir toda restauración por ese aviso.

### Confirmar resultado y límites

Compruebe autenticación con Authority esperada, proyecto/revisión activos, pantallas, scripts y fuentes del destino. En Runtime revise clase, valores, calidad y hora. Base/Historian se recuperan aparte mediante procedimientos nativos soportados; valores actuales de TAG no recrean historia. Material protegido de drivers puede requerir preparación local. Archivo corrupto, contraseña incorrecta, usuario restaurado deshabilitado, referencia Authority incompatible o permisos ausentes deben resolverse en su origen. Nunca edite stores, tokens ni archivos firmados para forzar aceptación. Ensaye en entorno controlado antes de depender de la copia para operación real.

[Abrir esta guía en Ayuda instalada](/help?topic=recovery.backup-system-recovery)
