# Home/Building: dispositivos y conexiones

## Home/Building: incorporar un dispositivo

Convierta descubrimiento en Equipo, TAGs y comandos comprobando lo que realmente se creará.

### Fuente, dispositivo y Ubicación son distintos

Incorporar transforma un dispositivo encontrado en recursos del proyecto. Fuente de datos indica cómo comunicar. Equipo identifica el dispositivo y Lugar indica dónde está.

Capacidades como OnOff, Temperature o Cover describen funciones: encender/apagar, temperatura o control de cobertura, según proveedor. No son permisos de seguridad. Encontrar e incorporar no autoriza a su cuenta a comandar; la política de Authority sigue decidiendo el acceso.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Fuente de integración | Fuente configurada en Fuentes de datos. Su tipo debe ofrecer descubrimiento para aparecer en este flujo; una lista vacía no prueba que toda la red esté vacía. |  |  |
| Dispositivo encontrado | Elija un candidato y revise TAGs, comandos, capacidades y avisos. Encontrado no significa incorporado; este formulario no incorpora candidatos sin materialización. |  |  |
| Nombre del dispositivo | Nombre legible del Equipo que se creará. No renombra el dispositivo físico ni su identidad de origen. | Luz da sala |  |
| Ruta del dispositivo | Organiza el Equipo en el proyecto. Compruebe conflictos en la vista previa; no es IP, NodeId ni tópico. | Home/Sala/Luz |  |
| Ubicación | Opcional: seleccione una Ubicación existente. Cree las Ubicaciones en su área antes de asociar. No concede permisos de acceso. |  |  |

### Del descubrimiento a la revisión activa

1. Configure y aplique la fuente en Ingeniería → Fuentes de datos con material protegido disponible en el servidor.
2. Abra Home/Building → Dispositivos, seleccione la fuente y descubra candidatos si el driver lo admite.
3. Elija dispositivo y lea avisos y listas de TAGs/comandos/capacidades.
4. Ajuste nombre, ruta y Ubicación y compruebe la vista previa; todavía no cambia la configuración.
5. Aplique solo una vista previa válida; si cambia el Workspace genere otra antes de aplicar.
6. Guarde/publique/active mediante el flujo del proyecto y compruebe estado, calidad, unidad y comandos autorizados.

### Sin candidato o función esperada

Compruebe fuente guardada, conectividad desde el servidor y funciones anunciadas por el driver. KNX/IP Secure no anuncia descubrimiento de Ingeniería; Home Assistant tiene la limitación de selección de su guía. Una función no mapeada no se considera entregada: revise avisos y campos realmente generados. Un gateway/bridge puede exponer un dispositivo sin protocolo nativo en SCADIVO. No importe repetidamente para corregir red o permisos.

[Abrir esta guía en Ayuda instalada](/help?topic=home-building.device-workflow)

## BACnet/IP: dispositivo, objetos y propiedades

Configure la red UDP y seleccione identidad del objeto; nombre y dirección de red no sustituyen el vínculo.

### Antes de conectar

Empiece con un objeto BACnet conocido de solo lectura, como entrada analógica. Pida al responsable instancia del dispositivo y datos de red: ruta, IP y puerto.

Este driver usa BACnet/IP por UDP. El servidor SCADIVO debe alcanzar esa red; descubrir por broadcast depende de la topología. En Docker compruebe también red y paso de UDP. Acceder desde el navegador no demuestra esas condiciones. No se anuncia BACnet Secure Connect.

### Campos de conexión y vínculo

