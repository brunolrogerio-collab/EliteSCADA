# Gestión de aplicaciones, recuperación y diagnóstico

## Exportar .escadapkg

Exporte un `.escadapkg` para guardar o transportar configuración: TAGs, pantallas, Scripts y otros objetos portátiles. Espere a terminar descarga e identifique proyecto/revisión. Para editar muchas filas de un tipo use el capítulo CSV/Excel; para compartir solo recursos visuales o Scripts reutilizables use Bibliotecas.

El paquete no contiene backup de usuarios/permisos de Authority, licencia ni base/histórico. Prepare esas copias por separado para recuperar una instalación completa.

En destino, **Inspect** examina contenido e información sin aplicar cambios. **Preview** comprueba contenido propuesto y muestra diagnósticos. **Apply** incorpora el candidato validado al borrador del proyecto, llamado Working. Después guarde, publique y active por separado; ver contenido importado en editor no significa que la revisión ya esté operando.

**Ayuda relacionada:** `/help?topic=packages.escadapkg`.

## Importar

### Procedimiento: importar una aplicación de forma segura

**Objetivo:** comprobar un paquete recibido e incorporarlo al proyecto antes de llevarlo a operación.

**Requisitos previos:** un paquete compatible y una sesión autorizada.

**Pasos:**
1. Seleccione el `.escadapkg`.
2. Ejecute Inspect cuando esté disponible.
3. Ejecute Preview.
4. Corrija los issues informados.
5. Ejecute Apply solamente para el candidato validado.
6. Guarde la aplicación resultante.
7. Publique y active por separado cuando corresponda.

**Resultado esperado:** Working recibe el contenido importado; Published/Active no cambian implícitamente.

**Diagnóstico:** si Working cambió después de Preview, vuelva a ejecutar la validación antes de Apply.

## Bibliotecas reutilizables

`.escadalib` es diferente de `.escadapkg`.

Asociar una biblioteca deja sus recursos compatibles disponibles en catálogo. Usar/incorporar copia el ítem elegido y sus dependencias validadas, los otros recursos necesarios para funcionar, al borrador Working del proyecto.

Desasociar elimina la disponibilidad en el catálogo y no borra el contenido ya incorporado. Runtime y Active no dependen de un archivo `.escadalib` externo.

**Ayuda relacionada:** `/help?topic=libraries.reusable-resources`.

## Backup de Authority

El backup de Authority conserva las identidades locales y asignaciones admitidas en un sobre protegido. No incluye contraseñas en texto plano, tokens, cookies, sesiones, licencia, aplicación ni datos de Historian.

La contraseña del backup de Authority es exclusiva de ese artefacto y no sirve para iniciar sesión ni como contraseña de Engineering Lock.

## Restore-first

Una instalación realmente vacía ofrece Restore backup antes de exigir la creación de un usuario/proyecto desechable.

La recuperación conserva autoridades separadas:

1. aplicación (`.escadapkg`);
2. Security Authority;
3. database/Historian;
4. Licensing como preocupación opcional e independiente.

### Procedimiento: recuperación completa en una instalación vacía

**Objetivo:** restaurar Authority y la aplicación sin crear una identidad/proyecto provisional.

**Requisitos previos:** instalación comprobada como vacía; backup de Authority y contraseña; `.escadapkg`; licencia opcional.

**Pasos de alto nivel comprobados por el contrato:**
1. seleccione los artefactos;
2. ejecute Inspect/Preview en todas las entradas antes de la mutación;
3. vuelva a validar que la instalación está vacía;
4. restaure Authority de forma atómica;
5. aplique el paquete validado a Working;
6. guarde la revisión raíz;
7. publique;
8. confirme que la project key configurada para Runtime corresponde a la aplicación restaurada;
9. active mediante la operación normal;
10. instale la licencia opcional a través del servicio de Licensing;
11. valide las invariantes finales.

**Resultado esperado:** existe una Authority utilizable, la aplicación está guardada/publicada/Active y Runtime es coherente con la revisión Active.

**Importante:** la recuperación de database/Historian es una autoridad independiente y su estado debe conocerse de forma explícita.

