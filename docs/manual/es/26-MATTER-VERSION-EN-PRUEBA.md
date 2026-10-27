# Matter Wi-Fi: versión en prueba

## Matter Wi-Fi: preparar y entender la versión en prueba

Compruebe la disponibilidad, prepare la fuente y distinga leer un atributo ya comisionado de emparejar un dispositivo.

<!-- HELP-REVIEW-AFTER-PRODUCT-786-MATTER: https://github.com/brunolrogerio-collab/EliteSCADA/issues/786; topic=drivers.matter-test-workflow; fields=communicationBinding; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

<!-- HELP-REVIEW-AFTER-PRODUCT-792-MATTER: https://github.com/brunolrogerio-collab/EliteSCADA/issues/792; topic=drivers.matter-test-workflow; fields=address,settings; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Empiece por la disponibilidad

Antes de configurar, confirme si Matter Wi-Fi está disponible. SCADIVO es multiplataforma, pero este conector es una previa aislada para host Linux x64 con proceso auxiliar local, llamado sidecar. No libera Matter para toda instalación.

La composición estándar no ofrece el tipo. Solo aparece si el responsable habilita la previa en Development/Test, sin HA. Si falta en Fuentes de datos, compruebe elegibilidad; no elija otro driver para imitarlo. La Ayuda sigue disponible para explicar límites.

### Qué puede preparar

La versión actual trabaja con atributos persistentes de dispositivos Matter Wi-Fi ya comisionados. Comisionar significa incorporar el dispositivo a la red lógica Matter, llamada fabric. Crear la fuente o su clave no realiza ese emparejamiento. Este conector no tiene asistente de comisionamiento, Thread, administración de dispositivos, Browse, Discover ni Probar conexión. Leer/escribir atributos no implica admitir todos los comandos de cada equipo. Mantenga la indicación de versión en prueba; la calificación futura no ha finalizado.

### Antes de abrir el editor

Pida al responsable los números del punto: nodo, endpoint, cluster y atributo, además del tipo de valor. No son el ID interno del TAG.

Nodo identifica el dispositivo en la red lógica Matter, llamada fabric. Endpoint identifica una función, como una salida. Cluster reúne funciones y atributos de un servicio, como On/Off. Atributo es un valor del grupo, como estado encendido/apagado. Un dispositivo puede tener varios endpoints; compruebe sus números sin copiar otra unidad.

El host necesita sidecar y red IPv6 preparados. La dependencia declarada requiere Node.js 22.13+ o 24+. La red del contenedor puede diferir de la máquina. Fabric y almacenamiento cifrado quedan en el host: importar .escadapkg no los transfiere ni recupera claves perdidas. Este procedimiento no pide claves o credenciales en campos del proyecto.

### Preparar la fuente y la clave local

1. En el entorno aislado elegible, abra Ingeniería → Fuentes de datos y cree una fuente Matter Wi-Fi con un nombre legible.
2. Compruebe los cambios y aplique la fuente al Workspace; todavía no es publicación/activación.
3. Seleccione la fuente aplicada. En el panel de clave Matter, elija Crear clave local y confirme. Aplique antes las ediciones pendientes: el botón no está disponible mientras la fuente difiera del estado aplicado.
4. Espere la respuesta y la recarga de la página. El resultado esperado es la indicación de clave configurada; después aparece Verificar clave local.
5. Continúe preparando los TAGs, revise, Guarde, Publique y Active solo el candidato validado. Crear/verificar la clave no contacta dispositivos ni demuestra comunicación. La operación requiere permiso para modificar Engineering y la versión actual del Workspace.

### Entienda campos y referencias