La identidad del vínculo contiene dispositivo, tipo/instancia de objeto y propiedad, con índice opcional. COV y prioridad no cambian esa identidad.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Instancia del dispositivo | Identidad BACnet del equipo, no su IP ni la instancia de un objeto. La instancia del dispositivo del vínculo TAG debe coincidir con la fuente. Confírmela en el controlador o mediante descubrimiento. | 1201 | 0–4194302. |
| Dirección manual del destino | Opcional: IPv4 del equipo con puerto UDP. Útil para un destino conocido; no sustituye la identidad deviceInstance. | 192.0.2.20:47808 |  |
| Puerto UDP local | Puerto donde el proceso SCADIVO escucha BACnet/IP. Otra aplicación/contenedor puede ocuparlo; compruebe firewall y exposición UDP en el servidor. | 47808 | 1–65535. |
| Intervalo de lectura (ms) | Ritmo solicitado de sondeo para puntos consultados. No es la frecuencia garantizada de eventos COV. | 1000 | 50–600000. |
| Timeout BACnet (ms) | Espera de una solicitud BACnet. Distinta de la ventana de descubrimiento y del sondeo. | 3000 | 100–60000. |
| Ventana de descubrimiento (ms) | Cuánto esperar respuestas durante descubrimiento. Aumentar puede ayudar en una red lenta, pero no hace que broadcasts crucen una subred bloqueada. | 1500 | 100–30000. |
| Dirección del BBMD | Opcional: punto de distribución BACnet configurado por el administrador para la red. No invente un BBMD para una red local simple. |  |  |
| Registro Foreign Device: vigencia (s) | Opcional: duración del registro en el BBMD. Exige bbmdAddress; no es vigencia del valor TAG ni retención histórica. | 600 | 30–32767. |
| Tipo del objeto | Código numérico del objeto BACnet. En el ejemplo, 0 identifica Analog Input; use el tipo del explorador/mapa del fabricante, no el tipo Double del TAG. | 0 |  |
| Instancia del objeto | Número del objeto dentro del dispositivo. Analog Input 37 difiere de otro tipo con instancia 37. | 37 | 0–4194303. |
| Propiedad | Código de propiedad a leer. El ejemplo 85 es Present Value. Nombre, descripción y valor son propiedades distintas; prefiera selección en el explorador para evitar códigos incorrectos. | 85 |  |
| Índice del array | Complete solo para una propiedad array si necesita un elemento concreto. Vacío conserva la propiedad completa; no añada índice a un valor escalar. |  |  |
| Usar COV si está disponible | Solicita notificaciones por cambio en lugar de depender solo del sondeo. Depende del servidor/objeto y el driver conserva comprobaciones por lectura; ausencia de cambio no implica desconexión. | true |  |
| Prioridad de escritura BACnet | Opcional para objeto escribible que acepte prioridad. No es prioridad de alarma ni permiso SCADIVO. Una escritura aceptada puede no cambiar el valor efectivo si otro comando tiene precedencia; compruebe feedback y política del equipo. | 8 | 1–16. |

### Ejemplo: valor de entrada analógica

El ejemplo identifica Present Value del Analog Input 37 en el dispositivo 1201. Describe un vínculo, no un paquete completo de importación. Use descubrimiento/exploración y compruebe tipo/unidad antes de activar; sustituya las identidades por las de su equipo.

```json
{"deviceInstance":1201,"objectType":0,"objectInstance":37,"propertyIdentifier":85,"useCov":true}
```

### Comprobación y diagnóstico

Pruebe conexión, descubra/explore objetos y revise la vista previa antes de aplicar; active mediante el flujo del proyecto. Sin respuesta: compruebe UDP, instancia, destino y topología/BBMD. Objeto encontrado con valor inválido: compruebe propiedad, índice y tipo. COV no admitido exige revisar notificaciones y lectura de comprobación; ausencia de cambio no es cero. Escritura rechazada exige acceso, objeto escribible y prioridad además de autorización SCADIVO.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.bacnet-setup)

## KNX/IP Secure: túnel, DPT y estado confirmado

Use direcciones del proyecto KNX y separe comando y feedback.

<!-- HELP-REVIEW-AFTER-PRODUCT-786: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.knx-setup; fields=writeGroupAddress; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Requisitos y alcance

Use datos del proyecto KNX: direcciones, gateway, tipo de punto y material de seguridad. Pídalos al responsable antes de completar la fuente. Separe el punto de comando del que informa estado real.

Usa túnel KNX IP Secure TCP y Data Secure. Requiere el proceso auxiliar XKNX de la instalación para Windows o Linux x64 y material protegido preparado en el servidor. Un contenedor también necesita esos recursos.

No presuponga túnel IP sin seguridad, enrutamiento multicast ni descubrimiento automático de Ingeniería: este tipo no los promete.

