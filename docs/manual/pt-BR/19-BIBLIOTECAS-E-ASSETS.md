# Bibliotecas e assets: reutilizar e incorporar

## Bibliotecas: associar, conferir e incorporar recursos

Reutilize recursos sem confundir o catálogo com o conteúdo efetivo do projeto.

### Escolha o tipo de reutilização

Uma biblioteca .escadalib é um conjunto de recursos reutilizáveis, como Template, Dínamo, Tela, Popup, Script ou arquivo gráfico. Ela inclui dependências, os outros recursos necessários para usar o item escolhido. Não é um projeto .escadapkg completo, backup de banco nem Authority.

Associar a biblioteca apenas a torna disponível no catálogo. Preview visual mostra a aparência sem copiar para o projeto. Usar/incorporar copia o recurso e as dependências necessárias para Working, o rascunho do projeto. Só então passam a fazer parte dele. No catálogo, confira se está vendo conteúdo de fábrica, do projeto ou de biblioteca associada.

### Procedimento: usar um recurso recebido

1. Abra Bibliotecas e associe o arquivo recebido. Confira nome, versão e origem.
2. Escolha a biblioteca e filtre o tipo de recurso. Confira dependências e Preview visual quando disponível. Associar e Preview não alteram o Working.
3. Use/incorpore o recurso e aguarde confirmação. Essa ação modifica o rascunho e pode reutilizar conteúdo equivalente já presente.
4. Abra o item na sua área de edição. Confira TAGs, comandos, parâmetros e referências: o desenho de uma bomba não escolhe sozinho as leituras da sua bomba.
5. Insira/configure os objetos em Telas ou Popups e teste. Salve, publique e ative antes de conferir no Runtime.
6. Para exportar seu conjunto, informe nome/versão, marque recursos e baixe .escadalib. Reabra num projeto de teste para conferir dependências incluídas.

### Campos, filtros e informações

Nome, tipo, categoria, origem e palavras-chave ajudam a localizar o recurso certo. Uma busca sem resultados pode ser um filtro combinado; limpe os filtros antes de importar outra cópia. Dados de versão/hash e dependências são informações de inspeção, não campos para digitar referências arbitrárias.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Arquivo para associar | Selecione o .escadalib recebido. O servidor valida estrutura/conteúdo; renomear outro arquivo para essa extensão não o converte. | bombas-1.0.escadalib |  |
| Origem / filtros do catálogo | Separe fábrica, projeto e biblioteca associada antes de usar. Recursos do projeto já pertencem ao Working; os associados ainda podem precisar de incorporação. | Associada → Template de bomba |  |
| Dependências / inspeção | Confira os recursos que acompanham a seleção. Uma Tela pode depender de Dínamos e imagens; a quantidade incorporada pode ser maior que um. IDs/caminhos internos servem à inspeção, não são nomes para recriar manualmente. | Tela + Dínamo + imagem |  |
| Nome da nova biblioteca | Ao exportar uma seleção, dê um nome que descreva o conjunto. Não é o nome de uma aplicação de Runtime. | Bombas da estação |  |
| Versão da nova biblioteca | Informe a versão para reconhecer a entrega. Ela descreve a biblioteca exportada; não muda automaticamente a revisão do projeto nem atualiza cópias já incorporadas. | 1.0 |  |
| Recursos para exportar | Marque recursos existentes no projeto; escolha o item principal e confira a inclusão das dependências. A exportação gera um arquivo, não publica uma revisão. | Template e Popup de bomba |  |

### Atualização, desassociação e falhas

Desassociar remove a biblioteca do catálogo associado; não apaga recursos já incorporados no projeto. A origem registrada é informativa: não constitui sincronização automática com futuras versões. Um mesmo ID de biblioteca com conteúdo diferente pode gerar conflito; confira qual versão precisa antes de desassociar e associar outro arquivo. Preview indisponível não prova que todo o pacote seja inválido: alguns tipos não têm prévia visual. Se o Workspace mudou durante a operação, recarregue antes de repetir. Uma associação não disponibiliza credenciais de Driver, usuários ou licença. O catálogo associado atual é local ao processo e pode desaparecer após reiniciar o serviço; guarde o arquivo para associá-lo de novo. Isso é diferente dos recursos já incorporados e salvos no projeto.

[Abrir este guia na Ajuda instalada](/help?topic=libraries.associate-incorporate)

## Assets visuais: importar, nomear e usar imagens

Organize os arquivos gráficos e preserve o vínculo ao renomear.

### Arquivo gráfico e objeto visual

Para usar uma imagem ou modelo numa Tela, primeiro importe o arquivo em Engenharia > Assets visuais. Asset é esse recurso gráfico; o objeto visual é quem o mostra na Tela. Importar o arquivo não cria TAG, comando, Dínamo parametrizado nem coloca uma instância no desenho.

Confira a origem antes de importar outra cópia. Conteúdo de fábrica e arquivos do projeto aparecem separados. Artes incluídas em Dínamos ou na fábrica podem não estar na lista de imagens do usuário; procure na origem correspondente.

### Procedimento e conferência

1. Importe um arquivo e aguarde a confirmação do serviço.
2. Confira prévia e nome; ajuste o rótulo se necessário.
3. Abra a Tela/Popup e insira um objeto compatível, escolhendo o asset pela biblioteca/seletor. Para um fundo ou branding, use o campo correspondente na área de configuração.
4. Ajuste tamanho/proporção na Tela; isso não altera os pixels do arquivo original.
5. Salve, publique e ative antes de conferir no Runtime. Se não aparecer, confira vínculo, tipo suportado, revisão e disponibilidade do conteúdo; não substitua o asset por outro só porque o nome parece igual.

### Campos e ações

Use a prévia para conferir conteúdo, depois um nome legível para localizá-lo no editor. Tipos aceitos no seletor ainda são validados pelo serviço; não basta trocar a extensão.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Importar arquivo | Selecione PNG, JPG, BMP ou SVG para imagens; o seletor também aceita GLB/GLTF para modelos suportados. Confira se o objeto de destino aceita esse tipo, não presuma que todo controle exibe 3D. | bomba-frente.svg |  |
| Nome de exibição | Inicialmente vem do nome do arquivo sem extensão. Renomear muda o rótulo e preserva a identidade estável usada pelos vínculos. | Bomba — vista frontal |  |
| Excluir / confirmar | Exige confirmação e pode ser recusado se o asset estiver em uso. Remova ou substitua referências no projeto antes de tentar de novo. | Excluir imagem não utilizada |  |

[Abrir este guia na Ajuda instalada](/help?topic=assets.management)
