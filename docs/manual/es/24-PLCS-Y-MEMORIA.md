# PLCs: conexiones, símbolos y memoria

## Siemens S7: CPU, memoria y primera lectura

Configure ISO-on-TCP distinguiendo conexión, acceso al DB y permiso de escritura.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-S7: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.siemens-setup; fields=communicationBinding,valueOrder; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Prepare el lado del PLC

Elija una variable conocida del programa del PLC y obtenga su dirección. Este driver S7 ISO-on-TCP lee direcciones absolutas: necesita saber dónde está el valor, no solo su nombre. No es navegación simbólica completa de TIA ni conexión OPC UA.

En S7-1200/1500 confirme acceso externo permitido, PUT/GET cuando se exige y DB de disposición absoluta compatible. DB es bloque de datos; offsets son posiciones dentro. Optimizar cambiando esas posiciones puede invalidar el mapa. SCADIVO no cambia esas opciones ni sustituye protección del PLC.

### Campos de la fuente

Elija RackSlot o TSAP explícito según la instalación. La prueba de conexión negocia una sesión; aún debe comprobar acceso al DB y a cada punto.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección del PLC | IP o nombre accesible desde el servidor SCADIVO. No es la dirección del navegador ni del proyecto. | 192.0.2.10 |  |
| Puerto TCP | Puerto del servicio habilitado en PLC o adaptador. Este driver usa 102 por defecto. Compruebe firewall y servicio real antes de cambiarlo. | 102 | 1–65535. |
| Familia de CPU | Seleccione S7300, S7400, S71200 o S71500 según la CPU instalada. Unknown no define un perfil operativo. Esto no habilita automáticamente servicios o permisos del PLC. | S71200 |  |
| Cómo localizar la conexión | RackSlot obtiene el destino desde rack, slot y función. ExplicitTsap usa el TSAP de destino indicado en la instalación. Elija un modo y complete sus campos. | RackSlot |  |
| Rack | En RackSlot, número del rack que contiene la CPU. No es el número de equipo del proyecto. | 0 | 0–7; obligatorio en RackSlot. |
| Slot de CPU | En RackSlot, posición real de CPU. El ejemplo 1 no sirve para todas las familias; compruebe el hardware configurado. | 1 | 0–31. |
| Función de conexión | En RackSlot: OperatorPanel, ProgrammingDevice o Basic, según la conexión permitida en el PLC. No es un rol de usuario de Authority. | OperatorPanel |  |
| Habilitar escritura de la fuente | Por defecto false. Escribir también requiere vínculo writable, TAG no ReadOnly, destino escribible y permiso. No habilita PUT/GET ni modifica protección de CPU. | false |  |
| TSAP de origen | Identificador del cliente en la sesión ISO. Por defecto 0x0100. No es puerto TCP; use el valor de configuración de comunicación. | 0x0100 |  |
| TSAP de destino | Obligatorio en ExplicitTsap. Use el identificador configurado para la conexión del PLC; compruébelo antes de copiar el ejemplo. | 0x0201 |  |
| Tiempo máximo de conexión | Milisegundos permitidos para establecer la conexión. | 5000 | 1–300000 ms. |
| Tiempo máximo de respuesta | Milisegundos permitidos para responder a una solicitud. Aumentarlo no corrige dirección, ruta o tipo incorrectos. | 3000 | 1–300000 ms. |
| Espera para reconectar | Milisegundos antes de reintentar tras fallo. No es intervalo de medición; cero no corrige un acceso rechazado. | 1000 | 0–300000 ms. |
| Tamaño de mensaje solicitado | Bytes de PDU solicitados al negociar S7. Por defecto 480; el tamaño aceptado depende del PLC. No es cantidad de TAGs ni tamaño de base de datos. | 480 | 240–960 bytes. |

### Campos del vínculo de TAG

