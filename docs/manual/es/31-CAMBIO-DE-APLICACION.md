# Cambiar la aplicación de la instalación

## Cambiar aplicación: comprobar, preservar y desvincular

Entienda detach y confirmaciones antes de devolver esta instalación al estado neutro.

### Cuándo corresponde

Abra Ingeniería → Instalación para preparar esta instalación para otra aplicación. No es editar pantalla o activar revisión siguiente del mismo proyecto. Detach desvincula aplicación/Authority, invalida sesiones y detiene efectos Runtime. No desinstala ni borra Historian silenciosamente. Licencia es decisión explícita. Opere solo en destino/parada autorizados; leer esta guía no ejecuta cambio.

### Comprobar y preparar copias

1. Revise Application actual, Working, Published, Active, Authority, licencia, Historian y protección; actualice si cambió.
2. Guarde cambios sin guardar cuando corresponda antes de considerar copia recuperable. Guardado/no publicado es distinto; compare revisiones.
3. Exporte Application .escadapkg y confirme descarga.
4. Para conservar usuarios/credenciales/policy use Nueva contraseña del backup y Exportar Authority. Guarde contraseña aparte; paquete no incluye accesos.
5. Respalde base/Historian y material protegido aparte; preservar en detach no es copia de recuperación.

### Resumen y preservación

Identifican el destino actual; léalos antes de confirmar.

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

### Confirmaciones y licencia

Abra Desvincular aplicación de esta instalación. Lea cada elemento; marcar no crea backup ni valida entorno nuevo.

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

### Ejecutar y verificar

Tras copias, consecuencias y licencia, elija Desvincular y volver al neutro una vez y espere. Interfaz valida y solicita detach; servidor revalida estado. Éxito vuelve a entrada neutra. Confirme antes de crear/importar/recuperar. Próxima aplicación/Authority exigen preparación normal; sesiones anteriores no autorizan nuevo vínculo. No añade lock, transporte ni contrato de detach.

### Fallo, interrupción o incertidumbre

Lea bloqueos y actualice tras resolver. No sustituya rechazo de licencia candidata quitando la válida por ensayo. Si pierde conexión/página, no suponga éxito, cancelación ni rollback: reabra dirección correcta y revise estado real. Registre etapa, mensaje y hora. No solicite otro cambio hasta reconciliar. Recuperación de aplicación/Authority usa copias; base/Historian usan procedimiento nativo separado. No sustituya por edición manual de base.

[Abrir esta guía en Ayuda instalada](/help?topic=installation.switch-workflow)