### Campos del gateway y del punto

El asistente genérico de TAG todavía genera un vínculo incompatible con el contrato KNX/IP. La corrección de autoría por interfaz está pendiente; los campos siguientes explican el contrato, pero no hay recorrido validado para completarlo con el asistente actual. No intente resolverlo cambiando DPTs ni desactivando seguridad.

Configure la fuente y vínculo con el DPT real. Contraseñas/llavero son referencias protegidas, no archivos secretos dentro de la aplicación.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host del gateway KNX | IP/nombre del gateway KNX/IP Secure accesible desde el servidor SCADIVO. No es dirección de grupo ni individual. | 192.0.2.40 |  |
| Puerto del túnel Secure | Puerto del endpoint Secure TCP configurado en el gateway. No suponga que un gateway de túnel IP sin seguridad ofrece este servicio. | 3671 | 1–65535. |
| Dirección individual | Dirección asignada al cliente KNX, en notación área.línea.dispositivo. Obténgala del proyecto KNX/administrador para evitar duplicados. | 1.1.250 |  |
| ID de usuario del túnel Secure | ID de usuario Secure del gateway, no un login SCADIVO. Debe corresponder al material de autenticación aprovisionado. | 1 | 1–127. |
| Referencia de contraseña Secure | Referencia del material protegido aprovisionado en el servidor para esta integración. Solicite la referencia al administrador; no pegue contraseñas, tokens ni claves secretas en ajustes comunes ni en el manual de la aplicación. |  |  |
| Referencia de autenticación del dispositivo | Referencia del material protegido aprovisionado en el servidor para esta integración. Solicite la referencia al administrador; no pegue contraseñas, tokens ni claves secretas en ajustes comunes ni en el manual de la aplicación. |  |  |
| Referencia del llavero Data Secure | Referencia del material protegido aprovisionado en el servidor para esta integración. Solicite la referencia al administrador; no pegue contraseñas, tokens ni claves secretas en ajustes comunes ni en el manual de la aplicación. |  |  |
| Grupo de comando / dirección del vínculo | Dirección en principal/intermedio/subgrupo para escritura. En el vínculo portable es Address del TAG; para un TAG de solo lectura compruebe el mapa del proyecto KNX. El asistente genérico de TAG todavía genera un vínculo incompatible con el contrato KNX/IP. La corrección de autoría por interfaz está pendiente; los campos siguientes explican el contrato, pero no hay recorrido validado para completarlo con el asistente actual. No intente resolverlo cambiando DPTs ni desactivando seguridad. | 1/2/3 |  |
| Grupo de estado confirmado | Origen del feedback. Para un TAG escribible debe diferir del grupo de comando; el driver no considera el envío como confirmación de estado. | 1/2/4 |  |
| Tipo de punto KNX (DPT) | Define la interpretación del telegrama. 1.001/1.002 usan TAG Boolean; 5.001 y 9.001/9.004/9.007/9.024 usan Float o Double en este driver. El ejemplo usa 9.001 para temperatura. No elija DPT solo por el tamaño aparente del valor. | 9.001 |  |

### Ejemplo: comando y retorno distintos

El ejemplo describe un TAG Double DPT 9.001 con grupo de comando 1/2/3 y retorno 1/2/4. Sustituya por grupos existentes en el proyecto KNX. No es paquete de importación ni contiene material de seguridad. Para punto escribible se rechazan grupos iguales; el retorno debe existir en el equipo.

```json
{"writeGroupAddress":"1/2/3","stateGroupAddress":"1/2/4","dpt":"9.001","tagType":"Double"}
```

### Compruebe por etapas

Compruebe primero proceso auxiliar y referencias protegidas, después host/puerto/usuario/dirección individual y grupos/DPT/tipo del TAG. La autoría con el asistente actual requiere la corrección indicada. Con vínculo correctamente validado guarde/publique/active mediante el proyecto y compruebe retorno antes de escribir. Un envío aceptado sin retorno no debe actualizar artificialmente el estado. Este driver no anuncia descubrimiento de Ingeniería.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.knx-setup)

## Home Assistant Bridge: conectar y elegir entidades