La ruta del TAG en SCADIVO identifica la variable de aplicación; área, DB, byte y bit localizan la memoria del PLC. Dirección portátil y campos del vínculo deben coincidir.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Área de memoria | Input: entradas; Output: salidas; Merker: memoria M; DataBlock: DB. Use el área real del programa. Leer entradas no vuelve esa área escribible. | DataBlock |  |
| Número de DB | En DataBlock, número de bloque distinto de cero. Fuera de esa área use cero. No es desplazamiento en bytes. | 1 | 1–65535 en DB; cero en otras áreas. |
| Posición inicial en bytes | Posición desde cero dentro del área/DB. DB1.DBD0 empieza en byte 0; Float32 ocupa cuatro bytes. No introduzca 32 por confundir bits y bytes. | 0 | 0–2097151; todo el valor debe caber. |
| Bit de Boolean | Solo Boolean usa bit 0–7 dentro del byte. Mantenga cero en otros tipos; no lo sustituya por el selector genérico de bits. | 0 |  |
| Tipo físico en PLC | Elija el almacenamiento real: Boolean, Byte, SInt, UInt16, Int16, UInt32, Int32, Int64, Float32, Float64, String, WString, Date o DateTime. Float32 corresponde a TAG Float, Float64 a Double, texto a String y fechas a DateTime. El nombre del TAG no convierte memoria. | Float32 |  |
| Capacidad de texto | Solo String/WString: capacidad declarada de 1–254 caracteres; no es tamaño de lectura numérica. Use cero en otros tipos. Reserve espacio real en DB para la cabecera del formato. | 0 |  |
| Permitir escritura en este punto | Empiece con false para comprobar la lectura. true solo habilita un punto que también sea escribible en memoria/protocolo; ReadOnly del TAG, permisos de sesión y demás restricciones siguen vigentes. | false |  |
| Orden de bytes y palabras | El descriptor todavía declara Normal, ByteSwap, WordSwap y ByteAndWordSwap, pero el vínculo actual no admite este ajuste ni order en la dirección. El orden se representa mediante la transformación física compartida de bytes/palabras. Esta divergencia forma parte de la corrección pendiente; no copie el campo del catálogo al vínculo como solución manual. | Normal |  |
| Autoría del vínculo en esta versión | El catálogo actual da al asistente genérico una identidad de vínculo distinta de la exigida por Runtime S7. Crear por esta vía se rechaza y espera corrección. Los demás campos y el ejemplo describen el vínculo S7 correcto, no un flujo de autoría ya corregido. No cambie IDs internos para eludir validación. El campo valueOrder también debe proyectarse a la transformación física en vez de guardarse como ajuste extra. |  |  |

### De la fuente a una lectura comprobada

La creación manual mediante el asistente actual encuentra la limitación descrita arriba. El ejemplo demuestra parámetros de vínculo válido para revisión; no indica forzar identidad interna en la pantalla. En un proyecto con vínculo S7 ya válido, compruebe fuente, lectura conocida y calidad después de publicar/activar por el flujo del proyecto. Una conexión correcta no demuestra lectura ni escritura probadas.

### Ejemplo: REAL al inicio del DB1

