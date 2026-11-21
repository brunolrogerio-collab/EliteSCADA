# Walkthrough of the simulated pumping station

Open the EEE application in Runtime with an account allowed to view screens, history and alarms. The example uses two simulated pumps. Labels drawn in this project remain in Portuguese in every interface language.

1. Select **EEE · Início** to open **Visão geral** (overview). Locate pumps 01 and 02. In **Instrumentação**, distinguish station measurements from individual pump readings. Compare pump currents in **Elétrica**.

2. Open **Inteligência B1**. Select **corrente (A)** in the trend legend and choose a period when the simulation was recording. A flat line at zero is a valid reading. Repeat in **Inteligência B2**. If a curve is missing, check the selection, time range and Historian capture for that TAG.

3. From each dashboard, open **KPI · Dados simulados**. Select a period with individual power and flow readings recorded together. Check the result in kWh/m³ and the paired reading count. Shared station flow cannot attribute energy use to an individual pump.

4. Open **Desvio B1** or **Desvio B2**. Choose a reference interval before the evaluation interval, with no overlap. For readings recorded by SCADIVO, choose the recording-time basis. Inspect accepted and discarded sets before interpreting the comparison.

5. In **Alarmes e eventos**, look for occurrences in the **Intelligence** area. Check the pump, condition and time. If the condition has already cleared, consult alarm history. Select **EEE · Início** to return to the overview.
