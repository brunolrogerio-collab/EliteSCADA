# Historian: captura e consulta

## Historian: configurar captura e conferir o histórico

Escolha o que gravar, em que condições e como verificar o que realmente foi armazenado.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-HISTORIAN: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=historian.capture-query; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Onde configurar e onde consultar

Engenharia > Historian lista os TAGs com histórico habilitado e resume estratégia, deadband e período. Para alterar a captura de um TAG, abra Engenharia > TAGs, selecione-o e expanda Histórico/Historian no editor. O monitor ao vivo lê o valor corrente; Histórico, Relatórios e Intelligence dependem de amostras armazenadas. Habilitar hoje não recupera o passado.

### Campos de captura do TAG

No campo de estratégia atual, use os nomes aceitos abaixo. Texto desconhecido ou configuração incompleta pode manter a captura legada de todas as observações, em vez da redução de volume esperada. Confira a configuração e as amostras; não suponha economia apenas porque preencheu o campo.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Histórico habilitado | Autoriza a captura desse TAG. Desabilitar não equivale a apagar o que já foi gravado nem cria um backup. | Habilitar o TAG de pressão usado no teste |  |
| Estratégia | periodic captura observações respeitando o período; onChange registra mudança de valor; onChangeDeadband exige variação numérica; onChangeDeadbandMaxInterval acrescenta intervalo máximo entre aceites quando chegam observações. | onChangeDeadbandMaxInterval |  |
| Deadband | Variação mínima absoluta em relação ao último valor aceito, na grandeza capturada. Não é porcentagem nem limite de alarme. Use estratégia numérica compatível. | 0.1 se a resolução desejada for um décimo | Número finito ≥ 0 |
| Período (ms) | Usado pela estratégia periodic. 1000 ms = 1 segundo. Seleciona observações que chegam; não aumenta o polling do Driver nem inventa uma amostra quando a fonte fica muda. | 1000 | Inteiro positivo |
| Período máximo (ms) | Usado por onChangeDeadbandMaxInterval para aceitar uma observação mesmo sem atingir o deadband quando o tempo expirou. Não garante captura durante uma interrupção de comunicação. | 60000 para até um minuto entre aceites com observações chegando | Inteiro positivo |

### Exemplo que mostra o efeito do deadband

Com qualidade constante, deadband 0.1 e último valor aceito 1.00, uma observação 1.03 pode ser descartada pela política; 1.12 ultrapassa o limiar e pode ser aceita. A comparação é com o último aceito, não com toda observação rejeitada. A primeira observação e transições de qualidade têm tratamento próprio e podem ser registradas mesmo sem a variação numérica. No modo com intervalo máximo, uma observação posterior também pode ser aceita pelo tempo. Portanto, não espere contagem calculada só pelo deadband.

### Procedimento de verificação

1. Confira o TAG ao vivo, tipo, escala e qualidade.
2. Habilite Histórico e escolha estratégia/campos válidos.
3. Execute Preview/Apply e salve/publique/ative.
4. Produza variações conhecidas numa fonte de teste e anote o intervalo.
5. Abra Histórico do Runtime no mesmo período, selecione Amostras e filtre esse TAG.
6. Confira valores, qualidade, horários e quantidade; compare com a estratégia.
7. Repita com sinal estável e mudança de qualidade. Para usar em Intelligence, confira também timestamp de origem e alinhamento exigidos pela análise.

### Consultar: período, filtros e páginas

Escolha o dataset certo: amostras não são transições de alarmes. Use um período relativo curto ou De/Até válido; o navegador histórico atual limita a janela a 31 dias por consulta. Depois adicione filtros pelos campos/operadores oferecidos pelo schema. O tipo do valor precisa corresponder ao campo: número, booleano, data, texto ou enum não são intercambiáveis. Ordenação controla a ordem, não o filtro. Navegar páginas continua a consulta, não aumenta o período. Se ficar vazio, retire um filtro por vez e confirme que havia captura nesse horário.

### Captura, retenção, backup e Playback são diferentes

Captura decide quais observações entram. Retenção decide por quanto tempo os registros permanecem. Agregação resume amostras; backup preserva uma cópia recuperável. Os campos de captura não são uma cota global do banco nem uma configuração de arquivamento. Playback apresenta valores históricos na visualização de consulta, não restaura o processo nem autoriza comandos; volte ao modo ao vivo antes de operar. Falta de amostras não significa valor zero ou equipamento desligado.

[Abrir este guia na Ajuda instalada](/help?topic=historian.capture-query)
