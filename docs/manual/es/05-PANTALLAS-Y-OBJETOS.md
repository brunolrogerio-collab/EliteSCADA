# Pantallas, Popups y objetos

Una pantalla reúne el dibujo del proceso, lecturas y acciones del operador. Un Popup es una ventana de apoyo para detalles de un equipo sin sustituir la pantalla principal. Este capítulo construye un ejemplo pequeño antes de presentar organización y reutilización.

## 1. Prepare un ejemplo comprobable

Use una aplicación de prueba con un TAG numérico de presión cuya unidad y valor actual conozca. Los ejemplos lo llaman `Plant.Pump.Pressure`, en bar. Es demostrativo: seleccione el TAG real de su aplicación en lugar de vincular una referencia inexistente. Para probar escritura, use una variable de prueba que admita escritura; no aprenda el editor enviando comandos reales al proceso.

Abra Ingeniería en el proyecto correcto. Confirme que su cuenta puede editar y que posee el bloqueo de edición cuando se exija. La edición visual tiene dos etapas: validar/aplicar el dibujo al proyecto y después guardar/publicar/activar una revisión. Ver un cambio en el editor no significa que Runtime ya lo ejecute.

## 2. Encuentre los controles del editor

| Región | Finalidad | Primer paso |
| --- | --- | --- |
| Lista de Pantallas o Popups | Seleccionar una definición o crear otra | Compruebe nombre y proyecto antes de editar. |
| Barra superior | Insertar formas, textos, valores, controles y navegadores; deshacer/rehacer | Empiece con texto y un valor mostrado. |
| Área de dibujo | Posicionar, seleccionar y dimensionar objetos | Busque el objeto en Estructura si no puede seleccionarlo en el dibujo. |
| Barra junto al dibujo | Alinear, distribuir, dimensionar, agrupar y bloquear; ajustar zoom | Seleccione los objetos que participarán. |
| Estructura / Biblioteca / Recursos | Encontrar objetos, insertar elementos reutilizables e importar recursos | Estructura ayuda con objetos superpuestos. |
| Propiedades / Dinámicas / Eventos | Configurar la selección | Sin objeto seleccionado, Propiedades muestra la superficie y Eventos puede mostrar eventos de pantalla. |
| Validación y aplicación | Comprobar el candidato y aplicarlo al proyecto | Lea errores antes de aplicar. |

El panel cambia con la selección. Si una propiedad desaparece, compruebe si seleccionó uno o varios objetos, o el fondo. Algunos campos solo se aplican a determinado tipo.

## 3. Construya la primera pantalla

1. Abra **Pantallas** y cree una definición. Dé un nombre visible como `Bombeo`. El identificador es la referencia estable, no el título visual. Consérvelo cuando otros elementos referencien la pantalla.
2. Sin objetos seleccionados, compruebe dimensiones, orientación y fondo de la superficie. Las dimensiones definen el área lógica; el zoom solo cambia la vista del editor. Cambiar orientación no diseña automáticamente otra versión para móvil.
3. Inserte texto. Escriba `Presión de descarga`, elija un color legible y colóquelo cerca de la lectura. Un texto fijo no necesita TAG.
4. Inserte un **Valor mostrado**. Selecciónelo y localice **Valor de TAG / variable** en **Propiedades**. Seleccione el TAG de presión y aplique el vínculo. El destino de este objeto es el texto mostrado.
5. Active los decimales fijos y elija `1` para mostrar décimas. Cambia la presentación, no la precisión del TAG. Añada una etiqueta de unidad `bar`; distinga número y unidad.
6. Posicione y dimensione los objetos. Alinee título y lectura con los controles de organización. Ejecute **Preview del cambio**, revise errores o candidato válido y elija **Aplicar al Workspace**. Los rótulos siguen el idioma seleccionado.
7. Siga guardar, publicar y activar en [Ingeniería y datos](03-INGENIERIA-FUENTES-DE-DATOS-TAGS.md). Configure navegación si es necesario; crear una pantalla no la convierte automáticamente en la inicial.
8. Ábrala en Runtime y compare con el TAG. Cambie el valor de prueba y compruebe actualización, decimales y unidad. Si faltan datos, consulte calidad y disponibilidad; cero no demuestra comunicación correcta.

**Resultado esperado:** título fijo y lectura variable aparecen juntos; la lectura sigue al TAG de la revisión activa. El título no cambia al variar el TAG.

## 4. Comprenda las propiedades antes de rellenarlas

