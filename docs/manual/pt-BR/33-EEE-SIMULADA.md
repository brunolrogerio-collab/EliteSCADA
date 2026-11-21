# Percurso prático na EEE simulada

Abra o Runtime da aplicação EEE com uma conta que possa consultar telas, histórico e alarmes. A demonstração usa duas bombas e medições simuladas. Os nomes desenhados no projeto aparecem em português em todos os idiomas.

1. Em **EEE · Início**, abra **Visão geral**. Localize as bombas 01 e 02. Em **Instrumentação**, identifique quais medições pertencem à estação e quais pertencem a cada bomba. Compare a corrente de cada bomba em **Elétrica**.

2. Abra **Inteligência B1**. Na legenda da tendência, selecione **corrente (A)** e escolha um período em que a simulação estava gravando. Uma linha em zero também é uma leitura válida. Repita em **Inteligência B2**. Se faltar uma curva, confira a seleção, o período e a captura do TAG no Historian.

3. Em cada painel, abra **KPI · Dados simulados** e selecione um período com potência e vazão individuais gravadas juntas. Consulte o resultado em kWh/m³ e a quantidade de leituras pareadas. A vazão compartilhada da estação não serve para atribuir consumo a uma bomba.

4. Abra **Desvio B1** ou **Desvio B2**. Escolha um intervalo de referência anterior ao intervalo de avaliação, sem sobreposição. Para leituras registradas pelo SCADIVO, selecione a base de horário de registro. Confira quantos conjuntos foram aceitos e quantos foram descartados antes de interpretar a comparação.

5. Em **Alarmes e eventos**, procure as ocorrências da área **Intelligence** e confira a bomba, a condição e o horário. Se a condição já voltou ao normal, consulte o histórico. Use **EEE · Início** para retornar à visão geral.