La fuente no recibe host, puerto ni ajustes públicos genéricos de protocolo. La referencia protegida se prepara mediante la acción del host. Los demás elementos explican el vínculo de un TAG; el ejemplo técnico no es un paquete completo ni un formulario de importación.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Clave de almacenamiento del fabric | Protege el almacenamiento local Matter. Use la acción del host; no introduzca clave, contraseña ni ruta. | Clave configurada | Referencia del host; el secreto nunca se muestra ni exporta. |
| Fuente del TAG | Asocie el punto con la fuente Matter preparada. El nombre no descubre un dispositivo. | matter.lab | La asociación debe identificar la misma fuente del vínculo. |
| Tipo del TAG | Elija el tipo real del atributo; Boolean corresponde a un estado encendido/apagado. | Boolean | Boolean, Int16, Int32, Int64, Float, Double o String; no suponga otros tipos. |
| Solo lectura | Empiece leyendo valor y calidad antes de probar actuación. | true | Permitir escritura del TAG no concede permiso al usuario ni hace escribible el atributo. |
| Dirección del atributo | Reúna los cuatro números reales en el formato mostrado; no es IP ni código de emparejamiento. | node/123/endpoint/1/cluster/6/attribute/0 | Decimal sin signo ni ceros iniciales; node de 1 a UInt64 máximo, endpoint UInt16, cluster/attribute UInt32. |
| Vínculo de comunicación | Declara identidad y versión de la representación entendida por Runtime. La dirección sola no crea este objeto. | Vínculo Matter v1 | El asistente genérico actual no construye el vínculo si el catálogo no declara campos de punto; aquí no se promete un procedimiento gráfico completo. |
| Versión del contrato | Identifica el formato del objeto, no la versión comercial de Matter. | 1 | El contrato actual del vínculo es 1. |
| Identificación del esquema | Identifica la interpretación Matter de la dirección; es referencia técnica. | elitescada.driver.matter.wifi | No sustituya por el esquema de configuración de la fuente. |
| Versión del esquema | Indica cómo interpretar el contenido del vínculo. | 1 | El planner actual acepta el esquema Matter v1. |
| Dirección dentro del vínculo | Repite la identidad completa del atributo dentro del vínculo. | node/123/endpoint/1/cluster/6/attribute/0 | Si Address está informado, debe ser idéntico a PortableAddress. |
| Ajustes adicionales | No añada ajustes genéricos para evitar un fallo del vínculo. | Sin ajustes adicionales | Settings de fuente/punto, selectors y transformaciones no identidad se rechazan en esta versión. |
| Cantidad de puntos | Mantenga solo los TAGs persistentes que desea ensayar en la fuente. | 1 TAG de lectura | De 1 a 512 TAGs asociados con identidades internas válidas; el número de nodo no sustituye esos IDs. |

### Ejemplo de atributo encendido/apagado

Es solo un fragmento de configuración para explicar identidad y validar el contrato. El nodo 123 es ficticio y puede no existir en su red. Cluster 6/atributo 0 representa On/Off cuando el dispositivo lo ofrece en el endpoint 1. El entorno controlado proporciona los IDs internos y la referencia preparada por el host. No pegue el fragmento como .escadapkg ni añada una clave. La aceptación del planner comprueba configuración, no conexión, valores reales ni calidad. Hoy Address y la acción de clave por sí solos no completan la autoría del vínculo en la interfaz; el candidato técnico debe prepararse/validarse en el ensayo controlado.

```json
{
  "source": {
    "key": "matter.lab",
    "driver": "matter.wifi"
  },
  "tag": {
    "path": "Lab.Lamp.On",
    "dataType": "Boolean",
    "readOnly": true,
    "address": "node/123/endpoint/1/cluster/6/attribute/0",
    "communicationBinding": {
      "contractVersion": 1,
      "schemaId": "elitescada.driver.matter.wifi",
      "schemaVersion": 1,
      "portableAddress": "node/123/endpoint/1/cluster/6/attribute/0"
    }
  }
}
```

### Lectura, escritura y seguridad operativa

Tras activar una configuración válida en el ensayo, revise Diagnóstico y el TAG en Runtime: valor, calidad y hora de la muestra. Mostrar cero/false sin una muestra válida no demuestra que la lámpara esté apagada. Antes de escribir, confirme que el atributo permite escritura y que el TAG, la Authority y la clase de sesión permiten la acción. Escribir el valor del atributo utiliza ProcessValueWrite; no lo convierta en CommandExecute ni prometa comandos no implementados. Compruebe el resultado real y la lectura de retorno. Un botón y una clave creada no confirman actuación.

### Investigue por síntoma

Tipo ausente: compruebe disponibilidad de la vista previa y ausencia de HA. Clave ausente: aplique la fuente y use la acción del host; editar una referencia JSON no crea la clave. Botón deshabilitado: hay ediciones sin aplicar u operación en curso. Vínculo obligatorio: se introdujo la dirección sin CommunicationBinding. Dirección inválida: revise orden, números, límites y ausencia de ceros iniciales. Esquema incompatible: revise esquema Matter y versión 1. Activación rechazada en HA: esta versión no admite ese modo; no eluda la validación. Configuración aceptada sin muestras: revise sidecar, almacenamiento del host, red y atributos ya comisionados. Nunca recree la clave como primer intento de recuperar un fabric existente. Perder clave/estado requiere recuperación planificada; el archivo de ingeniería no restaura esos secretos.

[Abrir esta guía en Ayuda instalada](/help?topic=drivers.matter-test-workflow)
