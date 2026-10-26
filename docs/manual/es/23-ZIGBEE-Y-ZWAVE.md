# Zigbee y Z-Wave: conexión, identidad y operación

## Zigbee y Z-Wave: elegir la ruta de conexión

Compruebe quién mantiene la red y qué tipo de fuente representa la instalación.

### Antes de completar campos

Esta es una lista de comprobación, no una pantalla nueva. La elección de fuente depende del coordinador/controlador y de la instalación externa real. SCADIVO y otro proceso no deben abrir simultáneamente la misma radio/puerto.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Ruta de conexión | Zigbee nativo usa el coordinador TI admitido en el host SCADIVO. Zigbee2MQTT Bridge usa una instalación externa Zigbee2MQTT y su broker. Z-Wave JS Controller usa un controlador serie Z-Wave preparado en el host. No son opciones intercambiables para un dispositivo. |  |  |
| Red y dispositivos preparados | Prepare la red y dispositivos ya asociados mediante el procedimiento del coordinador/instalación. El descubrimiento de Ingeniería consulta lo existente; no abre inclusión, reinicia dispositivos ni crea una red. |  |  |
| Valor recibido y confirmación | Un dispositivo encontrado puede no tener todas sus funciones mapeadas. Estado antiguo, confirmación de transporte y feedback reciente son distintos. Compruebe calidad y actualización antes de usar el TAG en comando, alarma o pantalla. |  |  |

### Red física, TAG e interfaz tienen etapas distintas

1. Identifique si la red usa coordinador local admitido, Zigbee2MQTT externo o controlador Z-Wave local.
2. Confirme red/dispositivos preparados, acceso del servidor y material protegido sin crear otra red por error.
3. Configure la fuente correspondiente y compruebe sus límites de autoría/selección.
4. Con vínculo válido compruebe TAG, tipo, calidad y feedback; guarde/publique/active mediante el proyecto.
5. Añada pantalla, alarma y comando autorizado solo tras comprobar una lectura conocida.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.radio-connections)

## Zigbee nativo: coordinador y estado On/Off

Prepare la fuente para el coordinador TI admitido y comprenda el alcance limitado de esta versión.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-ZIGBEE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.zigbee-native-setup; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Instalación necesaria

Este driver usa coordinador serie TI zStack/CC2652 y proceso auxiliar administrado de la distribución para Windows o Linux x64. En contenedor el host debe exponer recurso físico y estado persistente. Red/dispositivo deben estar preparados; este tipo no ofrece descubrimiento ni asistente de inclusión de Ingeniería. No promete todos los dispositivos/clusters Zigbee.

### Campos y límite de autoría

Los parámetros de red deben coincidir con coordinador/estado existentes. La autoría del vínculo en el asistente genérico es incompleta: Runtime rechaza sus campos extra. Los campos siguientes describen requisitos del driver; el asistente actual no ofrece un flujo de creación corregido.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Recurso de coordinador en el host | ID de un recurso físico disponible y serie registrado en la instalación como ZigbeeCoordinator, familia TI zStack y modelo CC2652. Aquí no se escribe una ruta COM/tty. Solicite al administrador su ID y acceso al dispositivo en Windows/Linux/contenedor. | radio.zigbee1 |  |
| Identidad de red | Identidad de la red preparada, que debe coincidir con la evidencia registrada en el recurso coordinador. No renombra la red física ni sustituye PAN ID/clave. | zigbee.lab1 |  |
| PAN ID | Identificador de la red existente. Use el valor de la instalación; prefiera decimal en el campo numérico. Cambiarlo no es un procedimiento de reparación. | 4660 | 1–65534. |
| PAN ID extendido | Identidad de red con 16 dígitos hexadecimales según el coordinador preparado. No es IEEE de un actuador ni clave secreta. | 00124b0000000001 |  |
| Canal de red | Canal Zigbee utilizado por la red existente. Cambiar solo este número en el proyecto no migra de forma segura todos los dispositivos. | 15 | 11–26. |
| Velocidad serie | Velocidad del enlace serie al coordinador, compatible con su firmware. No es frecuencia de medición ni canal de radio. | 115200 | 9600–1000000. |
| Referencia de clave de red | Referencia de material protegido aprovisionado en el host para esta red. El administrador proporciona la referencia/slot; no pegue la clave secreta ni la incluya en un paquete de Ingeniería. No sustituya una clave existente por otra nueva para intentar corregir conexión. |  |  |
| IEEE del dispositivo | Identidad de 16 dígitos hexadecimales del dispositivo ya asociado. Un nombre amigable o dirección de red corta no sustituye al IEEE. | 00124b0000000001 |  |
| Endpoint | Endpoint de la función del dispositivo según su mapa real. No es puerto TCP ni número de canal del dashboard. | 1 | 1–240. |
| Cluster admitido | Este driver nativo admite genOnOff en esta versión; no ofrece todos los clusters Zigbee ni medición analógica mediante este vínculo. | genOnOff |  |
| Atributo admitido | Atributo onOff con TAG Boolean. Se rechaza otro atributo o tipo numérico de TAG. | onOff |  |
| Dirección completa del TAG | Reúne IEEE/endpoint/cluster/atributo. El ejemplo es una función On/Off; sustituya por la identidad existente del dispositivo. | 00124b0000000001/1/genOnOff/onOff |  |
| Autoría del vínculo en esta versión | El asistente genérico actual guarda campos de identidad como ajustes extra, pero este driver los exige solo en la dirección. Runtime rechaza el objeto generado. La autoría por interfaz espera corrección; no eluda la validación. |  |  |

