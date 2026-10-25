# Bibliotecas y assets: reutilizar e incorporar

## Bibliotecas: asociar, revisar e incorporar recursos

Reutilice recursos sin confundir catálogo con contenido efectivo del proyecto.

### Elija el mecanismo de reutilización

Abra Ingeniería > Bibliotecas. Un archivo .escadalib agrupa recursos reutilizables como Templates, Dínamos, Pantallas, Popups, Scripts y assets visuales con dependencias. No es proyecto completo .escadapkg, backup de base ni Authority. El catálogo puede mostrar origen de fábrica, proyecto y bibliotecas asociadas. Asociar permite consultar; Usar/incorporar copia el recurso y dependencias necesarias a Working. Solo entonces forma parte del proyecto. Preview visual permite revisar apariencia sin incorporar.

### Campos, filtros e información

Nombre, tipo, categoría, origen y palabras clave ayudan a localizar el recurso. Sin resultados puede deberse a filtros combinados; límpielos antes de importar otra copia. Versión/hash y dependencias son datos de inspección, no campos para referencias arbitrarias.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Archivo para asociar | Seleccione el .escadalib recibido. El servidor valida estructura/contenido; renombrar otro archivo no lo convierte. | bombas-1.0.escadalib |  |
| Origen / filtros del catálogo | Distinga fábrica, proyecto y biblioteca asociada antes de usar. Recursos del proyecto ya pertenecen a Working; asociados pueden necesitar incorporación. | Asociada → Template de bomba |  |
| Dependencias / inspección | Revise recursos que acompañan la selección. Una Pantalla puede depender de Dínamos e imágenes; incorporar puede incluir más de uno. IDs/rutas internas sirven para inspección, no para recrear manualmente. | Pantalla + Dínamo + imagen |  |
| Nombre de nueva biblioteca | Al exportar una selección, nombre claramente el conjunto. No es nombre de aplicación Runtime. | Bombas de estación |  |
| Versión de nueva biblioteca | Introduzca versión para identificar la entrega. Describe la biblioteca; no cambia revisión del proyecto ni actualiza automáticamente copias incorporadas. | 1.0 |  |
| Recursos para exportar | Seleccione recursos existentes; elija el elemento principal y revise dependencias incluidas. Exportar crea archivo, no publica revisión. | Template y Popup de bomba |  |

### Procedimiento: usar un recurso recibido

1. Abra Bibliotecas y asocie el archivo. Revise nombre, versión y origen.
2. Elija biblioteca y filtre tipo. Revise dependencias; use Preview visual cuando exista. Asociar y Preview no cambian Working.
3. Use/incorpore el recurso y espere confirmación/actualización. Modifica Working y puede reutilizar contenido equivalente presente.
4. Abra el recurso en su área. Revise TAGs, comandos, parámetros y referencias: un dibujo de bomba no adivina su TAG.
5. Inserte/configure objetos en Pantallas o Popups y pruebe. Guarde, publique y active antes de comprobar Runtime.
6. Para exportar, introduzca nombre/versión, marque recursos y descargue .escadalib. Reábralo en un proyecto de prueba para comprobar dependencias.

### Actualizaciones, desasociación y fallos

Desasociar elimina la biblioteca del catálogo asociado; no borra recursos incorporados al proyecto. El origen registrado es informativo, no sincronización automática con futuras versiones. Un mismo ID con contenido distinto puede causar conflicto; revise la versión requerida antes de desasociar/asociar otro archivo. Preview no disponible no prueba paquete inválido: algunos tipos no tienen vista visual. Si cambió Workspace, recargue antes de repetir. Asociar no aporta credenciales de Driver, usuarios ni licencia. El catálogo asociado actual es local al proceso y puede desaparecer al reiniciar el servicio; conserve el archivo para asociarlo de nuevo. Es distinto de recursos ya incorporados y guardados en el proyecto.

[Abrir esta guía en Ayuda instalada](/help?topic=libraries.associate-incorporate)

## Assets visuales: importar, nombrar y usar imágenes

Organice archivos gráficos y conserve vínculos al renombrar.

### Archivo gráfico y objeto visual

Abra Ingeniería > Assets visuales. El asset es archivo de imagen/modelo; un objeto de Pantalla lo muestra. Importar no crea TAG, comando, Dínamo parametrizado ni instancia en Pantalla. Catálogo de fábrica y assets del proyecto se presentan por separado. Arte de Dínamos/fábrica puede no aparecer en imágenes del usuario; busque su origen antes de duplicar.

### Campos y acciones

Use la vista previa para revisar contenido y un nombre legible para localizarlo. Tipos del selector siguen validados por el servicio; cambiar extensión no basta.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Importar archivo | Seleccione PNG, JPG, BMP o SVG; el selector admite también GLB/GLTF para modelos soportados. Compruebe compatibilidad del objeto; no todo control muestra 3D. | bomba-frente.svg |  |
| Nombre visible | Inicialmente procede del archivo sin extensión. Renombrar cambia etiqueta y conserva identidad estable de los vínculos. | Bomba — vista frontal |  |
| Eliminar / confirmar | Exige confirmación y puede rechazarse si está en uso. Elimine o sustituya referencias antes de repetir. | Eliminar imagen sin uso |  |

### Procedimiento y comprobación

1. Importe archivo y espere confirmación.
2. Revise vista previa y nombre; ajuste etiqueta.
3. Abra Pantalla/Popup e inserte objeto compatible, eligiendo asset en biblioteca/selector. Para fondo o branding, use su campo de configuración.
4. Ajuste tamaño/proporción en Pantalla; no altera píxeles originales.
5. Guarde, publique y active antes de revisar Runtime. Si falta, compruebe vínculo, tipo, revisión y disponibilidad; no sustituya por otro solo por tener nombre parecido.

[Abrir esta guía en Ayuda instalada](/help?topic=assets.management)
