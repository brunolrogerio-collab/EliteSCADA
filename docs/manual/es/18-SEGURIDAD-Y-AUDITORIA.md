# Seguridad y auditoría: cuentas, permisos y protección

## Usuarios locales: crear, asignar roles y mantener acceso

Registre quién inicia sesión y distinga cuenta, rol, sesión y licencia.

### Antes de administrar cuentas

Abra Ingeniería > Seguridad. Usuarios y credenciales pertenecen a la Security Authority de la instalación; no son objetos del proyecto ni viajan en el paquete de ingeniería. La cuenta local del producto es distinta de una cuenta del portal comercial. Administrar usuarios requiere UserRoleAdmin o SystemAdmin; editar el proyecto por sí solo no lo concede. Conserve al menos un administrador local habilitado y confirme qué cuenta utiliza antes de cambiar roles o contraseña.

### Campos de la cuenta

Use Nuevo usuario para crear una identidad. Para una cuenta existente, selecciónela en la lista; el nombre de acceso se muestra para identificarla y no se renombra en este formulario. La contraseña se introduce al crear o en el formulario separado de restablecimiento, sin mostrar la actual.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Usuario / login | Nombre usado para iniciar sesión. No lo confunda con el nombre visible. | operacao.turno1 | 3–200 caracteres; nombre único. |
| Nombre visible | Identifica a la persona de forma legible. Cambiarlo no cambia login ni roles. | Operación — Turno 1 | 1–300 caracteres. |
| Contraseña / Nueva contraseña | Cree una contraseña individual y siga la política aceptada por el servidor. Restablecer sustituye la contraseña, no recupera la anterior. No copie contraseñas en mensajes, scripts ni paquetes. | Introducir de forma privada | El formulario exige al menos 8 caracteres; el servidor valida la política. |
| Habilitado | Permite usar la cuenta. Deshabilitar no borra identidad ni historial de acciones. Los cambios pueden invalidar la sesión; mantenga otra cuenta administrativa disponible. | Marcado |  |
| Roles | Seleccione roles existentes cuyas autorizaciones correspondan al trabajo. Las concesiones de los roles se combinan; añadir un rol no elimina permisos de otro. Llamarlo developer o administrator no crea privilegios por sí solo. | Operador de Estación 01 | Compruebe las concesiones, no solo el nombre. |

### Procedimiento: crear y probar un operador

1. Revise roles disponibles y capabilities/alcances en el panel de Authority. Si falta un rol adecuado, prepare y aplique primero la política.
2. Seleccione Nuevo usuario e introduzca login, nombre, contraseña privada y habilitación.
3. Seleccione solo roles necesarios y cree la cuenta.
4. Para cambios posteriores, revise el resumen y confirme guardar; editar campos no guarda por sí solo.
5. Inicie sesión con la nueva cuenta en una sesión separada y compruebe una lectura permitida y una acción que deba denegarse. Licencia y clase View Only/Interactive siguen limitando acciones.
6. Vuelva a la cuenta administrativa para corregir concesiones; una misma sesión no demuestra dos usuarios distintos.

### Restablecimiento, búsqueda y problemas frecuentes

La búsqueda filtra nombre/login/rol y el estado separa habilitados y deshabilitados; ocultar una fila no deshabilita la cuenta. Para restablecer contraseña, seleccione la cuenta, introduzca Nueva contraseña, revise el impacto y confirme. Cambios de cuenta/roles o contraseña pueden cerrar sesiones; vuelva a entrar cuando se solicite. El sistema rechaza eliminar el último acceso administrativo local habilitado y asignar roles inexistentes. 401 requiere entrar de nuevo; 403 indica falta de permiso. Una revisión del proyecto no transporta usuarios e importar .escadapkg no restaura Authority.

[Abrir esta guía en Ayuda instalada](/help?topic=security.local-accounts)

## Roles y alcances: conceder la acción sobre el recurso correcto

Prepare concesiones explícitas, compruebe el alcance y aplique la política validada de Authority.

### Cuenta, rol, capability y alcance

La cuenta identifica a la persona. Un rol reúne concesiones (grants). Cada grant une una capability, la acción permitida, a un alcance opcional, dónde se aplica. La política pertenece a Authority, no al dibujo ni al paquete del proyecto. Abra Ingeniería > Seguridad y el panel de roles/capabilities/alcances. Consultar requiere UserRoleAdmin. Validar/aplicar requiere EngineeringModify y UserRoleAdmin o SystemAdmin. Los nombres de roles no conceden acceso por sí solos.

