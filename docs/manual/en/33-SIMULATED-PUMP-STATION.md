# Walkthrough of the simulated pumping station

Open the EEE application in Runtime with an account allowed to view screens, history and alarms. The example uses two simulated pumps. Labels drawn in this project remain in Portuguese in every interface language.

1. Select **EEE · Início** to open **Visão geral** (overview). Locate pumps 01 and 02. In **Instrumentação**, distinguish station measurements from individual pump readings. In **Elétrica**, check the three station phase currents. To see an individual pump current, return via **EEE · Início** and open its dashboard.

2. Open **Inteligência B1**. Select **corrente (A)** in the trend legend and choose a period when the simulation was recording. A flat line at zero is a valid reading. Repeat in **Inteligência B2**. If a curve is missing, check the selection, time range and Historian capture for that TAG.

3. From each dashboard, open **KPI · Dados simulados**. Select a period with individual power and flow readings recorded together. Check the result in kWh/m³ and the paired reading count. Shared station flow cannot attribute energy use to an individual pump. For this demo revision, select 20 November 2026, 03:00–03:15 (São Paulo), and select **Analyze**. Each pump has 180 paired readings.

4. Open **Desvio B1** or **Desvio B2**. Choose a reference interval before the evaluation interval, with no overlap. For readings recorded by SCADIVO, choose the recording-time basis. Inspect accepted and discarded sets before interpreting the comparison. To reproduce the example on 20 November 2026 (São Paulo), use 22:26–22:31 for reference and 22:32–22:35 for evaluation, with a 10 s tolerance. Select **Analyze**.

5. In **Alarmes e eventos**, look for occurrences in the **Intelligence** area. Check the pump, condition and time. For cleared conditions, open the Runtime history toolbar, select the alarms dataset, choose the time range and run the query. In the advanced filters, look for `EEE.Pump01.Current` or `EEE.Pump02.Current` in the TAG / Path field. The demo activations and returns occurred on 19 November 2026 between 22:52 and 22:53 (São Paulo). Select **EEE · Início** to return to the overview.
