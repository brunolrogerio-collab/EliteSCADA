# Backup y recuperación

## Backup y recuperación: Authority, aplicación y datos

Separe archivos, valide antes de aplicar y entienda lo que restaura cada etapa.

### Elija qué recuperar

Aplicación significa ingeniería: TAGs, pantallas, scripts y objetos portables del .escadapkg. Security Authority conserva usuarios, credenciales y política en un backup protegido separado. Licencia es otro artefacto. Base de datos, Historian y material protegido de drivers siguen sus propios procedimientos. El paquete no es imagen completa de instalación y no contiene usuarios/password hashes/contraseñas de acceso. Tener .escadapkg no basta para recuperar accesos, historia o claves de drivers.

### Preparar y comprobar backups

Exporte mediante el flujo soportado y compruebe proyecto/revisión y destino. Trate cambios sin guardar antes de considerar una copia recuperable. Cambiar la aplicación de esta instalación ofrece Backup de Application y Backup de Authority antes del detach: exporte lo necesario y vuelva sin ejecutar el cambio. Nueva contraseña del backup protege la exportación de Authority; no es contraseña de acceso. Guárdela de forma protegida separada del archivo y confirme la descarga. No avance al detach solo para probar un backup. Mantenga copias y planes propios de base/Historian y claves protegidas; esta guía no crea retención/archivado ni promete incluirlos en el paquete.

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

### Recuperar Authority y aplicación en instalación neutra

1. Confirme instalación neutra elegible y servidor de destino correcto; no haga detach solo para abrir este flujo.
2. En configuración inicial elija Restaurar backup. Seleccione aplicación, Authority, contraseña y licencia opcional.
3. Elija Validar backups. Pare ante rechazo y corrija archivo/compatibilidad/credencial; aceptar extensión no valida.
4. Con archivos válidos, elija Restaurar Authority.
5. Entre con Administrador restaurado habilitado y con permisos efectivos de ingeniería/administración requeridos para el paquete; el nombre developer no basta.
6. Continúe; el servidor vuelve a validar aplicación con Authority restaurada.
7. Si se permite, elija Importar aplicación y espere resultado completo. Restaurar Authority y recuperar aplicación son etapas separadas, no promesa de una transacción única entre todos los archivos.

### Recuperar solo aplicación con Authority existente

Si no hay proyecto persistido pero sí Authority, entre con Administrador compatible y elija Importar aplicación. Seleccione .escadapkg, Valide, lea el resultado y después importe. Exige paquete compatible, catálogo/estado de destino apropiados y autorización; no es sobreescritura genérica de cualquier aplicación activa. Para modificar una aplicación instalada use ingeniería/portabilidad correspondientes o el procedimiento de cambio. No elimine usuarios/base para que aparezca la opción.

### Interrupción, fallo parcial y licencia

Tras restaurar Authority, la aplicación puede seguir sin recuperar. Si el navegador conserva archivos en memoria, la continuación mantiene selección e impide sustitución silenciosa. Si perdió archivos, seleccione los solicitados otra vez; una nueva previa es obligatoria antes de Apply. La selección retenida no es copia de los archivos. Si el servidor devuelve etapa/error intermedio, registre y compruebe estado persistido antes de repetir: revisión, publicación y activación son etapas distintas y no debe suponer revertida una completada. Si aplicación recuperada pero licencia opcional falló, lea el aviso y use Continuar sin licencia si corresponde; atienda licencia por separado sin repetir todo solo por ese aviso.

### Confirmar resultado y límites

Compruebe autenticación con Authority esperada, proyecto/revisión activos, pantallas, scripts y fuentes del destino. En Runtime revise clase, valores, calidad y hora. Base/Historian se recuperan aparte mediante procedimientos nativos soportados; valores actuales de TAG no recrean historia. Material protegido de drivers puede requerir preparación local. Archivo corrupto, contraseña incorrecta, usuario restaurado deshabilitado, referencia Authority incompatible o permisos ausentes deben resolverse en su origen. Nunca edite stores, tokens ni archivos firmados para forzar aceptación. Ensaye en entorno controlado antes de depender de la copia para operación real.

[Abrir esta guía en Ayuda instalada](/help?topic=recovery.backup-system-recovery)