| Campo o grupo | Qué define | Ejemplo y efecto |
| --- | --- | --- |
| Posición X/Y y ancho/alto | Ubicación y tamaño en el dibujo | Más ancho admite un valor largo; no cambia la escala del proceso. |
| Rotación | Orientación del objeto | Gire un símbolo para representar la instalación; revise texto y espacio ocupado. |
| Visible | Si el objeto se presenta | Útil con una condición dinámica; ocultar no detiene equipos. |
| Habilitado | Si el control admite interacción | Deshabilitar un botón no concede ni revoca permisos. |
| Texto, fuente, tamaño y alineación | Contenido y legibilidad | Un título y un valor operativo pueden necesitar tamaños diferentes. |
| Relleno, contorno y grosor | Apariencia de la forma | Use contraste y leyenda, no solo color para estados importantes. |
| Mínimo, máximo, valor y unidad | Intervalo y presentación del indicador/control | Una escala 0–10 bar necesita presión en la misma unidad. El intervalo visual no convierte automáticamente el TAG. |
| Decimales | Formato de lectura | Una posición muestra décimas; no mejora la exactitud del instrumento. |

Un **valor fijo**, un **vínculo** y una **dinámica** tienen funciones distintas. El valor fijo es la configuración inicial. El vínculo proporciona un valor de origen, como un TAG. La dinámica calcula o elige una presentación según datos. Si una propiedad cambia sola, revise sus dinámicas y vínculo antes de modificar repetidamente el valor fijo.

La Ayuda instalada ofrece una referencia individual por tipo, con campos admitidos, valores iniciales, unidades y límites. Abra **Ayuda → Pantallas y objetos** y elija el tipo; la búsqueda también encuentra propiedades. Los identificadores técnicos quedan disponibles como referencia sin exigir memorizarlos.

## 5. Elija el objeto por su función

| Necesidad | Objeto y configuración que revisar |
| --- | --- |
| Dibujar equipos y tuberías | Rectángulo, elipse, línea o polígono: posición, tamaño, contorno/relleno; polígonos también tienen puntos. Las formas no controlan por sí mismas el proceso. |
| Identificar o mostrar una lectura | Texto o Valor mostrado: texto/fuente/alineación; las lecturas también necesitan vínculo y formato. |
| Ver una magnitud dentro de un intervalo | Indicador, anillo, barra graduada, termómetro, bandas o real frente a objetivo: valor, mínimo/máximo, unidad, límites/meta y presentación aplicable al tipo. Elija un intervalo con significado operativo. |
| Cambiar una variable | Entrada numérica o slider: vínculo de valor, intervalo, paso y opción de escritura. Siguen siendo necesarias dirección de escritura del TAG, autorización y sesión Runtime. |
| Ejecutar una acción | Botón: leyenda, habilitación y evento/acción. Una leyenda “Arrancar” no configura un comando. |
| Observar evolución | Tendencia: añada series y configure TAG, leyenda, eje y escala. La ventana pertenece al gráfico; cada serie tiene sus ajustes. |
| Consultar ocurrencias | Navegador de alarmas o eventos: fuente/modo, período, filtros, columnas y ordenación. Reconocer una alarma no equivale a registrar un evento. |
| Usar medios o documentos | Imagen, símbolo SVG, modelo 3D, vídeo o PDF: seleccione un recurso admitido y ajuste presentación. No incluya credenciales en URLs. |
| Abrir informe | Lanzador de informe: seleccione uno existente y sus parámetros disponibles. El objeto visual no crea la definición. |
| Mostrar Intelligence | Use generación guiada para asociar objetos a perfiles/equipos y resultados existentes; vea [Inteligencia de Activos y Procesos](12-INTELIGENCIA-ACTIVOS-PROCESOS.md). |

### Tendencia: un ejemplo con dos magnitudes

Añada una serie de presión y otra de corriente. Dé leyendas claras como `Presión de descarga` y `Corriente del motor`. Use un eje para bar y otro para A: mezclar unidades incompatibles dificulta interpretar la escala. Distinga color y estilo. Una escala fija 0–10 bar facilita comparar amplitudes; valores fuera de ella pueden quedar fuera del área visible.

Compruebe modo en vivo/histórico, ventana, actualización y captura del Historian. Seleccionar un período no crea muestras nunca almacenadas. Empiece los navegadores sin filtros restrictivos y añada uno cada vez; una lista vacía puede deberse a configuración.

## 6. Añada dinámicas e interacción con intención clara

