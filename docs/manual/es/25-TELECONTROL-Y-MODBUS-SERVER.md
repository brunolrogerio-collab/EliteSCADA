# Telecontrol y Modbus Server

## DNP3: asociación, eventos y comandos

Configure outstation y distinga identidad, consulta, representación y confirmación.

### Preparar la asociación

SCADIVO actúa como maestro/cliente DNP3 TCP. Obtenga del responsable IP/puerto, direcciones de enlace, lista de puntos, variaciones, clases y operaciones permitidas. Un punto estático puede llegar por consulta y eventos espontáneamente. Este descriptor no anuncia Browse ni Probar conexión de Ingeniería; revise asociación y punto conocido en Runtime/Diagnóstico tras activar. La ausencia de botón no significa ausencia del driver.

### Campos de la fuente

Los tiempos DNP3 usan duración hh:mm:ss, no milisegundos de otros drivers. Las clases de evento no son prioridades de alarma. Respete el perfil de asociación remota al ajustar opciones avanzadas.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Transporte DNP3 | Seleccione tcp, transporte implementado en este perfil. Introducir un puerto serie como host no lo convierte en DNP3 serie. | tcp |  |
| Dirección de estación remota | IP o nombre accesible desde el servidor SCADIVO. El navegador no abre la conexión de protocolo. | 192.0.2.10 |  |
| Puerto TCP | Puerto del servicio remoto. Por defecto 20000; compruebe configuración de estación y ruta de red. | 20000 | 1–65535. |
| Dirección DNP3 del maestro | Dirección de enlace local que la outstation debe reconocer como maestro. No es IP, puerto TCP ni índice de TAG. | 1 | 0–65519; debe coincidir con la asociación remota. |
| Dirección DNP3 de outstation | Dirección de enlace de estación remota. Aunque IP/puerto sean correctos, una dirección DNP3 incorrecta impide la asociación esperada. | 10 | 0–65519. |
| Tiempo para conectar | Tiempo máximo para establecer TCP; por defecto 5 s. No es intervalo de lectura. | 00:00:05 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Tiempo de respuesta DNP3 | Espera de respuesta de protocolo, por defecto 5 s. Aumentarla no corrige dirección de enlace o índice incorrectos. | 00:00:05 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Espera mínima de reconexión | Espera inicial tras desconexión, por defecto 1 s. No debe superar el máximo. | 00:00:01 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Espera máxima de reconexión | Límite de espera progresiva, por defecto 30 s. Los comandos anteriores no se conservan para repetirse tras reconectar. | 00:00:30 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Supervisión de enlace | Por defecto 1 minuto para supervisar enlace. Una conexión activa no demuestra un punto actualizado. Cero es inválido; vaciar un campo no debe interpretarse automáticamente como desactivación. | 00:01:00 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica de integridad | Por defecto 15 minutos. Vuelve a consultar datos para resincronizar la imagen de estación; no es retención de Historian ni frecuencia de todos los eventos. | 00:15:00 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica de clase 1 | Opcional, sin intervalo predeterminado: consulta eventos de esa clase como complemento al tráfico espontáneo. La clase depende de la outstation, no de la prioridad de alarma SCADIVO. | 00:01:00 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica de clase 2 | Opcional, sin intervalo predeterminado: consulta eventos de esa clase como complemento al tráfico espontáneo. La clase depende de la outstation, no de la prioridad de alarma SCADIVO. | 00:01:00 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Consulta periódica de clase 3 | Opcional, sin intervalo predeterminado: consulta eventos de esa clase como complemento al tráfico espontáneo. La clase depende de la outstation, no de la prioridad de alarma SCADIVO. | 00:01:00 | Duración positiva en hh:mm:ss; 00:00:05 = 5 s. |
| Integridad inicial — clase 0 | Incluye esta clase en resincronización inicial; por defecto true. Clase 0 representa datos estáticos; 1–3 clases de eventos. Debe quedar habilitada al menos una clase inicial. | true |  |
| Integridad inicial — clase 1 | Incluye esta clase en resincronización inicial; por defecto true. Clase 0 representa datos estáticos; 1–3 clases de eventos. Debe quedar habilitada al menos una clase inicial. | true |  |
| Integridad inicial — clase 2 | Incluye esta clase en resincronización inicial; por defecto true. Clase 0 representa datos estáticos; 1–3 clases de eventos. Debe quedar habilitada al menos una clase inicial. | true |  |
| Integridad inicial — clase 3 | Incluye esta clase en resincronización inicial; por defecto true. Clase 0 representa datos estáticos; 1–3 clases de eventos. Debe quedar habilitada al menos una clase inicial. | true |  |
| Suspender espontáneos de clase 1 al iniciar | Por defecto true: solicita suspender tráfico espontáneo al preparar la asociación. No elimina eventos de outstation ni cambia su clase. | true |  |
| Reactivar espontáneos de clase 1 | Por defecto true: solicita reactivación después de integridad inicial. Requiere soporte/configuración en estación; marcarlo no hace que un punto genere eventos. | true |  |
| Consultar eventos disponibles de clase 1 | Por defecto true: consulta la clase cuando se indican eventos disponibles. No cambia muestreo del sensor ni sustituye la cola remota de eventos. | true |  |
| Suspender espontáneos de clase 2 al iniciar | Por defecto true: solicita suspender tráfico espontáneo al preparar la asociación. No elimina eventos de outstation ni cambia su clase. | true |  |
| Reactivar espontáneos de clase 2 | Por defecto true: solicita reactivación después de integridad inicial. Requiere soporte/configuración en estación; marcarlo no hace que un punto genere eventos. | true |  |
| Consultar eventos disponibles de clase 2 | Por defecto true: consulta la clase cuando se indican eventos disponibles. No cambia muestreo del sensor ni sustituye la cola remota de eventos. | true |  |
| Suspender espontáneos de clase 3 al iniciar | Por defecto true: solicita suspender tráfico espontáneo al preparar la asociación. No elimina eventos de outstation ni cambia su clase. | true |  |
| Reactivar espontáneos de clase 3 | Por defecto true: solicita reactivación después de integridad inicial. Requiere soporte/configuración en estación; marcarlo no hace que un punto genere eventos. | true |  |
| Consultar eventos disponibles de clase 3 | Por defecto true: consulta la clase cuando se indican eventos disponibles. No cambia muestreo del sensor ni sustituye la cola remota de eventos. | true |  |
| Resincronizar tras desbordamiento de eventos | Por defecto true: solicita integridad cuando outstation informa desbordamiento. Recupera la imagen actual disponible, no eventos ya perdidos. | true |  |
| Sincronización de reloj remoto | disabled es predeterminado. lan o nonLan seleccionan el método DNP3 compatible. Puede ajustar el reloj remoto; coordine con responsable de estación. No define zona horaria ni valida sensores automáticamente. | disabled |  |
| Límite de solicitudes pendientes | Por defecto 16: limita solicitudes/comandos de usuario en cola para la asociación. No es límite de licencias de usuario ni permite repetir comandos antiguos tras reconectar. | 16 | 1–1024. |

