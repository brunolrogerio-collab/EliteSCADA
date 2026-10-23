# Operación, seguridad y licencias

## Runtime

Runtime ejecuta la revisión Active bajo la autoridad del backend. La sesión recibe capabilities efectivas y, cuando corresponda, un lease de Runtime.

La pérdida/expiración de la autoridad interactiva no convierte la sesión en autorizada por defecto.

## Interactive y View Only

Interactive y View Only representan clases de sesión. View Only reduce la autoridad operacional; en particular, no concede acceso de escritura/comandos aunque el usuario tenga permisos que podría utilizar en una sesión interactiva.

La concesión de puestos/capacidad procede del entitlement de licencia y del servidor; los sockets o los controles visuales no son la unidad de autoridad.

## Commands

Commands representan la intención del operador y pasan por validación/autorización del lado del servidor. Un botón renderizado no demuestra que la acción vaya a aceptarse.

**Ayuda relacionada:** `/help?topic=bindings-commands.overview`.

## Diagnóstico y calidad de comunicación

Use diagnóstico por capas: backend, identidad/capabilities, Active, Data Source, Driver, conexión, binding, quality/timestamp y presentación. Ejecute las capabilities de Engineering del Driver solo cuando estén declaradas por su descriptor real.

**Ayuda relacionada:** `/help?topic=diagnostics.overview`.

## Authority, usuarios, roles, capabilities y scopes

Authority administra identidades y asignaciones. Los roles alimentan la autorización; las capabilities efectivas se calculan/aplican en el servidor.

No considere el nombre de un role como prueba suficiente de privilegio. El producto debe resolver las capabilities efectivas.

## Engineering Lock

Engineering Lock protege el contenido de desarrollo de la aplicación y es independiente de:

- la contraseña de la cuenta del usuario;
- la contraseña del backup de Authority;
- Licensing.

Cuando la aplicación está bloqueada, no se deben exponer los editores protegidos. Las funciones administrativas permitidas siguen sujetas a la autorización normal del backend.

Los cambios de Engineering Lock forman parte de Working y deben seguir Save/Publish/Activate para convertirse en estado Active duradero.

### Procedimiento: desbloquear la aplicación actual

**Requisitos previos:** acceso permitido a la administración restringida y el secreto correcto de Engineering Lock.

**Pasos:**
1. Abra la superficie de protección de la aplicación.
2. Introduzca el secreto de Engineering Lock.
3. Ejecute Unlock.
4. Confirme que Engineering vuelve a estar disponible conforme a las capabilities de la sesión.

**Resultado esperado:** solo se desbloquea la aplicación actual; la operación no autentica a un usuario ni cambia la licencia.

**Error esperado:** un secreto incorrecto mantiene Engineering bloqueado.

## Conceptos de licencia

Cuando no hay una licencia instalada, el estado es Demo. Una licencia válida tiene firma asimétrica y está vinculada a la máquina.

La licencia controla el entitlement comercial; no reemplaza Authority, Engineering Lock, lifecycle ni la validación del package.

### Demo/sin licencia

Según el contrato operacional implementado:

- Engineering puede contener más de 200 TAGs;
- se permite Run en Demo para proyectos de hasta 200 TAGs;
- cada Run Demo exitoso inicia una sesión continua de hasta 300 minutos;
- al expirar, Runtime industrial termina de forma controlada y Engineering se conserva;
- un nuevo Run explícito inicia una nueva sesión Demo.

Una licencia instalada pero inválida/tampered/wrong-hardware bloquea Run; no debe tratarse silenciosamente como si no hubiera licencia.

## Machine binding y Request

La instalación genera un Machine Request Code versionado a partir de la identidad estable de la máquina. El flujo normal no necesita exponer identificadores brutos de hardware al operador.

### Procedimiento: obtener el request

1. Abra Licensing.
2. Consulte el estado actual.
3. Copie el Machine Request Code.
4. Envíe solamente el request a la autoridad de licenciamiento.

**Resultado esperado:** el código puede utilizarse en la herramienta controlada de emisión sin distribuir la clave privada.

## Install / Replace / Remove

La instalación de la licencia valida la firma, el key ID, el vínculo de hardware y la validez antes de reemplazar el estado instalado.

Para reemplazarla, instale la nueva licencia validada mediante el flujo normal. Un intento inválido no debe reemplazar una licencia válida existente.

Remove elimina la licencia instalada; las activaciones futuras vuelven a las reglas de Demo, siempre que un estado externo inválido no bloquee Run.

## TAG tiers

Tiers documentados: 500, 1000, 1500, 3000, 5000 e Unlimited. Una licencia válida elimina el timeout de Demo y aplica el límite de TAGs correspondiente.

## Cuotas de sesión Runtime / Interactive / View Only

El estado de licenciamiento expone los entitlements de puestos View Only e Interactive cuando están presentes. El conteo y la aplicación corresponden al servidor; no debe confundirse una conexión de transporte con una sesión lógica licenciada.

## Entitlement de redundancia

El estado de licencia también puede exponer un entitlement de HA Runtime. El comportamiento operacional completo de redundancia sigue bloqueado por los workstreams #421/#423 y no se describe como terminado en esta fase.

## License Generator

License Generator es una herramienta offline/controlada de la autoridad de licenciamiento. La clave privada de firma nunca debe incluirse en GitHub, CI normal, un instalador de cliente ni Runtime.

Los usuarios finales de EliteSCADA usan request/install; solo el personal autorizado de emisión debe operar el entorno de firma.

**Ayuda relacionada:** `/help?topic=licensing.overview`.
