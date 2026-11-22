# Percurso prático na EEE simulada

Abra o Runtime da aplicação EEE com uma conta que possa consultar telas, histórico e alarmes. A demonstração usa duas bombas e medições simuladas. Os nomes desenhados no projeto aparecem em português em todos os idiomas.

1. Em **EEE · Início**, abra **Visão geral**. Localize as bombas 01 e 02. Em **Instrumentação**, identifique quais medições pertencem à estação e quais pertencem a cada bomba. Em **Elétrica**, confira as correntes das três fases da estação. Para ver a corrente individual, volte por **EEE · Início** e abra o painel da bomba.

2. Abra **Inteligência B1**. Na legenda da tendência, selecione **corrente (A)** e escolha um período em que a simulação estava gravando. Uma linha em zero também é uma leitura válida. Repita em **Inteligência B2**. Se faltar uma curva, confira a seleção, o período e a captura do TAG no Historian.

3. Em cada painel, abra **KPI · Dados simulados** e selecione um período com potência e vazão individuais gravadas juntas. Consulte o resultado em kWh/m³ e a quantidade de leituras pareadas. A vazão compartilhada da estação não serve para atribuir consumo a uma bomba. Na revisão de demonstração, use 20/11/2026, das 03:00 às 03:15 (São Paulo), e selecione **Analisar**. Há 180 leituras pareadas por bomba.

4. Abra **Desvio B1** ou **Desvio B2**. Escolha um intervalo de referência anterior ao intervalo de avaliação, sem sobreposição. Para leituras registradas pelo SCADIVO, selecione a base de horário de registro. Confira quantos conjuntos foram aceitos e quantos foram descartados antes de interpretar a comparação. Para reproduzir o exemplo em 20/11/2026 (São Paulo), use 22:26–22:31 como referência e 22:32–22:35 como avaliação, com tolerância de 10 s. Selecione **Analisar**.

5. Em **Alarmes e eventos**, procure as ocorrências da área **Intelligence** e confira a bomba, a condição e o horário. Para condições que já voltaram ao normal, abra **Histórico** na barra do Runtime, selecione **Alarmes**, escolha o período e clique em **Consultar**. Nos filtros avançados, procure `EEE.Pump01.Current` ou `EEE.Pump02.Current` no campo TAG / Path. Na demonstração, os disparos e retornos estão em 19/11/2026, entre 22:52 e 22:53 (São Paulo). Use **EEE · Início** para retornar à visão geral.