### Campos de punto y comando

En TAGs seleccione fuente DNP3 y use asistente de familia/índice. Compruebe tipo y aplique vínculo; campos avanzados describen el contrato y pueden requerir la definición o archivos de Ingeniería del proyecto porque el asistente visible no ofrece todos los perfiles avanzados. Dirección, familia e índice deben coincidir.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección de punto DNP3 | Formato dnp3:tipo:índice, por ejemplo dnp3:analogInput:7. La fuente identifica outstation; no repita IP ni dirección de enlace en el punto. | dnp3:analogInput:7 |  |
| Familia del punto | binaryInput/binaryOutputStatus usan Boolean; doubleBitBinaryInput usa Enum; analogInput/analogOutputStatus usan tipo numérico compatible; counter/frozenCounter usan Int32 o Int64. Familia e índice identifican el punto, no el grupo de mensaje. | analogInput |  |
| Índice dentro de la familia | Índice publicado por outstation, de 0 a 65535; se admiten huecos. Índice 7 de analogInput no es índice 7 de binaryInput. | 7 |  |
| Representación estática | Opcional, formato GxVy. Debe corresponder a familia y tipo de TAG: G30V5 es analógico Float; G30V6 es Double. Variación 0 pide valor del dispositivo cuando se admite; no garantiza un tipo arbitrario. | G30V5 |  |
| Representación de evento | Opcional. G32V7 es ejemplo de analógico Float con tiempo. Representaciones estática y de evento deben coincidir con familia/tipo. La variación no cambia identidad del punto. | G32V7 |  |
| Clase de evento esperada | Opcional: class1, class2 o class3 para validación/diagnóstico. No reclasifica el punto remoto ni representa prioridad de alarma SCADIVO. | class1 |  |
| Habilitar comando en este punto | Por defecto false. Solo binaryOutputStatus/analogOutputStatus admiten comandos. También aplican ReadOnly, permiso y vínculo de comando; marcar true no convierte entrada en salida. | false |  |
| Seleccionar antes de operar | selectBeforeOperate es predeterminado; use directOperate solo si estación y procedimiento lo permiten. No se ofrece Direct Operate No Response. Confirmación de protocolo no demuestra actuación física. | selectBeforeOperate |  |
| Operación para verdadero | Comando binario: latchOn, latchOff, pulseOn o pulseOff. Por defecto latchOn. true es el valor solicitado; la operación real depende de este mapeo. | latchOn |  |
| Operación para falso | Mismo conjunto de operaciones; por defecto latchOff. Compruebe lógica remota antes de invertir significados, especialmente abrir/cerrar o arrancar/parar. | latchOff |  |
| Indicación Trip/Close | none, trip o close según equipo. Por defecto none. Este calificador no es enclavamiento ni autorización adicional para maniobrar. | none |  |
| Conteo de operación | Por defecto 1; conteo transportado en el comando binario. No es cantidad de reintentos SCADIVO. Acuerde interpretación con outstation. | 1 | 1–255. |
| Tiempo activo del comando | Duración hh:mm:ss del comando binario, por defecto 00:00:00. Para pulsos use la duración prevista por equipo; no es timeout de respuesta. | 00:00:00 |  |
| Tiempo inactivo del comando | Duración complementaria de comando binario, por defecto 00:00:00. No es retardo de alarma ni intervalo de adquisición. | 00:00:00 |  |
| Tipo de comando analógico | int16, int32, float32 o float64 debe corresponder a TAG Int16, Int32, Float o Double. Representación de comando G41 difiere del estado recibido. | float32 |  |

