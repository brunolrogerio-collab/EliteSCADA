# Relatórios: autoria e operação

## Relatórios: configurar dados, período e resolução

Comece pela pergunta que o relatório deve responder, antes de desenhar a página.

### Prepare dados que você possa conferir

Comece pela pergunta: quais leituras precisam aparecer e de qual período? Em Engenharia > Relatórios, use primeiro um TAG de pressão cujo histórico já exista e um intervalo curto com valores conhecidos. Assim você confere os dados antes de desenhar a página.

O relatório consulta registros armazenados. Ele não habilita captura nem recupera um passado não gravado. Sua conta precisa de autorização para editar e consultar a fonte escolhida. Para valores use o conjunto de amostras; para transições de alarme, o conjunto próprio de alarmes.

### Primeiro teste, antes do desenho final

1. Escolha consulta incorporada, que pertence ao relatório, e historian.samples, o conjunto de amostras.
2. Adicione um TAG conhecido pelo seletor de referências.
3. Escolha um período curto e resolução Bruto.
4. Execute Preview. Confira linhas, horários, valores e qualidade disponíveis.
5. Mude para Resumo com Máximo e intervalo horário. Compare cada saída com os dados brutos: o máximo do intervalo não é uma lista de todas as leituras.
6. Depois organize a página e valide a definição. Se a prévia não tem linhas, confira TAG, período, captura e permissão antes de mudar cores ou larguras.

### Identificação e fonte da consulta

Crie um relatório ou selecione um existente. Use seletores de referências em vez de inventar IDs. Estes campos configuram a definição; executar uma prévia é uma ação separada.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome, identificador e descrição | Nome aparece na lista; identificador mantém referências estáveis; descrição explica objetivo e contexto. Renomear não cria dados novos. | Pressão da bomba — turno |  |
| Data Query guardada ou consulta incorporada | Uma Data Query reutiliza uma definição existente; incorporada pertence ao relatório. Confira a origem selecionada antes de ajustar o layout. O editor não recebe SQL livre. | Consulta incorporada para o primeiro exemplo |  |
| Fonte de dados / dataset | historian.samples consulta amostras; alarm.events consulta ocorrências/transições de alarmes. Os campos disponíveis dependem da escolha. Trocar dataset pode invalidar campos do desenho. | historian.samples |  |
| Período padrão e período da prévia | O padrão pertence à definição; o período da prévia só escolhe a janela de teste atual. Escolha a unidade oferecida pelo editor. Uma hora equivale a 3600 segundos; não confunda unidade de tempo com quantidade de linhas. | Última hora para conferir poucas amostras |  |
| Dados do relatório / variáveis | Adicione os TAGs pelo navegador de referências. Confira caminho, tipo e unidade. A apresentação de cada variável é configurada separadamente. | TAG de pressão da Bomba 01 |  |
| Linhas por página | Tamanho da página solicitado na consulta; confira a quantidade realmente retornada. Não define por quanto tempo o Historian armazena dados. | 50 | 1–200 |

### Escolha como os dados serão apresentados

Resolução define como recuperar e resumir os registros para esta consulta. Bruto permite ver as amostras armazenadas; as outras opções organizam ou resumem a saída. Confira os valores brutos antes de usar um resumo.

Essa escolha não muda o que já foi capturado, não elimina registros brutos e não configura uma política permanente de agregação do banco.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Bruto, intervalo fixo ou resumo | Bruto examina amostras armazenadas. Intervalo fixo organiza recuperação em passos definidos. Resumo calcula a função escolhida por intervalo. Um valor médio pode ocultar um pico; volte ao bruto para investigar. | Bruto para conferir; resumo horário para visão geral |  |
| Intervalo fixo | Escolha a distância temporal entre posições de consulta. Não é o polling do Driver nem uma garantia de nova amostra em cada posição. Falta de dados continua relevante. | 5 minutos, se suficiente para a pergunta |  |
| Agregação e agrupar cada | Média, mínimo, máximo, soma, contagem, primeiro e último têm significados diferentes. Escolha a largura do intervalo. Somar leituras de vazão não calcula automaticamente volume produzido; isso exigiria integração e unidades apropriadas. | Máximo por hora para procurar picos |  |
| Tabela longa ou larga | Longa organiza amostras em linhas. Larga organiza intervalos para comparar variáveis. No modo bruto, a opção larga fica desabilitada. | Longa para inspecionar amostras |  |

