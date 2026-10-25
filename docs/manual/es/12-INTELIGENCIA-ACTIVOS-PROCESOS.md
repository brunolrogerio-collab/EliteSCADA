# Inteligencia de Activos y Procesos

Este capítulo describe los flujos disponibles actualmente. Inteligencia es opcional: la licencia habilita la función, pero usted configura los equipos, perfiles y etapas que pretende analizar. No necesita aplicar análisis a toda la planta.

**Ayuda contextual:** `/help?topic=intelligence.overview`.

## 1. Empezar por el equipo y los TAGs

Antes de crear un perfil, identifique el recurso estable de Equipo y los TAGs que representan mediciones útiles, como corriente, tensión, presión, caudal o temperatura. Revise unidad, tipo, quality, timestamp y captura de Historian para el período que va a analizar. No incluya una señal solo porque esté disponible; indique qué pregunta operacional ayuda a responder.

Para editar se requieren `EngineeringModify` y un permiso explícito `AssetProcessIntelligenceManage`, global o que abarque el recurso. Las funciones de Inteligencia también requieren la licencia correspondiente.

## 2. Crear un perfil de medición y una asignación

1. Abra Engineering > Inteligencia de activos y cree un Perfil de medición con un nombre/clave identificables.
2. Añada los TAGs que describen el equipo. Marque cada señal como obligatoria u opcional y complete los calificadores semánticos solicitados; una etiqueta no sustituye la unidad ni la referencia real del TAG.
3. Guarde la configuración en Working.
4. En las asignaciones, elija el Equipo existente y el perfil. Una identidad de equipo puede usar varios perfiles.
5. Revise las referencias ausentes y duplicadas antes de guardar y seguir el lifecycle normal del proyecto.

El perfil organiza referencias y significado de señales. No crea TAGs, no habilita por sí solo la captura de Historian ni demuestra que los instrumentos estén calibrados.

## 3. Configurar el contexto operacional

El contexto diferencia condiciones de operación que no deben compararse como si fueran iguales, por ejemplo, rangos de caudal o modos indicados por TAG. Cree el contexto para la asignación de Equipo correcta y añada predicados para los TAGs del perfil.

Los predicados pueden representar un intervalo numérico inclusivo o igualdad booleana, de string o enum. Los límites numéricos se guardan en la unidad actual de Engineering; si cambia la unidad, revise y confirme explícitamente los límites. Para que una consulta use el contexto, los predicados deben coincidir con la evidencia temporal requerida en la misma marca UTC de origen, sincronizada y exacta. Si no hay coincidencia, el análisis puede no tener contexto elegible; no debilite el criterio para forzar un resultado.

## 4. Organizar etapas del proceso

Use **Etapas ordenadas del proceso** para montar la secuencia que desea supervisar. Cree cada etapa con una clave estable y un nombre legible; añada las asignaciones de Equipo/perfil y ajuste explícitamente el orden. La etapa organiza miembros, pero no infiere por sí sola causalidad, balance ni un KPI agregado del proceso.

Guarde la configuración en Working. Los datos y análisis de Runtime siguen sujetos a la revisión Active y a las autorizaciones vigentes.

## 5. Solicitar análisis

Seleccione el Equipo asignado y elija el análisis disponible, como cobertura del perfil, rendimiento o desviación contextual. Defina el período y solicite explícitamente la consulta. La desviación contextual requiere condiciones operacionales configuradas y evidencia temporal suficiente. El análisis es de solo lectura; no cambia un TAG, no controla equipos ni sustituye Alarmas.

Interprete el resultado junto con cobertura, quality, timestamps y las limitaciones de cada señal. La ausencia, quality deficiente, falta de historial o desalineación temporal no significan cero, operación normal ni falla confirmada. Los análisis actuales no son un diagnóstico de mantenimiento ni una prueba de eficiencia física. Para miembros de una etapa, examine un equipo a la vez; no combine resultados independientes como un score del proceso.

**Ayuda contextual:** `/help?topic=intelligence.process-stage-member-insights`.

## 6. Generar borradores de Pantallas