Ejemplo de configuración, no paquete completo. El programa del PLC debe guardar realmente un REAL en DB1.DBD0. Se espera una lectura Float de ese valor con escritura deshabilitada, no un valor de prueba generado por el driver.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "102",
    "cpuFamily": "S71200",
    "connectionMode": "RackSlot",
    "rack": "0",
    "slot": "1",
    "connectionRole": "OperatorPanel",
    "writeEnabled": "false"
  },
  "address": "s7iso:v1;area=DataBlock;db=1;byte=0;bit=0;type=Float32;string=0;writable=false",
  "binding": {
    "area": "DataBlock",
    "dbNumber": "1",
    "byteOffset": "0",
    "bitOffset": "0",
    "valueType": "Float32",
    "stringLength": "0",
    "writable": "false"
  },
  "tagDataType": "Float"
}
```

### Importación TIA y diagnóstico

La importación anunciada acepta exportaciones compatibles de PLC TAGs en XLSX, XML y SDF; no importa un proyecto TIA completo ni TIA Openness. Revise candidatos, tipos y direcciones antes de aplicar. Sin conexión: compruebe IP, puerto 102, modo, rack/slot/TSAP y acceso permitido. Conectado sin lectura: revise DB, disposición absoluta, offset y tamaño. Valor incoherente: compare tipo, bytes y orden con el programa. Escritura rechazada: revise writeEnabled, writable, ReadOnly y permiso; no relaje la protección del PLC para probar.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.siemens-setup)

## Allen-Bradley Logix: ruta, símbolos y lectura

Use acceso simbólico ControlLogix/CompactLogix y compruebe límites antes de importar o escribir.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-LOGIX: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.logix-setup; fields=communicationBinding,bitIndex; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Qué conexión está configurando

Empiece por un símbolo conocido de ControlLogix o CompactLogix, la referencia con nombre usada por el programa. Tenga IP, perfil y ruta reales y compruebe acceso externo al símbolo.

Usa mensajes explícitos EtherNet/IP para esos símbolos. No es scanner de entradas/salidas cíclicas para todos los dispositivos EtherNet/IP ni driver genérico SLC, PLC-5 o MicroLogix. Compruebe familia antes de preparar la fuente.

### Campos de la fuente

Una ruta CIP difiere del puerto TCP. Identificar el controlador no demuestra lectura de símbolos ni escritura.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección del PLC | IP o nombre accesible desde el servidor SCADIVO. No es la dirección del navegador ni del proyecto. | 192.0.2.10 |  |
| Puerto TCP | Puerto del servicio habilitado en PLC o adaptador. Este driver usa 44818 por defecto. Compruebe firewall y servicio real antes de cambiarlo. | 44818 | 1–65535. |
| Perfil del controlador | ControlLogix o CompactLogix según el equipo. El perfil no descubre automáticamente el slot ni configura el PLC. | CompactLogix |  |
| Ruta CIP hasta la CPU | Saltos ordenados puerto,enlace separados por /. 1,0 representa un salto concreto, no puerto TCP 1. Use la ruta documentada; un destino directo puede usar ruta vacía según el perfil. No añada saltos por ensayo. | 1,0 |  |
| Intervalo de lectura | Milisegundos solicitados entre ciclos de adquisición. 1000 equivale aproximadamente a un ciclo por segundo, según red y procesamiento. No garantiza la frecuencia de actualización del programa del PLC. | 1000 | 50–3600000 ms. |
| Tiempo máximo de respuesta | Milisegundos permitidos para responder a una solicitud. Aumentarlo no corrige dirección, ruta o tipo incorrectos. | 3000 | 1–60000 ms. |
| Espera mínima de reconexión | Espera inicial tras fallo de conexión, en milisegundos. No debe superar la espera máxima. | 500 | 1–60000 ms. |
| Espera máxima de reconexión | Límite de la espera progresiva, en milisegundos. No es vigencia del dato ni tiempo de confirmación de comando. | 15000 | 1–300000 ms. |
| Puntos por lote | Limita la agrupación de solicitudes. Por defecto 16; aumentarlo puede elevar carga y tamaño de mensajes. No modifica la cantidad de TAGs licenciados. | 16 | 1–64. |
| Requisito de seguridad del transporte | Unsecured es la implementación disponible. CipSecurityRequired es un requisito explícito que esta versión rechaza; no activa CIP Security ni reduce seguridad silenciosamente. Si la instalación exige CIP Security, esta vía no satisface ese requisito. | Unsecured |  |

### Campos del vínculo simbólico

La identidad incluye ámbito, programa cuando corresponde, ruta y tipo nativo. No copie una dirección portátil dejando campos que apunten a otro símbolo. Bool es legible; escritura nativa de Bool no está habilitada en esta versión.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Ámbito del símbolo | Controller para símbolo global; Program para símbolo de un programa, con programName. No es ámbito de autorización de Authority. | Controller |  |
| Programa del PLC | En ámbito Program, introduzca el nombre real del programa. No es nombre de aplicación SCADIVO ni de rutina. |  |  |
| Ruta del símbolo | Nombre y ruta real admitida, con miembro/índice si corresponde. TankLevel debe existir en el PLC como REAL. Renombrar el TAG SCADIVO no renombra el símbolo. | TankLevel |  |
| Tipo nativo del símbolo | Lectura en esta versión: Bool, Sint, Int, Dint, Lint y Real, correspondientes a Boolean, Int16, Int16, Int32, Int64 y Float. Lreal/String pueden aparecer en catálogo/importación pero no están habilitados para lectura Runtime. Una estructura completa no es un escalar. | Real |  |
| Acceso externo definido en PLC | ReadOnly permite leer; ReadWrite también permite escribir tipos compatibles; None bloquea acceso. Unknown no demuestra permiso de escritura. Este campo describe el símbolo; no modifica el PLC. | ReadOnly |  |
| Símbolo constante | true identifica una constante del PLC e impide escritura. No use false para intentar volverla escribible. | false |  |
| Bit físico de un entero | Proyección Boolean opcional desde Sint/Int/Dint/Lint: índices máximos 7/15/31/63. No aplica a Real o Bool. No convierte la escritura en operación segura de bit; compruebe el soporte antes de comandar. Un vínculo admitido usa el selector físico compartido, no un ajuste extra bitIndex; la proyección del asistente también está pendiente. |  |  |
| Autoría del vínculo en esta versión | Runtime guarda identidad simbólica en dirección portátil y exige ajustes extra vacíos. El asistente genérico actual copia los campos de esta tabla a ajustes extra y el vínculo se rechaza. Crear por esta vía espera corrección; el ejemplo muestra el contrato de un punto válido, no un asistente corregido. |  |  |

### De la fuente a una lectura comprobada

La creación manual con el asistente actual encuentra la limitación descrita arriba. El ejemplo muestra una identidad de lectura válida para revisión. En un proyecto con vínculo Logix ya válido, revise fuente, símbolo, tipo y calidad tras publicar/activar. Browse/importación/conciliación anunciados requieren revisar candidatos y vínculos antes de aplicar; abrir un archivo no demuestra importación correcta.

### Ejemplo: símbolo REAL de lectura

TankLevel debe existir en ámbito Controller como REAL. Use TAG Float y ReadOnly para la primera comprobación. El ejemplo no configura una ruta de rack por usted.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "44818",
    "profile": "CompactLogix",
    "securityMode": "Unsecured",
    "scanIntervalMs": "1000"
  },
  "address": "logix:v1;scope=controller;symbol=TankLevel;native=real;access=readonly;constant=false",
  "binding": {},
  "tagDataType": "Float"
}
```