[Abrir este guia na Ajuda instalada](/help?topic=reports.data-designer)

## Relatórios: página, variáveis, campos e resumos

Separe o valor consultado, seu formato e o lugar onde ele será impresso.

### Entenda a página

No Designer, Projeto/Design é onde você monta a página; Prévia/Preview mostra uma execução com dados. Comece com um título e um campo de leitura antes de acrescentar grupos e totais.

As dimensões são em milímetros, como numa página impressa, e não em pixels da Tela Runtime. Retrato é vertical; Paisagem é horizontal. Reserve as margens e faça o conteúdo caber na largura restante. O zoom só muda como você vê a prévia, não o tamanho da impressão.

### Como formatar uma variável

A variável continua ligada ao TAG selecionado. Estes campos mudam a apresentação; não recalibram o instrumento nem convertem dados por simples mudança de rótulo.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Mostrar coluna e ordem | Escolha o que aparece e use Mover acima/abaixo para ordenar. Remover a variável do relatório não exclui o TAG do projeto. | Pressão antes de corrente |  |
| Rótulo visível | Cabeçalho legível da variável, diferente da identidade do TAG. | Pressão de descarga |  |
| Unidade automática, oculta ou rótulo próprio | Automática usa a unidade do TAG. Oculta não a mostra. Rótulo próprio só troca o texto: escrever psi num valor em bar não faz a conversão. | Automática quando a unidade já está correta |  |
| Decimais e formato numérico | Escolha padrão, fixo ou científico. Decimais afetam leitura, não exatidão nem valor armazenado. Não use arredondamento para esconder variação relevante. | Uma casa para décimos | 0–15 casas no editor |
| Data/hora e textos booleanos | Escolha data/hora local, só data, só hora ou ISO 8601. Para booleanos, informe textos de Verdadeiro/Falso coerentes; não chame falta de dados de Falso. | Ligado / Desligado somente se esse for o significado |  |

### Seções e controles do desenho

Etiqueta é um texto fixo, como Pressão de descarga. Campo mostra um dado da consulta. Esses são os controles oferecidos diretamente para inserção; comece por eles.

Coloque o título no cabeçalho do relatório e os campos de registros em Detalhe, a seção que recebe as linhas da consulta. Cabeçalho e rodapé de página repetem em cada página; os de relatório pertencem ao relatório. Os de grupo dependem do agrupamento configurado.

Se a consulta não tem linhas, o título pode aparecer e Detalhe ficar vazio. Confira primeiro os dados antes de tentar resolver esse caso aumentando a altura da seção.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Altura da seção | Espaço reservado para a faixa. Um controle mais alto pode ser cortado; aumentar a altura não cria linhas de dados. | Ajustar a faixa ao conteúdo |  |
| X, Y, largura e altura (mm) | Posição e tamanho no layout. Confira limites da seção e largura útil; números não representam coordenadas do monitor. | Campo dentro da faixa Detalhe |  |
| Etiqueta: texto | Conteúdo fixo, como um título. Para um valor que muda por linha, use Campo; digitar o caminho do TAG numa etiqueta não cria vínculo. | Leituras do turno |  |
| Consulta e campo | Consulta escolhe de qual resultado vêm os dados; Campo precisa existir nesse dataset. Copie o nome exposto pelo schema, por exemplo value ou timestamp para amostras, não um nome inventado. | Consulta main / campo value |  |
| Recurso de imagem | Um controle de imagem depende de um recurso existente e resolvido. Se o seletor não oferecer o recurso, não invente um ID; comece com os controles editáveis e confira a disponibilidade dessa configuração. | Recurso existente quando disponível |  |