Una fuente conecta a Home Assistant administrado por el usuario; solo entidades seleccionadas entran en el proyecto.

<!-- HELP-REVIEW-AFTER-PRODUCT-785: https://github.com/brunolrogerio-collab/EliteSCADA/issues/785; topic=drivers.homeassistant-setup; fields=selectedEntityIds; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Bridge local con identidad explícita

Use esta fuente para conectar con una instalación Home Assistant ya preparada por usted o el responsable. Debe ser accesible desde el servidor SCADIVO. Prepare su token autorizado como material protegido antes de usarla.

HAB abrevia Home Assistant Bridge; HA en SCADIVO sigue significando Alta Disponibilidad. El puente integra entidades admitidas, pero no gestiona cuentas ni copia la base Home Assistant. Sus integraciones no se convierten por ello en drivers nativos SCADIVO.

### Conexión, identidad y límite actual de selección

Probar conexión verifica transporte/autenticación, no incorporación. En la versión actual la pantalla genérica de descubrimiento no proporciona selección de entidades; una selección vacía no significa necesariamente fallo de red. Para un TAG conocido configure fuente/vínculo explícitamente en el editor de TAGs y compruebe lectura antes de activar. No hay importación automática de toda la instancia.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host Home Assistant | Nombre DNS o IP accesible desde el servidor SCADIVO. Use el equipo o servicio de origen sin ruta de URL; localhost es el servidor/contenedor, no el navegador. | 192.0.2.30 |  |
| Puerto | Puerto del servicio de origen, distinto de los puertos de interfaz/API SCADIVO. Al cambiar TLS compruebe también el puerto real del servicio. | 8123 | 1–65535. |
| Usar TLS | Protege el transporte si el servicio ofrece TLS. No instala certificados ni cambia el puerto automáticamente. | false |  |
| Ruta base | Prefijo opcional si Home Assistant se publica por proxy. El cliente añade /api/websocket. Sin prefijo déjelo vacío; no repita el endpoint completo. |  |  |
| Referencia del token Home Assistant | Referencia del material protegido aprovisionado en el servidor para esta integración. Solicite la referencia al administrador; no pegue contraseñas, tokens ni claves secretas en ajustes comunes ni en el manual de la aplicación. |  |  |
| Tiempo para conectar (ms) | Plazo de establecimiento de conexión WebSocket. Distinto del timeout de cada solicitud. | 10000 | 100–300000. |
| Timeout de respuesta (ms) | Plazo de respuesta de una solicitud; no es el período de medición ni prueba de actuación física. Un valor demasiado pequeño puede rechazar una fuente lenta. | 10000 | 100–300000. |
| Espera mínima de reconexión (ms) | Pausa mínima tras perder conexión; evite intentos continuos. No corrige autenticación rechazada. | 1000 | 100–300000. |
| Espera máxima de reconexión (ms) | Límite de espera progresiva; debe ser mayor o igual a la mínima. | 30000 | 100–3600000. |
| Entidad y campo | Use entidad y campo exactos: entity:sensor.temperature:state para estado; entity:light.room:attribute:brightness para atributo. El nombre mostrado no es entity_id. Renombrar entity_id en Home Assistant exige revisar el vínculo. | entity:sensor.temperature:state |  |
| Selección de entidades para incorporar | El driver exige selección explícita, pero esta versión de la pantalla Home/Building todavía no permite elegir entidades. El descubrimiento puede devolver selección vacía sin dispositivo para incorporar aunque la conexión sea válida. Esa pantalla todavía no tiene selector; la corrección está pendiente. |  |  |

### Ejemplos de dirección

Use state para estado de entidad y attribute para un atributo existente. El ejemplo muestra lectura de sensor y brillo de luz; no es archivo de importación. Compruebe tipo/unidad: lectura numérica no es texto de presentación y los comandos de brillo usan la conversión del driver. Renombrar la entidad en origen exige revisar vínculos.

```json
{"state":"entity:sensor.temperature:state","attribute":"entity:light.room:attribute:brightness"}
```

### Comandos e indisponibilidad