### Ejemplo de función preparada

El ejemplo describe identidad de una función booleana existente. No es paquete de importación ni abre inclusión. El vínculo guarda identidad en la dirección completa sin ajustes extra duplicados. ReadOnly sigue controlando escritura junto con autorización de Authority.

```json
{"address":"00124b0000000001/1/genOnOff/onOff","ieeeAddress":"00124b0000000001","endpoint":1,"cluster":"genOnOff","attribute":"onOff","dataType":"Boolean"}
```

### Diagnóstico y conservación de red

Compruebe recurso disponible, modelo/familia, acceso serie e identidad de red antes de revisar TAGs. Después compruebe IEEE/endpoint y Boolean/genOnOff/onOff. No repare fallos borrando estado persistente, cambiando claves ni creando otra red sin el procedimiento de mantenimiento adecuado. Escritura seguida de lectura comprueba estado, no actuación física.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.zigbee-native-setup)

## Z-Wave JS Controller: red preparada y valores persistidos

Use el controlador registrado en el host y conserve la identidad exacta de cada valor.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-ZWAVE: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.zwave-setup; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Qué hace este tipo

El proceso auxiliar administrado usa controlador serie local y comunicación interna restringida al host. La instalación admite Windows/Linux x64; Docker también necesita dispositivo y directorio de estado persistente. Trabaja con valores persistidos preparados: no ofrece inclusión, exclusión, SmartStart, reset, administración del controlador ni descubrimiento de Ingeniería.

### Fuente e interpretación de dirección

Las referencias S0/S2 son opcionales en el formato de fuente, pero los dispositivos seguros necesitan material de su red. Región RF e identidad NVM pertenecen al recurso del host. La autoría de vínculos nuevos por interfaz está pendiente: el descriptor sin campos adicionales no produce el vínculo exigido por Runtime.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Recurso de controlador en el host | ID de recurso serie ZWaveController disponible con región RF e identidad NVM de red registradas por el administrador. No escriba ruta serie ni parámetros RF en esta fuente. Región/identidad deben coincidir con el controlador preparado. | radio.zwave1 |  |
| Clave S0 Legacy: referencia | Referencia de material protegido aprovisionado en el host para esta red. El administrador proporciona la referencia/slot; no pegue la clave secreta ni la incluya en un paquete de Ingeniería. No sustituya una clave existente por otra nueva para intentar corregir conexión. |  |  |
| Clave S2 Access Control: referencia | Referencia de material protegido aprovisionado en el host para esta red. El administrador proporciona la referencia/slot; no pegue la clave secreta ni la incluya en un paquete de Ingeniería. No sustituya una clave existente por otra nueva para intentar corregir conexión. |  |  |
| Clave S2 Authenticated: referencia | Referencia de material protegido aprovisionado en el host para esta red. El administrador proporciona la referencia/slot; no pegue la clave secreta ni la incluya en un paquete de Ingeniería. No sustituya una clave existente por otra nueva para intentar corregir conexión. |  |  |
| Clave S2 Unauthenticated: referencia | Referencia de material protegido aprovisionado en el host para esta red. El administrador proporciona la referencia/slot; no pegue la clave secreta ni la incluya en un paquete de Ingeniería. No sustituya una clave existente por otra nueva para intentar corregir conexión. |  |  |
| Dirección persistida del valor | Identifica nodo, endpoint, Command Class, propiedad y clave opcional, conservando tipo string/number. No es el nombre mostrado al operador. Debe coincidir con el inventario preparado del controlador. | node/5/endpoint/0/cc/37/property/string/currentValue/key/none/- |  |
| Nodo | Número asignado al dispositivo en la red Z-Wave existente; no es dirección IP. Exclusión/reinclusión puede exigir revisar el vínculo. | 5 | 1–232. |
| Endpoint | Endpoint del valor en el dispositivo, incluido 0 para un valor del nodo raíz. No es puerto serie/TCP. | 0 | 0–127. |
| Command Class | Código de la clase a la que pertenece el valor. El ejemplo 37 es Switch Binary; el driver admite valores persistidos limitados y rechaza clases fuera de alcance. No es permiso de Authority. | 37 |  |
| Propiedad y su tipo | Use string/currentValue para la propiedad textual del ejemplo. Las propiedades number/1 y string/1 tienen identidades distintas; no quite el tipo. No suponga que currentValue sea escribible. | string/currentValue |  |
| Clave opcional y su tipo | key/none/- significa sin clave adicional. Si existe una clave conserve su tipo number/string y el contenido escapado de la dirección; no omita partes del formato. | none/- |  |
| Autoría de un vínculo nuevo | Este driver exige vínculo de comunicación versionado además de Address. El asistente genérico no crea un vínculo nuevo si el descriptor no tiene campos adicionales, como aquí. Escribir solo la dirección de un TAG nuevo no basta; la corrección de autoría está pendiente. Un vínculo válido recibido por paquete/API es un caso distinto. |  |  |

