# Historian: captura e consulta

## Historian: configurar captura e conferir o histórico

Escolha o que gravar, em que condições e como verificar o que realmente foi armazenado.

<!-- HELP-REVIEW-AFTER-PRODUCT-779-HISTORIAN: https://github.com/brunolrogerio-collab/EliteSCADA/issues/779; topic=historian.capture-query; fields=; status=WAITING_PRODUCT_IMPLEMENTATION_AND_VALIDATION -->

### Onde configurar e onde consultar

Para ver o que aconteceu antes, o SCADIVO precisa ter gravado os dados. Historian é esse histórico de amostras dos TAGs. O monitor ao vivo mostra o valor corrente; Histórico, Relatórios e Intelligence usam o que foi armazenado. Habilitar hoje não recupera um passado não gravado.

Em Engenharia > Historian, veja os TAGs com captura habilitada e seu resumo. Para mudar como um TAG grava, abra Engenharia > TAGs, selecione-o e expanda Histórico/Historian no editor.

### Exemplo que mostra o efeito do deadband

Deadband é a variação mínima usada para decidir se uma nova observação numérica deve ser gravada. Ele é comparado com o último valor aceito.

Com qualidade constante, deadband 0.1 e último aceito 1.00, uma observação 1.03 pode ser descartada. Já 1.12 ultrapassa o limiar e pode ser aceita. A próxima comparação parte do último aceito, não de cada observação descartada.

A primeira observação e mudanças de qualidade têm tratamento próprio e podem ser gravadas mesmo sem essa variação. Na estratégia com intervalo máximo, uma observação recebida depois do prazo também pode ser aceita. Por isso, não calcule a quantidade esperada de registros usando apenas o deadband.

### Procedimento de verificação

1. Confira o TAG ao vivo: valor, tipo, escala e qualidade.
2. No editor do TAG, habilite Histórico e escolha uma estratégia com seus campos válidos.
3. Execute Preview e Apply; salve, publique e ative a revisão. A edição no rascunho não muda sozinha a captura em execução.
4. Numa fonte de teste, produza variações conhecidas e anote quando ocorreram.
5. Abra Histórico do Runtime, escolha Amostras e filtre o TAG no mesmo período.
6. Compare valores, qualidade, horários e quantidade com a estratégia configurada.
7. Repita com o sinal estável e com mudança de qualidade. Para usar em Intelligence, confira também o timestamp de origem e o alinhamento pedido pela análise.

### Campos de captura do TAG

No campo de estratégia atual, use os nomes aceitos abaixo. Texto desconhecido ou configuração incompleta pode manter a captura legada de todas as observações, em vez da redução de volume esperada. Confira a configuração e as amostras; não suponha economia apenas porque preencheu o campo.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Histórico habilitado | Autoriza a captura desse TAG. Desabilitar não equivale a apagar o que já foi gravado nem cria um backup. | Habilitar o TAG de pressão usado no teste |  |
| Estratégia | periodic captura observações respeitando o período; onChange registra mudança de valor; onChangeDeadband exige variação numérica; onChangeDeadbandMaxInterval acrescenta intervalo máximo entre aceites quando chegam observações. | onChangeDeadbandMaxInterval |  |
| Deadband | Variação mínima absoluta em relação ao último valor aceito, na grandeza capturada. Não é porcentagem nem limite de alarme. Use estratégia numérica compatível. | 0.1 se a resolução desejada for um décimo | Número finito ≥ 0 |
| Período (ms) | Usado pela estratégia periodic. 1000 ms = 1 segundo. Seleciona observações que chegam; não aumenta o polling do Driver nem inventa uma amostra quando a fonte fica muda. | 1000 | Inteiro positivo |
| Período máximo (ms) | Usado por onChangeDeadbandMaxInterval para aceitar uma observação mesmo sem atingir o deadband quando o tempo expirou. Não garante captura durante uma interrupção de comunicação. | 60000 para até um minuto entre aceites com observações chegando | Inteiro positivo |

### Consultar: período, filtros e páginas

Comece escolhendo o conjunto de registros, chamado dataset. Amostras de valores e transições de alarmes são conjuntos diferentes. Selecione um período relativo curto ou preencha De/Até; a janela atual permite até 31 dias por consulta.

Depois use os filtros oferecidos para esse conjunto. Cada campo espera um tipo de valor: número, booleano, data, texto ou enum. Escolha o operador disponível e preencha o tipo correto, em vez de escrever qualquer texto.

Ordenar muda a ordem de apresentação, não seleciona outros registros. Passar de página continua a consulta, não amplia o período. Se não houver resultado, retire um filtro por vez e confira se havia captura no horário.

### Captura, retenção, backup e Playback são diferentes

São decisões diferentes. Captura escolhe quais observações gravar. Retenção define por quanto tempo guardá-las. Agregação resume amostras; backup guarda uma cópia recuperável. Configurar a captura de um TAG não define cota global do banco nem arquivamento.

Playback é a visualização de valores históricos. Ele não restaura o processo nem permite comandar a partir do passado. Volte ao modo ao vivo antes de operar. Falta de amostras não significa zero nem equipamento desligado.

[Abrir este guia na Ajuda instalada](/help?topic=historian.capture-query)