### Ejemplo de lectura analógica

Ejemplo de parámetros, no paquete completo. Outstation debe publicar punto analógico 7 en representaciones indicadas. Cree TAG Float de lectura; tras publicar/activar compare valor, calidad y origen de tiempo con una medición conocida. No suponga valor esperado solo porque conectó la asociación.

```json
{
  "source": {
    "transport": "tcp",
    "host": "192.0.2.10",
    "port": "20000",
    "masterAddress": "1",
    "outstationAddress": "10"
  },
  "address": "dnp3:analogInput:7",
  "binding": {
    "pointKind": "analogInput",
    "index": "7",
    "staticVariation": "G30V5",
    "eventVariation": "G32V7",
    "expectedEventClass": "class1",
    "writable": "false"
  },
  "tagDataType": "Float",
  "readOnly": true
}
```

### Ejemplo de salida binaria

Define el perfil de comando de una salida real acordada con outstation. No autoriza probar maniobras en equipo activo. Runtime debe admitir el comando y la sesión necesita capacidad correspondiente. Aceptación de protocolo y actualización de estado son evidencias distintas; compruebe retorno publicado por el punto.

```json
{
  "source": {
    "transport": "tcp",
    "host": "192.0.2.10",
    "port": "20000",
    "masterAddress": "1",
    "outstationAddress": "10"
  },
  "address": "dnp3:binaryOutputStatus:1",
  "binding": {
    "pointKind": "binaryOutputStatus",
    "index": "1",
    "writable": "true",
    "commandMode": "selectBeforeOperate",
    "binaryTrueOperation": "latchOn",
    "binaryFalseOperation": "latchOff",
    "tripCloseCode": "none",
    "commandCount": "1",
    "commandOnTime": "00:00:00",
    "commandOffTime": "00:00:00"
  },
  "tagDataType": "Boolean",
  "readOnly": false
}
```

