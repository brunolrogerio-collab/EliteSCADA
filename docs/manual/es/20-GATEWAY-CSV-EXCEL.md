# Gateway y archivos CSV/Excel

## Gateway de TAGs: configurar y probar una ruta

Transfiera valores entre TAGs del servidor con calidad, conversión y diagnóstico explícitos.

### Cuándo usarlo

Abra Ingeniería > Gateway. Una ruta lee TAG origen y solicita escritura al destino mediante Runtime. Puede enlazar protocolos ya configurados, pero no crea Data Source, dirección ni conexión. Es transferencia unidireccional, no sincronización bidireccional automática. No forme circuito de retorno sin analizar efectos. Aprenda con dos TAGs Server Memory sin equipos físicos.

### Identidad y extremos de ruta

Los selectores ofrecen TAGs con fuente habilitada y propiedad del servidor. Client Memory pertenece al navegador y no puede ser extremo. Simulation puede ser origen, no destino. Destino debe permitir escritura; extremos deben ser distintos. IDs conservan identidad interna; rutas ayudan a comprobar significado.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Identificador de ruta | Clave para reconocer ruta en proyecto y diagnóstico. El formulario mantiene nombre junto a identificador; en tabla Key y Name son columnas separadas. | copy-speed | Obligatorio, sin espacios. |
| Habilitada | Controla participación en revisión activada. No corrige fallo del destino. | Sí |  |
| TAG origen | Seleccione valor que alimenta destino. Revise tipo, calidad y escala en Monitor de TAGs. | Server.Source |  |
| TAG destino | Seleccione TAG escribible ya configurado para equipo o Server Memory. La ruta no cambia automáticamente su permiso. | Server.Destination |  |

### Disparo, inicio y conversión

GoodOnly es la política actual: mala calidad no se transfiere como lectura válida. Campos condicionales dependen del modo/conversión. Períodos están en milisegundos y no cambian adquisición del Driver.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| OnChange / Periodic | OnChange reacciona a actualizaciones aceptables con deadband/intervalo mínimo. Periodic comprueba valor corriente; no fabrica calidad Good ante fuente mala. | OnChange |  |
| Transferencia inicial | Sincronizar usa primer valor aceptable disponible al iniciar. Esperar próximo evita sincronización inicial y espera siguiente condición. Evalúe efecto antes de destino físico. | WaitForNextAcceptableValue |  |
| Deadband (OnChange) | Diferencia absoluta en unidad origen respecto al último valor transferido con éxito. Menor se omite; igualdad permite continuar. No es porcentaje ni deadband de alarma. | 0.1 | Número finito ≥ 0; solo OnChange. |
| Intervalo mínimo (OnChange, ms) | Limita frecuencia de intentos en modo cambio. Actualizaciones rápidas pueden agruparse y se relee valor actual; no es cola garantizada de cada muestra. | 1000 ms = 1 s | Si se configura: 100 a 86400000 ms. |
| Período (Periodic, ms) | Intervalo de transferencia periódica. No combine con deadband/intervalo mínimo OnChange en archivo. | 1000 | Obligatorio en Periodic: 100 a 86400000 ms. |
| Exact / CheckedNumeric | Exact exige compatibilidad directa sin gain/offset. CheckedNumeric transforma numéricos y valida destino. Desbordamiento o fracción perdida al convertir a entero falla; no hay truncado silencioso. | CheckedNumeric |  |
| Gain (ganancia) | Multiplicador en destino = origen × gain + offset. Ausente usa 1. No es escala del Driver. | 2 |  |
| Offset | Se suma tras ganancia; ausente usa 0. Confirme unidad/rango destino antes de publicar. | 5 |  |

### Procedimiento: 11 se convierte en 27 sin equipo real

1. Cree TAGs Server Memory escribibles Server.Source Int16 y Server.Destination Int32 sin destino físico.
2. Cree copy-speed con esos extremos, OnChange, Esperar próximo, CheckedNumeric, gain 2 y offset 5. Deje deadband/intervalo mínimo vacíos.
3. Ejecute Preview, corrija errores y aplique al Workspace. Guarde, publique y active.
4. En prueba actualice origen a 11 con calidad Good. Compruebe 27 en destino: 11 × 2 + 5.
5. Revise diagnóstico/último error; aceptación no demuestra actuación física.
6. Evalúe rango, unidad, calidad, inicio y ritmo antes de usar TAGs reales. No use rutas para eludir Authority, licencia ni autoridad HA.

### CSV equivalente y diagnóstico

Este CSV usa rutas legibles sin IDs. Ambos TAGs y fuente deben existir antes de Preview/Apply. En Import/Export elija Rutas Gateway y CSV; el archivo no crea TAGs. Diagnóstico Runtime describe revisión activa, no borrador. Esperando origen exige revisar fuente/calidad; Degraded y último error exigen conversión/rango/destino. Contadores omitidos/agrupados no significan automáticamente defecto: revise modo/límites. Importar no transfiere antes del ciclo apropiado.

```csv
Key,Name,SourceTagPath,DestinationTagPath,TransferMode,QualityPolicy,ConversionPolicy,InitialTransferPolicy,Gain,Offset,Enabled
copy-speed,Copy speed,Server.Source,Server.Destination,OnChange,GoodOnly,CheckedNumeric,WaitForNextAcceptableValue,2,5,true
```

