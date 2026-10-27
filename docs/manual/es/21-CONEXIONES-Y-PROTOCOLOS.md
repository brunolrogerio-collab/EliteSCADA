# Conexiones, Modbus, MQTT y OPC UA

## Fuente de datos: de la conexión al TAG comprobado

Configure una conexión antes de enlazar valores con pantallas, alarmas e histórico.

### Antes de completar

Empiece por una lectura conocida, como una presión comprobable en el equipo. En Ingeniería > Fuentes de datos, la fuente guarda cómo llegar al equipo. El driver habla su protocolo; el vínculo identifica el punto a leer; el TAG es la variable que recibe el valor.

Tenga el mapa del fabricante y los parámetros de red o puerto serie. Empiece con un TAG de solo lectura. La comunicación parte del servidor SCADIVO: acceder desde el navegador no demuestra que ese servidor alcance el equipo. Los campos siguientes sirven de consulta al configurar.

### Preparar, comprobar y activar

1. Cree o seleccione la fuente y elija el driver del protocolo del equipo.
2. Complete campos básicos. Abra avanzados cuando necesite consultar o cambiar valores.
3. Use Probar conexión si está disponible. La prueba usa el borrador; no aplica la edición ni cambia Runtime.
4. Revise cambios y diagnósticos. Aplicar al Workspace lleva la configuración al borrador de trabajo.
5. Cree un TAG con esa fuente, tipo de valor y vínculo del punto. Pruebe lectura cuando sea posible.
6. Guarde, publique y active con los controles del proyecto para usarlo en Runtime.
7. Compare valor y unidad con la referencia conocida. Compruebe calidad y actualización antes de añadir pantalla, alarma o comando.

### Consultar campos de la fuente

Use esta tabla al preparar la conexión. El procedimiento anterior da la secuencia; nombres y ejemplos ayudan si surge duda en un campo.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Clave de la fuente | Identifica la conexión en las referencias de TAGs. Use una clave legible distinta de otras fuentes; no es la dirección IP. | plc.bomba01 |  |
| Nombre | Nombre mostrado en la lista. Describa el equipo o servicio al que sirve esta conexión. | Bomba 01 |  |
| Tipo de conexión / driver | Selecciona el protocolo y sus campos. Modbus TCP, Modbus RTU Master, MQTT Raw y OPC UA son conexiones distintas; cambiar el tipo exige revisar los vínculos de TAGs. | modbus.tcp |  |
| Habilitada | Incluye la fuente en la configuración operativa al activar su revisión. Habilitar no corrige la red, las credenciales ni un TAG incorrecto. | true |  |
| Referencias protegidas | Los campos de contraseña/certificado reciben la referencia de material aprovisionado en el servidor. Solicite la referencia correcta al administrador; no pegue contraseñas, claves privadas ni certificados en los campos comunes. |  |  |

### Conecta pero no hay valor

Conectar y leer un punto son comprobaciones distintas. Probar conexión no verifica todo el mapa ni una acción física. Sin valor, compruebe fuente habilitada, vínculo del TAG, revisión activa y diagnósticos.

Lea el motivo: acceso denegado no es tiempo excedido; lectura inválida no es cero. Aunque el protocolo permita escritura, siguen vigentes ReadOnly y permisos. Descubrimiento y pruebas dependen del driver. MQTT Raw, por ejemplo, no ofrece prueba de conexión de Ingeniería.

[Abrir esta guía en Ayuda instalada](/help?topic=sources.connection-workflow)

## Modbus: TCP, serie y mapa de registros

Prepare la conexión y use el mapa del equipo para comprobar una primera lectura.

### Elija cliente o maestro

Elija primero quién consulta a quién. Use Modbus TCP para que SCADIVO lea un controlador por red. Use Modbus RTU Master para consultar equipos por el cable serie del servidor; responden como esclavos.

Tenga el mapa de registros del fabricante, la lista con dirección y representación de cada valor. Empiece con una lectura conocida, sin escritura. Modbus Server tiene otro papel: SCADIVO responde a un cliente externo. Use su guía si ese es el objetivo.

