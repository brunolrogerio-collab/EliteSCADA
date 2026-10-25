# Presentación, multimedia y ubicaciones

## Encabezado y Móvil: presentación y navegación

Configure identidad visual y elija pantalla adecuada al dispositivo.

### El encabezado no es la pantalla del proceso

Abra Ingeniería > Encabezado. Marca, títulos, accesos y reloj están sobre la pantalla de proceso. Cambiarlos no altera TAGs ni dibuja objetos. Valide vista previa y Aplique a Working, después guarde, publique y active.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Presentación de marca | Predeterminada usa SCADIVO; Texto usa texto indicado; Imagen usa recurso; Oculta quita marca. | Texto: Estación de bombeo |  |
| Texto y subtítulo | Título identifica aplicación; subtítulo añade contexto. Con imagen, texto también identifica de forma accesible. | Captación / Unidad 1 | Texto hasta 128; subtítulo hasta 256 caracteres |
| Imagen del proyecto | Seleccione/importe archivo admitido. No es URL de cámara; use Fuentes multimedia. | Logotipo de aplicación |  |
| Altura y colores | Altura ocupa espacio útil. Seguir tema mantiene colores; color personalizado los sustituye. | Compruebe lectura en escritorio y móvil | Altura 56–168 px |
| Título, nombre de pantalla y tipografía | Elija alineación y fuente/tamaño/peso/color. Mostrar nombre sigue navegación sin renombrar pantalla. | Título izquierda; nombre debajo |  |
| Posición y orden de controles | Mueve grupo de navegación al lado elegido; orden organiza posición relativa. Menú de usuario sigue a la derecha. | Controles a la derecha |  |
| Fecha y hora | Habilite si es útil; elija modo, formato, posición y orden. Este reloj no es timestamp de adquisición TAG. | Fecha y hora en encabezado |  |
| Accesos a pantallas | Para cada botón elija rótulo, pantalla existente e imagen opcional. La pantalla debe existir en revisión activa. | Bombas → Pantalla Bombeo | Hasta 16 accesos en editor |

### Móvil: orientación y variante

Abra Ingeniería > Móvil. Elija Horizontal o Vertical para dispositivos táctiles; bloqueo físico depende del navegador. Para cada pantalla elija Misma pantalla del escritorio o alternativa existente. Debe diseñar alternativa: Vertical no reorganiza automáticamente objetos. Comparten TAGs/Runtime, no copia de datos. Encabezado se configura en su área.

### Procedimiento y comprobación

1. Configure marca y accesos necesarios.
2. Valide/aplique.
3. Diseñe variante en Pantallas y selecciónela en Móvil si hace falta.
4. Valide/aplique Móvil.
5. Guarde/publique/active.
6. Compruebe botones, navegación, lectura y orientación en escritorio/móvil. Diseño anterior: revisión activa; imagen ausente: recurso seleccionado.

[Abrir esta guía en Ayuda instalada](/help?topic=application.presentation)

## Fuentes multimedia: cámara, dirección y credencial

Registre la fuente y después elija su presentación visual.

### Fuente y objeto son pasos distintos

Abra Ingeniería > Fuentes multimedia. La fuente describe origen; un objeto vídeo/imagen define presentación. Registrar RTSP no garantiza reproducción nativa: depende del relay del servidor. Compruebe infraestructura antes de cambiar parámetros repetidamente.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre visible | Identifica cámara/fuente en lista. | Cámara entrada |  |
| Clave estable | Referencia única; use letras, números, punto, guion o subrayado. | camara-entrada |  |
| Protocolo | Elija formato de origen: HTTP/HTTPS, HLS, MJPEG o RTSP/RTSPS. Cambiar opción no convierte contenido. | HLS si la fuente lo proporciona |  |
| Dirección | URL absoluta sin usuario, contraseña, parámetros ni fragmento. Credenciales van en formulario protegido. | https://camera.example.invalid/stream |  |
| Habilitada | Habilita en configuración; no prueba acceso ni reproducción. | Habilitar fuente en uso |  |
| Credenciales protegidas | Tras crear fuente elija Basic con usuario/contraseña o Bearer con token. Guarde por separado. Son de solo escritura, fuera de paquetes; configure de nuevo en host destino. | Use panel protegido, no URL con contraseña |  |

### Procedimiento y problemas comunes

1. Cree fuente con nombre, clave, protocolo, dirección y habilitación.
2. Guarde y configure credenciales si necesita.
3. Configure objeto visual con fuente admitida.
4. Guarde/publique/active y compruebe reproducción. Si falla revise endpoint/protocolo, acceso del servicio, credencial del host, relay exigido y revisión activa. Quitar credencial puede bloquear reproducción. Exportar proyecto no transfiere contraseñas de cámara.

[Abrir esta guía en Ayuda instalada](/help?topic=sources.media)

## Home/Building: ubicaciones y dispositivos

Organice ubicación y compruebe capacidades reales del dispositivo.

### Separe ubicación, dispositivo y conexión

Abra Ingeniería > Home/Building. Ubicación describe dónde está; dispositivo/equipo identifica instancia; Fuente de datos conecta. Una sala no es Driver; cambiar ubicación no cambia protocolo. Empiece con dispositivos/TAGs realmente expuestos.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre de ubicación | Nombre legible en jerarquía. | Sala de bombas |  |
| Tipo de ubicación | Elija Site, Edificio, Planta, Área/Sala o Zona. No concede permisos ni crea automatización. | Área/Sala |  |
| Ubicación superior | Dónde pertenece. Elija superior existente sin relaciones circulares; sin superior es raíz cuando se permite. | Edificio 1 → Planta 1 → Sala de bombas |  |
| Ubicación del dispositivo | Asocia instancia a ubicación; organiza proyecto, no prueba comunicación. | Bomba 01 en Sala de bombas |  |
| Nombre y ruta de dispositivo | Nombre es rótulo; ruta referencia estable. Revise candidato y conserve identidad al reorganizar. | Compruebe instancia y referencia en vista previa |  |

### Procedimiento y comprobación

1. Configure conexión y revise TAGs/capacidades.
2. Cree jerarquía con nombre/tipo/superior.
3. Asigne dispositivos y revise identidad.
4. Preview y aplique candidato validado.
5. Guarde/publique/active.
6. Pruebe lecturas/acciones en instalación de prueba. Mostrar equipo no crea capacidades/comandos ausentes; revise proveedor/mapeo/permisos. Para faceplates vea Equipos y Plantillas.

[Abrir esta guía en Ayuda instalada](/help?topic=home-building.organization)
