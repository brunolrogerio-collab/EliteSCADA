# Plantillas y Equipos

## Plantillas: crear un faceplate reutilizable

Dibuje una vez y comprenda cómo los cambios llegan a equipos vinculados.

### Comprenda qué está creando

Una Plantilla Faceplate es una definición gráfica compartida, como una bomba con nombre, estado y lectura. Equipo identifica una bomba concreta. Bomba 01 y Bomba 02 pueden usar la misma plantilla; un equipo también puede existir sin ella. Un perfil de Intelligence organiza mediciones y no sustituye al dibujo.

### Dónde entrar y qué rellenar

Abra Ingeniería > Plantillas. El editor usa los mismos objetos, Propiedades, Dinámicas y Eventos que Pantallas. Necesita permiso de edición y bloqueo cuando se solicite.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre de plantilla | Nombre legible en biblioteca. Describa su finalidad, no una instancia física. | Bomba con estado y presión |  |
| Identificador | Referencia estable de los vínculos. Consérvela al actualizar el dibujo; cambiar el nombre visible no exige otro equipo. | pump-faceplate |  |
| Objetos y vínculos | Composición gráfica. Una referencia TAG fija sigue apuntando al mismo TAG al reutilizarse. Revise referencias de cada instancia antes de representar otro equipo. | Título, estado y valor mostrado |  |

### Procedimiento: crear y reutilizar

1. Seleccione Nueva plantilla y complete nombre e identificador.
2. Inserte un conjunto pequeño y configure propiedades.
3. Añada vínculos/dinámicas/eventos; no suponga sustitución automática de TAGs por nombre de equipo.
4. Ejecute Preview, corrija errores y aplique.
5. Abra Equipos, cree o seleccione una instancia y elija esta plantilla opcional. Guarde el vínculo.
6. En Pantallas insértela desde la biblioteca de equipos/faceplates, cuando esté disponible.
7. Guarde, publique y active. Compruebe dos instancias y sus lecturas en Runtime.

### Actualizar sin confundir definición e instancia

Editar la plantilla compartida actualiza el dibujo de todas las instancias vinculadas en esa revisión. Revise el impacto; use otra plantilla si necesita apariencia distinta. Cambiar el vínculo cambia el dibujo asociado y conserva la identidad del equipo. Un Dínamo es otro conjunto visual reutilizable; un Grupo solo organiza objetos.

### Si falta o no se actualiza

Compruebe plantilla aplicada, vínculo correcto y revisión activa. Una copia independiente no se vincula por compartir nombre. Actualice y valide si cambió el proyecto. Si aparece la lectura de otra bomba, revise referencias y no solo el título.

[Abrir esta guía en Ayuda instalada](/help?topic=templates.overview)

## Equipos: crear, identificar y asociar

Registre el activo real y después elija su dibujo y mediciones.

### Empiece por la identidad del activo

Abra Ingeniería > Equipos. Equipo representa una instancia real, como Bomba 01. No es Driver, TAG ni dibujo. Puede crearlo sin faceplate y asociar plantilla después. Intelligence usa esta identidad; dibujar una bomba no registra el equipo.

### Campos de instancia

El formulario muestra nombre y plantilla opcional. El producto genera la ruta; no necesita inventarla ni escribirla.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Nombre del equipo | Rótulo para encontrar el activo. Distinga las instancias. | Bomba 01 |  |
| Ruta generada | Referencia estable para comprobar. Renombrar una instancia conserva su ruta. Una ruta generada duplicada impide crear otra. | Compruebe la vista previa de ruta |  |
| Plantilla Faceplate (opcional) | Selecciona dibujo compartido. Sin plantilla es válido; quitar el vínculo no elimina equipo. Cambiarlo no elige TAGs ni perfiles de Intelligence automáticamente. | Plantilla Bomba o Sin plantilla |  |

### Procedimiento: registrar y comprobar

1. Introduzca un nombre distinto.
2. Compruebe ruta y elija plantilla o Sin plantilla.
3. Pulse Crear equipo; el producto valida antes de aplicar.
4. Selecciónelo en la lista para editar nombre/vínculo y guarde cambios.
5. Guarde/publique/active cuando se necesite en Runtime.
6. En Intelligence seleccione esta identidad y asigne perfil. Configure TAGs y captura antes de analizar.

### Verificación y errores frecuentes

La instancia debe aparecer con nombre, ruta y vínculo correcto. Sin plantilla no significa equipo inválido. Plantilla no disponible indica vínculo no resuelto: elija una existente o quite la asociación. Ruta duplicada requiere nombre distinto. Ante conflicto actualice y revise; no cree copias repetidas.

[Abrir esta guía en Ayuda instalada](/help?topic=equipment.overview)