## Application detach / instalación neutral / cambio A -> B -> A

Engineering > Instalación desvincula la Application y Authority actuales y devuelve la instalación a un estado neutral para un nuevo bootstrap. No es eliminar el proyecto, desinstalar el producto ni quitar automáticamente la licencia.

### Procedimiento: cambiar la Application de forma segura

**Requisitos previos:** acceso autorizado a la administración de la instalación; decisión sobre lo que debe conservarse; backups de Application y Authority cuando sean necesarios para volver atrás.

**Pasos:**
1. Revise el resumen de la instalación, incluidos los cambios Working aún no publicados.
2. Exporte la Application como `.escadapkg`. Si necesita conservar usuarios/identidades locales, exporte Authority por separado con la contraseña solicitada por la página. `.escadapkg` no contiene credenciales.
3. Abra **Desvincular aplicación de esta instalación** y lea las consecuencias. Confirme los acknowledgements mostrados para Application, Authority, sesiones antiguas, Historian y cambios Working.
4. Decida si mantiene, quita o reemplaza la licencia de la máquina. La candidata sustituta se valida antes del detach.
5. Lea y resuelva los bloqueos. Si decide continuar sin uno de los backups recomendados, reconózcalo explícitamente en la página.
6. Elija **Desvincular y volver a neutral** y espere la transacción del servidor. Después de un error, actualice y compruebe el estado real antes de repetir.
7. En el bootstrap neutral, vincule la siguiente Application y configure su Authority mediante el flujo normal.

**Resultado esperado:** la Application y Authority anteriores dejan de estar vinculadas y los tokens/sesiones antiguos dejan de autorizar acciones; la instalación vuelve al bootstrap neutral. Historian se conserva; la base de datos y sus backups siguen siendo una preocupación separada. La licencia sigue la opción confirmada.

**Ayuda relacionada:** `/help?topic=installation.switching`, `/help?topic=application.export-import`, `/help?topic=recovery.backup-system-recovery`, `/help?topic=getting-started.neutral-bootstrap`.

## Flujo de diagnóstico

Use este mismo orden antes de crear una solución provisional:

1. reproduzca el problema;
2. identifique la autoridad/capa que debería proporcionar el estado;
3. compare el backend y la UI;
4. valide identidad/capabilities;
5. valide lifecycle/Active;
6. para datos, valide Data Source -> Driver -> binding -> quality/timestamp;
7. para el histórico, valide la ingestión antes de la presentación;
8. para Scripts, valide la referencia, sandbox, timeout y aislamiento de fallos;
9. para Licensing, distinga Demo, Valid e Invalid;
10. para la recuperación, identifique la etapa que falló y no declare un éxito parcial como éxito completo.

### Inicio

Confirme el estado del backend y la disponibilidad de la UI. En una instalación vacía, distinga la creación de una instalación nueva de Restore-first.

### Inicio de sesión

Un fallo de inicio de sesión debe tratarse en Authority. Engineering Lock y la licencia no son credenciales de acceso.

### Comunicación del Driver

Use solamente las capabilities declaradas por el Driver y verifique la configuración, endpoint, conexión, binding y diagnósticos.

### Quality del TAG

No fuerce Good. Corrija el origen del estado de quality.

### Runtime

Confirme la revisión Active, el entitlement y la autoridad de la sesión.

### Scripts

Verifique las dependencias declaradas, la API pública, sandbox y los diagnósticos de la instancia.

### Licensing

- licencia ausente: Demo;
- licencia válida: entitlement del tier;
- licencia presente pero inválida: Run bloqueado.

### Authority

No restaure tokens/sesiones. Después de la restauración, las identidades restauradas se autentican mediante el mecanismo normal.

### Importar/Restaurar

Preview es obligatorio antes de Apply cuando el flujo lo ofrece. Apply no reemplaza Save/Publish/Activate.

**Ayuda relacionada:** `/help?topic=recovery.backup-system-recovery`, `/help?topic=troubleshooting.overview`, `/help?topic=diagnostics.overview`.