Elija un perfil o etapa y genere una Pantalla disponible. La generación usa metadatos de TAG para crear una Pantalla editable; no consulta valores en vivo ni Historian y no inventa KPIs. Revise el borrador en el editor, ejecute Preview y use Aplicar al Workspace. Después, guarde, publique y active mediante los pasos explícitos del lifecycle. La Pantalla generada no se activa automáticamente.

## 7. Problemas comunes

- **Recurso bloqueado:** compruebe licencia, `EngineeringModify`, `AssetProcessIntelligenceManage` y scope.
- **Perfil/asignación incompletos:** compruebe equipo, perfil, TAGs y calificadores obligatorios.
- **Sin cobertura/historial:** confirme que el TAG recibía muestras útiles con quality y timestamps y que había captura en el período. Historian no completa datos retroactivamente.
- **Sin coincidencia de contexto:** revise predicados, unidad, intervalo y la sincronización/igualdad temporal requerida.
- **Borrador no disponible:** complete las selecciones requeridas por ese tipo de Pantalla y refine el resultado en el editor normal.
- **El resultado cambia tras editar:** guarde la configuración correcta, compruebe la revisión Active y solicite explícitamente una consulta nueva.

**Ayuda relacionada:** `/help?topic=historian.overview`, `/help?topic=tags.quality`, `/help?topic=tags.timestamps`, `/help?topic=screens.overview`, `/help?topic=engineering.lifecycle`.

<!-- installed-system-guides -->

## Intelligence: habilitar, pausar y resolver bloqueos

Licencia disponible, recurso seleccionado y ejecución activa son condiciones diferentes.

### Siga la secuencia completa

Abra Ingeniería > Inteligencia de activos. Cree Equipo y TAGs, después perfiles, asociaciones Equipo/perfil, contextos y etapas útiles. Guarde configuración y alcances; publique/active revisión. Después active explícitamente el recurso para análisis. Crear perfil, marcar casilla o tener licencia no habilita ejecución automáticamente.

### Permisos y selección de recursos

EngineeringModify permite configurar; AssetProcessIntelligenceManage debe ser explícito, global o sobre Equipo/Ubicación. El nombre Developer o SystemAdmin por sí solo no sustituye ese permiso. Consultas también necesitan TrendUse y licencia Intelligence. Conceda en Seguridad y revise capacidades efectivas. Seleccione recursos sin memorizar IDs y conserve identidad.

### Activar solo donde sea útil

1. Seleccione Equipos/Ubicaciones en Alcances elegibles.
2. Guarde alcances y configuración.
3. Publique/active y resuelva cambios pendientes.
4. Pulse Activar en el recurso y confirme permiso habilitado.
5. Abra consulta de asociación y elija período.
6. Pausar cancela trabajo pendiente sin eliminar TAGs/histórico. Pause antes de quitar un recurso de selección.

### Activo configurado no significa habilitado

Tras revocación, el permiso puede quedar cerrado aunque configuración siga activa. Restaurar permiso no rearma silenciosamente: revise y Active otra vez, también tras reinicio cuando se indique. Evita reiniciar análisis sin decisión del responsable. Distíngalo de errores Historian o falta de datos.

### Ejemplo: empezar pequeño

Empiece con Bomba 01 en un área. Configure corriente y velocidad si existen; no invente vibración. Añada mediciones/análisis admitidos por catálogo. Mantenga pausado lo no utilizado. La selección controla ejecución elegible, no promete consumo fijo de CPU/RAM.

[Abrir esta guía en Ayuda instalada](/help?topic=intelligence.activation)

## Intelligence: significado de señales y unidades

Describa qué se midió, no solo el nombre del TAG.

### Por qué no basta el nombre

Un TAG Potencia puede ser activa/aparente o calculada; presión puede ser aspiración, descarga o diferencial. Las reglas no deben adivinar por nombre. Seleccione papel semántico controlado requerido y complete calificadores mostrados. El papel legado es descriptivo.

### Campos y elecciones

Elija solo significado sustentado por instrumento y proceso. Los selectores muestran opciones del catálogo actual.

