# Templates e Equipamentos

## Templates: criar um faceplate reutilizável

Desenhe uma vez e entenda como a alteração chega aos equipamentos vinculados.

### Entenda o que está criando

Use um Template Faceplate quando várias máquinas precisam do mesmo desenho. Por exemplo, monte uma bomba com nome, estado e leitura e reutilize esse desenho para Bomba 01 e Bomba 02. Template é a definição gráfica compartilhada; cada Equipamento identifica uma bomba concreta.

Você pode cadastrar o equipamento sem Template e escolher o desenho depois. O perfil da Intelligence tem outra função: reúne medições para análise, não substitui o desenho.

### Procedimento: criar e reutilizar

1. Em Engenharia > Templates, use Novo Template e preencha nome e identificador.
2. Comece com poucos objetos: título, estado e uma leitura, por exemplo. Ajuste suas propriedades no editor.
3. Configure os vínculos de dados, as mudanças visuais e os eventos necessários. O nome do equipamento não substitui automaticamente os TAGs escolhidos.
4. Execute Preview, corrija erros e aplique ao projeto.
5. Em Equipamentos, crie ou selecione uma instância real e escolha esse Template opcional. Salve a associação.
6. No editor de Tela, use a biblioteca de equipamentos/faceplates para inserir a instância, quando disponível.
7. Salve, publique e ative a revisão. No Runtime, confira duas instâncias: o desenho pode ser igual, mas cada leitura deve pertencer ao equipamento correto.

### Onde entrar e o que preencher

Abra Engenharia > Templates. O editor usa os mesmos objetos, Propriedades, Dinâmicas e Eventos das Telas. Tenha permissão de edição e o bloqueio de edição quando solicitado.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome do Template | Nome legível na biblioteca. Descreva a finalidade, não uma única instância física. | Bomba com estado e pressão |  |
| Identificador | Referência estável usada pelos vínculos. Preserve-a ao atualizar o desenho; mudar só o nome não exige criar outro equipamento. | pump-faceplate |  |
| Objetos e vínculos | Composição do desenho. Um TAG fixo continua apontando para o mesmo TAG quando o desenho é reutilizado. Confira as referências de cada instância antes de representar outro equipamento. | Título, estado e valor exibido |  |

### Atualizar sem confundir definição e instância

Uma mudança na definição compartilhada pode aparecer em todas as instâncias vinculadas quando a revisão correspondente entrar em uso. Antes de aplicar, confira quais equipamentos usam esse Template. Para uma aparência diferente só numa parte, considere outra definição.

Trocar o Template associado muda o desenho e conserva a identidade do equipamento. Dínamo é outra forma de conjunto visual reutilizável. Grupo apenas organiza objetos; ele não tem o mesmo vínculo com uma definição compartilhada.

### Se não encontrar ou não atualizar

Confira se o Template foi aplicado ao projeto, se o equipamento aponta para a definição correta e se a revisão foi ativada. Uma cópia independente de objetos não ganha vínculo por ter o mesmo nome. Quando a validação informar mudança do projeto, atualize e valide novamente. Se aparecer o valor de outra bomba, confira referências, não apenas o título.

[Abrir este guia na Ajuda instalada](/help?topic=templates.overview)

## Equipamentos: criar, identificar e associar

Cadastre o ativo real, depois escolha desenho e medições que pertencem a ele.

### Comece pela identidade do ativo

Comece cadastrando a máquina que existe na planta, como Bomba 01. Em Engenharia > Equipamentos, esse cadastro dá uma identidade à instância real. Ele é separado da conexão, dos TAGs e do desenho que você colocará na tela.

Você pode criar o equipamento sem faceplate e associar um Template depois. Intelligence usa essa identidade ao associar perfis. Desenhar uma bomba numa Tela não cadastra automaticamente o equipamento.

### Procedimento: cadastrar e conferir

1. Informe um nome distinto.
2. Confira o caminho gerado; escolha Template ou deixe Sem Template.
3. Selecione Criar equipamento. O produto valida a alteração antes de aplicá-la ao projeto.
4. Selecione a instância na lista para editar nome ou vínculo; use Salvar alterações.
5. Salve/publique/ative a revisão quando o equipamento precisar estar disponível no Runtime.
6. Na Intelligence, escolha a identidade desse equipamento e associe o perfil de medições. Configure TAGs e captura antes de consultar análise.

### Campos da instância

O formulário mostra nome e Template opcional. O caminho é gerado pelo produto, não precisa ser inventado nem digitado para criar a instância.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome do equipamento | Rótulo para encontrar o ativo. Use nomes que diferenciem instâncias. | Bomba 01 |  |
| Caminho gerado | Referência estável mostrada para conferência. Ao editar o nome de uma instância existente, o caminho é preservado. Um nome que gere caminho já usado bloqueia uma nova criação. | Conferir a prévia do caminho |  |
| Template Faceplate (opcional) | Seleciona o desenho compartilhado. Sem Template é válido; remover o vínculo não exclui o equipamento. A troca não escolhe automaticamente TAGs nem perfis da Intelligence. | Template Bomba ou Sem Template |  |

### Verificação e erros frequentes

A instância deve aparecer na lista com nome, caminho e vínculo correto. Sem Template não significa equipamento inválido. Template indisponível indica vínculo sem definição resolvida; selecione um Template existente ou retire a associação. Caminho duplicado exige um nome distinto para a nova instância. Conflito de edição exige atualizar e revisar, não criar cópias até a mensagem desaparecer.

[Abrir este guia na Ajuda instalada](/help?topic=equipment.overview)