### Conexión TCP y tiempos comunes

Host y puerto son de TCP. Unit ID, sondeo, timeout y hueco también se aplican a RTU, con su límite de Unit ID.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host del controlador | Nombre DNS o IP accesible desde el servidor SCADIVO que ejecuta el driver. localhost es ese servidor o contenedor, no el equipo del navegador. | 192.0.2.10 |  |
| Puerto TCP | Puerto donde el controlador atiende Modbus TCP. No es el puerto web ni el de la API SCADIVO. | 502 | 1–65535; predeterminado 502. |
| Unit ID de la fuente | Dirección lógica del dispositivo; en un gateway TCP/serie puede seleccionar un esclavo. Se aplica a TAGs sin sustitución individual. Use el valor del manual del equipo, no el último número de la IP. | 1 | TCP: 0–255. RTU: 0–247. Predeterminado 1; el destino debe responder a lecturas. |
| Intervalo de sondeo (ms) | Tiempo solicitado entre ciclos de lectura. 1000 ms equivale a 1 segundo; reducirlo aumenta tráfico y carga. Una respuesta lenta no se acelera al reducir este campo. | 1000 | 10–600000; predeterminado 1000. |
| Tiempo máximo de respuesta (ms) | Cuánto esperar por una petición. Es distinto del sondeo: un timeout corto puede fallar con una red/bus lento; uno largo retrasa la detección de ausencia. | 3000 | 50–60000; predeterminado 3000. |
| Hueco máximo entre direcciones | Avanzado: reúne lecturas cercanas en un bloque, incluido el hueco. Algunos controladores rechazan bloques que atraviesan direcciones no implementadas; reduzca el hueco en ese caso. No cambia las direcciones de TAGs. | 8 | 0–125; predeterminado 8. |

### Serie en Windows, Linux y Docker

Use los parámetros reales del equipo. El host administra el bus; los ajustes compartidos deben ser compatibles. Si no abre el puerto, compruebe dispositivo, permiso del proceso/contenedor y otro programa ocupándolo antes de cambiar el mapa de TAGs.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Puerto serie del servidor | Dispositivo visible al servidor/DriverHost: por ejemplo COM3 en Windows o /dev/ttyUSB0 en Linux. En Docker el host debe exponer el dispositivo al contenedor y permitir acceso. No se usa el puerto del navegador; puede preparar la configuración sin conexión. |  |  |
| Velocidad (baudios) | Debe coincidir con los dispositivos del mismo bus serie. Una velocidad mayor requiere soporte del equipo y de la instalación. | 9600 | 300–4000000; predeterminado 9600. |
| Bits de datos | Parte del formato serie. Copie el ajuste del equipo; no es el tamaño del tipo numérico del TAG. | 8 | 5–8; predeterminado 8. |
| Paridad | Verificación del formato serie; los dispositivos que comparten línea necesitan configuración compatible. | None | None / Odd / Even / Mark / Space; predeterminado None. |
| Bits de parada | Completa el formato serie junto con baudios, bits de datos y paridad. Debe coincidir con el equipo. | One | One / OnePointFive / Two; predeterminado One. |

### Vínculo del TAG y conversión

