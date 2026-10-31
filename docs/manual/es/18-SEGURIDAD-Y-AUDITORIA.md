# Seguridad y auditoría: cuentas, permisos y protección

## Usuarios locales: crear, asignar roles y mantener acceso

Registre quién inicia sesión y distinga cuenta, rol, sesión y licencia.

### Antes de administrar cuentas

Abra Ingeniería > Seguridad para registrar quién entra a esta instalación. Cuentas locales, contraseñas y permisos pertenecen a Security Authority, el registro de seguridad de la instalación. No son objetos del proyecto ni viajan en el paquete de ingeniería. Cuenta local tampoco es cuenta del portal comercial.

Administrar usuarios requiere UserRoleAdmin o SystemAdmin. Editar el proyecto no concede acceso. Antes de cambiar roles o contraseña compruebe su cuenta y conserve al menos un administrador local habilitado.

### Procedimiento: crear y probar un operador

1. Compruebe roles disponibles, acciones permitidas y recursos de cada uno. Si falta el adecuado, prepare y aplique política primero.
2. Use Nuevo usuario y complete login, nombre, contraseña privada y habilitación.
3. Elija solo roles necesarios y cree la cuenta.
4. Al editar después, revise resumen y confirme guardado. Cambiar un campo no guarda solo.
5. En otra sesión entre con la cuenta creada y pruebe una lectura permitida y una acción que debe denegarse. Licencia y clase View Only/Interactive siguen limitando acciones.
6. Vuelva a la cuenta administrativa para corregir concesiones. Una sesión no prueba acceso de dos usuarios distintos.

### Campos de la cuenta

Use Nuevo usuario para crear una identidad. Para una cuenta existente, selecciónela en la lista; el nombre de acceso se muestra para identificarla y no se renombra en este formulario. La contraseña se introduce al crear o en el formulario separado de restablecimiento, sin mostrar la actual.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Usuario / login | Nombre usado para iniciar sesión. No lo confunda con el nombre visible. | operacao.turno1 | 3–200 caracteres; nombre único. |
| Nombre visible | Identifica a la persona de forma legible. Cambiarlo no cambia login ni roles. | Operación — Turno 1 | 1–300 caracteres. |
| Contraseña / Nueva contraseña | Cree una contraseña individual y siga la política aceptada por el servidor. Restablecer sustituye la contraseña, no recupera la anterior. No copie contraseñas en mensajes, scripts ni paquetes. | Introducir de forma privada | El formulario exige al menos 8 caracteres; el servidor valida la política. |
| Habilitado | Permite usar la cuenta. Deshabilitar no borra identidad ni historial de acciones. Los cambios pueden invalidar la sesión; mantenga otra cuenta administrativa disponible. | Marcado |  |
| Roles | Seleccione roles existentes cuyas autorizaciones correspondan al trabajo. Las concesiones de los roles se combinan; añadir un rol no elimina permisos de otro. Llamarlo developer o administrator no crea privilegios por sí solo. | Operador de Estación 01 | Compruebe las concesiones, no solo el nombre. |

### Restablecimiento, búsqueda y problemas frecuentes

La búsqueda filtra nombre/login/rol y el estado separa habilitados y deshabilitados; ocultar una fila no deshabilita la cuenta. Para restablecer contraseña, seleccione la cuenta, introduzca Nueva contraseña, revise el impacto y confirme. Cambios de cuenta/roles o contraseña pueden cerrar sesiones; vuelva a entrar cuando se solicite. El sistema rechaza eliminar el último acceso administrativo local habilitado y asignar roles inexistentes. 401 requiere entrar de nuevo; 403 indica falta de permiso. Una revisión del proyecto no transporta usuarios e importar .escadapkg no restaura Authority.

[Abrir esta guía en Ayuda instalada](/help?topic=security.local-accounts)

## Roles y alcances: conceder la acción sobre el recurso correcto

Prepare concesiones explícitas, compruebe el alcance y aplique la política validada de Authority.

### Cuenta, rol, capability y alcance

Empiece por quién puede hacer qué y dónde. La cuenta identifica a la persona. El rol reúne permisos asignados a la cuenta. Cada concesión vincula una acción permitida con todos los recursos o un alcance específico. La interfaz llama capability a la acción y scope o escopo al alcance.

Abra Ingeniería > Seguridad y el panel de roles/capabilities/alcances. La política pertenece a Authority de la instalación, no al dibujo ni paquete del proyecto. El nombre del rol no concede acceso solo.

Consultar requiere UserRoleAdmin. Validar/aplicar requiere EngineeringModify junto con UserRoleAdmin o SystemAdmin. Compruébelos antes de editar.

### Procedimiento: preparar y aplicar una política

