# Intelligence Stage 4F — modelo de dados e critérios

Este pacote contém um modelo vazio para preparar a avaliação do Stage 4F. Ele não contém dados de planta nem implementa a análise conjunta. O plano aceito do Stage 4F define as metas iniciais de avaliação e os controles de validação; a escolha final do método, sua calibração, a suficiência estatística e o desempenho continuam dependentes da análise de uma coorte representativa autorizada. O gate #655 permanece **NOT READY** até que essa evidência seja avaliada.

## Arquivos

- `intelligence-stage4f-data-template.xlsx`: abas Leia-me, Observações, Mapa TAG, Coortes, Critérios e Dicionário.
- `historian-observations.csv`: cabeçalho vazio para observações em formato longo, uma linha por amostra.

O CSV usa os campos do contrato `historian.samples.v2` como base. `export.id` e `cohort.id` são anotações desta coleta, e `value.kind` achata `cells.value.kind` do envelope JSON. Essas três colunas não são campos consultáveis adicionais do produto. A unidade e o contexto de perfil ficam na aba Mapa TAG, pois não fazem parte de cada linha da resposta Historian v2.

Para uma verificação local e estrutural do CSV preparado, use o [Stage 4F input preflight](../../research/intelligence-stage4f-input-preflight/README.md). Ele só verifica formato/contrato; não aprova elegibilidade analítica nem prontidão.

## Mapeamento do Historian v2

O contrato está definido em [`HistoricalQueryContracts.cs`](../../../src/Scada.Core/HistoricalQueries/HistoricalQueryContracts.cs) e a projeção do provider em [`TimescaleHistoricalQueryProvider.cs`](../../../src/Scada.Historian.TimescaleDb/TimescaleHistoricalQueryProvider.cs). As colunas nativas relevantes são:

`tag.id`, `tag.path`, `quality`, `value`, `timestamp`, `source.timestamp`, `source.timestamp.status`, `server.timestamp`, `server.timestamp.status`, `capture.policy.profileId`, `capture.policy.fingerprint` e `capture.policy.status`.

- `timestamp` é o instante observado/publicado pelo EliteSCADA (`TagValue.Timestamp` / Historian `ts`). Não o trate como horário de aquisição do dispositivo.
- `source.timestamp` é opcional e distinto. Preserve seu estado explícito de evidência: `NotRecorded`, `NotProvided`, `PresentUnverified`, `Unsynchronized` ou `Synchronized`. Não infira `Synchronized` por proximidade, pelo nome do driver ou pelo relógio do servidor.
- `server.timestamp` também é separado. Ele não comprova sincronização do relógio de origem.
- `quality` deve permanecer como veio da fonte. No EliteSCADA os códigos incluem `Good`, `Uncertain`, `Bad`, `BadCommunication`, `BadConfiguration`, `BadDevice`, `Stale`, `Disabled` e `Unavailable`. Não converta qualidade diferente de Good em zero, Good ou linha ausente.
- Para achatar o JSON, mantenha `value.kind` junto de `value`. Use decimal invariável com ponto quando o tipo for numérico; preserve `null`, string vazia e zero como estados diferentes.
- Escreva timestamps como ISO 8601 em UTC, com `Z` ou `+00:00`, preservando a precisão disponível. Se a origem não tiver fuso ou evidência de sincronização, registre a limitação; não faça conversão silenciosa do horário local.
- `capture.policy.*` identifica a política de admissão de armazenamento. Não demonstra intervalo de aquisição, completude ou ausência de lacunas. A cadência de origem só pode ser preenchida no Mapa TAG quando houver uma fonte externa documentada. Consulte [`ADR-014`](../../ADR-014-ASSET-PROCESS-INTELLIGENCE-TEMPORAL-PROVENANCE.md).

Para outra plataforma, mapeie campos apenas quando a semântica for equivalente e registre o sistema, versão, filtros e conversões. Não renomeie o timestamp observado da outra plataforma como timestamp de origem. Se ela não fornece evidência de sincronização, não marque as linhas como sincronizadas.

## Coortes e validação

Use uma coorte por intervalo/perfil/contexto claramente delimitado. Rótulos de operação, não-falha, degradação e falha precisam de uma referência independente do resultado do detector. Se a condição não for conhecida, use `unknown_unlabeled`. Os códigos disponíveis são `normal_variation`, `known_nonfault`, `sensor_quality_loss`, `synchronized`, `misaligned_unsynchronized`, `correlated_signals`, `injected_single_signal`, `injected_multi_signal`, `known_degradation`, `known_fault`, `synthetic_fixture` e `unknown_unlabeled`. A aba Dicionário descreve cada código. `synchronized` só deve ser usado quando a fonte fornecer evidência explícita; os demais estados permanecem distintos.

O [`plano de aceitação do Stage 4F`](../../WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md) é a referência para metas numéricas, regra de episódios, cenários, separação temporal de dados, tratamento de incerteza e limites de retenção. Este modelo não solicita que o Product Owner defina novamente essas decisões.

Antes de avaliar uma coleta específica, o responsável pelos dados e a equipe que a preparou devem registrar:

1. O equipamento, perfil, contexto, TAGs e unidades efetivamente cobertos, com a versão e a vigência das configurações aplicáveis.
2. O sistema e a versão de origem, intervalo consultado, filtros, ordenação e proveniência dos timestamps. Preserve a distinção entre instante observado, instante de origem e instante de servidor.
3. A referência de autorização para esse uso, finalidade, local aprovado, pessoas com acesso e forma de expurgo. Aplique os limites do plano aceito e qualquer regra mais restritiva do responsável pelos dados.
4. A fonte independente que sustenta cada rótulo de condição. Use `unknown_unlabeled` quando a condição não for conhecida.
5. O manifesto de separação da coorte em blocos temporais completos, sem dividir o mesmo evento, execução ou janelas sobrepostas entre partições.
6. A cobertura, lacunas, qualidade e limitações observadas, sem imputação ou promoção silenciosa de evidência.

Falso alarme deve ser medido em dados retidos de operação normal e variação não-falha. Sensibilidade deve ser medida separadamente por cenário conhecido e validado. Dados sintéticos podem provar comportamento determinístico de teste, mas não substituem evidência de campo. O Stage 4G continua um gate separado.

## Proteção dos dados

Não adicione dados operacionais reais, credenciais, configurações sensíveis ou referências que revelem segredo industrial a este repositório, PR ou issue. Guarde o export bruto somente no local privado aprovado pelo responsável dos dados. No GitHub, registre apenas o identificador da coleta e a referência de autorização, sem anexar o histórico. O arquivo CSV deste pacote tem somente o cabeçalho.

Este modelo não é um formato de importação do produto nem uma declaração de que 4F está pronto, validado ou completo. Depois que houver autorização específica e uma coorte preparada com a proveniência exigida, Main revisará a evidência e decidirá sobre uma ordem de implementação 4F separada. A validação/fechamento 4G permanece posterior e independente.
