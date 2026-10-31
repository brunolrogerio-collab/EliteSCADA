# Estaciones Runtime en varios monitores

## Estaciones Runtime: usar ventanas en varios monitores

Elija una pantalla por ventana y organícelas en el escritorio extendido.

<!-- HELP-REVIEW-AFTER-PRODUCT-781: https://github.com/brunolrogerio-collab/EliteSCADA/issues/781; topic=runtime.station-multimonitor; fields=station.name,station.screen,station.mode,station.windows; status=WAITING_PREVIEW_ACCEPTANCE -->

### Qué organiza una estación

Una estación agrupa de una a cuatro ventanas independientes de la misma aplicación Runtime. Para cada ventana, el perfil define la pantalla que abre primero y si puede navegar o permanece fija en ella. El perfil no selecciona un monitor físico ni guarda posición, tamaño o escala de la ventana. Después de abrirlas, distribúyalas manualmente con el sistema operativo y el navegador. También funciona al acceder al Runtime por red desde un equipo con varios monitores.

### Configurar el perfil

1. Cree y aplique las pantallas que usará.
2. Abra Ingeniería > Estaciones / Monitores y elija Añadir estación.
3. Asigne un nombre reconocible, como Sala de control EEE.
4. Para cada ventana, elija la pantalla inicial y el comportamiento. Use Añadir ventana para agregar posiciones, hasta cuatro; cada posición se convertirá en una ventana Runtime.
5. Seleccione Validar y resuelva los errores indicados.
6. Cuando la configuración sea válida, elija Aplicar a Working.
7. Guarde y publique la revisión y actívela mediante el ciclo del proyecto. La estación aparecerá en Runtime activo después de activarla.

### Entender los campos

El número de ventana indica el orden en que se abrirá. No es el número del monitor. Los selectores usan pantallas ya existentes en el proyecto; crear una estación no crea ni publica pantallas.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre de la estación | Identifica el conjunto en el selector Runtime. Elija un nombre que el equipo reconozca; no identifica un equipo ni un monitor físico. | Sala de control EEE | El perfil necesita al menos una ventana. |
| Pantalla de cada ventana | Elige qué pantalla existente se abrirá primero en esa ventana. Configure cada fila por separado para mostrar información distinta en paralelo. | Ventana 1: General; Ventana 2: Bombas | La pantalla debe existir en la aplicación que se activará. |
| Comportamiento de la ventana | Navegación permite cambiar de pantalla en esa ventana. Pantalla fija conserva la pantalla elegida y solicita sesión Solo visualización; no cambia los permisos de la cuenta. | General: Navegación; Bombas: Pantalla fija | Cada ventana autenticada cuenta como una conexión Runtime independiente. Siguen aplicándose los límites de licencia y los permisos de la cuenta. |
| Añadir ventana | Agrega otra pantalla al conjunto. Cada fila se convierte en una ventana independiente al iniciar la estación. | Cuatro ventanas para cuatro áreas operativas | El perfil admite de una a cuatro ventanas; la ubicación física en cada monitor es manual. |

### Iniciar y distribuir las ventanas

Abra Runtime en una ventana normal, con el encabezado visible. Si está a pantalla completa, salga primero. En el encabezado, seleccione la estación y pulse Iniciar estación. El navegador puede bloquear ventanas nuevas; en ese caso Runtime muestra enlaces para abrirlas una por una. Arrastre cada ventana al monitor deseado. Ajuste su tamaño y, si resulta útil, ponga cada una a pantalla completa. El producto no detecta ni restaura automáticamente la posición física, la orientación o la escala de los monitores.

Después de iniciar, consulte la lista Estado de las sesiones por pantalla en la ventana de origen. Cada fila indica si el servidor admitió esa ventana y qué clase concedió, o si no había una plaza disponible. Una ventana rechazada por el límite de licencia no fue admitida; revise las conexiones en uso y finalice explícitamente las que ya no necesita desde el menú de cuenta de cada ventana antes de volver a intentarlo. Iniciar una estación no aumenta el límite de licencia, y la ventana rechazada también muestra su propio aviso. Si una fila sigue sin confirmación, abra la ventana correspondiente y lea el mensaje de Runtime: no se pudo confirmar no significa que la sesión haya sido admitida. Si la instalación no usa admisión autenticada, la lista lo indica; ese mensaje no confirma una sesión autenticada/con licencia.

### Permisos y conexiones por ventana

Una ventana fija solicita acceso de Solo visualización; una ventana de navegación solicita acceso Interactivo. El servidor decide el acceso de cada ventana autenticada según los límites de licencia y los permisos de la cuenta. Compruebe el resultado en el menú de cuenta antes de enviar comandos. Si no hay una conexión Interactiva disponible, la ventana puede quedar en Solo visualización o ser rechazada. Solo visualización permite observar, pero no enviar comandos. Consulte también Sesión Runtime: visualizar, interactuar y finalizar.

### Finalizar, reabrir y resolver problemas

Para finalizar una sesión y liberar una conexión de licencia de inmediato, abra el menú de cuenta en cada ventana y elija Finalizar sesión antes de cerrarla. Cerrar la ventana o perder un monitor no garantiza que el servidor reciba la finalización; la licencia puede seguir contando esa conexión hasta que expire la sesión.

Si se cierra una ventana de la estación mientras la ventana de origen sigue abierta, la fila cambia a “ventana cerrada” y aparece el enlace “Volver a abrir pantalla N”. Úselo para abrir solo esa pantalla otra vez. La nueva ventana solicita su propia sesión Runtime; confirme el resultado de admisión y que haya una conexión disponible en la licencia antes de usarla. No se restaura la posición física en el monitor. Si también cerró o recargó la ventana de origen, abra Runtime, inicie la estación otra vez y distribuya las ventanas manualmente. Si no hay una conexión disponible, finalice sesiones que no use o espere a que expire la sesión anterior.

Si no aparece el selector de estación, compruebe que el encabezado esté habilitado, que Runtime no esté a pantalla completa y que la revisión en uso incluya el perfil. Si una ventana aparece en la pantalla equivocada, revise la pantalla seleccionada en esa fila y active la revisión actualizada. Si una ventana fija no cambia de pantalla, funciona según la configuración. Si el acceso Interactivo se limita o se rechaza, compruebe cuántas conexiones de licencia están en uso y los permisos de la cuenta.

#### Ejemplo visual

Las capturas muestran la interfaz real en portugués durante una prueba automatizada. El nombre de la aplicación y “Demo Overview” son datos de demostración; en su proyecto, elija la pantalla adecuada para cada ventana.

**Antes de validar**

![Perfil de cuatro ventanas rellenado, antes de validar](../assets/runtime-station-profile-before-validation.png)

**Después de seleccionar Validar**

![Perfil de cuatro ventanas con la configuración válida](../assets/runtime-station-profile-validated.png)

[Abrir esta guía en Ayuda instalada](/help?topic=runtime.station-multimonitor)
