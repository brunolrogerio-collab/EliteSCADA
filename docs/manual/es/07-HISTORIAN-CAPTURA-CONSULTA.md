# Historian: captura y consulta

## Historian: configurar captura y comprobar histórico

Elija qué guardar, en qué condiciones y cómo comprobar almacenamiento.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-HISTORIAN: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=historian.capture-query; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Dónde configurar y consultar

Para ver qué ocurrió antes, SCADIVO debe haber guardado los datos. Historian es ese histórico de muestras de TAGs. El monitor en vivo muestra el valor actual; Histórico, Informes e Intelligence usan lo almacenado. Habilitar hoy no recupera un pasado no guardado.

En Ingeniería > Historian vea TAGs con captura habilitada y su resumen. Para cambiar cómo graba un TAG, abra Ingeniería > TAGs, selecciónelo y expanda Histórico/Historian en el editor.

### Ejemplo del efecto de deadband

Deadband es la variación mínima para decidir si se guarda una nueva observación numérica. Se compara con el último valor aceptado.

Con calidad constante, deadband 0.1 y último aceptado 1.00, una observación 1.03 puede descartarse. El valor 1.12 supera el umbral y puede aceptarse. La próxima comparación parte del último aceptado, no de cada observación descartada.

La primera observación y los cambios de calidad tienen tratamiento propio y pueden guardarse sin esa variación. Con intervalo máximo, una observación recibida después del plazo también puede aceptarse. No calcule la cantidad esperada de registros solo con deadband.

### Procedimiento de comprobación

1. Compruebe el TAG en vivo: valor, tipo, escala y calidad.
2. En el editor del TAG, habilite Histórico y elija estrategia con sus campos válidos.
3. Ejecute Preview y Apply; guarde, publique y active. Editar el borrador no cambia por sí solo la captura en ejecución.
4. En una fuente de prueba produzca variaciones conocidas y anote cuándo ocurrieron.
5. Abra Histórico del Runtime, elija Muestras y filtre el TAG en ese período.
6. Compare valores, calidad, horarios y cantidad con la estrategia configurada.
7. Repita con señal estable y cambio de calidad. Para Intelligence compruebe también timestamp de origen y alineación exigida.

### Campos de captura TAG

Use los nombres admitidos en estrategia. Texto desconocido/configuración incompleta puede mantener captura legada de todas las observaciones, no la reducción esperada. Compruebe configuración/muestras; rellenar no garantiza ahorro.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Histórico habilitado | Habilita captura TAG. Deshabilitar no elimina histórico ni crea backup. | Habilite TAG presión de prueba |  |
| Estrategia | periodic respeta período; onChange registra cambios; onChangeDeadband exige variación numérica; onChangeDeadbandMaxInterval añade intervalo máximo entre observaciones aceptadas que llegan. | onChangeDeadbandMaxInterval |  |
| Deadband | Cambio absoluto mínimo desde último aceptado. No es porcentaje ni límite de alarma. Use estrategia numérica compatible. | 0.1 para cambio de una décima | Número finito ≥ 0 |
| Período (ms) | Usado por periodic. 1000 ms = 1 segundo. Selecciona observaciones entrantes, no aumenta polling ni inventa muestras si fuente calla. | 1000 | Entero positivo |
| Período máximo (ms) | Usado por onChangeDeadbandMaxInterval para aceptar observación al vencer tiempo pese a cambio pequeño. No garantiza captura durante corte de comunicación. | 60000 para hasta un minuto entre aceptaciones con observaciones llegando | Entero positivo |

### Consultar: período, filtros y páginas

Empiece eligiendo el conjunto de registros, llamado dataset. Muestras de valores y transiciones de alarmas son conjuntos distintos. Elija un período relativo corto o complete Desde/Hasta; la ventana actual permite hasta 31 días por consulta.

Use después los filtros ofrecidos para ese conjunto. Cada campo espera un tipo: número, booleano, fecha, texto o enum. Elija un operador disponible y el tipo correcto, no cualquier texto.

Ordenar cambia el orden de presentación, no selecciona otros registros. Cambiar página continúa la consulta; no amplía el período. Sin resultado, retire un filtro por vez y compruebe que existía captura en ese horario.

### Captura, retención, backup y Playback difieren

Son decisiones diferentes. Captura elige observaciones para guardar. Retención define cuánto tiempo conservarlas. Agregación resume muestras; backup guarda una copia recuperable. Configurar captura de un TAG no define cuota global de base ni archivado.

