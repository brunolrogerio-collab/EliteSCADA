# Relatórios: autoria e operação

## Relatórios: configurar dados, período e resolução

Comece pela pergunta que o relatório deve responder, antes de desenhar a página.

### Prepare dados que você possa conferir

Abra Engenharia > Relatórios. Para o primeiro exemplo, use um TAG de pressão com captura histórica já existente e um período curto em que você conhece os valores. O relatório consulta dados armazenados; ele não liga a captura nem recupera o passado que não foi gravado. Tenha autorização para editar e consultar o dataset escolhido. Um relatório de histórico e um de transições de alarme usam fontes diferentes.

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

A resolução é uma escolha de consulta. Ela não muda o que foi capturado nem elimina dados brutos. Uma agregação de relatório não configura a política permanente de agregação do banco.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Bruto, intervalo fixo ou resumo | Bruto examina amostras armazenadas. Intervalo fixo organiza recuperação em passos definidos. Resumo calcula a função escolhida por intervalo. Um valor médio pode ocultar um pico; volte ao bruto para investigar. | Bruto para conferir; resumo horário para visão geral |  |
| Intervalo fixo | Escolha a distância temporal entre posições de consulta. Não é o polling do Driver nem uma garantia de nova amostra em cada posição. Falta de dados continua relevante. | 5 minutos, se suficiente para a pergunta |  |
| Agregação e agrupar cada | Média, mínimo, máximo, soma, contagem, primeiro e último têm significados diferentes. Escolha a largura do intervalo. Somar leituras de vazão não calcula automaticamente volume produzido; isso exigiria integração e unidades apropriadas. | Máximo por hora para procurar picos |  |
| Tabela longa ou larga | Longa organiza amostras em linhas. Larga organiza intervalos para comparar variáveis. No modo bruto, a opção larga fica desabilitada. | Longa para inspecionar amostras |  |

### Primeiro teste, antes do desenho final

1. Use consulta incorporada e historian.samples.
2. Adicione um TAG conhecido.
3. Escolha período curto e Bruto.
4. Execute Preview e confira linhas, horários, valores e qualidade disponíveis.
5. Mude para Resumo, Máximo e intervalo horário; compare com os dados brutos, sem assumir que as duas saídas são iguais.
6. Só depois organize a página e valide a definição. Se não houver linhas, confirme TAG, período, captura e permissão antes de mudar fontes, larguras ou cores.

[Abrir este guia na Ajuda instalada](/help?topic=reports.data-designer)

## Relatórios: página, variáveis, campos e resumos

Separe o valor consultado, seu formato e o lugar onde ele será impresso.

### Entenda a página

No Designer, use Projeto/Design para editar e Prévia/Preview para ver uma execução. Dimensões são em milímetros, não pixels da Tela Runtime. Retrato é vertical; Paisagem é horizontal. O conteúdo precisa caber na largura útil depois das margens. Mudar o zoom da prévia não muda as dimensões impressas.

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

Cabeçalho e rodapé do relatório valem para o relatório; os de página repetem por página. Detalhe recebe as linhas da consulta. Cabeçalho/rodapé de grupo depende do agrupamento. Se a consulta não retorna linhas, o Detalhe pode ficar vazio mesmo com título visível. Comece com Etiqueta e Campo, os controles oferecidos diretamente para inserção.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Altura da seção | Espaço reservado para a faixa. Um controle mais alto pode ser cortado; aumentar a altura não cria linhas de dados. | Ajustar a faixa ao conteúdo |  |
| X, Y, largura e altura (mm) | Posição e tamanho no layout. Confira limites da seção e largura útil; números não representam coordenadas do monitor. | Campo dentro da faixa Detalhe |  |
| Etiqueta: texto | Conteúdo fixo, como um título. Para um valor que muda por linha, use Campo; digitar o caminho do TAG numa etiqueta não cria vínculo. | Leituras do turno |  |
| Consulta e campo | Consulta escolhe de qual resultado vêm os dados; Campo precisa existir nesse dataset. Copie o nome exposto pelo schema, por exemplo value ou timestamp para amostras, não um nome inventado. | Consulta main / campo value |  |
| Recurso de imagem | Um controle de imagem depende de um recurso existente e resolvido. Se o seletor não oferecer o recurso, não invente um ID; comece com os controles editáveis e confira a disponibilidade dessa configuração. | Recurso existente quando disponível |  |

### Agrupamento não é agregação temporal

Agrupar por organiza linhas pelo campo escolhido, com ordem ascendente/descendente. Um resumo escolhe campo, função e alcance: grupo ou relatório inteiro. A largura de tempo vem de Resolução, não do nome do grupo. Não some o mesmo conjunto novamente sem saber se a consulta já o agregou. Contagem é quantidade de registros; não significa horas de funcionamento. Confira um grupo pequeno à mão antes de confiar no total.

### Da prévia à revisão ativa

1. Monte título, Detalhe e campos reais.
2. Execute a prévia dos dados e confira paginação/cortes.
3. Use Validar para localizar referências e estrutura inválidas.
4. Aplique somente o candidato válido; se o projeto mudar, atualize e valide novamente.
5. Salve, publique e ative a revisão.
6. Gere o relatório no Runtime com os mesmos parâmetros de teste e compare. A prévia executada não é uma definição salva nem garante que a revisão já esteja ativa.

[Abrir este guia na Ajuda instalada](/help?topic=reports.layout-variables)

## Relatórios no Runtime: gerar, conferir e exportar

A saída pertence a uma execução com período e parâmetros próprios.

### Encontre o relatório disponível

Abra Relatórios no Runtime e pesquise pelo nome. A lista depende da revisão ativa e da autorização da sessão. Ter acesso à Engenharia ou ver um objeto de relatório numa Tela não garante permissão de consultar todos os dados. Se a lista estiver vazia, confira revisão e permissões antes de recriar a definição.

### Período, parâmetros e execução

1. Selecione um relatório.
2. Escolha período relativo ou De/Até, conforme os modos permitidos pela definição. Relativo é calculado para a execução; repetir depois pode usar outra janela.
3. Em intervalo absoluto, escolha início anterior ao fim e confira o fuso dos horários.
4. Complete parâmetros com o tipo e as opções exigidos; um campo de número, data ou booleano não aceita qualquer texto.
5. Selecione Gerar relatório e aguarde; cancelar interrompe a solicitação, não apaga a definição.
6. Confira o horário de geração, a janela e os parâmetros usados antes de interpretar a saída.

### Prévia, PDF, planilha e impressão

Use anterior/próxima página e zoom para conferir leitura; ajustar à largura não muda a definição. PDF e impressão servem à apresentação paginada; XLSX e CSV permitem trabalhar com a saída. São exportações do resultado, não pacotes para importar projeto ou alterar TAGs. Para atualizar os dados, gere novamente: abrir um arquivo exportado depois não o transforma numa consulta ao vivo. Confira se a planilha/CSV apresenta unidades, datas e decimais como o destinatário espera.

### Problemas que exigem verificações diferentes

Relatório ausente: revisão e autorização. Cabeçalho visível sem Detalhe: consulta sem linhas, período/filtros/captura. Campo vazio ou erro: campo e consulta referenciados, tipo e parâmetros. Número inesperado: resolução, função, unidade apenas rotulada e escopo de resumo. Layout cortado: milímetros, largura útil e altura da seção. Dados antigos: confira quando a execução foi gerada e gere de novo. Uma falha não deve ser tratada alterando a permissão ou a unidade às cegas.

[Abrir este guia na Ajuda instalada](/help?topic=reports.runtime-output)