### Ejemplo: leer estado de interruptor binario

El ejemplo es una lectura Boolean de currentValue del nodo 5. Use los valores reales de la instalación. La propiedad puede no ser escribible; el comando exige valor anunciado como escribible por el controlador y autorización SCADIVO. No es paquete de importación.

```json
{"address":"node/5/endpoint/0/cc/37/property/string/currentValue/key/none/-","nodeId":5,"endpoint":0,"commandClass":37,"property":"currentValue","dataType":"Boolean","readOnly":true}
```

### Comprobación y fallos comunes

Los tipos TAG admitidos son Boolean, Int16/Int32/Int64, Float/Double y String dentro de propiedades persistidas aceptadas. Compruebe recurso, región, identidad NVM y material de seguridad; después dirección completa y metadatos reales del valor. Valores ausentes, tipos incompatibles o escrituras no anunciadas no se corrigen quitando validación. Los eventos transitorios no se convierten automáticamente en TAGs persistidos. Compruebe retorno antes de considerar completo un comando.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.zwave-setup)

## Zigbee2MQTT Bridge: inventario, selección y actualidad

Conecte a una instalación externa y distinga inventario, incorporación e informes recientes.

<!-- HELP-REVIEW-AFTER-PRODUCT-785-Z2M: https://github.com/brunolrogerio-collab/EliteSCADA/issues/785; topic=drivers.zigbee2mqtt-setup; fields=selectedIeeeAddresses; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-787: https://github.com/brunolrogerio-collab/EliteSCADA/issues/787; topic=drivers.zigbee2mqtt-setup; fields=inventoryFormat; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Instalación externa y flujo disponible

Zigbee2MQTT y broker se administran fuera de SCADIVO. Los dispositivos deben estar asociados a la red externa; este conector no abre inclusión, administra el coordinador ni sustituye Zigbee nativo. Se anuncian prueba de conexión, descubrimiento y lectura, pero sin selección solo se lista inventario. La interfaz actual no envía la selección necesaria para incorporar; ese flujo espera corrección.

El formato externo incluye funciones en definition.exposes; esta versión del parser busca exposes en la raíz. Por ello puede listar un dispositivo sin producir TAGs. La corrección de compatibilidad está pendiente por separado de la selección en interfaz.

### Campos de fuente

