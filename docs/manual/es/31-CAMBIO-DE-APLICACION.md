# Cambiar la aplicación de la instalación

## Cambiar aplicación: comprobar, preservar y desvincular

Prepare copias y entienda los efectos antes de retirar la aplicación actual de esta instalación.

### Qué hace la operación

Use Ingeniería, Instalación para preparar este servidor para otra aplicación. Retira el vínculo actual, invalida accesos antiguos y vuelve a configuración inicial. La aplicación deja de operar aquí. Producto sigue instalado y no se borra Historian por este flujo. Para editar pantalla o ejecutar otra revisión del mismo proyecto use lifecycle, no este cambio.

### Preparar copias antes de decidir

1. Revise Application actual, Working y Active para confirmar instalación.
2. Compruebe cambios sin guardar y Guarde si debe conservarlos; guardar no los ejecuta.
3. Exporte Application y confirme .escadapkg: contiene ingeniería.
4. Para usuarios/permisos exporte Authority. Authority guarda accesos; backup separado con Nueva contraseña del backup. Guárdela aparte.
5. Confirme copias de base, historia y claves con responsable; no todo se incluye en el paquete.

### Confirmar y esperar

1. Con copias revisadas abra Desvincular aplicación de esta instalación.
2. Lea consecuencias de aplicación, usuarios/sesiones, historia y trabajo sin guardar. Confirme solo lo entendido/decidido.
3. Elija licencia de máquina: Mantener conserva, Quitar retira, Sustituir exige código válido. Es decisión separada.
4. Resuelva bloqueos y actualice.
5. Elija Desvincular y volver al neutro una vez y espere. Servidor comprueba de nuevo antes de aplicar. Éxito vuelve a configuración inicial; aún debe crear/importar/recuperar la próxima aplicación.

### Consultar resumen y copias

Consulte si un campo no está claro. Exportar una copia no requiere ejecutar el cambio.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Application actual | Proyecto que quedará desvinculado de esta instalación. | Proyecto actual | No selecciona servidor remoto ni proyecto nuevo. |
| Working | Revisión y cambios sin guardar. | Cambios sin guardar | Guardar no publica/activa; exporte copia necesaria. |
| Published y Active | Compare preparada/ejecutada para preservar/parar. | Revisiones mostradas | Working no necesariamente coincide con Active. |
| Security Authority | Identidades/policy a desvincular; sesiones antiguas se invalidan. | Authority presente | Backup separado; .escadapkg no recupera contraseñas de acceso. |
| Historian | Historia/base preservadas por este flujo. | Preservado | No exporta ni garantiza compatibilidad/vínculo automático de próxima aplicación. |
| Protección de ingeniería | Protección y condiciones a resolver por flujo autorizado. | Estado de protección | No quite protección/credenciales para eludir bloqueos. |
| Exportar Application | Copia portable de ingeniería por flujo soportado. | Descarga .escadapkg | Compruebe archivo/revisión/destino; descarga cancelada no es copia. |
| Nueva contraseña del backup / Exportar Authority | Exporta Authority protegida con contraseña propia. | Backup separado | 12–1024 caracteres; no publicar archivo/contraseña ni confundir con login. |

### Consultar confirmaciones y licencia

Confirmaciones permiten reconocer consecuencias. Marcar no crea copia. Si un mensaje no está claro, vuelva a preparar/comprobar antes de continuar.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Decisión de backups | Confirma exportación de esta revisión o decisión explícita de omitir copia. | Backups exportados | Omitir acepta falta de copia; no es automático. |
| Pérdida de Working sin guardar | Exigido si hay cambios sin guardar al continuar. | Consecuencia aceptada | No guarda ni garantiza recuperar. |
| Desvincular Application | Reconoce que dejará de operar vinculada aquí. | Consecuencia leída | No es cerrar la interfaz. |
| Desvincular Authority y sesiones | Reconoce desvinculación e invalidación de autorización anterior. | Consecuencia leída | Identidades/policy nuevas o restauradas siguen bootstrap/recuperación. |
| Preservar Historian/base | Reconoce datos históricos separados sin borrado silencioso. | Datos preservados | No migra, descarta ni restaura historia. |
| Licencia de máquina | Mantener conserva; Quitar retira deliberadamente; Sustituir exige candidata válida. | Mantener licencia | De instalación, no embebida en aplicación; compruebe resultado. |
| Código de licencia sustituta | Solo al Sustituir; se valida antes de detach. | Código autorizado | No es clave criptográfica; corrija candidata inválida antes de continuar. |
| Bloqueos y diagnóstico | Lea motivos/etapa y revalide tras resolver causa. | Bloqueo mostrado | No eluda botón editando store o repitiendo solicitudes. |
| Desvincular y volver al neutro | Envía decisión; servidor previsualiza/revalida antes de aplicar. | Instalación neutra | Cambios entre etapas pueden causar rechazo. |
| Cancelar / Volver | Vuelve a preparar antes de confirmar ejecución. | Revisar copias | No promete deshacer operación ya recibida/completada. |

### Si cae conexión o aparece error

No repita inmediatamente. Reabra dirección correcta y compruebe aplicación anterior o configuración inicial. Una caída no confirma finalización. Registre mensaje/etapa/hora y consulte estado si hay duda. Cancelar/Volver antes de confirmar no deshace operación ya completada. Corrija licencia candidata inválida; no quite la válida por ensayo. Para restaurar copias use Backup y recuperación.

[Abrir esta guía en Ayuda instalada](/help?topic=installation.switch-workflow)