### Browse, importación y conciliación

Browse online enumera símbolos Controller con paginación; no promete navegar programas completos. La importación compatible L5X/L5K puede incluir ámbito de programa. Revise candidatos compatibles, arrays, miembros y External Access; un símbolo visible puede ser incompatible. Reconcile compara identidades; no modifica automáticamente el programa del PLC. Ante fallo de conexión, revise IP/44818 y ruta. Símbolo ausente: ámbito, programa y nombre. Escritura rechazada: tipo compatible, constante, External Access, ReadOnly y permiso.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.logix-setup)

## Mitsubishi MELSEC: perfil, ruta y dispositivos

Configure MC/SLMP y compruebe base numérica, anchura de valor y límites de CPU.

### Preparar el servicio MC

Pida al responsable perfil, puerto y una posición de memoria conocida para probar. El servicio MC/SLMP TCP compatible debe estar habilitado en el PLC.

Usa tramas binarias 3E, el formato de mensaje previsto para la conexión. No presuponga todo modelo Mitsubishi, mensajes ASCII, UDP ni toda memoria del PLC. Compare perfil y equipo antes de completar direcciones.

### Campos de la fuente

Empiece por campos básicos. Ruta y agrupación avanzadas deben corresponder a la instalación; los valores predeterminados no descubren automáticamente la topología.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección del PLC | IP o nombre accesible desde el servidor SCADIVO. No es la dirección del navegador ni del proyecto. | 192.0.2.10 |  |
| Puerto TCP | Puerto del servicio habilitado en PLC o adaptador. Este driver usa 5007 por defecto. Compruebe firewall y servicio real antes de cambiarlo. | 5007 | 1–65535. |
| Perfil de familia Mitsubishi | Fx5U32MtDs o IqR04EnCpu: límites de memoria específicos, no certificación de todas las CPU MELSEC. Si el modelo difiere, compruebe compatibilidad antes de usar. | Fx5U32MtDs |  |
| Intervalo de lectura | Milisegundos solicitados entre ciclos de adquisición. 1000 equivale aproximadamente a un ciclo por segundo, según red y procesamiento. No garantiza la frecuencia de actualización del programa del PLC. | 1000 | 10–600000 ms. |
| Tiempo máximo de conexión | Milisegundos permitidos para establecer la conexión. | 5000 | 100–60000 ms. |
| Tiempo máximo de respuesta | Milisegundos permitidos para responder a una solicitud. Aumentarlo no corrige dirección, ruta o tipo incorrectos. | 6000 | 250–60000 ms. |
| Temporizador de supervisión MC | Tiempo solicitado en la trama MC, en unidades de 250 ms. Por defecto 20 equivale a 5 s. Difiere del timeout local de respuesta, que debe permitir ese tiempo y la red. | 20 | 1–65535 unidades. |
| Número de red MC | Enrutamiento MC de la instalación; por defecto 0 para destino directo configurado. No es número de interfaz Ethernet del ordenador. | 0 | 0–239. |
| Estación destino MC | Campo de estación de la trama. Por defecto 255 (FF). Use el valor de ruta MC, no un Unit ID de Modbus. | 255 | 1–255. |
| Número I/O del módulo | Campo de ruta hexadecimal, por defecto 03FF. No es dirección de salida Y; conserve el valor de la instalación. | 03FF |  |
| Estación multidrop | Campo adicional de ruta MC; por defecto 0. Use la ruta real sin confundirlo con IP o estación de trama. | 0 | 0–31. |
| Palabras por solicitud | Límite de agrupación de palabras, por defecto 64. Un valor de 32 bits ocupa dos palabras; no confunda palabras con cantidad de TAGs. | 64 | 2–960. |
| Bits por solicitud | Límite de lectura de dispositivos de bit, por defecto 64. Ajuste según capacidad del PLC y carga medida. | 64 | 1–7168. |
| Puntos dispersos por solicitud | Limita agrupación de direcciones no contiguas, por defecto 16. No crea direcciones inexistentes ni cambia el mapa. | 16 | 1–192. |
| Bloques por solicitud | Limita bloques agrupados, por defecto 8. Más bloques no garantizan lectura más rápida con red o CPU saturadas. | 8 | 1–120. |
| Límite explícito de dirección R | Opcional. Sin valor, R queda deshabilitada. Introduzca el máximo validado para la memoria instalada; el parser actual limita R a 32767. El máximo amplio del campo no amplía ese límite del perfil. |  |  |

