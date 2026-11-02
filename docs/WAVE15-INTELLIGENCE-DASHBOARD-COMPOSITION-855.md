# #855 — composição guiada de painéis

Continuação da auditoria #791, candidata sobre #854. A prova anterior de publicação/reinício da visão básica não equivale ao aceite funcional de um dashboard analítico completo.

## Implementação candidata

- O gerador central oferece por TAG os seis objetos genéricos aceitos e valor numérico. Reutiliza escalas válidas do TAG ou exige mínimo/máximo explícitos. Faixas e meta exigem valores do autor dentro da escala. O tema é escolhido separadamente. Nada altera os valores, unidades ou métodos analíticos.
- Para uma associação de equipamento, as seleções existentes de desempenho e desvio contextual podem compor vistas independentes. Não é mais necessário configurar ambas para gerar a que já está disponível. Cada Tela recebe navegação somente para as vistas efetivamente geradas.
- O formulário explica análises não configuradas e fornece acesso à associação. Referência, condições e score conjunto experimental são identificados como consultas disponíveis na Engenharia, sem prometer uma vista Runtime que não existe.
- Help e manual pt-BR/en/es acompanham os campos. O reparo do termo inglês de fundo também elimina a falha do guard editorial no CI anterior a3e0234b.

## Evidência deste incremento

- 22/22 testes locais: seis layouts com escala/tema/unidade, bloqueio sem limites, faixas/meta persistidas, composição parcial com desempenho e sem desvio, vínculos entre Telas, geração pelo formulário montado e gauge no editor canônico.
- Build Web passou. Help focal 4/4 passou. Alinhamento do manual/Help: 69 arquivos.
- A primeira tentativa do teste montado esqueceu de abrir o detalhe da medição antes de escolher o objeto. Corrigiu-se a interação do teste; o formulário não sofreu alteração para contornar a falha.

## Prova com API e PostgreSQL reais — 02/11/2026

A candidata foi exercitada no projeto sintético isolado, sem alterar Preview/DEMO. Foram geradas e persistidas três Telas vinculadas: medições/cobertura, desempenho e desvio contextual. Quatro objetos de medição foram usados no percurso real: gauge, anel, faixas e valor com meta; os seis layouts continuam cobertos pelo teste do gerador.

O Historian recebeu 960 registros Good com horário de origem explicitamente Synchronized, distribuídos em quatro sinais e dois períodos separados de 120 minutos. Para índice de minuto t, a potência foi `1000 + 20*sin(0.8*t)` W, acrescida de 200 W no período de avaliação; a produção foi `2 + 0.1*cos(0.6*t)` m³/s. As condições foram 100 e 300 kPa. A referência usa t=0..119 e a avaliação t=240..359. Os limites declarados no painel de contexto são canônicos: 0..500000 Pa, equivalentes a 0..500 kPa. Uma primeira configuração sintética de 0..500 Pa ficou corretamente fora de faixa; corrigiu-se a configuração, sem flexibilizar a análise.

- Desempenho: HTTP 200, 120 pares qualificados; energia específica média de aproximadamente 0,00016665 kWh/m³. É média aritmética dos resultados pontuais, sem ponderação temporal.
- Desvio: HTTP 200, 120 vetores em cada período dentro das duas condições. Ambos os sinais retornaram 120 valores de desvio. Potência: score robusto entre 8,52 e 10,42; produção: entre -0,95 e 0,95. São resultados descritivos sintéticos, sem diagnóstico ou classificação de falha.
- Os resultados foram apresentados pelo renderer normal do Runtime, consultados explicitamente e vinculados à revisão ativa 17. A API foi reiniciada durante o percurso; as Telas persistiram e o escopo precisou de reativação explícita.
- [Resumo numérico sem credenciais](evidence/intelligence-855/synthetic-results.json), [widgets](evidence/intelligence-855/widgets-runtime.png), [desempenho](evidence/intelligence-855/performance-runtime.png), [desvio](evidence/intelligence-855/deviation-runtime.png). A imagem dos widgets foi capturada na revisão 12, antes das correções de metadados; as consultas finais estão na revisão 17.

### Defeitos de produto encontrados e corrigidos

1. As conversões de `TagEngineeringDto` para `TagDefinition` descartavam `ControlledEngineeringUnit`. A referência canônica agora acompanha o TAG nos 21 conversores de Engenharia e na projeção do Gateway. Não houve mudança de conexão, protocolo ou regras de unidades. Há regressão de compilação Modbus/simulação e de serialização/reativação de memória.
2. Desempenho e cobertura na interface exigiam `scopeKind: "Equipment"`, enquanto a API normal responde `0`. Os validadores aceitam as duas representações exatas de Equipment e continuam verificando identidades, projeto, revisão e janela. Os testes Runtime usam agora a representação numérica real.
3. A prévia de uma Tela gerada isolada perdia os destinos das outras Telas. Prévia/Aplicar incluem o conjunto vinculado inteiro, com guarda de versão e colisão; o editor explica isso antes da aplicação. Há regressão dos quatro destinos no candidato.
4. Os limites presentes no export não eram oferecidos quando a projeção de escopos os omitisse. A interface reutiliza os limites salvos apenas quando ID, caminho, tipo e unidade correspondem. A coleção é memoizada para não reiniciar os painéis durante a consulta.
5. O estilo inline genérico forçava o relatório de desvio a uma linha flexível comprimida. O objeto usa grade de uma coluna e preserva o estado oculto; desempenho estende o conteúdo à largura disponível. As capturas foram conferidas após a correção.