### Agrupamento não é agregação temporal

Agrupar por junta linhas pelo campo escolhido e permite ordenar dentro dessa organização. Resolução define os intervalos de tempo usados na consulta. São escolhas diferentes: dar nome a um grupo não muda o intervalo.

Para um resumo, escolha campo, função e alcance: somente o grupo ou o relatório inteiro. Confira se a consulta já resumiu os dados antes de somar de novo. Contagem informa quantidade de registros; não representa horas de funcionamento. Teste um grupo pequeno e confira o total manualmente.

### Da prévia à revisão ativa

1. Monte um título, a seção Detalhe e campos ligados a dados reais.
2. Execute a prévia e confira dados, páginas e cortes.
3. Use Validar e corrija referências ou estrutura inválidas.
4. Aplique somente o candidato válido. Se o projeto mudou durante a edição, atualize e valide novamente.
5. Salve, publique e ative a revisão pelos controles do projeto.
6. Gere o relatório no Runtime com os mesmos parâmetros do teste e compare. A prévia mostra uma execução; ela não salva a definição nem prova que a nova revisão está ativa.

[Abrir este guia na Ajuda instalada](/help?topic=reports.layout-variables)

## Relatórios no Runtime: gerar, conferir e exportar

A saída pertence a uma execução com período e parâmetros próprios.

### Encontre o relatório disponível

Abra Relatórios no Runtime e procure pelo nome. A lista usa a revisão ativa e as permissões da sua sessão. Se o relatório não aparece, confira essas duas condições antes de recriá-lo.

Ter acesso à Engenharia ou ver um objeto de relatório numa tela não concede acesso a todos os dados. O usuário que gera também precisa poder consultar a fonte escolhida.

### Período, parâmetros e execução

1. Selecione o relatório que deseja gerar.
2. Escolha período relativo ou De/Até entre os modos permitidos. Relativo é calculado na hora da execução: repetir mais tarde pode trazer outra janela.
3. Para intervalo absoluto, informe início anterior ao fim e confira o fuso mostrado.
4. Complete os parâmetros com os tipos e opções pedidos. Número, data e booleano não aceitam qualquer texto.
5. Use Gerar relatório e aguarde. Cancelar interrompe a solicitação; não apaga a definição.
6. Antes de interpretar, confira quando foi gerado, qual período foi consultado e quais parâmetros foram usados.

### Prévia, PDF, planilha e impressão

Confira o resultado com anterior/próxima página e zoom. Ajustar à largura muda só a visualização. Use PDF ou impressão para a apresentação em páginas; XLSX e CSV permitem trabalhar com os dados da saída.

São exportações do resultado, não pacotes para importar projeto ou alterar TAGs. Um arquivo exportado representa aquela execução. Para atualizar os dados, gere novamente; abrir o arquivo depois não faz uma consulta ao vivo.

Antes de enviar a planilha ou CSV, confira unidades, datas e decimais no formato que o destinatário espera.

### Problemas que exigem verificações diferentes

Si el informe no aparece, revise publicación/activación y autorización. Si aparece el título sin filas, revise período, filtros y captura. Para un campo vacío o error, confirme campo y consulta referenciados, tipo y parámetros.

Un número inesperado pide revisar resolución, función, unidad y alcance del resumen. Un rótulo distinto no convierte la unidad. Para contenido cortado, revise milímetros, ancho disponible y altura de sección.

Si los datos parecen antiguos, mire la hora de generación y genere de nuevo. No cambie permisos o unidades a ciegas para intentar corregir una salida.

[Abrir este guia na Ajuda instalada](/help?topic=reports.runtime-output)