### Memoria y campos del vínculo

Los dispositivos de bit y palabra almacenan de forma distinta. Compruebe el formato de este driver aunque otro software escriba la dirección de otra forma.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección de dispositivo | X/Y/B/W usan números hexadecimales; M/L/D/R usan decimales. D100 es palabra decimal; X10 es hexadecimal 10, no la convención octal de otro software. El perfil limita el rango permitido. | D100 |  |
| Tipo físico | Bit para áreas de bits; Int16, UInt16, Int32, UInt32 o Float32 para palabras. Int32/UInt32/Float32 ocupan dos palabras consecutivas. Elija TAG compatible y conserve orden del PLC; UInt32 usa TAG Int64. En SCADIVO, Int16/UInt16 físicos usan TAG Int32; Int32/UInt32 físicos usan Int64; Float32 usa Float; Bit usa Boolean. | Int16 |  |
| Permitir escritura en este punto | Empiece con false para comprobar la lectura. true solo habilita un punto que también sea escribible en memoria/protocolo; ReadOnly del TAG, permisos de sesión y demás restricciones siguen vigentes. | false |  |

### De la fuente a una lectura comprobada

1. En Ingeniería > Fuentes de datos, cree la fuente correspondiente e introduzca los parámetros de la instalación.
2. Guarde y use Probar conexión cuando esté disponible. Una conexión correcta no demuestra todos los puntos.
3. En TAGs, seleccione esta fuente, elija tipo compatible y complete dirección y vínculo mediante el asistente. En autoría manual, dirección y campos deben representar el mismo punto; los ejemplos no son paquetes completos de importación.
4. Compruebe una lectura conocida mediante la prueba de punto, cuando exista, o después de publicar/activar por el flujo del proyecto. Revise valor, calidad y actualización en Monitor de TAGs.
5. Después configure pantalla, alarma o escritura autorizada. Compare con el programa y la condición real del equipo.