La dirección elige dónde leer; el tipo del equipo elige cómo interpretar. Son decisiones distintas. discrete/input son de lectura; habilitar escritura en el TAG no convierte el área en escribible. El tipo del TAG debe contener el resultado de la conversión.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Área y dirección del TAG | Use coil para salida booleana, discrete para entrada booleana, holding para registro de lectura/escritura e input para registro de entrada. La dirección canónica empieza en cero: holding:0. Si el fabricante llama 40001 al primer holding, confirme la convención antes de convertir; no escriba 40001 automáticamente como offset. | holding:0 | Offset 0–65535; el valor completo debe caber en el área. |
| Unit ID individual | Sustituye el Unit ID de la fuente solo para este TAG. Déjelo vacío para heredar; en RTU compruebe también los límites del esclavo/bus. | 1 |  |
| Tipo en el equipo | Define cómo interpretar los bits del equipo. Si se omite se infiere del tipo del TAG. Int16/UInt16 usan 1 registro, Int32/UInt32/Float32 usan 2, Int64/UInt64/Float64 usan 4; confirme el mapa del fabricante y la compatibilidad del tipo del TAG. | Int16 |  |
| Orden de palabras | Solo afecta valores de varios registros: HighWordFirst coloca primero la palabra más significativa y LowWordFirst la menos significativa. No es el orden de bytes dentro de cada palabra; no lo cambie al azar para ocultar una dirección/tipo incorrecto. | HighWordFirst |  |
| Multiplicador | Convierte el valor leído: valor mostrado = bruto × multiplicador + offset. Para décimas de bar, 253 × 0.1 + 0 = 25.3 bar. Evite repetir la conversión en otra capa. | 0.1 | Finito y distinto de cero; predeterminado 1. |
| Desplazamiento | Suma posterior al multiplicador. Úsela solo si el mapa del equipo requiere una corrección de origen. | 0 | Finito; predeterminado 0. |

### Ejemplo y diagnóstico por capas

1. Configure la fuente TCP con host real, puerto 502, Unit ID del controlador y tiempos iniciales 1000/3000 ms.
2. Cree TAG Double, solo lectura, holding:0 y tipo en equipo Int16 para un registro que represente décimas de bar.
3. Use multiplicador 0.1, offset 0; si el bruto conocido es 253, espere 25.3 bar.
4. Pruebe la lectura y compruebe antes de activar la revisión. Timeout exige comprobar conexión/Unit ID; dirección ilegal exige área/base; valores absurdos exigen tipo/orden/conversión. No use una escritura como prueba de lectura.

```json
{"address":"holding:0","wireType":"Int16","raw":253,"scale":0.1,"offset":0,"expected":25.3}
```

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.modbus-setup)

## MQTT: tópicos, JSON y vigencia de valores

Reciba una medición enviada por mensaje y compruebe que su valor esté actualizado antes de usarlo.

### Un broker no es un sensor

En MQTT, el equipo publica mensajes y el broker los distribuye a suscriptores. El tópico nombra el canal; payload es el contenido. MQTT Raw recibe lo publicado en los tópicos configurados.

Antes de crear la fuente, tenga dirección/puerto del broker, protección TLS, autenticación y un ejemplo real. Elija una magnitud para probar, con TAG Double, un número con decimales, de solo lectura si la magnitud es compatible. El driver no busca registros como Modbus ni convierte cualquier JSON automáticamente en TAGs.

### Conexión, sesión y recursos