### Investigar por síntoma

Sin asociación: revise IP/20000, maestro/outstation y red desde servidor. Asociado sin valor: familia/índice e integridad inicial. Eventos ausentes: clase, soporte espontáneo, consultas y configuración remota. Valor incompatible: variación y tipo de TAG; cambiar clase no corrige tipo. Un evento sin tiempo de origen no gana precisión por recibirse ahora. Comando rechazado: salida, tipo, modo, permisos y asociación; no repita automáticamente tras desconexión.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.dnp3-setup)

## IEC 104: estaciones, puntos observados y comandos

Configure CA/IOA y tipos sin tratar observación como lista completa de puntos.

### Preparación y qué demuestra Browse

SCADIVO es cliente/maestro IEC 60870-5-104. Obtenga de estación remota listas CA/IOA, tipos, reloj y comandos. Probar conexión demuestra solo el alcance de su resultado; Browse observa de forma limitada interrogación general y tráfico recibido, no garantiza barrido del mapa completo. El resultado es parcial; ausencia no demuestra que un punto no exista.

### Campos de fuente y punto

Los tiempos de fuente usan segundos. CA/IOA es identidad; Type ID define interpretación y perfil de comando es separado. No añada Type ID a dirección para eludir conflicto de configuración.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección de estación remota | IP o nombre accesible desde el servidor SCADIVO. El navegador no abre la conexión de protocolo. | 192.0.2.10 |  |
| Puerto TCP | Puerto del servicio remoto. Por defecto 2404; compruebe configuración de estación y ruta de red. | 2404 | 1–65535. |
| Estaciones — Common Addresses | Lista decimal de CA separados por coma, por ejemplo 1,2. Es dirección lógica de estación, no IP ni IOA de punto. El CA del TAG debe pertenecer a esta lista. | 1 | CA 0–65535 según perfil remoto. |
| Zona horaria de estación | Identificador de zona reconocido por servidor para interpretar CP56Time2a. UTC solo corresponde si la estación usa UTC. Si usa hora local configure su zona real, no la del navegador por conveniencia. Compruebe identificador en instalación Windows/Linux. | UTC |  |
| Originador de solicitud | Identificador de origen del protocolo; por defecto 0. No es usuario de login ni capacidad de Authority. Use el perfil de estación. | 0 | 0–255. |
| t0 — conexión | Tiempo para establecer conexión, en segundos. Por defecto 30. | 30 | 0.1–3600 s. |
| t1 — supervisión de transmisión | Espera supervisada de confirmación, por defecto 15 s. Debe superar t2. | 15 | 0.1–3600 s. |
| t2 — confirmación de recepción | Límite para confirmación diferida, por defecto 10 s; debe ser menor que t1. No es retardo de alarma. | 10 | 0.1–3600 s. |
| t3 — supervisión en reposo | Supervisión de sesión en reposo, por defecto 20 s. No es caducidad de una medición. | 20 | 0.1–86400 s. |
| k — ventana de envío | Máximo de unidades de información sin confirmar. Por defecto 12; no es cantidad de TAGs. Ajuste según perfil remoto. | 12 | 1–32767. |
| w — ventana de confirmación | Umbral de recepciones para confirmar, por defecto 8; no debe superar k. No aumenta cantidad de puntos disponibles. | 8 | 1–32767; w ≤ k. |
| CA del punto | Common Address que contiene el punto. Use uno configurado en fuente; puntos con igual IOA y diferente CA son distintos. | 1 | 0–65535. |
| IOA del punto | Dirección del objeto de información según lista de estación. El asistente genera ca=1;ioa=100 en el ejemplo; no es número de fila de tabla. | 100 | 0–16777215. |
| Tipo de información monitorizada | MSpNa1/MSpTb1 usan Boolean; MDpNa1/MDpTb1 usan Enum; MBoNa1/MBoTb1 usan Int32; MMeNb1/MMeTe1 usan Int16; MMeNa1/MMeTd1 y MMeNc1/MMeTf1 usan Float. La familia normalizada no se convierte automáticamente en voltios o bar. Tipos compatibles con/sin tiempo siguen refiriéndose al mismo CA/IOA. | MMeNc1 |  |
| Tipo de comando | Solo para TAG escribible: CScNa1 Boolean, CDcNa1 Enum, CSeNa1/CSeNc1 Float o CSeNb1 Int16. Debe ser compatible con tipo monitorizado y perfil remoto; no convierte entrada en salida. | CSeNc1 |  |
| Modo de ejecución de comando | direct o sbo según estación. El asistente comienza en sbo si no hay opción guardada; el descriptor declara direct como predeterminado. Elija explícitamente el modo deseado. sbo selecciona antes de operar; no sustituye enclavamientos de proceso. | sbo |  |
| Calificador de comando | Valor 0–31, por defecto 0, según operación remota. No es prioridad de alarma ni rol de usuario. | 0 |  |

