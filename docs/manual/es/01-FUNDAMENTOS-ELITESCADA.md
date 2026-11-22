# SCADIVO — Fundamentos para empezar

## 1. Conozca el recorrido de una aplicación

SCADIVO permite crear pantallas para supervisar y operar aplicaciones residenciales, de edificios o industriales. Piense en una bomba: quiere ver presión, consultar histórico y ofrecer una acción a un operador autorizado. Cada parte tiene una función y puede prepararlas poco a poco.

En **Ingeniería (Engineering)** prepara conexiones, TAGs, pantallas y reglas. En **Runtime** una persona usa la versión puesta en operación. Editar un dibujo en Ingeniería no cambia inmediatamente la pantalla que está operando.

No necesita aprender todos los términos antes de empezar. Prepare un ejemplo pequeño y consulte el glosario de la sección 5 cuando encuentre una palabra nueva.

## 2. Muestre una primera lectura

1. Siga [Primer proyecto](30-PRIMER-PROYECTO.md) para preparar una aplicación de prueba y lectura conocida en Server Memory, la memoria del servidor, sin equipo físico.
2. El **TAG** identifica esa lectura en el proyecto. Compruebe valor, calidad y hora antes de dibujar; [Ingeniería y datos](03-INGENIERIA-FUENTES-DE-DATOS-TAGS.md) explica origen y dirección.
3. En [Pantallas, Popups y objetos](05-PANTALLAS-Y-OBJETOS.md), inserte un valor mostrado y seleccione su TAG mediante un vínculo. Un Popup es una ventana de detalles abierta sobre la pantalla.
4. Compruebe Preview y aplique la edición al proyecto cuando exista ese flujo. Resuelva diagnósticos antes de continuar.
5. Guarde, publique y active la revisión en pasos separados, explicados abajo.
6. En Runtime compruebe revisión activa y lectura esperada. Añada alarmas, histórico o Scripts después de entender este primer recorrido.

## 3. Sepa qué guarda cada parte

| Parte | Para qué sirve | Qué comprobar |
| --- | --- | --- |
| Application / aplicación | Ingeniería del proyecto: TAGs, pantallas, Scripts y otros objetos portátiles. | ¿Qué proyecto y revisión se editan o utilizan? |
| Security Authority | Cuentas, contraseñas, roles y decisiones de acceso de la instalación. | ¿Esta persona solo puede visualizar o también ejecutar la acción? |
| License / licencia | Funciones y límites comerciales disponibles. | ¿La instalación tiene licencia compatible con las funciones usadas? |
| Historian | Guarda y consulta muestras si se configuró captura. | ¿El período solicitado tiene datos guardados con calidad adecuada? |

Una licencia no concede permiso de seguridad. El servidor decide autorización aunque se vea un botón. Clase de sesión y licencia pueden restringir funciones; no conceden permisos ausentes en Authority.

Un `.escadapkg` transporta la aplicación; no es backup de cuentas/Authority, licencia ni base/histórico. Estas copias tienen procedimientos propios en [Backup y recuperación](28-BACKUP-Y-RECUPERACION.md). En los flujos actuales mediante archivo, la licencia se vincula a la máquina de destino; compruebe estado en Licencias.

Pantallas, Scripts e informes usan valores proporcionados por Runtime. No cree en ellos una conexión privada al Driver para eludir fuente, autorización o ciclo del proyecto. Conversión de escala y acceso no pasan a tener otra implementación en el navegador.

## 4. Edite ahora, opere cuando esté preparado

Antes de editar compruebe proyecto, permiso de Ingeniería y control de edición solicitado. El borrador editable se llama **Working**. Una copia guardada e identificada es una **Revision**. **Published** indica revisión publicada; **Active** es la que ejecuta Runtime.

1. Abra Ingeniería en el proyecto correcto.
2. Edite el recurso en Working.
3. Valide y use Preview cuando exista. La previa comprueba propuesta sin aplicarla.
4. Use Apply cuando corresponda para incorporar edición validada al proyecto. Después guarde con Save/Guardar.
5. Cree o seleccione revisión correspondiente y compruebe cambios.
6. Publique revisión preparada con Publish/Publicar.
7. Use Activate/Activar cuando el cambio pueda entrar en operación. Compruebe revisión activa informada por servidor y resultado en Runtime.