El soporte se limita a vínculos implementados: estado switch/light, brillo/color de light y posición de cover según equipo. Este formulario no ofrece llamadas arbitrarias a cualquier servicio Home Assistant. unknown y unavailable no equivalen a cero o apagado. Tras un comando compruebe el estado autoritativo; éxito de solicitud no prueba actuación física.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.homeassistant-setup)

## ESPHome: API nativa, entidades y suspensión

Conecte directamente al dispositivo e incorpore solo las funciones mapeadas por el driver.

### Qué debe estar disponible

Compruebe primero que el firmware ofrece API nativa ESPHome, el servicio para leer entidades por red local. El servidor SCADIVO conecta; el navegador no comunica directamente con el microcontrolador.

Este camino no requiere Home Assistant, Dashboard ESPHome ni proceso Python auxiliar. Para modo Noise prepare la clave de API en el servidor antes de conectar.

### Conexión y vínculo

Prepare la fuente, pruebe conexión y use descubrimiento para obtener las claves reales de entidades. Los campos avanzados no corrigen una API desactivada ni una clave incompatible.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host ESPHome | Nombre DNS o IP accesible desde el servidor SCADIVO. Use el equipo o servicio de origen sin ruta de URL; localhost es el servidor/contenedor, no el navegador. | 192.0.2.30 |  |
| Puerto | Puerto del servicio de origen, distinto de los puertos de interfaz/API SCADIVO. Al cambiar TLS compruebe también el puerto real del servicio. | 6053 | 1–65535. |
| Cifrado de API | noise es preferido con clave aprovisionada. plaintext es una opción explícita de compatibilidad en una red confiable y debe corresponder al firmware. No confunda la clave Noise con la contraseña Wi-Fi. | noise |  |
| Referencia de clave Noise | Referencia del material protegido aprovisionado en el servidor para esta integración. Solicite la referencia al administrador; no pegue contraseñas, tokens ni claves secretas en ajustes comunes ni en el manual de la aplicación. |  |  |
| Timeout de respuesta (ms) | Plazo de respuesta de una solicitud; no es el período de medición ni prueba de actuación física. Un valor demasiado pequeño puede rechazar una fuente lenta. | 5000 | 100–300000. |
| Plazo de confirmación de escritura (ms) | Cuánto esperar el estado autoritativo tras un comando. Envío aceptado no es feedback; aumentar el plazo no garantiza movimiento/actuación. | 3000 | 100–300000. |
| Espera mínima de reconexión (ms) | Pausa mínima tras perder conexión; evite intentos continuos. No corrige autenticación rechazada. | 500 | 100–300000. |
| Espera máxima de reconexión (ms) | Límite de espera progresiva; debe ser mayor o igual a la mínima. | 30000 | 100–3600000. |
| Keep Alive (ms) | Intervalo de supervisión de conexión; no aumenta la frecuencia de medición del sensor. | 30000 | 1000–300000. |
| Política de suspensión | normal para dispositivos continuos; expected solo para nodos que duermen intencionadamente. Cambia valores de reconexión: mínimo 500→10000 ms, máximo 30000→60000 ms. La ausencia sigue no disponible, nunca saludable. | normal |  |
| Dirección de la entidad | Formato tipo:device-id:clave-hex.campo. El descubrimiento proporciona la clave hexadecimal de 8 dígitos; no use el nombre mostrado como clave. Ejemplo switch:0:00000010.state. | switch:0:00000010.state |  |
| Tipo de escritura | Define una escritura admitida para la entidad, no un servicio arbitrario: switchstate, lightstate, lightbrightness, coverposition, fanstate, fanspeedlevel, numberstate o selectstate. Prefiera el vínculo generado por descubrimiento. Se siguen comprobando tipo de entidad/valor y ReadOnly. | switchstate |  |

### Ejemplo de entidad

Esta dirección representa state de una entidad Switch, device-id 0 y clave hexadecimal 00000010. Use la clave devuelta por su dispositivo; no invente IDs. Un TAG Boolean escribible exige tipo de escritura compatible. No es paquete de importación.

```json
{"address":"switch:0:00000010.state","writeKind":"switchstate"}
```

### Mapa de funciones y diagnóstico