Validação adicional deste reparo: 51/51 testes Chromium passaram (50 do gerador/Engenharia/Runtime de desempenho/cobertura e 1 do desvio/layout); 16/16 regressões focadas .NET passaram; API compilada sem aviso/erro e 69 arquivos do manual alinhados. A execução ampla sem o laboratório L3 terminou com 2099 aprovados, 1 ignorado e timeout de 2 s em `EspHomeReconnectDiagnosticsTests.Disconnect_MarksBadCommunication_ReconnectsAndResubscribes`; esse caso passou isoladamente em 90 ms, sem alteração de parâmetros. Uma execução ampla anterior encontrou três falhas em revogação de leitura, Gateway CSV e bootstrap de sidecar; os 11 casos focados passaram na conferência isolada. Isso não torna a execução ampla verde nem comprova a causa dos timeouts. O teste L3 exige `ELITESCADA_L3_MQTT_HOST` e falhou sem o laboratório; não foi alterado, desabilitado ou apresentado como aprovado.

SHA e CI exatos ficam também no registro vivo da #855/#854. Este percurso não comprova a aceitação completa da #791.

## Continuação — unidades familiares e perfil com duas medições

A autoria do desvio contextual agora usa a unidade mostrada no TAG. O catálogo controlado continua sendo a fonte do fator de conversão; os valores persistidos na Tela e enviados à API continuam canônicos. No percurso real foram digitados 0 e 500 kPa nos dois sinais de pressão, persistidos 0 e 500000 Pa e obtidos novamente 120 vetores em cada período na revisão ativa 19. Quando o TAG não tem texto de unidade, o campo mostra a unidade canônica e recebe valores nela. Unidade controlada ausente ou fator inválido continuam impedindo a seleção do contexto.

Os rótulos explicam Mínimo (incluído) e Máximo (não incluído). Trocar um sinal limpa seus limites para impedir a reutilização acidental de uma faixa em outra grandeza/unidade. A regressão montada confere vírgula decimal (100,5 kPa → 100500 Pa), fator 1, limpeza e bloqueio de overflow na conversão. O contrato `[mínimo, máximo)` e o método não mudaram.

Foi criado outro perfil sintético no mesmo banco isolado, contendo somente potência e produção. Pela interface, a regra de desempenho foi selecionada e o gerador criou duas Telas vinculadas: valores/tendência/cobertura e desempenho. Desvio contextual ficou indisponível com uma explicação de que são necessárias quatro medições distintas; isso não bloqueou as duas vistas elegíveis. A geração não executou consulta analítica.

As duas Telas foram aplicadas, salvas, publicadas e ativadas. A cobertura retornou 120 amostras aceitas por sinal, e o desempenho apresentou 120 pares qualificados. Depois de reiniciar a API, a revisão 23, as identidades e os vínculos das duas Telas persistiram. A consulta respondeu HTTP 409 com `INTELLIGENCE_EXECUTION_PERMIT_REQUIRED` até a reativação explícita; depois o Runtime voltou a apresentar os mesmos 120 pares. Uma primeira expectativa do roteiro usou HTTP 403; foi corrigida para o 409 do contrato, sem alterar o produto.

Evidências: [resumo sem credenciais](evidence/intelligence-855/partial-and-unit-results.json), [entrada em kPa](evidence/intelligence-855/context-input-kpa.png), [gerador parcial](evidence/intelligence-855/partial-generator.png), [cobertura](evidence/intelligence-855/partial-coverage.png), [desempenho após reinício](evidence/intelligence-855/partial-after-restart.png). Não demonstra todas as regras possíveis com poucos sensores; comprova este percurso elegível, sem vibração e sem os dois sensores adicionais de contexto. Campo continua adiado.

Neste incremento passaram 51/51 testes Chromium (gerador, configuração, composição parcial, conversão e Runtime de desempenho/cobertura), 30/30 testes focados de Help, build Web e alinhamento dos 69 arquivos. Os auxiliares API/Web foram encerrados depois da prova; o banco sintético permanece preservado.

O T1 anterior `41382956383`, head `ca028496`, terminou reprovado na auditoria de dependências Z-Wave em Windows/Linux (11 avisos moderados na cadeia que contém ESLint/minimatch). .NET, Web, regressões de Intelligence e Chromium passaram. Não foi falha analítica deste percurso; tampouco foi corrigida ou ignorada. O novo SHA precisa de seu próprio CI e permanece sem autorização de integração baseada em verde anterior.

## Pendências de aceite da #855

| Ponto | Situação |
| --- | --- |
| Escolha dos seis layouts, temas e limites | Seis layouts no gerador; quatro layouts no percurso real persistido; aceite visual final pendente |
| Equipamento com somente uma análise configurada | Prova real com duas medições, consultas, persistência e reinício concluída acima; aceite visual pendente |
| Desempenho e desvio com resultados visíveis e dados sintéticos | Consultas reais e resultados visíveis comprovados acima; falta aceite visual do proprietário |
| Etapas do processo | Visão de TAGs/widgets disponível; composição central das vistas de membros ainda pendente. Ações especializadas existentes permanecem na seção de etapas |
| Referência, condições e score conjunto no Runtime | Consumidores não entregues; consultas da Engenharia não são equivalência funcional no Runtime |
| Aceite visual, figuras finais, integração | Pendentes; não fechar #855/#791 com este incremento |

Preservar contratos congelados, campo adiado, ausência de KPI agregado, segurança/licenciamento e plataforma Windows/Linux. Não alterar Preview/DEMO nem iniciar HA-D2.
