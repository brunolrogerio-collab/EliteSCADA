# Plantillas y Equipos

## Plantillas: crear un faceplate reutilizable

Dibuje una vez y comprenda cómo los cambios llegan a equipos vinculados.

### Comprenda qué está creando

Use un Template Faceplate cuando varias máquinas necesiten el mismo dibujo. Por ejemplo, monte una bomba con nombre, estado y lectura y reutilice el dibujo para Bomba 01 y Bomba 02. Template es la definición gráfica compartida; cada Equipo identifica una bomba concreta.

Puede registrar el equipo sin Template y elegir dibujo después. El perfil de Intelligence reúne mediciones para análisis; no sustituye el dibujo.

### Procedimiento: crear y reutilizar

1. En Ingeniería > Templates use Nuevo Template y complete nombre e identificador.
2. Empiece con pocos objetos: título, estado y una lectura, por ejemplo. Ajuste propiedades en el editor.
3. Configure vínculos de datos, cambios visuales y eventos necesarios. El nombre del equipo no sustituye automáticamente los TAGs elegidos.
4. Ejecute Preview, corrija y aplique al proyecto.
5. En Equipos cree o seleccione una instancia real y elija el Template opcional. Guarde la asociación.
6. En el editor de Pantalla use la biblioteca de equipos/faceplates para insertar la instancia cuando esté disponible.
7. Guarde, publique y active. En Runtime compruebe dos instancias: el dibujo puede ser igual, pero cada lectura debe pertenecer al equipo correcto.

### Dónde entrar y qué rellenar

Abra Ingeniería > Plantillas. El editor usa los mismos objetos, Propiedades, Dinámicas y Eventos que Pantallas. Necesita permiso de edición y bloqueo cuando se solicite.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre de plantilla | Nombre legible en biblioteca. Describa su finalidad, no una instancia física. | Bomba con estado y presión |  |
| Identificador | Referencia estable de los vínculos. Consérvela al actualizar el dibujo; cambiar el nombre visible no exige otro equipo. | pump-faceplate |  |
| Objetos y vínculos | Composición gráfica. Una referencia TAG fija sigue apuntando al mismo TAG al reutilizarse. Revise referencias de cada instancia antes de representar otro equipo. | Título, estado y valor mostrado |  |

### Actualizar sin confundir definición e instancia

Cambiar la definición compartida puede afectar a todas las instancias vinculadas cuando entre en uso su revisión. Antes de aplicar compruebe qué equipos usan ese Template. Para otra apariencia solo en algunas instancias, considere otra definición.

Cambiar el Template asociado cambia el dibujo y conserva identidad del equipo. Un Dínamo es otro conjunto visual reutilizable. Grupo solo organiza objetos; no tiene el mismo vínculo a definición compartida.

### Si falta o no se actualiza

Compruebe plantilla aplicada, vínculo correcto y revisión activa. Una copia independiente no se vincula por compartir nombre. Actualice y valide si cambió el proyecto. Si aparece la lectura de otra bomba, revise referencias y no solo el título.

[Abrir esta guía en Ayuda instalada](/help?topic=templates.overview)

## Equipos: crear, identificar y asociar

Registre el activo real y después elija su dibujo y mediciones.

### Empiece por la identidad del activo

Empiece registrando la máquina que existe en planta, como Bomba 01. En Ingeniería > Equipos el registro identifica la instancia real, separada de conexión, TAGs y dibujo.

Puede crear el equipo sin faceplate y asociar Template después. Intelligence usa esa identidad para perfiles. Dibujar una bomba en Pantalla no registra automáticamente el equipo.

### Procedimiento: registrar y comprobar

1. Introduzca un nombre distinto.
2. Compruebe ruta y elija plantilla o Sin plantilla.
3. Pulse Crear equipo; el producto valida antes de aplicar.
4. Selecciónelo en la lista para editar nombre/vínculo y guarde cambios.
5. Guarde/publique/active cuando se necesite en Runtime.
6. En Intelligence seleccione esta identidad y asigne perfil. Configure TAGs y captura antes de analizar.

### Campos de instancia

El formulario muestra nombre y plantilla opcional. El producto genera la ruta; no necesita inventarla ni escribirla.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre del equipo | Rótulo para encontrar el activo. Distinga las instancias. | Bomba 01 |  |
| Ruta generada | Referencia estable para comprobar. Renombrar una instancia conserva su ruta. Una ruta generada duplicada impide crear otra. | Compruebe la vista previa de ruta |  |
| Plantilla Faceplate (opcional) | Selecciona dibujo compartido. Sin plantilla es válido; quitar el vínculo no elimina equipo. Cambiarlo no elige TAGs ni perfiles de Intelligence automáticamente. | Plantilla Bomba o Sin plantilla |  |

### Verificación y errores frecuentes

La instancia debe aparecer con nombre, ruta y vínculo correcto. Sin plantilla no significa equipo inválido. Plantilla no disponible indica vínculo no resuelto: elija una existente o quite la asociación. Ruta duplicada requiere nombre distinto. Ante conflicto actualice y revise; no cree copias repetidas.

[Abrir esta guía en Ayuda instalada](/help?topic=equipment.overview)