La materialización incluye entidades de proceso admitidas como sensor, binary sensor, switch, light, cover, fan, number y select. Entidades de configuración/diagnóstico y funciones no mapeadas pueden omitirse con aviso; encontrar climate, lock o button en inventario no implica control completo. Compruebe primero API, modo/clave y conexión; después dirección/tipo y avisos. Para nodos dormidos expected ajusta el ciclo de conexión, pero mantiene la ausencia como no disponible.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.esphome-setup)

## Shelly Gen2+: componentes, medición y comandos

Use RPC local y revise canales/campos realmente ofrecidos por el dispositivo.

### Conexión local con generación compatible

Compruebe generación y elija un canal conocido para probar. Este driver usa comunicación local RPC de Shelly Gen2+ por HTTP/WebSocket. Tenga dirección, puerto y autenticación.

Componente significa canal o función del Shelly, no usuario o Lugar SCADIVO. No usa API de nube ni promete compatibilidad con todo Shelly Gen1.

### Campos de fuente y TAG

Pruebe conexión y descubra el dispositivo para obtener componentes reales. La vista previa muestra solo mediciones/comandos mapeados; un nombre amigable no sustituye component key.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Host Shelly | Nombre DNS o IP accesible desde el servidor SCADIVO. Use el equipo o servicio de origen sin ruta de URL; localhost es el servidor/contenedor, no el navegador. | 192.0.2.30 |  |
| Puerto | Puerto del servicio de origen, distinto de los puertos de interfaz/API SCADIVO. Al cambiar TLS compruebe también el puerto real del servicio. | 80 | 1–65535. |
| Usar TLS | Protege el transporte si el servicio ofrece TLS. No instala certificados ni cambia el puerto automáticamente. | false |  |
| Usuario del dispositivo | Cuenta de autenticación RPC del Shelly; predeterminada admin. No es el usuario SCADIVO ni una cuenta de nube Shelly. | admin |  |
| Referencia de contraseña Shelly | Referencia del material protegido aprovisionado en el servidor para esta integración. Solicite la referencia al administrador; no pegue contraseñas, tokens ni claves secretas en ajustes comunes ni en el manual de la aplicación. |  |  |
| Timeout de respuesta (ms) | Plazo de respuesta de una solicitud; no es el período de medición ni prueba de actuación física. Un valor demasiado pequeño puede rechazar una fuente lenta. | 5000 | 100–300000. |
| Espera mínima de reconexión (ms) | Pausa mínima tras perder conexión; evite intentos continuos. No corrige autenticación rechazada. | 1000 | 100–300000. |
| Espera máxima de reconexión (ms) | Límite de espera progresiva; debe ser mayor o igual a la mínima. | 30000 | 100–3600000. |
| Componente y campo | Formato componente.campo, por ejemplo switch:0.output o switch:0.aenergy.total. El número identifica el canal; no use el nombre amigable como dirección. | switch:0.output |  |
| Método RPC de escritura | Para vínculo escribible, método admitido por equipo y driver; en el ejemplo Switch.Set. Prefiera el vínculo generado por descubrimiento sin asumir que cualquier método pueda ser comando. | Switch.Set |  |
| Parámetro RPC de escritura | Campo que recibe el valor de escritura; Switch.Set usa on para el Boolean. Exige TAG escribible, método compatible y autorización. El estado confirmado se lee por separado. | on |  |

### Ejemplo: salida del canal cero

El ejemplo vincula output de switch:0 a un TAG Boolean; la escritura compatible usa Switch.Set con parámetro on. Compruebe el canal real antes de crear comandos. El JSON ilustra el vínculo; no es una solicitud directa ni paquete de importación.

```json
{"address":"switch:0.output","writeMethod":"Switch.Set","writeParameter":"on"}
```

### Comprobar medición y feedback

Los campos dependen del modelo: por ejemplo output, potencia apower, voltage, current y aenergy.total si existen. El driver también mapea funciones limitadas light/cover y sensores admitidos. Campo ausente no es lectura cero; revise avisos y tipo/unidad. Para escritura compruebe respuesta y estado confirmado por separado. Si falla autenticación compruebe referencia protegida y cuenta del dispositivo sin usar credenciales de nube.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.shelly-setup)