Los campos de sesión difieren entre MQTT 3.1.1 y 5. Compruebe la versión antes de los ajustes avanzados. Los límites de tamaño/cola acotan la memoria; no constituyen un histórico.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host del broker | Servidor que recibe/publica mensajes, accesible desde el servidor SCADIVO. No es el tópico ni una URL con ruta. | mqtt.example.invalid |  |
| Puerto del broker | Use el puerto configurado realmente en el broker; debe corresponder al uso de TLS. | 8883 | 1–65535; predeterminado 8883. |
| Conexión TLS | Activa transporte protegido. Desactivar no cambia automáticamente el puerto ni corrige un certificado inválido. Respete el modo del broker y su cadena de confianza. | true |  |
| ID del cliente MQTT | Identifica esta conexión ante el broker. Elija un valor único por cliente activo; reutilizarlo en otra conexión puede desconectar la sesión anterior. No es el login. | scadivo-pump01 |  |
| Versión MQTT | mqtt5 usa Clean Start y expiración de sesión; mqtt311 usa Clean Session. Seleccione la versión admitida por el broker sin mezclar sus opciones. | mqtt5 |  |
| Usuario del broker | Cuenta autorizada por el broker para los tópicos utilizados; no es la cuenta de acceso a SCADIVO. |  |  |
| Referencia de contraseña del broker | Referencia protegida resuelta por el servidor. Sin material aprovisionado o permisos en el broker, la autenticación falla; no escriba una contraseña literal en lugar de la referencia. |  |  |
| Keep Alive (segundos) | Intervalo de supervisión de conexión con el broker; no solicita nuevas mediciones al dispositivo. | 30 | 1–65535; predeterminado 30. |
| Tiempo para conectar (ms) | Plazo de un intento de conexión, distinto de la vigencia de un valor de TAG. | 10000 | 100–300000. |
| Espera inicial de reconexión (ms) | Espera mínima entre intentos tras perder la conexión. Ajústela con el máximo para evitar intentos continuos. | 1000 | 100–300000. |
| Espera máxima de reconexión (ms) | Limita el crecimiento de la espera. Debe ser mayor o igual a la mínima. | 30000 | 100–3600000. |
| MQTT 3.1.1: limpiar sesión | false solicita continuidad de sesión en el broker con el mismo Client ID. true solicita una sesión limpia. No use esta opción en modo MQTT 5. | false |  |
| MQTT 5: inicio limpio | true inicia sin reutilizar la sesión anterior; false solicita continuidad. Es independiente del tiempo de expiración tras desconectar. | false |  |
| MQTT 5: expiración de sesión (s) | Cuánto mantiene el broker la sesión tras desconectar. Con Clean Start false debe ser mayor que cero. No mantiene valores siempre actuales ni sustituye al histórico. | 3600 | 0–4294967295. |
| Máximo por mensaje (bytes) | Límite de memoria por payload recibido; se rechazan mensajes mayores. Dimensione para el documento real, no para el número de TAGs. | 1048576 | 1–67108864. |
| Límite de fallos de conexión | Límite de fallos consecutivos utilizado por la supervisión de disponibilidad. Aumentarlo no corrige credenciales ni un broker ausente. | 5 | 1–1000. |
| Máximo de mensajes en el búfer | Limita la cola de mensajes del transporte. Un búfer mayor consume más memoria; no es archivado ni garantiza guardar todo durante una avería. | 4096 | 1–1000000. |

### Lectura, calidad y escritura

Cada TAG usa tópico exacto y un tipo. Un documento puede alimentar distintos TAGs con Pointers diferentes. QoS, retención y freshness cumplen funciones distintas; un valor puede existir y estar Stale.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Tópico de lectura | Tópico exacto que alimenta este TAG, respetando mayúsculas. No use + ni # como identidad de TAG; cree TAGs propios para tópicos/campos separados. | plant/pump01/state |  |
| Formato del contenido | utf8Scalar lee un valor textual, como 12.5 o true. json lee JSON y puede extraer un campo con JSON Pointer. El resultado debe ser compatible con el tipo del TAG. | json |  |
| Campo del valor en JSON | Ruta JSON Pointer, no Python ni JSONPath: /current lee current en la raíz; /motor/current lee dentro de motor; /values/0 lee el primer elemento. ~1 representa / dentro del nombre; ~0 representa ~. Vacío usa la raíz, que debe ser un escalar compatible. | /current |  |
| Campo del instante de medición | Campo JSON que indica cuándo midió la fuente el valor. El ejemplo utiliza UTC con Z; el instante de medición difiere de la llegada al broker. | /timestamp |  |
| Exigir instante de origen | Con true, configure también el Pointer del timestamp y publique un instante válido; sin él se rechaza la muestra. | true |  |
| Vigencia sin nueva muestra (ms) | Opcional: tras este intervalo sin muestra más reciente el valor pasa a Stale. No solicita publicación al dispositivo. Deje margen sobre el período normal de envío. | 5000 | 1–2147483647 si se indica. |
| Valor retenido por el broker | staleWithoutSourceTimestamp marca como Stale un valor retenido sin timestamp de origen. acceptAsCurrent lo acepta como actual; úselo solo si la aplicación puede asumirlo. Un mensaje retenido no prueba conexión actual del equipo. | staleWithoutSourceTimestamp |  |
| QoS de lectura | 0: como máximo una entrega; 1: al menos una, puede repetirse; 2: entrega MQTT exactamente una vez en la sesión. QoS no confirma actuación física ni validez del sensor. | 1 |  |
| Tópico de escritura | Necesario para un punto escribible y debe ser exacto. Use el tópico de comando del equipo, separado de la confirmación si el contrato lo exige. No publique accidentalmente en el tópico de medición. |  |  |
| QoS de escritura | Calidad de entrega solicitada al publicar, con los mismos valores 0/1/2. Confirmación del broker no es confirmación del proceso. | 1 |  |
| Retener publicación | false de forma predeterminada. true pide al broker guardar el último payload publicado; evalúe el riesgo de que un nuevo suscriptor reciba un comando antiguo. Solo para puntos escribibles. | false |  |