En **Dinámicas**, seleccione un solo objeto. Elija propiedad y origen. Una condición booleana puede cambiar visibilidad; un mapa relaciona estados y presentación; un relleno analógico representa nivel. Use opciones ofrecidas para el tipo y revise condiciones de calidad/ausencia de valor disponibles. Pruebe verdadero, falso y sin datos; no presente pérdida de comunicación como equipo parado.

En **Eventos**, distinga acciones preparadas y handlers de Script. Para una acción simple, seleccione un botón, elija acción de clic y destino. El asistente usa **Click** y conserva una acción por disparador; añadir otra para el mismo disparador sustituye la anterior. Para más lógica configure el handler Client y dependencias según [Scripting](06-SCRIPTING-ALARMAS-HISTORICO.md). Una acción preparada y un Script son configuraciones diferentes.

Para escribir una variable de prueba, seleccione acción y referencia real respetando tipo e intervalo. Para ejecutar un comando, seleccione el comando real. `CommandExecute` y `ProcessValueWrite` son autorizaciones distintas; ninguna propiedad visual las concede. Use retorno y estado real para indicar éxito: cambiar el color del botón no confirma que el equipo arrancó.

## 7. Cree y abra un Popup

1. Abra **Popups**, cree uno y llámelo `Detalles de la bomba`. Elija dimensiones adecuadas al dispositivo. Seleccione una plantilla existente solo si la aplicación la necesita; un Popup también puede ser independiente.
2. Inserte identificación y lecturas con los mismos objetos de pantalla. Vincule los TAGs del equipo correcto. Valide y aplique el Popup al proyecto.
3. Vuelva a la pantalla de origen y seleccione un botón. En **Eventos**, elija **Open popup / Abrir Popup**, seleccione destino y añada la acción. Los controles actuales pueden mostrar el nombre en inglés.
4. Valide y aplique la pantalla; guarde, publique y active la revisión con ambos recursos.
5. En Runtime pulse el botón, compruebe lecturas y cierre el Popup. La pantalla principal debe seguir siendo el contexto operativo.

Si aparecen datos de otra bomba, corrija vínculos o parámetros de la instancia. Cambiar solo el nombre no cambia fuentes de datos. Abrirlo tampoco concede permiso para sus acciones.

## 8. Organice y reutilice sin perder referencias

Use Estructura para seleccionar objetos detrás de otros. El orden visual determina qué queda delante; alineación posiciona bordes/centros; distribución organiza espacios; tamaño común uniformiza dimensiones. Agrupar facilita trabajar con un conjunto, pero no crea equipos ni combina permisos. Bloquear evita movimientos accidentales; no es un bloqueo de seguridad.

Los recursos visuales son archivos importados, como imágenes. Los dínamos son dibujos reutilizables con objetos y parámetros; cada instancia puede exponer parámetros propios. Después de insertar uno, compruebe parámetros, dimensiones, orientación y referencias de instancia. La apariencia de una bomba no selecciona automáticamente sus TAGs. Una copia también necesita revisar vínculos antes de representar otro equipo.

## 9. Diagnostique por síntoma

| Síntoma | Primera comprobación |
| --- | --- |
| Falta una propiedad | Tipo/cantidad seleccionada, filtro del panel y si el campo es editable. |
| Lectura fija | Vínculo aplicado, TAG real, disponibilidad/calidad y revisión activa. |
| El objeto cambia pese al valor fijo | Dinámicas, vínculos y Script sobre la misma propiedad. |
| Botón sin efecto | Acción/evento, destino existente, habilitación, revisión y autorización. |
| Escritura rechazada | Tipo/intervalo, dirección de escritura del TAG, permiso y clase de sesión Runtime. |
| Objeto ausente | Visibilidad, posición/tamaño, superposición, recurso e intervalo/escala. |
| Aplicar se rechaza después de validar | El proyecto puede haber cambiado; actualice, revise cambios y valide de nuevo. |
| Runtime muestra el dibujo anterior | Guardar, publicar y activar son distintos de aplicar el dibujo. Revise la revisión activa. |

## 10. Continúe con Ayuda

Temas instalados: `visual.first-screen`, `visual.object-selection`, `visual.properties`, `visual.dynamics`, `visual.events`, `visual.popup-workflow`, `visual.trend-series`, `visual.browser-filters` y `libraries.reusable-resources`. Abra uno mediante `/help?topic=<identificador>` en el producto. Las capturas finales procederán de la interfaz real después de la auditoría visual; una imagen ilustrada no sustituye la interfaz aceptada.
