# Recorrido por la estación de bombeo simulada

Abra la aplicación EEE en Runtime con una cuenta que permita consultar pantallas, historial y alarmas. El ejemplo utiliza dos bombas simuladas. Las etiquetas dibujadas en el proyecto se muestran en portugués en los tres idiomas.

1. Seleccione **EEE · Início** para abrir **Visão geral** (vista general). Localice las bombas 01 y 02. En **Instrumentação**, distinga las mediciones de la estación de las individuales. Compare las corrientes de las bombas en **Elétrica**.

2. Abra **Inteligência B1**. Seleccione **corrente (A)** en la leyenda de la tendencia y elija un período en el que la simulación estaba registrando datos. Una línea en cero también es una lectura válida. Repita en **Inteligência B2**. Si falta una curva, compruebe la selección, el período y la captura del TAG en Historian.

3. Desde cada panel, abra **KPI · Dados simulados**. Elija un período con lecturas individuales de potencia y caudal registradas juntas. Compruebe el resultado en kWh/m³ y la cantidad de lecturas emparejadas. El caudal compartido de la estación no permite atribuir el consumo a una bomba.

4. Abra **Desvio B1** o **Desvio B2**. Elija un intervalo de referencia anterior al de evaluación, sin solapamiento. Para lecturas registradas por SCADIVO, elija la base de tiempo de registro. Revise los conjuntos aceptados y descartados antes de interpretar la comparación.

5. En **Alarmes e eventos**, busque las ocurrencias del área **Intelligence**. Compruebe la bomba, la condición y la hora. Si la condición ya volvió a la normalidad, consulte el historial. Seleccione **EEE · Início** para volver a la vista general.