### Primera lectura de punto conocido

1. Cree/guarde fuente y pruebe conexión desde servidor.
2. Use Browse para revisar puntos observados, o lista remota para autoría manual. Revise candidatos antes de aplicar.
3. En TAGs seleccione fuente y abra asistente IEC 104; introduzca CA, IOA y tipo monitorizado compatible. Empiece con TAG de solo lectura.
4. Publique/active por flujo del proyecto y compruebe valor, calidad y tiempo en Monitor de TAGs. La estación debe suministrar realmente el punto; los parámetros no generan medición ficticia.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "2404",
    "commonAddresses": "1",
    "stationTimeZone": "UTC"
  },
  "address": "ca=1;ioa=100",
  "binding": {
    "iec104.typeId": "MMeNc1"
  },
  "tagDataType": "Float",
  "readOnly": true
}
```

### Perfil compatible de comando

Este ejemplo configura el mismo punto como setpoint Float con selección antes de operación. Úselo solo si estación define ese CA/IOA para comando. Permiso, admisión Runtime, confirmación de protocolo y retorno de proceso son comprobaciones distintas. No habilite escritura solo para eliminar mensaje de tipo incompatible.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "2404",
    "commonAddresses": "1",
    "stationTimeZone": "UTC"
  },
  "address": "ca=1;ioa=100",
  "binding": {
    "iec104.typeId": "MMeNc1",
    "iec104.commandTypeId": "CSeNc1",
    "iec104.commandMode": "sbo",
    "iec104.qualifier": "0"
  },
  "tagDataType": "Float",
  "readOnly": false
}
```

### Diagnóstico por capas

Sin sesión: revise IP/2404 y parámetros APCI, incluidos t2 < t1 y w ≤ k. Sesión sin puntos: CA solicitado, finalización de interrogación general, ventana de observación y lista remota. Valor incompatible: IOA y familia Type ID; normalizado, entero escalado y Float no son intercambiables. Hora desplazada: reloj de estación, zona configurada y validez de CP56Time2a; cambiar solo reloj del navegador no corrige datos. Comando rechazado/ambiguo: tipo, modo, calificador y sesión; timeout no demuestra que estación no operó.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.iec104-setup)