### Campos de rol y concesión

Nuevo rol crea un borrador. Duplicar copia configuración a otro rol, no crea usuarios. En la jerarquía mostrada, los IDs identifican nodos/recursos existentes; el panel permite seleccionarlos, pero no crear ni reorganizar ese árbol. No invente un UUID para sustituir un nodo ausente.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Identificador estable | Clave usada para asignar a usuarios. Edítela antes del primer Apply. Después queda protegida; cambiar Nombre visible no cambia la clave. | operador-estacao01 |  |
| Nombre visible | Etiqueta legible para seleccionar y reconocer el rol. No es su lista de permisos. | Operador de Estación 01 |  |
| Descripción | Explique finalidad y alcance previsto para evitar errores posteriores. | Leer la estación y reconocer sus alarmas |  |
| Capability | Seleccione la acción específica. Consulte la referencia; nombres parecidos no hacen intercambiables las capabilities. | AlarmAcknowledge |  |
| Alcance | Sin alcance concede esa capability globalmente. Un nodo restringe la concesión al recurso correspondiente. Global no significa todas las capabilities. | Nodo de Estación 01 |  |
| Incluir descendientes | Con un nodo seleccionado, incluye sus descendientes en la jerarquía de autorización. Desmarcado cubre solo ese nodo. No es búsqueda por prefijo de nombre/ruta. | Estación y equipos descendientes | Deshabilitado sin nodo. |

### Referencia de acciones disponibles

Estas son las opciones de la versión actual. El permiso no crea recursos, cambia licencia ni evita revisión activa, preparación, contexto o límites de la operación.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| View | Ver Runtime; no concede comandos. | View |  |
| TagRead | Leer TAGs para el recurso/alcance autorizado. | TagRead |  |
| CommandExecute | Ejecutar comandos registrados. Distinto de escribir un valor de proceso. | CommandExecute |  |
| ProcessValueWrite | Solicitar escritura de proceso, como un setpoint. No sustituye permiso de comando. | ProcessValueWrite |  |
| AlarmAcknowledge | Reconocer alarmas; no corrige la causa física. | AlarmAcknowledge |  |
| AlarmShelve | Suprimir/restablecer alarmas mediante la operación disponible y según la definición. | AlarmShelve |  |
| TrendUse | Usar consultas/tendencias autorizadas. | TrendUse |  |
| TrendSave | Guardar configuraciones de tendencias donde esté disponible; no concede edición general. | TrendSave |  |
| EngineeringView | Consultar Ingeniería autorizada. | EngineeringView |  |
| EngineeringModify | Modificar Ingeniería respetando Engineering Lock y requisitos de cada operación. | EngineeringModify |  |
| UserRoleAdmin | Administrar usuarios/roles en flujos autorizados. Cambiar la política también exige edición de Ingeniería. | UserRoleAdmin |  |
| SystemAdmin | Administrar operaciones que requieren esta capability, como consultar Audit. No suponga que sustituye todas las específicas. | SystemAdmin |  |
| HighAvailabilityObserve | Observar estado HA. | HighAvailabilityObserve |  |
| HighAvailabilityTransfer | Solicitar transferencia de autoridad HA según preparación y permisos. | HighAvailabilityTransfer |  |
| HighAvailabilityAdmin | Administrar configuración HA. Una operación puede exigir otros permisos de Ingeniería. | HighAvailabilityAdmin |  |
| AssetProcessIntelligenceManage | Gestionar Intelligence mediante concesión explícita global o restricción válida Equipment/Location. Licencia y activación siguen siendo necesarias. | AssetProcessIntelligenceManage |  |

### Procedimiento: preparar y aplicar una política

1. Cargue la política actual e identifique quién conservará administración.
2. Cree o seleccione un rol; compruebe usuarios asignados antes de editar.
3. Añada un grant por capability necesaria y elija global o el nodo adecuado. Revise el alcance antes de incluir descendientes.
4. Para Intelligence añada AssetProcessIntelligenceManage explícitamente: global o Equipment/Location con vínculo válido al recurso. No sustituya este grant por SystemAdmin aislado al configurar herramientas.
5. Ejecute Preview de la política, corrija errores y aplique solo el candidato validado. Otra edición invalida Preview.
6. Asigne el rol y pruebe accesos permitidos/denegados con esa cuenta.
7. Si otro administrador cambió la política, recargue y edite la versión actual. No aplique una versión antigua ni elimine usuarios para evitar conflictos.