### Ejemplo: entero en D100

D100 debe contener un entero de 16 bits con signo. Un valor 123 debe aparecer como 123 en TAG Int32; compruebe el valor bruto antes de convertir unidades.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "5007",
    "familyProfile": "Fx5U32MtDs",
    "scanIntervalMilliseconds": "1000"
  },
  "address": "D100",
  "binding": {
    "address": "D100",
    "physicalDataType": "Int16",
    "writable": "false"
  },
  "tagDataType": "Int32"
}
```

### Diagnóstico dirigido

Sin conexión: revise servicio TCP, puerto y ruta MC. Dirección inválida: área, decimal/hexadecimal y límite de familia; R necesita límite explícito. Valor desplazado: tipo y palabras ocupadas. Solicitudes lentas: compare unidades MC de 250 ms, timeout local, lotes y carga; no aumente todo a la vez. Una prueba de punto no es Browse completo ni certifica escritura o actuación física.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.melsec-setup)

## Panasonic MEWTOCOL: TCP o serie y mapa de memoria

Elija el transporte real e interprete palabras y contactos sin mezclar otros protocolos.

### Elegir perfil y transporte

Elija según instalación: MEWTOCOL-COM TCP usa servicio o adaptador TCP compatible; Host Serial usa puerto físico disponible en el servidor SCADIVO. Son fuentes distintas con ajustes propios.

Para serie compruebe cable, estándar eléctrico RS-232/RS-485 y parámetros de línea. Tenga el mapa de memoria para una lectura de prueba. Esta configuración no convierte un adaptador Ethernet genérico en servidor de todos los protocolos Panasonic.

### Campos de fuente y vínculo

En TCP complete host/puerto/timeout de conexión; en serie complete puerto y formato de línea. Estación, familia, modo de trama y adquisición aplican a ambos. Los tres últimos campos describen el punto.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Dirección del PLC | IP o nombre accesible desde el servidor SCADIVO. No es la dirección del navegador ni del proyecto. | 192.0.2.10 |  |
| Puerto TCP del adaptador | Obligatoria en TCP, sin puerto predeterminado supuesto. Use el puerto MEWTOCOL-COM del PLC/adaptador; no copie el puerto MC Mitsubishi. | 9094 | 1–65535; ejemplo, no valor predeterminado. |
| Tiempo máximo de conexión | Milisegundos permitidos para establecer la conexión. | 5000 | 100–60000 ms. |
| Puerto serie del servidor | Solo Host Serial: dispositivo accesible al servicio SCADIVO, por ejemplo COM4 en Windows o /dev/ttyUSB0 en Linux. En Docker, dispositivo y permisos pertenecen a contenedor/host, no al navegador. | /dev/ttyUSB0 |  |
| Velocidad serie | Solo serie: bits por segundo iguales al PLC. Por defecto 9600; no es intervalo de lectura. | 9600 | 300–230400. |
| Bits de datos | Solo serie: 7 u 8 según instalación. Por defecto 8. Con paridad y stop bits define el formato serie. | 8 |  |
| Paridad | Solo serie: None, Odd o Even, como en el PLC. Por defecto None. Paridad incorrecta puede provocar timeout sin respuesta comprensible. | None |  |
| Bits de parada | Solo serie: One o Two según PLC. Por defecto One; no use valores de otro driver fuera de esta lista. | One |  |
| Espera de cambio de dirección | Solo serie: pausa en milisegundos para línea/adaptador tras transacción. Por defecto 0; ajuste según necesidad real, no para corregir estación incorrecta. | 0 | 0–1000 ms. |
| Estación MEWTOCOL | Estación configurada en el PLC, obligatoria de 1 a 99. No es slot de rack, IP ni Unit ID de otro protocolo. | 1 |  |
| Perfil de familia Panasonic | Fp0rF32, FpXhCommon o Fp7RClassicCom. Cada uno limita áreas, direcciones y escritura. FP7 aquí es el subconjunto classic COM, no toda memoria/función del FP7. | Fp0rF32 |  |
| Formato de trama | Standard es predeterminado. Expanded aumenta capacidad solo en perfiles que lo admiten; Fp0rF32 lo rechaza. No habilita MEWTOCOL7 ni amplía áreas de memoria. | Standard |  |
| Intervalo de lectura | Milisegundos solicitados entre ciclos de adquisición. 1000 equivale aproximadamente a un ciclo por segundo, según red y procesamiento. No garantiza la frecuencia de actualización del programa del PLC. | 1000 | 10–600000 ms. |
| Tiempo máximo de respuesta | Milisegundos permitidos para responder a una solicitud. Aumentarlo no corrige dirección, ruta o tipo incorrectos. | 3000 | 250–60000 ms. |
| Dirección Panasonic | Use área y número admitidos por el perfil: contactos X/Y/R/L/T/C o palabras WX/WY/WR/WL/DT/LD. DT100 es palabra decimal; en contactos X/Y/R/L el último dígito es bit hexadecimal 0–F de un grupo decimal. No reinterprete toda la dirección como hexadecimal. | DT100 |  |
| Tipo físico | Boolean para contacto; UInt16 o Int16 para palabra, con TAG del tipo correspondiente. Este driver no ofrece REAL de 32 bits o string como si fueran una palabra de 16 bits. En este driver tanto Int16 como UInt16 físicos usan TAG Int32. Boolean usa TAG Boolean. | Int16 |  |
| Permitir escritura en este punto | Empiece con false para comprobar la lectura. true solo habilita un punto que también sea escribible en memoria/protocolo; ReadOnly del TAG, permisos de sesión y demás restricciones siguen vigentes. | false |  |

### De la fuente a una lectura comprobada

1. En Ingeniería > Fuentes de datos, cree la fuente correspondiente e introduzca los parámetros de la instalación.
2. Guarde y use Probar conexión cuando esté disponible. Una conexión correcta no demuestra todos los puntos.
3. En TAGs, seleccione esta fuente, elija tipo compatible y complete dirección y vínculo mediante el asistente. En autoría manual, dirección y campos deben representar el mismo punto; los ejemplos no son paquetes completos de importación.
4. Compruebe una lectura conocida mediante la prueba de punto, cuando exista, o después de publicar/activar por el flujo del proyecto. Revise valor, calidad y actualización en Monitor de TAGs.
5. Después configure pantalla, alarma o escritura autorizada. Compare con el programa y la condición real del equipo.

### Ejemplo: palabra DT100 por TCP

El puerto 9094 es ejemplo de adaptador configurado, no un valor universal Panasonic. DT100 debe contener un entero de 16 bits con signo; TAG Int32 debe mostrar el mismo número. En Host Serial sustituya campos de red por configuración serie de la instalación.

```json
{
  "source": {
    "host": "192.0.2.10",
    "port": "9094",
    "station": "1",
    "familyProfile": "Fp0rF32",
    "frameMode": "Standard",
    "scanIntervalMilliseconds": "1000"
  },
  "address": "DT100",
  "binding": {
    "address": "DT100",
    "physicalDataType": "Int16",
    "writable": "false"
  },
  "tagDataType": "Int32"
}
```

### Diagnosticar sin cambiar el mapa por ensayo

Sin respuesta TCP: compruebe puerto de servicio y estación. Sin respuesta serie: acceso al dispositivo en servidor, cableado, baud/paridad/stop bits y estación. Error de dirección: área y límite del perfil, incluido dígito de contacto. Valor incorrecto: Int16 frente a UInt16 y palabra frente a contacto. Una escritura rechazada en X/T/C u otra área protegida no se resuelve cambiando solo ReadOnly. Conexión y prueba de punto no demuestran toda la memoria ni actuación física.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.panasonic-setup)