## Modbus Server: publicar TAGs por TCP o RTU

Construya un mapa limitado y distinga cliente externo, escritura interna y valor actualizado.

### Servidor difiere de maestro

En Server SCADIVO atiende otro maestro/cliente y publica TAGs mapeados en Holding Registers. Esta fuente no consulta un PLC remoto. Para publicar valor de otro driver cree TAG destino Server y ruta Gateway compatible; un TAG no adquiere dos fuentes. TCP escucha interfaz/puerto; RTU responde mediante dispositivo serie del servidor.

### Campos de fuente y mapa

Campos de red pertenecen a TCP; de línea a RTU. Rangos y vínculos definen el mismo modelo de mapa. Editor no convierte automáticamente etiquetas 40001 de otras herramientas.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Interfaz local de servidor TCP | Dirección local de escucha, no IP de PLC remoto. Por defecto 0.0.0.0 escucha interfaces IPv4; 127.0.0.1 restringe al propio host. En Docker compruebe también puertos publicados y red del contenedor. Dentro de un contenedor, 127.0.0.1 es loopback del contenedor, no del ordenador host. | 127.0.0.1 |  |
| Puerto de escucha TCP | Por defecto 502. El ejemplo usa 1502 para instalación de prueba; el cliente externo debe usar el mismo puerto accesible. Otra fuente/proceso no puede ocupar el mismo endpoint. | 1502 | 1–65535. |
| Unit ID servido | Identificador admitido por servidor, por defecto 1. TCP acepta 0–247; RTU usa 1–247. Unit ID del vínculo debe coincidir con fuente. No es IP. | 1 |  |
| Rangos de Holding Registers | Use editor estructurado de inicio/fin. Rangos inclusivos desde cero, sin solaparse: v1:0-9 representa diez registros. Todos los registros de un valor deben caber en un mismo rango; no crea TAGs automáticamente. | v1:0-9 | Hasta 32 rangos y 16384 registros totales; direcciones 0–65535. |
| Clientes TCP simultáneos | Solo TCP: máximo de conexiones, por defecto 16. No es cantidad de Views de licencia ni autorización de usuario. | 16 | 1–128. |
| Tiempo de cliente TCP inactivo | Solo TCP: espera máxima de siguiente solicitud, por defecto 30000 ms. No es frecuencia de actualización de TAG. | 30000 | 1000–600000 ms. |
| Puerto RTU en servidor | Solo RTU: dispositivo accesible al servicio en Windows/Linux/contenedor, como COM4 o /dev/ttyUSB0. Es exclusivo mientras servidor esté activo; no lo comparta con un maestro que controle el mismo puerto físico. | /dev/ttyUSB0 |  |
| Velocidad de línea RTU | Bits por segundo iguales al maestro y demás dispositivos. Por defecto 9600; no es polling del servidor. | 9600 | 300–4000000. |
| Bits de datos RTU | Por defecto 8; use la línea real. El rango del campo no garantiza que adaptador/host admita toda combinación. | 8 | 5–8; compruebe instalación. |
| Paridad RTU | None, Odd, Even, Mark o Space; por defecto None. Debe coincidir con maestro y soporte del puerto. | None |  |
| Bits de parada RTU | One, OnePointFive o Two; por defecto One. Use combinación válida para línea y host, no cantidad de registros. | One |  |
| Tiempo para completar trama RTU | Por defecto 1000 ms para recibir resto de trama tras comenzar tráfico. No es intervalo de medición. | 1000 | 50–10000 ms. |
| Dirección de TAG en el mapa | Use holding:N desde cero. Herramientas con etiquetas de referencia suelen llamar holding:0 40001; compruebe convención del cliente. Coils/Input Registers no forman parte de este perfil. | holding:0 |  |
| Unit ID del vínculo | Opcional: si falta usa fuente. En Server, sustituirlo exige coincidir con Unit ID servido; no crea varias estaciones en un mapa. | 1 |  |
| Tipo en registros | Boolean/Int16/UInt16 usan un registro; Int32/UInt32/Float32 usan dos; Int64/UInt64/Float64 usan cuatro. Tipo físico, tipo de TAG y anchura deben ser compatibles. Ejemplo usa Int16 físico y TAG Int16 sin conversión. | Int16 |  |
| Orden de palabras | HighWordFirst es predeterminado; LowWordFirst invierte palabras de valores multirregistro. Debe coincidir con cliente. Un Int16 de una palabra no tiene dos palabras para invertir. | HighWordFirst |  |
| Multiplicador del valor | Por defecto 1: valor de Ingeniería = bruto × scale + offset. Debe ser finito y distinto de cero. Publicar en registros usa conversión inversa; no aplique escala duplicada en cliente. | 1 |  |
| Desplazamiento del valor | Cantidad sumada tras multiplicar, por defecto 0. No es desplazamiento de dirección ni cambia holding:0. | 0 |  |
| Escritura de cliente externo | ReadOnly es predeterminado; ReadWrite habilita escritura externa en este TAG mapeado. Es separado de ReadOnly para escrituras internas SCADIVO. Modbus no solicita login SCADIVO en cada petición; planifique acceso de red y mapa autorizado antes de habilitar ReadWrite. | ReadOnly |  |