1. Cargue la política actual y compruebe quién seguirá administrando.
2. Cree o elija un rol. Antes de cambiarlo, compruebe usuarios ya asignados.
3. Añada una concesión por acción necesaria. Elija global o nodo correcto. Si incluye descendientes, compruebe recursos abarcados.
4. Para Intelligence añada explícitamente AssetProcessIntelligenceManage: global o Equipment/Location con vínculo válido. SystemAdmin solo no sustituye esa concesión al configurar herramientas.
5. Ejecute Preview, corrija y aplique solo el candidato validado. Editar de nuevo invalida la previa anterior.
6. Asigne el rol y pruebe acceso permitido/denegado con esa cuenta.
7. Si otro administrador cambió política, recargue y rehaga sobre la versión actual. No fuerce versión antigua ni elimine usuarios para eludir conflicto.

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

### Vista previa, sesión y rechazos

La previa de la cuenta elegida muestra lo que parecen conceder los roles del borrador. Ayuda a revisar, pero no decide acceso a un recurso concreto. Las capabilities efectivas mostradas pertenecen a su sesión actual, no a la persona seleccionada.

El servidor decide con persona, recurso y alcance; Runtime considera también clase de sesión y licencia. La política tiene versión y Preview/Apply propios de Authority; exportar o activar proyecto no la sustituye.

Se rechaza eliminar rol asignado o perder el último administrador. Ante nodo ausente o incompatible elija vínculo válido. No amplíe a global solo para eliminar el error.

[Abrir esta guía en Ayuda instalada](/help?topic=security.policy-scopes)

## Runtime público: mostrar pantallas sin abrir Engineering

Runtime público permite que alguien vea una presentación sin crear una cuenta. Empieza deshabilitado. Al habilitarlo, solo quedan disponibles la revisión Active elegida y los recursos seleccionados; el inicio de sesión normal continúa funcionando.

### Configurar la vista pública

Inicie sesión con una cuenta autorizada para administrar Security Authority y modificar Engineering. Primero publique y active la revisión que se mostrará. Después abra Engineering > Security > Runtime público.

1. Marque Permitir visualización pública sin inicio de sesión.
2. Elija una pantalla pública como Pantalla inicial. También debe estar seleccionada en Pantallas públicas.
3. Marque solo las ventanas emergentes, TAG e imágenes necesarios. Quien abra la dirección podrá leer cada TAG seleccionado; no lo incluya si su valor debe mantenerse privado.
4. Guarde la política pública. Esta política pertenece a Authority de la instalación; no cambia el paquete `.escadapkg` ni sustituye publicar y activar la aplicación.
5. Abra la dirección en una ventana privada u otro navegador sin sesión y compruebe lo que aparece.

### Qué pueden hacer los visitantes

El visitante puede abrir la pantalla Runtime y leer los TAG seleccionados. La sesión usa la capacidad View Only de la licencia o Demo; si no hay plaza, se rechaza el acceso. El enlace Ingresar vuelve al login normal.

El visitante no recibe cuenta, role ni permiso de Authority. Commands, escrituras de TAG, reconocimiento y supresión de alarmas, Engineering, histórico, informes, fuentes multimedia y administración siguen protegidos. Ocultar un botón no cambia estas reglas: el servidor también rechaza el acceso directo a las rutas protegidas.

### Deshabilitar el acceso y resolver problemas

Desmarque Permitir visualización pública sin inicio de sesión y guarde para retirar el acceso anónimo. Cambiar la política o activar otra revisión también invalida las sesiones públicas existentes. Si la dirección muestra login, compruebe que la opción esté habilitada y que la revisión Active coincida con la configuración. Si Runtime no abre, compruebe la capacidad View Only disponible. Un estado incompatible de Authority o de la aplicación cierra el acceso de forma segura.

[Abrir esta guía en Ayuda instalada](/help?topic=security.public-runtime-view)

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

Use Engineering Lock para proteger contenido de ingeniería. El secreto desbloquea esa protección, pero no concede permisos ausentes. Es distinto de la reserva de edición cuando alguien trabaja en el proyecto.

Guarde cada secreto según finalidad: contraseña de cuenta identifica persona; secreto Lock protege aplicación; contraseña de backup Authority protege el archivo. No los escriba en pantallas, Scripts o Ayuda. Administración, importación/exportación, restauración y desbloqueo siguen exigiendo permisos normales aunque esté bloqueada.

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

1. Con acceso autorizado abra el candado.
2. Defina secreto privado. Use Configurar para seguir trabajando o Configurar y bloquear para restringir de inmediato.
3. Compruebe respuesta y estado del servidor.
4. Complete Guardar → Publicar → Activar. Configurar cambia el borrador Working; el ciclo lleva protección a la revisión activa que debe permanecer tras reinicio. Configurar solo no lo completa.
5. Para editar después use Desbloquear con secreto correcto y permisos adecuados.
6. Compruebe contenido y protección de la revisión a publicar. Ante rechazo lea el motivo. No borre usuarios, base ni Authority para intentar desbloquear.

[Abrir esta guía en Ayuda instalada](/help?topic=security.lock-workflow)