### Ejemplo de payload: corriente del motor

Publique este ejemplo en plant/pump01/state del broker de prueba. En el TAG Double use formato json, Pointer /current, timestamp /timestamp y timestamp obligatorio. El valor decodificado esperado es 12.5. El instante es ilustrativo: en una prueba en vivo use timestamp actual y compruebe calidad/edad, no solo el número. No pegue este JSON como configuración de la fuente.

```json
{"current":12.5,"timestamp":"2026-10-25T12:00:00Z"}
```

### Límites de publicación y diagnóstico

Extraer /current para lectura no crea una plantilla de documento de comando. Se rechaza escribir JSON con Pointer no vacío: el producto no reconstruye el envelope. Para escritura use contrato escalar o JSON en la raíz compatible con el equipo, TAG escribible, tópico de publicación y autorización. Si conecta sin valor, compruebe tópico exacto, publicación, Pointer, tipo y timestamp. Para Stale compruebe retención, instante de origen y freshness; no oculte ausencia de medición aceptando todo como actual.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.mqtt-setup)

## OPC UA: endpoint, certificados e importación de nodos

Localice el servidor, compruebe la confianza de la conexión y elija una variable conocida para leer.

### Descubrimiento no es activación

Empiece localizando el servidor OPC UA y una variable conocida. En la fuente, descubrimiento muestra endpoints, direcciones de conexión y modos ofrecidos. Elegir un candidato completa parámetros.

Compruebe después autenticación y confianza de certificados. Elegir no crea credenciales protegidas ni aprueba automáticamente el servidor. Pruebas y navegación de Ingeniería son temporales: preparan la fuente sin cambiar la revisión activa.

### Conexión e identidades

Canal seguro y autenticación del usuario son ajustes distintos. El servidor debe aceptar al cliente y SCADIVO reconocer al servidor aprobado. Solicite al administrador el aprovisionamiento y las referencias; el archivo de Ingeniería no debe convertirse en almacén de secretos.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Endpoint del servidor OPC UA | URL opc.tcp completa, accesible desde el servidor SCADIVO. Descubra los endpoints y elija uno ofrecido realmente por el servidor. | opc.tcp://192.0.2.20:4840 |  |
| Modo de seguridad | None no protege el canal; Sign firma; SignAndEncrypt firma y cifra. El modo debe existir en el endpoint y corresponder a la política; no lo reduzca para ocultar un fallo de certificado. | SignAndEncrypt |  |
| URI de política de seguridad | Use la URI completa ofrecida por el endpoint, no solo el nombre corto. None exige una política terminada en #None; un modo protegido no admite esa política. | http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256 |  |
| ApplicationUri aprobada | Identidad de la aplicación servidora cuando se fija. No es la dirección de conexión. Compárela con la identidad aprobada por el administrador. |  |  |
| SHA-256 del certificado aprobado | Runtime protegido exige aprobación explícita del certificado del servidor. Verifique la huella por un canal conocido antes de registrarla; un cambio de certificado exige nueva comprobación. No es una clave privada. |  |  |
| Autenticación en el servidor OPC UA | Anonymous, UserName o Certificate según permita el servidor. Anonymous no elimina la protección del canal; es distinto del login de SCADIVO. | Anonymous |  |
| Usuario OPC UA | Complete en modo UserName con una cuenta autorizada en el servidor de origen. |  |  |
| Referencia de contraseña OPC UA | En modo UserName, referencia protegida a la contraseña aprovisionada en el host; no es la contraseña en texto. |  |  |
| Referencia del certificado cliente | Identidad de la aplicación cliente para un canal protegido. Debe estar aprovisionada en el host y ser aceptada por el servidor. No la confunda con el certificado de usuario. |  |  |
| Referencia del certificado de usuario | En modo Certificate identifica al usuario por separado del certificado de la aplicación cliente. El administrador proporciona la referencia protegida. |  |  |
| Timeout de sesión | Duración solicitada de sesión; no es intervalo de muestreo. Formato horas:minutos:segundos. | 00:01:00 | 00:00:05–01:00:00; predeterminado 00:01:00. |
| Intervalo de publicación | Ritmo solicitado de entrega de notificaciones de la suscripción. Distinto del muestreo de cada nodo; el servidor puede negociar valores admitidos. | 00:00:01 | 00:00:00.050–00:05:00. |
| Confianza temporal en Ingeniería | Solo para una sesión temporal de Ingeniería. No aprueba el certificado para Runtime: se rechaza activar con auto-trust. Tras inspeccionar registre la identidad aprobada y deje esta opción desactivada en la configuración operativa. | false |  |

