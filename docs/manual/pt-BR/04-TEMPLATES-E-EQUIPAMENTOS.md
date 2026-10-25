# Templates e Equipamentos

## Templates: criar um faceplate reutilizável

Desenhe uma vez e entenda como a alteração chega aos equipamentos vinculados.

### Entenda o que está criando

Um Template Faceplate é uma definição gráfica compartilhada: por exemplo, o desenho de uma bomba com nome, estado e leitura. Equipamento é a identidade de uma bomba concreta. Você pode ter Bomba 01 e Bomba 02 usando o mesmo Template, e pode criar um equipamento sem Template. Um perfil da Intelligence organiza medições e não substitui esse desenho.

### Onde entrar e o que preencher

Abra Engenharia > Templates. O editor usa os mesmos objetos, Propriedades, Dinâmicas e Eventos das Telas. Tenha permissão de edição e o bloqueio de edição quando solicitado.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome do Template | Nome legível na biblioteca. Descreva a finalidade, não uma única instância física. | Bomba com estado e pressão |  |
| Identificador | Referência estável usada pelos vínculos. Preserve-a ao atualizar o desenho; mudar só o nome não exige criar outro equipamento. | pump-faceplate |  |
| Objetos e vínculos | Composição do desenho. Um TAG fixo continua apontando para o mesmo TAG quando o desenho é reutilizado. Confira as referências de cada instância antes de representar outro equipamento. | Título, estado e valor exibido |  |

### Procedimento: criar e reutilizar

1. Selecione Novo Template e preencha nome e identificador.
2. Insira um conjunto pequeno de objetos e configure suas propriedades.
3. Adicione vínculos/dinâmicas/eventos necessários; não presuma substituição automática de TAGs pelo nome do equipamento.
4. Execute Preview, corrija erros e aplique ao projeto.
5. Abra Equipamentos, crie ou selecione uma instância e escolha esse Template opcional. Salve a associação.
6. No editor de Tela, use a biblioteca de equipamentos/faceplates para inserir a instância, quando disponível.
7. Salve, publique e ative a revisão. Confira duas instâncias e suas leituras no Runtime.

### Atualizar sem confundir definição e instância

Editar o Template compartilhado atualiza o desenho acompanhado por todas as instâncias vinculadas na revisão correspondente. Revise o impacto antes de aplicar; para uma aparência específica, considere outro Template. Alterar o vínculo do equipamento muda seu desenho associado e preserva a identidade do equipamento. Um Dínamo é outra forma de conjunto visual reutilizável; um Grupo apenas organiza objetos.

### Se não encontrar ou não atualizar

Confira se o Template foi aplicado ao projeto, se o equipamento aponta para a definição correta e se a revisão foi ativada. Uma cópia independente de objetos não ganha vínculo por ter o mesmo nome. Quando a validação informar mudança do projeto, atualize e valide novamente. Se aparecer o valor de outra bomba, confira referências, não apenas o título.

[Abrir este guia na Ajuda instalada](/help?topic=templates.overview)

## Equipamentos: criar, identificar e associar

Cadastre o ativo real, depois escolha desenho e medições que pertencem a ele.

### Comece pela identidade do ativo

Abra Engenharia > Equipamentos. Equipamento representa uma instância real, como Bomba 01. Não é o Driver, o TAG nem o desenho. Você pode criá-lo sem faceplate e associar um Template depois. Intelligence usa essa identidade nas associações de perfil; desenhar uma bomba numa Tela não cadastra esse equipamento.

### Campos da instância

O formulário mostra nome e Template opcional. O caminho é gerado pelo produto, não precisa ser inventado nem digitado para criar a instância.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome do equipamento | Rótulo para encontrar o ativo. Use nomes que diferenciem instâncias. | Bomba 01 |  |
| Caminho gerado | Referência estável mostrada para conferência. Ao editar o nome de uma instância existente, o caminho é preservado. Um nome que gere caminho já usado bloqueia uma nova criação. | Conferir a prévia do caminho |  |
| Template Faceplate (opcional) | Seleciona o desenho compartilhado. Sem Template é válido; remover o vínculo não exclui o equipamento. A troca não escolhe automaticamente TAGs nem perfis da Intelligence. | Template Bomba ou Sem Template |  |

### Procedimento: cadastrar e conferir

1. Informe um nome distinto.
2. Confira o caminho gerado; escolha Template ou deixe Sem Template.
3. Selecione Criar equipamento. O produto valida a alteração antes de aplicá-la ao projeto.
4. Selecione a instância na lista para editar nome ou vínculo; use Salvar alterações.
5. Salve/publique/ative a revisão quando o equipamento precisar estar disponível no Runtime.
6. Na Intelligence, escolha a identidade desse equipamento e associe o perfil de medições. Configure TAGs e captura antes de consultar análise.

### Verificação e erros frequentes

A instância deve aparecer na lista com nome, caminho e vínculo correto. Sem Template não significa equipamento inválido. Template indisponível indica vínculo sem definição resolvida; selecione um Template existente ou retire a associação. Caminho duplicado exige um nome distinto para a nova instância. Conflito de edição exige atualizar e revisar, não criar cópias até a mensagem desaparecer.

[Abrir este guia na Ajuda instalada](/help?topic=equipment.overview)