### Crear un archivo del Historian

En Ingeniería > Base de datos, en Archivar y restaurar muestras, elija el inicio y el fin del período. La zona horaria local del navegador se convierte a UTC en el archivo. El período debe terminar en el pasado. El archivo contiene solo muestras sin procesar de la tabla del Historian; no incluye agregados, alarmas, eventos, Auditoría, aplicación, Authority, licencia ni estado HA.

El destino es una carpeta del servidor de la API. El administrador del servicio puede definirla con `Historian:Archive:RootPath`. En Docker, monte esta carpeta en un volumen persistente para que los archivos sobrevivan a la recreación del contenedor. Con un Historian remoto, las muestras pasan por el servicio SCADIVO; el servidor PostgreSQL no necesita acceso a la carpeta del archivo.

Use Crear archivo verificado. El servicio divide el período en segmentos diarios UTC, comprime cada segmento, comprueba su cantidad de filas y SHA-256 mediante lectura de vuelta y guarda el progreso antes de continuar. La lista muestra En cola, En curso, Pausado o Completado y verificado. Si se reinicia el proceso, se reanudan los segmentos confirmados. Si el destino se llena o no está disponible, la tarea se pausa y las muestras originales se conservan; corrija el destino y use Reanudar.

Descargue solo cuando el estado sea Completado y verificado. Para restaurar, seleccione el `.scadhist` y use Verificar y restaurar. SCADIVO comprueba el paquete completo antes de insertar muestras. En cada segmento compara cuántas ocurrencias idénticas ya existen e inserta solo las que faltan; así evita duplicarlas cuando las muestras originales siguen en la base. El progreso queda registrado en la base Historian activa. Repetir el archivo no duplica muestras y puede recuperar datos que una política de retención eliminó después.

Borrar muestras originales es opcional y está desmarcado al inicio. Para habilitarlo, marque la opción y la confirmación adicional. La eliminación ocurre solo después de cerrar el paquete completo y verificarlo mediante lectura de vuelta. Afecta solo a las muestras sin procesar del período elegido; la retención de agregados es otra política. Mantenga el origen si aún necesita las muestras en el Historian activo.

### Programar archivos y definir la rotación

Configure una programación para esta instalación en la misma sección. Comenzar a archivar desde establece la primera posición del cursor. Archivar solo datos con al menos estos días deja fuera los datos recientes; Ejecutar cada define la frecuencia; Archivar hasta por ejecución limita cada lote a 31 días. El cursor avanza solo cuando el archivo del lote está completado y verificado. Revise el cursor y el estado de las tareas. Una tarea pausada no avanza; corrija el destino y use Reanudar.

Conservar los archivos sin fecha de expiración mantiene todos los paquetes completados. Si define un plazo, la rotación elimina paquetes `.scadhist` completados cuando superan esa antigüedad. No elimina muestras de la base Historian. Esta política se aplica también a los archivos manuales. Haga copias externas si necesita conservarlos más tiempo.

El destino es una carpeta del servidor de la API. En Docker, móntela en almacenamiento persistente. Si varios nodos de API usan la misma base, configure en todos la misma ruta hacia una carpeta compartida y persistente; el almacenamiento local independiente no basta. Para una base Historian remota, la carpeta sigue perteneciendo al servicio SCADIVO, no al host PostgreSQL.

La programación también puede eliminar el origen, pero la opción aparece desmarcada y requiere confirmación explícita. La eliminación comienza solo después de leer de nuevo y verificar el paquete completo. El progreso se guarda por segmento en la misma transacción de base de datos que elimina ese segmento; tras reiniciarse el servicio, se omiten los segmentos ya confirmados y las muestras que lleguen después permanecen en la base. El lote se marca como Completado solo después de procesar todos los segmentos autorizados. Configure la antigüedad del archivo antes de la retención o del límite por tamaño; esos mecanismos pueden eliminar muestras antes de que el cursor las alcance. Pruebe la restauración y supervise las tareas antes de depender del archivado para liberar espacio.

Playback muestra valores históricos. No restaura el proceso ni permite comandar desde el pasado. Vuelva al modo en vivo antes de operar. La falta de muestras no significa cero ni equipo detenido.

[Abrir esta guía en Ayuda instalada](/help?topic=historian.capture-query)
