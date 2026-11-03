# Intelligence #853 — conferência local da interface real

Data: 01/11/2026. Coordenação #791; candidata #854 sobre #852. Evidência de software com simulação, sem aceite de campo.

## Ambiente e percurso

API normal do produto, autenticação local, licença assinada exclusiva de teste e PostgreSQL/TimescaleDB em Docker. Banco descartável exclusivo `scadivo_intelligence_browser_853`; nenhuma conexão ao Preview/DEMO. Aplicação sintética criada, salva, publicada e ativada pelas APIs canônicas. Histórico gravado pelo `TimescaleDbHistorian`, com horários de origem sincronizados: 1710 amostras e 1710 pares únicos TAG/horário; nenhuma amostra descartada.

O navegador integrado consultou os endpoints reais pela interface. Um perfil contém corrente e vazão numéricas e modo Enum. O contexto exige modo `RUN`. Não se criou diagnóstico, eficiência, limiar ou fórmula nova.

## Resultados observados

- Referência e comparação: cada sinal numérico recebeu 180 leituras aceitas, sem rejeição. A coorte obrigatória não se aplica ao modo não numérico; a coorte dos sinais numéricos permanece disponível com 180 instantes.
- Contexto: condição de modo com 180 leituras aceitas e 180 correspondentes; corrente e vazão com 180 leituras compatíveis cada. A condição Enum não é confundida com um alvo numérico.
- Desvio conjunto experimental: ajuste disponível, 180 instantes completos de referência, 110 avaliados e 40 de aquecimento. Resultado contínuo, sem classificação ou limiar.
- Após reinício da API, o escopo persistiu e a permissão de processo voltou fechada. A interface exigiu reativação explícita antes de consultar novamente.

## Lacunas de apresentação corrigidas nesta continuação

1. Orientação antiga de Stage 1 dizia que não havia análise, apesar das consultas existentes. Agora explica configuração, ativação e consulta sob demanda.
2. Referência mostrava apenas “Não disponível” e zero para Enum. Agora apresenta o motivo canônico e separa a evidência dos sinais numéricos.
3. Contexto ocultava a evidência das condições. Agora mostra aceitas/correspondentes por condição antes das medições.
4. Painéis usavam controles sem o tema e cores de fallback inadequadas no escuro. Agora usam as variáveis da Engenharia.
5. No celular, o fluxo em colunas excedia a largura da página. O layout corrigido teve `scrollWidth=clientWidth=375` na conferência de 390×844; a tabela experimental tem rolagem local.

Conferência visual desktop 1440×1000 e celular 390×844. Capturas reais guardadas no pacote local de evidências; ainda não são capturas finais aceitas do manual.

## Validação e limites

26/26 regressões das consultas passaram; a regressão focal de contexto/referência incluindo largura mobile passou 2/2. Help 131/131, build Web e alinhamento de 69 arquivos passaram. Help e manual pt-BR/en/es explicam as condições e as contagens.

O CI anterior verde `41315209090` pertence a `c9b7b31c`; não é evidência deste incremento. Registrar o novo head/run em #853 após publicação. PRs continuam draft. Não integrar antes do aceite visual da cadeia #852/#854. Esta conferência não encerra a auditoria completa #791, o KPI agregado entre equipamentos nem o campo adiado #701. HA-D2 não foi desenvolvido nem testado nesta etapa.