### Explorar y vincular TAGs

En el explorador OPC UA seleccione las variables, compruebe tipo/acceso y revise la vista previa de importación. Aplique el candidato solo tras comprobar conflictos. El vínculo guarda la identidad del nodo; renombrar el TAG no debe significar apuntar a otro nodo.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| NodeId | Identificador del nodo en el servidor; por ejemplo ns=2;s=Pump.Current. No es el nombre mostrado ni la ruta del TAG SCADIVO. Prefiera seleccionarlo en el explorador OPC UA para evitar errores. | ns=2;s=Pump.Current |  |
| URI del namespace | Identidad estable del namespace. Si está disponible permite reconocer el mismo nodo aunque cambie el índice ns=. Use la URI del servidor; no la invente a partir del nombre del equipo. | urn:example:plant |  |
| Intervalo de muestreo | Ritmo solicitado para observar este nodo. Formato horas:minutos:segundos; elija una frecuencia adecuada al proceso y compruebe el comportamiento negociado por el servidor. | 00:00:01 |  |
| Tamaño de cola del nodo | Número de muestras que la cola del elemento puede retener entre entregas. Un tamaño mayor consume más recursos; no sustituye al Historian. | 1 | 1–10000. |
| Descartar la más antigua | Al llenarse la cola, true prioriza muestras nuevas; false solicita preservar las antiguas en lugar de sustituir la más antigua. Compruebe la política de cola del servidor. | true |  |

### Ejemplo de identidad, no archivo de importación

Este ejemplo muestra dos campos del vínculo: NodeId ns=2;s=Pump.Current y namespaceUri urn:example:plant. Sustitúyalos por valores de su servidor o use el explorador. Si el índice cambia de 2 a 4 manteniendo URI e identificador s=Pump.Current, la identidad estable sigue igual. No use el nombre del ejemplo para adivinar un nodo existente.

```json
{"nodeId":"ns=2;s=Pump.Current","namespaceUri":"urn:example:plant"}
```

### Comprobar y resolver fallos

1. Descubra y seleccione endpoint/modo/política compatibles.
2. Verifique identidad/certificado del servidor y aprovisione material del cliente para su autenticación.
3. Pruebe conexión y explore una variable conocida.
4. Revise/aplique la importación de al menos un TAG y compruebe la lectura.
5. Quite confianza temporal y confirme la configuración aprobada para Runtime.
6. Guarde/publique/active mediante el flujo del proyecto y compruebe calidad/timestamps. Certificado rechazado exige confianza/identidad; nodo ausente exige NodeId/namespace; acceso denegado exige cuenta/permiso; tipo incompatible exige revisar el TAG. Escritura aceptada no prueba actuación: compruebe feedback del equipo.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.opcua-setup)