Host, tópico base y credenciales deben coincidir con el broker usado realmente por la instalación. Compruebe conexión desde el servidor SCADIVO, no solo el navegador.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host del broker MQTT | Nombre/IP del broker usado por la instalación externa Zigbee2MQTT, accesible desde el servidor SCADIVO. No es URL de su interfaz ni un tópico. | mqtt.example.invalid |  |
| Puerto del broker | Puerto real del broker. El formulario usa 1883 de forma predeterminada; con TLS compruebe el puerto correspondiente sin suponer cambio automático. | 1883 | 1–65535. |
| Usar TLS | Activa transporte protegido si el broker lo ofrece. No instala certificados ni sustituye autenticación del broker. | false |  |
| Tópico base | Prefijo configurado en Zigbee2MQTT externo. El driver usa, por ejemplo, baseTopic/bridge/state y baseTopic/bridge/devices. No escriba aquí el friendly name de un dispositivo. | zigbee2mqtt |  |
| ID del cliente MQTT | Opcional; vacío genera un ID estable desde la identidad de la fuente. Si lo define no reutilice el ID en otro cliente activo. No es usuario ni contraseña. |  |  |
| Versión MQTT | mqtt5 o mqtt311 según admita el broker. El driver usa sus propios valores de sesión; no copie todos los ajustes avanzados de MQTT Raw en esta fuente. | mqtt5 |  |
| Usuario del broker | Cuenta autorizada en los tópicos de la instalación externa. Distinta del login SCADIVO. |  |  |
| Referencia de contraseña del broker | Referencia de material protegido aprovisionado en el host para esta red. El administrador proporciona la referencia/slot; no pegue la clave secreta ni la incluya en un paquete de Ingeniería. No sustituya una clave existente por otra nueva para intentar corregir conexión. |  |  |
| Keep Alive (s) | Supervisión de conexión con el broker. No solicita mediciones ni despierta dispositivos a batería. | 30 | 1–65535. |
| Timeout de conexión (ms) | Plazo para conectar al broker, distinto del tiempo de solicitud o confirmación de escritura. | 10000 | 100–300000. |
| Timeout de solicitud (ms) | Plazo de una operación de consulta. No es intervalo de sondeo de todos los sensores. | 5000 | 100–30000. |
| Espera de confirmación de escritura (ms) | Plazo para un informe de estado compatible y suficientemente reciente tras el comando. Aceptación MQTT sin feedback no confirma el estado del equipo. | 10000 | 100–60000. |
| Espera mínima de reconexión (ms) | Espera inicial tras perder conexión. Reducirla no corrige broker ausente ni autenticación rechazada. | 1000 | 100–300000. |
| Espera máxima de reconexión (ms) | Limita la espera progresiva y debe ser mayor o igual a la mínima. | 30000 | 100–3600000. |
| Tamaño máximo por mensaje (bytes) | Limita payload recibido, incluidos inventarios; aumente solo si lo exige el documento real y hay recursos. No es capacidad de histórico. | 1048576 | 1–67108864. |
| Límite de fallos de conexión | Límite de fallos consecutivos usado por supervisión de disponibilidad. No permite operar con una fuente inaccesible. | 10 | 1–1000. |
| Máximo de mensajes en cola | Limita el búfer del transporte. Más mensajes consumen más memoria; no es almacenamiento persistente. | 2048 | 1–65535. |
| Máximo de dispositivos en inventario | Límite de inventario/selección para esta fuente. No promete capacidad de radio ni elimina cuotas de licencia. | 256 | 1–4096. |
| Máximo de funciones por dispositivo | Limita descripciones de propiedades procesadas por dispositivo. Expose es una función/propiedad anunciada por Zigbee2MQTT, no un TAG incorporado automáticamente. | 256 | 1–2048. |

### Vínculos generados y selección pendiente