[Abrir esta guía en Ayuda instalada](/help?topic=gateway.configure-test)

## CSV y Excel: exportar, editar e importar conservando referencias

Prepare una tabla por entidad, valide candidato y revise creación/actualización.

### Qué archivo usar

Use CSV o Excel para crear o ajustar muchos objetos del mismo tipo. Por ejemplo, exporte TAGs, añada filas para otros motores y revise importación antes de aplicar. Empiece siempre con el modelo exportado por el producto.

En Import/Export/Gestión del proyecto, CSV/Excel admite TAGs, Alarmas, Data Sources, Rutas Gateway y Eventos Operacionales. Entidad es el tipo de objeto de cada fila; elija una por archivo. Para transportar Pantallas, Popups, Templates, Scripts y dependencias compuestas, use paquetes de proyecto o bibliotecas. CSV/XLSX de ingeniería no exporta valores históricos ni hace backup de Authority/base.

### Procedimiento completo y recuperación de errores

1. Elija una entidad, exporte objetos actuales y conserve copia sin editar.
2. Edite pocas filas primero. Para ampliar use rutas/claves nuevas y distintas. Cree Data Sources antes de TAGs y después alarmas, rutas o eventos dependientes.
3. Guarde solo valores en el formato elegido y seleccione el archivo guardado. Sustituya fórmulas XLSX por valores antes de importar.
4. Elija modo de mezcla: crear nuevos, actualizar existentes o ambos. La tabla explica CreateOnly, UpdateExisting y CreateAndUpdate. Ninguno elimina todo lo ausente del archivo.
5. Ejecute Preview: comprueba antes de aplicar y no cambia Working, el borrador. Lea recuentos y errores por fila; ignorado no significa aplicado.
6. Corrija el archivo y repita Preview. Si cambia archivo, entidad, formato o modo, compruebe de nuevo. No borre filas solo para ocultar dependencias.
7. Confirme Apply del candidato validado, revise objetos en editor y guarde. Publique y active por separado para cambiar Runtime.
8. Si una actualización pierde una opción, compare columnas omitidas/valores predeterminados con la exportación completa original. Exportar copia configuración, no deshace activación automáticamente.

### Controles del flujo

Archivo y entidad/formato/modo identifican candidato. Cualquier cambio exige Preview de nuevo. El resumen muestra creaciones, actualizaciones, omitidos y errores; omitido no significa aplicado.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Entidad | Elija tipo de objetos de las filas. Una hoja de rutas no puede leerse como TAGs. | gateway-routes |  |
| CSV / XLSX | CSV usa comas y comillas para texto con comas/comillas/saltos. XLSX admite una hoja con nombre exportado para la entidad. Renombrar .xls antiguo no crea XLSX. | GatewayRoutes en XLSX |  |
| Modo de combinación | CreateOnly crea nuevos/omite existentes; UpdateExisting actualiza existentes/omite nuevos; CreateAndUpdate hace ambos. Coincide por identidad, no posición de fila. No sustituye todo ni borra ausentes. | CreateAndUpdate |  |
| Archivo y confirmación | Seleccione archivo guardado, Preview y lea diagnósticos antes de confirmar Apply. Contenido debe coincidir con validado; recargue/revalide si cambia Workspace. | Exportar → editar → Preview → Apply |  |
| Claves y rutas de referencia | Exportaciones actuales usan rutas/claves legibles, ocultando IDs internos. Conserve ruta/clave al actualizar; una nueva clave puede crear otro objeto. Referencias deben existir en proyecto/candidato adecuado. | SourceTagPath=Server.Source |  |

### Reglas al editar en Excel o texto

Conserve encabezados técnicos aunque la UI esté traducida. Columnas desconocidas/duplicadas pueden bloquear importación.

Use punto decimal (2.5) y booleanos true/false; no añada unidades al número. JSON exige JSON válido, no Python ni lista con punto y coma.

Conserve rutas/claves como texto para evitar fechas/números automáticos. Fórmulas XLSX se rechazan incluso con resultado guardado: pegue valores antes de guardar.

Una sola hoja: Tags, Alarms, DataSources, GatewayRoutes u OperationalEvents según entidad. No incluya usuarios, hashes de contraseñas, credenciales ni secretos; referencias protegidas no son secretos.

Para campos complejos parta de exportación real.

### Ejemplo: dos TAGs para prueba Gateway

Debe existir fuente Server Memory con clave memory.server. Este CSV mínimo crea TAGs del ejemplo, no la fuente. En proyecto de prueba seleccione TAGs/CSV y Preview, revise dos elementos y aplique mediante flujo protegido. Luego importe ruta del guía Gateway. Tabla reducida sirve para creación guiada: conserve exportación completa al actualizar objetos, pues columnas omitidas pueden asumir valores predeterminados y no conservar los anteriores.

```csv
Path,Name,DataType,DataSourceKey,ReadOnly,InitialValueDataType,InitialValueJson
Server.Source,Source,int16,memory.server,false,int16,0
Server.Destination,Destination,int32,memory.server,false,int32,0
```

[Abrir esta guía en Ayuda instalada](/help?topic=engineering.tabular-workflow)