| Campo | Finalidad y cómo rellenarlo | Ejemplo | Límites |
| --- | --- | --- | --- |
| Obligatorio | Marca señales cuya ausencia compromete cobertura. No haga opcional una señal esencial solo para obtener resultado. | Corriente necesaria para comparación |  |
| Papel semántico | Magnitud/función controlada; análisis usa ese papel, no semejanza de nombre. | Potencia activa de entrada, si se mide |  |
| Base de corriente | Distingue base admitida, como corriente RMS apropiada; no deduzca de etiqueta genérica. | Seleccione opción documentada del instrumento |  |
| Tipo de señal | Distingue medición, referencia y función exigida. Una consigna no prueba valor medido. | Medición real cuando se exija |  |
| Referencia de presión | Identifica base/origen; diferencial salida menos entrada difiere de solo descarga. | Diferencial medido salida − entrada |  |
| Dirección del flujo | Identifica sentido de transferencia; el opuesto puede invalidar regla. | Bomba hacia proceso, si corresponde |  |
| Referencia de frecuencia | Explica frecuencia según catálogo; distinga referencia eléctrica y velocidad física. | Compruebe origen del instrumento |  |
| TAG y unidad | Seleccione TAG real y unidad controlada. Cambiar rótulo no convierte datos. Algunos cálculos exigen unidades/versiones específicas. | kW para potencia si corresponde al dato real |  |

### Ejemplo: no confundir indicador con eficiencia

Corriente creciente a igual velocidad puede justificar investigación, pero no calcula eficiencia física. Potencia de entrada, caudal y diferencial pueden habilitar otras reglas con papeles/unidades/alineación adecuados. Si falta instrumento/calificador, lea motivo de indisponibilidad; no invente valores.

[Abrir esta guía en Ayuda instalada](/help?topic=intelligence.semantics)

## Intelligence: consultar resultados y crear dashboards

Elija la pregunta, compruebe datos disponibles y después interprete el indicador.

### Empiece por cobertura

Con asociación guardada y ejecución autorizada, abra cobertura. Introduzca inicio/fin y consulte explícitamente. El formulario usa hora local y respuesta identifica ventana UTC; fin debe seguir inicio. Revise señales obligatorias, muestras aceptadas/rechazadas, calidad y alineación antes de mínimos/máximos/media. Contar muestras no prueba cadencia, completitud ni sincronización.

### Rendimiento: elija regla elegible

Abra Rendimiento y elija regla del catálogo. Opciones deshabilitadas explican señales/papeles/unidades ausentes. No elija otra magnitud solo para obtener número. Complete Desde/Hasta, consulte y revise estado/unidad/ventana/límites. Indisponible no es cero; un indicador no es eficiencia física. Cambiar asociación/revisión/parámetros exige nueva consulta.

### Dos tipos de contexto con límites distintos

Contextos operativos guardados usan límites numéricos inclusivos en unidad actual y evidencia del mismo timestamp UTC exacto de origen. El generador de pantalla de desviación usa selección distinta: 2–8 objetivos y dos contextos numéricos distintos entre sí y de objetivos; al menos cuatro señales utilizables. Sus rangos usan unidad controlada mostrada y límite inferior ≤ valor < superior. No copie automáticamente límites del perfil: unidad y extremo superior pueden diferir.

### Genere pantalla y compruebe la cadena completa

1. Elija asociación/etapa y tipo de pantalla.
2. Complete selecciones y lea motivos si se bloquea.
3. Genere borrador: organiza objetos/referencias, no consulta Historian ni activa.
4. Revise nombre, dimensiones, objetos, vínculos y navegación; Preview y Apply.
5. Guarde/publique/active y abra en Runtime.
6. Configure/ejecute consulta y revise ventana/resultado. Una etapa agrupa miembros sin crear automáticamente KPI agregado ni probar causalidad.

### Cómo investigar resultado vacío

Compruebe en orden: licencia/permisos; alcance guardado/permiso habilitado; proyecto/revisión activa; asociación/TAGs; período; captura Historian; calidad; papeles/unidades; alineación/contexto. Corrija la primera carencia comprobada y consulte. No amplíe límites ni cambie significado solo para obtener resultado. El análisis apoya investigación; no confirma defectos ni predicción validada en campo.

[Abrir esta guía en Ayuda instalada](/help?topic=intelligence.analysis-guide)
