# Bibliotecas y assets: reutilizar e incorporar

## Bibliotecas: asociar, revisar e incorporar recursos

Reutilice recursos sin confundir catálogo con contenido efectivo del proyecto.

### Elija el mecanismo de reutilización

Una biblioteca .escadalib reúne recursos reutilizables, como Template, Dínamo, Pantalla, Popup, Script o archivo gráfico. Incluye dependencias, los otros recursos necesarios para usar el ítem. No es proyecto .escadapkg completo, backup de base ni Authority.

Asociar solo la deja disponible en catálogo. Preview visual muestra apariencia sin copiar al proyecto. Usar/incorporar copia recurso y dependencias al borrador Working. Solo entonces forman parte del proyecto. Compruebe si ve contenido de fábrica, proyecto o biblioteca asociada.

### Procedimiento: usar un recurso recibido

1. Abra Bibliotecas y asocie el archivo recibido. Compruebe nombre, versión y origen.
2. Elija biblioteca y filtre tipo de recurso. Revise dependencias y Preview visual cuando esté disponible. Asociar y Preview no modifican Working.
3. Use/incorpore y espere confirmación. Cambia el borrador y puede reutilizar contenido equivalente existente.
4. Abra el ítem en su editor. Compruebe TAGs, comandos, parámetros y referencias: dibujar una bomba no elige sus lecturas.
5. Inserte/configure en Pantallas o Popups y pruebe. Guarde, publique y active antes de comprobar Runtime.
6. Para exportar conjunto indique nombre/versión, seleccione recursos y descargue .escadalib. Reabra en proyecto de prueba para comprobar dependencias.

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

### Actualizaciones, desasociación y fallos

Desasociar elimina la biblioteca del catálogo asociado; no borra recursos incorporados al proyecto. El origen registrado es informativo, no sincronización automática con futuras versiones. Un mismo ID con contenido distinto puede causar conflicto; revise la versión requerida antes de desasociar/asociar otro archivo. Preview no disponible no prueba paquete inválido: algunos tipos no tienen vista visual. Si cambió Workspace, recargue antes de repetir. Asociar no aporta credenciales de Driver, usuarios ni licencia. El catálogo asociado actual es local al proceso y puede desaparecer al reiniciar el servicio; conserve el archivo para asociarlo de nuevo. Es distinto de recursos ya incorporados y guardados en el proyecto.

[Abrir esta guía en Ayuda instalada](/help?topic=libraries.associate-incorporate)

## Assets visuales: importar, nombrar y usar imágenes

Organice archivos gráficos y conserve vínculos al renombrar.

### Archivo gráfico y objeto visual

Para usar imagen o modelo en Pantalla importe primero el archivo en Ingeniería > Assets visuales. Asset es ese recurso gráfico; el objeto visual lo muestra en Pantalla. Importar no crea TAG, comando, Dínamo parametrizado ni instancia en el dibujo.

Compruebe origen antes de importar otra copia. Fábrica y archivos del proyecto están separados. Artes de Dínamos o fábrica pueden no aparecer en imágenes del usuario; busque en su origen.

### Procedimiento y comprobación

1. Importe archivo y espere confirmación.
2. Revise vista previa y nombre; ajuste etiqueta.
3. Abra Pantalla/Popup e inserte objeto compatible, eligiendo asset en biblioteca/selector. Para fondo o branding, use su campo de configuración.
4. Ajuste tamaño/proporción en Pantalla; no altera píxeles originales.
5. Guarde, publique y active antes de revisar Runtime. Si falta, compruebe vínculo, tipo, revisión y disponibilidad; no sustituya por otro solo por tener nombre parecido.

### Campos y acciones

Use la vista previa para revisar contenido y un nombre legible para localizarlo. Tipos del selector siguen validados por el servicio; cambiar extensión no basta.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Importar archivo | Seleccione PNG, JPG, BMP o SVG; el selector admite también GLB/GLTF para modelos soportados. Compruebe compatibilidad del objeto; no todo control muestra 3D. | bomba-frente.svg |  |
| Nombre visible | Inicialmente procede del archivo sin extensión. Renombrar cambia etiqueta y conserva identidad estable de los vínculos. | Bomba — vista frontal |  |
| Eliminar / confirmar | Exige confirmación y puede rechazarse si está en uso. Elimine o sustituya referencias antes de repetir. | Eliminar imagen sin uso |  |

[Abrir esta guía en Ayuda instalada](/help?topic=assets.management)