### Publicar y comprobar lectura

1. Cree fuente Server TCP o RTU y configure interfaz/línea, Unit ID y rangos.
2. Probar conexión TCP reserva temporalmente interfaz/puerto local y lo libera; RTU prueba apertura/reserva exclusiva y libera línea. Ninguno demuestra lectura por cliente remoto ni deja servidor activo.
3. En TAGs seleccione fuente Server y asistente Modbus. Elija Holding Register, dirección desde cero y tipo; empiece con acceso externo ReadOnly. Permita escritura interna si recibe datos Gateway o escrituras internas autorizadas.
4. Publique/active. Alimente valor conocido y revise actualización en Monitor. En cliente externo lea Unit ID/dirección correspondientes; compruebe tipo y orden.

```json
{
  "source": {
    "bindAddress": "127.0.0.1",
    "port": "1502",
    "unitId": "1",
    "holdingRanges": "v1:0-9"
  },
  "address": "holding:0",
  "metadata": {
    "modbus.valueType": "Int16",
    "modbus.wordOrder": "HighWordFirst",
    "modbus.scale": "1",
    "modbus.offset": "0",
    "modbus.server.clientAccess": "ReadOnly"
  },
  "tagDataType": "Int16",
  "readOnly": false,
  "internalValue": 123
}
```

### Resultado esperado y reinicio

Después de escritura interna autorizada de 123, cliente debe leer 123 en holding:0, Unit ID 1, puerto 1502. El valor no aparece automáticamente por constar en ejemplo. El mapa en memoria empieza en cero: leer cero no demuestra muestra actual del TAG y registros no transportan automáticamente calidad/tiempo. Tras reinicio compruebe alimentación; no es Historian ni memoria retentiva garantizada.

### Escritura externa y diagnóstico

Un rango permitido no autoriza escritura libre: cada registro escrito debe pertenecer a TAG mapeado con ReadWrite externo. Servidor rechaza valores multirregistro parciales, huecos no mapeados, TAGs solapados y tramos fuera de rango. FC03 admite 1–125 registros; FC06 escribe un registro y FC16 escribe 1–123 registros; la operación debe respetar cada valor completo. Sin escucha: puerto ocupado, interfaz, firewall y Docker. Lectura incorrecta: Unit ID, base cero, tipo/orden y valor alimentado. Escritura interna rechazada: ReadOnly/permiso; externa rechazada: clientAccess y mapa completo.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.modbus-server-setup)