**Resultado esperado:** la revisión activa anterior sigue en uso hasta activación explícita. Guardar no publica; publicar no activa. Apply de editor o importación tampoco equivale a activar.

Si un cambio parece aparecer antes de activar, compruebe qué revisión informa el servidor como Active y qué área está viendo. La apariencia local del editor no prueba un cambio en operación.

## 5. Consulte términos cuando los necesite

| Término del producto | Significado en su tarea |
| --- | --- |
| Engineering / Ingeniería | Área para crear y editar la aplicación. |
| Runtime | Área que presenta y ejecuta revisión en operación. |
| Working | Borrador actual del proyecto; puede tener cambios sin guardar. |
| Revision | Copia identificada del trabajo guardado. |
| Published / Active | Publicada está preparada para activar; activa es la revisión usada por Runtime. |
| Data Source / fuente | Configuración del origen de un valor, como conexión a equipo o memoria interna. |
| Driver | Comunica mediante un protocolo de equipo. Memoria interna es otra categoría de origen. |
| Server Memory | Memoria del servidor que conserva valores retentivos; una pantalla puede leerlos mediante TAGs. |
| Client Memory | Estado local de este cliente Runtime; puede perderse al terminar contexto y no se comparte automáticamente con otros clientes. |
| TAG | Variable identificada del proyecto con tipo y origen. Su valor actual no es todo su histórico. |
| Binding / vínculo | Relación entre propiedad y recurso, como valor mostrado y su TAG. |
| Quality / calidad | Condición de una lectura. Datos ausentes o mala calidad no significan cero. |
| Timestamp / hora de muestra | Instante de la lectura; ayuda a comprobar si está actualizada. |
| Script | Regla escrita para realizar una tarea en servidor o cliente según el tipo elegido. |
| Handler | Función declarada del Script que recibe un evento; su nombre debe coincidir con la función preparada. |
| Template Faceplate | Dibujo compartido asociado a equipos. Cambiar definición puede afectar instancias vinculadas. |
| Dínamo | Composición visual reutilizable con definición compartida y valores por instancia cuando está parametrizada. |
| Popup | Ventana de detalles sobre una pantalla, con objetos y vínculos propios. |
| Equipo | Identidad de un activo, como Bomba 01, separada de TAGs y dibujo que lo representa. |
| Preview / Apply | La previa comprueba propuesta; Apply incorpora contenido validado al proyecto. Ninguno es Activate. |
| Authority | Cuentas y reglas usadas por servidor para decidir quién puede hacer qué. |
| Rol / role | Permisos asignados a personas. Compruebe recursos y acciones realmente permitidos. |
| Capability / permiso | Acción permitida; escribir un valor y ejecutar un comando son acciones distintas. |
| Scope / alcance | Recurso o parte de aplicación donde se aplica un permiso. |
| Engineering Lock / secreto de aplicación | Protección opcional de contenido de Ingeniería. Su secreto difiere de contraseña de acceso y backup de Authority. |
| Reserva de edición | Control de quién puede editar en un momento, cuando se exige. No sustituye permisos de cuenta. |
| Historian | Grabación y consulta de muestras; requiere captura y datos disponibles. |
| License / licencia | Condiciones comerciales de uso/capacidad separadas de cuenta y roles. |

## 6. Continúe por la tarea necesaria

Use el [índice en español](README.md) para encontrar capítulos por tarea. En Ayuda instalada busque función, campo o comando. Las referencias detalladas explican opciones después del procedimiento inicial.

Si falta un valor, compruebe fuente, TAG, vínculo y revisión activa. Si se rechaza acción, lea retorno y compruebe sesión/permiso; no repita un comando incierto sin comprobar estado. Si falta histórico, revise captura y período, no solo valor actual.

**Ayuda relacionada:** `/help?topic=getting-started.first-project`, `/help?topic=engineering.lifecycle`, `/help?topic=bindings-commands.overview`, `/help?topic=recovery.backup-system-recovery`.
