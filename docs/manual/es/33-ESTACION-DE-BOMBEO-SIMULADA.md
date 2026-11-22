# Recorrido por la estación de bombeo simulada

Abra la aplicación EEE en Runtime con una cuenta que permita consultar pantallas, historial y alarmas. El ejemplo utiliza dos bombas simuladas. Las etiquetas dibujadas en el proyecto se muestran en portugués en los tres idiomas.

1. Seleccione **EEE · Início** para abrir **Visão geral** (vista general). Localice las bombas 01 y 02. En **Instrumentação**, distinga las mediciones de la estación de las individuales. En **Elétrica**, compruebe las corrientes de las tres fases de la estación. Para ver la corriente individual, vuelva mediante **EEE · Início** y abra el panel de la bomba.

2. Abra **Inteligência B1**. Seleccione **corrente (A)** en la leyenda de la tendencia y elija un período en el que la simulación estaba registrando datos. Una línea en cero también es una lectura válida. Repita en **Inteligência B2**. Si falta una curva, compruebe la selección, el período y la captura del TAG en Historian.

3. Desde cada panel, abra **KPI · Dados simulados**. Elija un período con lecturas individuales de potencia y caudal registradas juntas. Compruebe el resultado en kWh/m³ y la cantidad de lecturas emparejadas. El caudal compartido de la estación no permite atribuir el consumo a una bomba. En esta revisión de demostración, seleccione el 20 de noviembre de 2026, de 03:00 a 03:15 (São Paulo), y pulse **Analizar**. Hay 180 lecturas emparejadas por bomba.

4. Abra **Desvio B1** o **Desvio B2**. Elija un intervalo de referencia anterior al de evaluación, sin solapamiento. Para lecturas registradas por SCADIVO, elija la base de tiempo de registro. Revise los conjuntos aceptados y descartados antes de interpretar la comparación. Para reproducir el ejemplo del 20 de noviembre de 2026 (São Paulo), use 22:26–22:31 como referencia y 22:32–22:35 como evaluación, con tolerancia de 10 s. Pulse **Analizar**.

5. En **Alarmes e eventos**, busque las ocurrencias del área **Intelligence**. Compruebe la bomba, la condición y la hora. Para condiciones que ya volvieron a la normalidad, abra el historial desde la barra de Runtime, seleccione el conjunto de alarmas, elija el período y ejecute la consulta. En los filtros avanzados, busque `EEE.Pump01.Current` o `EEE.Pump02.Current` en el campo TAG / Path. En la demostración, las activaciones y los retornos ocurrieron el 19 de noviembre de 2026 entre las 22:52 y las 22:53 (São Paulo). Seleccione **EEE · Início** para volver a la vista general.
