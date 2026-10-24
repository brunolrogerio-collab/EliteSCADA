# Inteligencia de Activos y Procesos

Este capítulo describe los flujos disponibles actualmente. Inteligencia es opcional: la licencia habilita la función, pero usted configura los equipos, perfiles y etapas que pretende analizar. No necesita aplicar análisis a toda la planta.

**Ayuda contextual:** `/help?topic=intelligence.overview`.

## 1. Empezar por el equipo y los TAGs

Antes de crear un perfil, identifique el recurso estable de Equipo y los TAGs que representan mediciones útiles, como corriente, tensión, presión, caudal o temperatura. Revise unidad, tipo, quality, timestamp y captura de Historian para el período que va a analizar. No incluya una señal solo porque esté disponible; indique qué pregunta operacional ayuda a responder.

Para editar se requieren `EngineeringModify` y `AssetProcessIntelligenceManage` en el scope del recurso. Las funciones de Inteligencia también requieren la licencia correspondiente.

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
