# Stage 4F — resultados do estudo offline

**Estado:** experimento executado; **INSUFFICIENT EVIDENCE** para aceitação, qualidade de produto, implantação ou decisão de método. Nenhuma variante foi selecionada como vencedora.

## Cadeia congelada e execução

- Fonte: UCI 447, DOI `10.24432/C5CW21`; SHA-256 `24128AAD2EE45EEA7E6B63EBBD9992CDF25D0483A2CEBEFBFC13BC69079AF1F2`.
- Protocolo e divisão pré-ajustados: commits `78dbe2fd470d874fe1523210ef1e2d165c594e3b` e `bf9b78f87d6e94ee3ee2607dd83e2725735f88bb`; correção de portabilidade do harness: `ca2fb6c97d64fd7ee7dafb9c53e567bb073119f2`.
- Configuração fit/calibration congelada **antes** do holdout: commit `e4a3e11b696da78ccaa74669cbb01d38d2ddf06e`, SHA-256 `F0D2F8DED8F5969CD73D1BBFC549C8B621D70809461B243FF2F442C04815DF75`; `holdoutOpened=false` no arquivo congelado.
- O holdout foi aberto uma vez, depois desse commit, usando o mesmo arquivo e configuração congelada. Saída versionada em `holdout-results.json`; o JSON mantém somente estatísticas agregadas por ciclo e candidato, sem scores por registro.
- **Hardening pós-execução** (commits `7ec7d04fa020422af261f278f2b5b89e827edfe9`, `a2a7cfcf9bb2b59b7258b5d9692dac24b9aa58e1` e `fa51d9d286ca7bb050991990b8b09261e6fd428f`): a reivindicação atômica local agora usa a identidade fonte + SHA-256 do manifesto + ciclos de holdout, e o recibo versionado `holdout-consumption-receipt.json` mantém essa proveniência disponível também em clones novos. O mesmo arquivo e partição são recusados mesmo que se altere apenas a configuração congelada; uma nova partição sobre o mesmo arquivo exige autorização versionada vinculada ao protocolo, split e configuração novos (`holdout-reopen-authorization.json`). A validação do arquivo-fonte precede a reivindicação local; a reivindicação precede qualquer leitura de linhas do holdout. Esse hardening veio **depois** da execução registrada: os resultados preservados foram produzidos pelo estudo no commit `ca2fb6c97d64fd7ee7dafb9c53e567bb073119f2`. Nenhum ciclo real foi relido para adicioná-lo; `frozen-config.json` e `holdout-results.json` permanecem sem alteração.
- Fit/calibration Windows 11 / Python 3.14.6: 4,55 s e pico RSS 106.541.056 bytes. Comparação fit/calibration em Docker Linux amd64, Python 3.12.15, sem rede, limite de um CPU: 16,81 s e 94.314.496 bytes. Os 1.138 valores numéricos do modelo/calibração coincidiram exatamente (delta absoluto máximo 0); digests completos diferem porque registram horário e runtime.
- Holdout Windows 11 / Python 3.14.6: 2,52 s e pico RSS 34.992.128 bytes.

## Contagens no holdout

Cada dimensão contém 12.000 posições de vetor nos ciclos 1796 e 1797. “Excedências” compara os scores com o quantil 99% calibrado nos ciclos 1794–1795. São excedências de referência, não falsos positivos. Percentuais são apresentados apenas para descrever esse recorte; não estimam desempenho operacional.

| Dimensão | Método | λ | Posto / condição regularizada | Limite q99 | Excedências por ciclo 1796 / 1797 | Total / 12.000 |
| ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 3 | Conjunto | 0,05 | 3 / 568,87 | 25,0402 | 56 / 59 | 115 (0,958%) |
| 3 | Conjunto | 0,10 | 3 / 352,26 | 23,0656 | 50 / 55 | 105 (0,875%) |
| 3 | Conjunto | 0,25 | 3 / 144,40 | 20,7085 | 63 / 63 | 126 (1,050%) |
| 3 | Conjunto | 0,50 | 3 / 52,67 | 21,4462 | 60 / 58 | 118 (0,983%) |
| 3 | Controle marginal | — | — | 19,3223 | 0 / 0 | 0 (0,000%) |
| 5 | Conjunto | 0,05 | 5 / 1.705,49 | 32,1290 | 59 / 65 | 124 (1,033%) |
| 5 | Conjunto | 0,10 | 5 / 967,88 | 27,2855 | 61 / 67 | 128 (1,067%) |
| 5 | Conjunto | 0,25 | 5 / 366,63 | 26,6446 | 63 / 67 | 130 (1,083%) |
| 5 | Conjunto | 0,50 | 5 / 128,59 | 33,4949 | 61 / 65 | 126 (1,050%) |
| 5 | Controle marginal | — | — | 22,2100 | 36 / 37 | 73 (0,608%) |
| 7 | Conjunto | 0,05 | 7 / 1.771,80 | 32,6653 | 60 / 66 | 126 (1,050%) |
| 7 | Conjunto | 0,10 | 7 / 988,89 | 28,0309 | 64 / 65 | 129 (1,075%) |
| 7 | Conjunto | 0,25 | 7 / 369,60 | 27,3462 | 64 / 69 | 133 (1,108%) |
| 7 | Conjunto | 0,50 | 7 / 128,96 | 34,1758 | 61 / 64 | 125 (1,042%) |
| 7 | Controle marginal | — | — | 22,2100 | 36 / 37 | 73 (0,608%) |

O posto da covariância não regularizada foi completo para cada dimensão (3/3, 5/5, 7/7). Condições não regularizadas: 1.277,14; 5.439,55; 6.176,74, respectivamente. Regularizar reduziu a condição numérica na grade, mas isso, sozinho, não demonstra melhor detecção, robustez de campo ou que uma escolha de λ seja superior.

## Interpretação e limites

- O controle marginal da dimensão 3 não excedeu seu limiar; os candidatos conjuntos tiveram contagens diferentes. Isso é uma observação descritiva deste recorte, não evidência de que o conjunto detectou falhas ou de que o controle perdeu eventos.
- A fonte não fornece rótulos independentes de saudável/falha, timestamps UTC por amostra, qualidade `Good` ou estado `Synchronized`. A indicação `stable=0` descreve condição estável do ensaio, não verdade-terreno independente de operação sem falha.
- Há somente dois ciclos de calibração e dois de holdout, adjacentes no arquivo. Ciclos são grupos, não garantem independência nem intervalo de tempo. Não cabem aqui intervalos de confiança confiáveis, taxa de falso positivo validada, episódios por hora, sensibilidade a eventos ou latência.
- Critérios de ≥168 horas saudáveis em 14 dias, ≥100 blocos independentes por classe, recall ≥90% e latência ≤10 min continuam **não medidos**. Não se converte a duração de um ciclo em exposição contínua.
- A pesquisa lê os arquivos diretamente e não importa registros no EliteSCADA, não chama API de produto, não altera `MaximumRawRowsAcrossQuery = 50.000` e não muda runtime, UI, catálogo, licença, banco, HA ou drivers.

Portanto, Stage 4F demonstra somente que o harness offline consegue ajustar, calibrar e comparar de forma reproduzível duas formas de score nesta pequena amostra. São necessários dados operacionais elegíveis, rótulos e exposição temporal para qualquer validação de desempenho; isso fica fora desta etapa.