Los campos técnicos siguientes los produce la incorporación desde selección/inventario. No son IDs que el desarrollador deba inventar. Hasta que la pantalla envíe selección, listar un dispositivo no garantiza Equipo/TAGs listos. Las funciones escalares mapeadas son binary y numeric; capacidades funcionales dependen de propiedades admitidas.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| IEEE, endpoint y propiedad | Dirección estable generada desde IEEE, endpoint opcional y propiedad. Friendly name puede cambiar y no debe ser identidad del vínculo. | ieee:0x00124b0024cafe01/endpoint:/property:temperature |  |
| Identidad interna de fuente — automática | Campo técnico generado por incorporación. Relaciona con ID estable de la fuente guardada. No invente ni copie IDs entre fuentes. |  |  |
| Clave de fuente — automática | Campo técnico generado por incorporación. Relaciona con la clave legible de fuente. No invente ni copie IDs entre fuentes. |  |  |
| IEEE — automático | Campo técnico generado por incorporación. IEEE normalizado del dispositivo seleccionado, no nombre amigable. No invente ni copie IDs entre fuentes. | 0x00124b0024cafe01 |  |
| Endpoint — automático | Campo técnico generado por incorporación. Endpoint anunciado por la función; puede faltar. No invente ni copie IDs entre fuentes. |  |  |
| Propiedad — automática | Campo técnico generado por incorporación. Nombre de propiedad anunciada, como temperature o state. No invente ni copie IDs entre fuentes. | temperature |  |
| Tipo de función — automático | Campo técnico generado por incorporación. binary produce TAG Boolean; numeric produce TAG Double. Otros tipos no se convierten en TAG escalar mediante este flujo. No invente ni copie IDs entre fuentes. | numeric |  |
| Acceso anunciado — automático | Campo técnico generado por incorporación. Bits combinables: 1 estado, 2 escritura, 4 lectura por solicitud. Debe incluir 1; 5 significa estado y lectura por solicitud. No concede autorización de seguridad. No invente ni copie IDs entre fuentes. | 5 |  |
| Unidad — automática | Campo técnico generado por incorporación. Debe coincidir con la unidad del TAG; no es conversión arbitraria de escala. No invente ni copie IDs entre fuentes. | °C |  |
| Mínimo — automático | Campo técnico generado por incorporación. Límite numérico inferior anunciado, si existe. No invente ni copie IDs entre fuentes. |  |  |
| Máximo — automático | Campo técnico generado por incorporación. Límite numérico superior anunciado, si existe. No invente ni copie IDs entre fuentes. |  |  |
| Paso — automático | Campo técnico generado por incorporación. Paso numérico positivo si se anuncia; no define frecuencia de lectura. No invente ni copie IDs entre fuentes. |  |  |
| Valor encendido — automático | Campo técnico generado por incorporación. Valor JSON explícito para verdadero de una función binaria. Texto ON y Boolean true son tipos distintos. No invente ni copie IDs entre fuentes. | "ON" |  |
| Valor apagado — automático | Campo técnico generado por incorporación. Valor JSON explícito distinto del encendido para una función binaria. No invente ni copie IDs entre fuentes. | "OFF" |  |
| Identidad del punto — automática | Campo técnico generado por incorporación. Combina fuente, IEEE, endpoint y propiedad; copiar desde otra fuente es inválido. No invente ni copie IDs entre fuentes. |  |  |
| Función del Equipo — automática | Campo técnico generado por incorporación. Por ejemplo Temperature u OnOff; no es capability de seguridad concedida al usuario. No invente ni copie IDs entre fuentes. | Temperature |  |
| Contrato de confirmación — automático | Campo técnico generado por incorporación. Si se admite exige informe de estado no retenido compatible tras el comando, con evidencia de actualidad de origen. No prueba actuación física. No invente ni copie IDs entre fuentes. | non-retained-state-match |  |
| Selección explícita para incorporar | El servicio admite selección de IEEE, pero la pantalla Home/Building actual no la envía. Sin selección el descubrimiento lista inventario sin contenido para incorporar; elegir una fila no repite la solicitud con IEEE. La incorporación por interfaz espera corrección. |  |  |
| Formato de inventario — límite actual | El formato externo incluye funciones en definition.exposes; esta versión del parser busca exposes en la raíz. Por ello puede listar un dispositivo sin producir TAGs. La corrección de compatibilidad está pendiente por separado de la selección en interfaz. | definition.exposes |  |

### Ejemplo de inventario anunciado

El ejemplo siguiente conserva el formato externo. El parser actual no extrae sus funciones; demuestra la limitación, no una importación completa. No reescriba mensajes del broker como solución operativa.

```json
[
  {
    "ieee_address": "0x00124b0024cafe01",
    "friendly_name": "sensor_sala",
    "supported": true,
    "type": "EndDevice",
    "definition": {
      "model": "Example",
      "vendor": "Example",
      "exposes": [
        {
          "type": "numeric",
          "property": "temperature",
          "access": 5,
          "unit": "°C"
        },
        {
          "type": "binary",
          "property": "state",
          "access": 7,
          "value_on": "ON",
          "value_off": "OFF"
        }
      ]
    }
  }
]
```

### Ejemplo de estado y calidad

Un mensaje retenido es Stale en este conector. Sin last_seen de origen posterior a la época de conexión/bridge, el informe puede ser Uncertain aunque retain sea false. Confirmar una operación exige además ser posterior al envío. Configure la información de medición en la instalación externa y compruebe instantes; recibir ahora no prueba medir ahora. El instante del ejemplo es ilustrativo: use medición actual en pruebas en vivo.

```json
{"temperature":21.5,"state":"OFF","last_seen":"2026-10-25T12:00:01Z"}
```

### Resolver por capas

Sin conexión: compruebe host/puerto/TLS/autenticación. Conectado sin inventario: compruebe baseTopic y bridge/state/devices. Inventario sin incorporación: compruebe límite de selección de interfaz en lugar de recrear la red. Valor Uncertain/Stale: compruebe retención, last_seen y reconexión. Escritura sin confirmar: compruebe acceso anunciado, vínculo de confirmación, disponibilidad e informe reciente; no confirme el TAG solo porque el broker aceptó la publicación.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.zigbee2mqtt-setup)