### Vista previa, sesión y rechazos

La vista previa del usuario seleccionado combina grants de roles del borrador; es explicativa y no demuestra permiso sobre un recurso concreto. Las capabilities efectivas pertenecen a su sesión actual, no a la persona seleccionada. El backend decide cada operación según identidad, recurso y alcance; Runtime considera también clase de sesión y licencia. La política de Authority tiene Preview/Apply y versión propios: exportar un proyecto no la activa. Se rechazan eliminar un rol asignado y perder el último administrador. Si falta un nodo o es incompatible, seleccione un vínculo correcto; no amplíe a global solo para eliminar el error.

[Abrir esta guía en Ayuda instalada](/help?topic=security.policy-scopes)

## Auditoría: localizar acciones y comprobar la grabación

Use filtros adecuados, lea resultados y distinga una lista vacía de un fallo de persistencia.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-AUDIT: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=audit.query-diagnostics; fields=audit.retention; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Qué responde Auditoría

Abra Auditoría en el sistema. El acceso requiere SystemAdmin en el Runtime autorizado. Registra acciones protegidas y sus resultados: quién intentó qué operación, sobre qué recurso y cuándo. Un éxito administrativo no demuestra arranque físico de bomba; compruebe feedback del comando y TAGs. Audit no sustituye alarm.events, operational.events ni lecturas del Historian. La pantalla consulta/diagnostica y no edita ocurrencias.

### Campos de consulta

Deje filtros opcionales vacíos para ampliar la consulta. Los filtros completos se combinan: un registro debe satisfacerlos todos. Son valores exactos, no fragmentos de texto libre. Use valores de un evento conocido en lugar de adivinar IDs. Editar filtros no consulta hasta seleccionar Consultar.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Desde | Inicio en hora local del navegador, convertida a UTC para consultar. Vacío significa sin límite inferior. | Día del incidente, 08:00 | No puede ser posterior a Hasta. |
| Hasta | Fin del período; se incluye el instante introducido. Si falta un evento, compruebe zona horaria y período antes de suponer pérdida. | Mismo día, 09:00 |  |
| Usuario / subject | Identidad estable del autor registrada en el evento, no necesariamente nombre visible ni login. Cópiela de sus detalles. | ID de identidad observada |  |
| Acción | Nombre exacto de la operación registrada. Empiece por período/resultado si no lo conoce. | command.execute |  |
| Resultado | Todos no restringe resultado. Succeeded indica éxito registrado; Denied es rechazo de autorización; Failed es operación fallida. | Denied |  |
| Tipo de recurso (avanzado) | Categoría del destino, no nombre de acción ni tipo de TAG. | command |  |
| Recurso / clave (avanzado) | Identificador exacto usado por el evento para su destino. Puede ser clave/ruta o ID según la operación; no siempre es UUID. | pump.start |  |
| Área (avanzado) | Área registrada en el contexto. Un filtro no añade área a un evento que no la tiene. | Estacao01 |  |
| Correlation ID (avanzado) | Relaciona registros de la misma solicitud/flujo cuando existe correlación. No es el ID individual del evento. | Copiar de un evento conocido |  |
| Eventos por página | Limita registros visibles por página. No cambia retención ni total almacenado. | 100 | Entero de 1 a 1000. |

### Procedimiento: investigar un rechazo

1. Elija un período corto alrededor del intento conocido y Resultado = Negado.
2. Seleccione Consultar. Seleccione una fila para ver detalles; teclado y Enter también seleccionan.
3. Lea identidad, acción, destino y motivo; no suponga que la licencia causó el rechazo.
4. Copie acción/identidad/destino para refinar filtros si es necesario.
5. Revise roles, grants y alcance con administración de Authority. No altere el histórico para intentar dar acceso.
6. Use Siguiente/Anterior para páginas del mismo filtro. Cambiar filtros y Consultar vuelve a la primera página.
7. Limpiar quita filtros y consulta de nuevo; no elimina eventos. Actualizar relee la página actual y diagnósticos.

### Diagnósticos: valores de lectura

Expanda Diagnósticos. Una cola temporal no demuestra pérdida, pero fallos/rechazos/descartes requieren investigación. La salud de consulta y de grabación son distintas. Estos indicadores son de lectura: esta pantalla no tiene campos para editar retención.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Persistidos / última persistencia | Cantidad del almacenamiento y última escritura informadas por el servicio; no solo elementos de la página. | Compruebe cantidad y hora juntas |  |
| Fallos de persistencia | Indica fallos de escritura; revise diagnóstico del servicio y disponibilidad de la base. Un contador histórico no prueba un fallo actual. | Comparar cantidad/último fallo |  |
| Cola pendiente | Registros esperando envío desde el buffer. Compruebe si la cola disminuye al recuperar el servicio. | 0 en reposo |  |
| Enviados / fallos de envío | Siguen salida del buffer y fallos de envío; distintos del recuento de consulta. | Comprobar último envío |  |
| Rechazados por límite / descartados al cerrar | Informan registros rechazados por capacidad o descartados al cerrar. No considere una lista consultable prueba de completitud si aparecen. | Investigar cantidades distintas de cero |  |
| Retención / última retención | Muestra habilitación, condición finita/indefinida y ejecución de limpieza configurada en el servicio. Indefinida significa sin plazo finito activo, no disco ilimitado. Backup y archivo son procedimientos distintos. | Consultar diagnóstico; no introducir valor |  |

### Sin resultados o sin acceso

Una lista vacía puede indicar filtros sin coincidencia, período/zona incorrectos o ausencia de grabaciones. Limpie filtros, compruebe hora y diagnósticos antes de concluir pérdida. Una página llena sin Siguiente no demuestra ausencia de eventos fuera del filtro. 401 requiere login; 403 exige permiso; 400 indica filtro inválido. La paginación usa referencias internas del servidor: no hay cursor que introducir. Esta pantalla no configura tamaño global de base ni archivo; no confunda diagnósticos de retención con esa administración.

[Abrir esta guía en Ayuda instalada](/help?topic=audit.query-diagnostics)

## Engineering Lock: proteger y reabrir la aplicación

Use el secreto de la aplicación sin confundirlo con contraseña de usuario, backup ni permiso.

### Cuándo usarlo y qué protege

Engineering Lock protege contenido de ingeniería de la aplicación. No es reserva de edición de otro operador ni concede capabilities a quien conoce el secreto. La contraseña de cuenta autentica a la persona; el secreto del Lock protege la aplicación; la contraseña del backup de Authority protege otro material. Guárdelos según procedimientos, no en pantallas, scripts ni ayuda. Bloqueada, administración del sistema, Import/Export/Restaurar y desbloqueo siguen sujetos a permisos normales.

### Estados y acciones

En Ingeniería desbloqueada, abra el candado de la barra superior. Bloqueada, la superficie restringida presenta el formulario de desbloqueo y administración disponible. Los editores protegidos no quedan abiertos detrás de un simple mensaje.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Secreto configurado | Indica que existe secreto. Configurado no significa necesariamente bloqueado ahora. | Configurado y desbloqueado |  |
| Nuevo secreto / Sustituir secreto | Introduzca de forma privada para configurar o sustituir. No se muestra el secreto actual. Configurar mantiene abierto; Configurar y bloquear cierra Ingeniería protegida. | Usar un secreto propio de la aplicación |  |
| Bloquear ahora | Usa el secreto configurado y pasa a la superficie restringida. No envía parada al Runtime. | Tras configurar el secreto |  |
| Secreto de Engineering Lock / Desbloquear | Introduzca el secreto de la aplicación para reabrir contenido. La contraseña de usuario no lo sustituye; desbloquear no concede permisos ausentes. | Introducir sin compartir en mensajes |  |
| Eliminar secreto | Elimina protección configurada mediante el flujo permitido. No restablece contraseña de usuario ni borra proyecto. | Solo si se desea retirar protección |  |

### Procedimiento y persistencia tras reinicio

1. Con acceso autorizado, abra el candado.
2. Defina el secreto privado y elija Configurar para seguir trabajando o Configurar y bloquear para restringir inmediatamente.
3. Compruebe el estado del servidor.
4. Trate el cambio como Working: Guardar → Publicar → Activar es necesario para que la protección pertenezca a la revisión activa duradera tras reinicio. No suponga que Configurar completa el ciclo.
5. Para editar, use Desbloquear con secreto correcto y permisos adecuados.
6. Compruebe contenido accesible y conserve protección prevista en la revisión publicada. Si se rechaza, lea el motivo; no borre usuarios, base ni Authority para intentar desbloquear.

[Abrir esta guía en Ayuda instalada](/help?topic=security.lock-workflow)
