# Apresentação, mídia e locais

## Cabeçalho e Mobile: apresentar e navegar

Configure a identidade visual e escolha a tela adequada ao dispositivo.

### Cabeçalho não é a tela de processo

O cabeçalho reúne marca, títulos, atalhos e relógio acima da tela do processo. Configure-o em Engenharia > Cabeçalho. Ele ajuda a identificar e navegar na aplicação; editar esse conjunto não muda TAGs nem acrescenta objetos ao desenho da tela.

Use Validar prévia e Aplicar ao Working, o rascunho do projeto. Depois salve, publique e ative a revisão para usar a mudança no Runtime.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Apresentação da marca | Padrão usa SCADIVO; Texto usa o texto informado; Imagem usa um recurso do projeto; Oculta retira a marca. | Texto: Estação de bombeamento |  |
| Texto e subtítulo | Título identifica a aplicação; subtítulo complementa. Em imagem, o texto também serve como identificação acessível. | Captação / Unidade 1 | Texto até 128; subtítulo até 256 caracteres |
| Imagem do projeto | Escolha ou importe um arquivo admitido. Não é campo para URL de câmera; use Fontes de mídia para isso. | Logotipo da aplicação |  |
| Altura e cores | A altura ocupa parte da área útil. Seguir tema mantém as cores do tema; cor personalizada passa a ser sua escolha explícita. | Conferir leitura em desktop e celular | Altura 56–168 px |
| Título, nome da tela e tipografia | Escolha alinhamento e fonte/tamanho/peso/cor. Mostrar nome da tela acompanha a navegação; não renomeia a tela. | Título à esquerda; nome da tela abaixo |  |
| Posição e ordem dos controles | Move o conjunto de navegação para o lado escolhido; ordem organiza sua posição relativa. O menu do usuário permanece à direita. | Controles à direita |  |
| Data e hora | Habilite se útil, escolha modo, formato, posição e ordem. Esse relógio não é o timestamp de aquisição dos TAGs. | Data e hora no cabeçalho |  |
| Atalhos de telas | Para cada botão informe rótulo, tela existente e imagem opcional. A tela precisa estar na revisão ativa. | Bombas → Tela Bombeamento | Até 16 atalhos no editor |

### Mobile: orientação e variante

Em Engenharia > Mobile, escolha como apresentar em dispositivos com toque. Orientação seleciona Paisagem ou Retrato; a possibilidade de travar a orientação física depende do navegador.

Para cada tela desktop, use a mesma tela ou escolha uma alternativa existente. A alternativa precisa ser desenhada por você em Telas: selecionar Retrato não reorganiza automaticamente seus objetos. Ambas usam os mesmos TAGs e Runtime, sem copiar os dados. Para mudar a parte superior da aplicação, volte a Cabeçalho.

### Procedimento e conferência

1. Configure a marca e somente os atalhos necessários.
2. Valide e aplique ao projeto.
3. Se houver variante mobile, desenhe-a em Telas e selecione-a em Mobile.
4. Valide/aplique Mobile.
5. Salve, publique e ative.
6. Abra o Runtime em desktop e celular, confira acesso aos botões, retorno entre telas, leitura dos valores e orientação. Se aparecer a versão antiga, confira a revisão ativa; se faltar uma imagem, confira o recurso selecionado.

[Abrir este guia na Ajuda instalada](/help?topic=application.presentation)

## Fontes de mídia: câmera, endereço e credencial

Cadastre a fonte e depois escolha como apresentá-la no objeto visual.

### Fonte e objeto são etapas distintas

Prepare primeiro a origem do vídeo ou imagem em Engenharia > Fontes de mídia. A fonte diz de onde vem o conteúdo. Depois escolha essa fonte no objeto da Tela, que define onde ele aparece e como é apresentado.

RTSP precisa do relay do servidor, o serviço que repassa o fluxo para reprodução. Cadastrar a fonte não garante que o navegador reproduza RTSP diretamente. Confira essa infraestrutura antes de tentar corrigir a reprodução mudando vários parâmetros.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome de exibição | Identifica a câmera/fonte na lista. | Câmera entrada |  |
| Chave estável | Referência única; use letras, números, ponto, hífen ou sublinhado. | camera-entrada |  |
| Protocolo | Escolha o formato fornecido pela fonte: HTTP/HTTPS, HLS, MJPEG ou RTSP/RTSPS. Mudar a opção não converte o conteúdo. | HLS quando a fonte fornece HLS |  |
| Endereço | URL absoluta sem usuário, senha, parâmetros de consulta ou fragmento. Credenciais pertencem ao formulário protegido. | https://camera.example.invalid/stream |  |
| Habilitada | Define se a fonte está habilitada na configuração. Não comprova que está acessível ou que o vídeo já está aparecendo. | Habilitar a fonte em uso |  |
| Credenciais protegidas | Após criar a fonte, escolha Basic com usuário/senha ou Bearer com token. Salve separadamente. São somente de escrita e não são exportadas no pacote; reconfigure no host de destino. | Preencher no painel protegido, sem URL com senha |  |

### Procedimento e problemas comuns

1. Crie a fonte com nome, chave, protocolo, endereço e habilitação.
2. Salve-a. Se precisar de credencial, configure-a no painel da fonte criada.
3. Insira ou selecione o objeto visual compatível e escolha a fonte suportada.
4. Salve, publique e ative a revisão; confira a reprodução no Runtime.

Se não reproduzir, verifique endereço/protocolo, acesso a partir do serviço, credencial do host, relay quando exigido e revisão ativa. Remover a credencial pode impedir o acesso à câmera. Exportar o projeto não transporta sua senha.

[Abrir este guia na Ajuda instalada](/help?topic=sources.media)

## Home/Building: locais e dispositivos

Organize onde o dispositivo está e confira quais capacidades ele realmente oferece.

### Separe local, dispositivo e conexão

Em Engenharia > Home/Building, organize primeiro onde o dispositivo está. Local pode ser uma sala ou área. Equipamento é a instância do dispositivo, como uma luminária. A Fonte de dados, também chamada Data Source, guarda a conexão usada para comunicar.

São informações diferentes: mover a luminária para outra sala não troca seu protocolo. Comece com os dispositivos e TAGs que a conexão realmente oferece.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Nome do local | Nome legível dentro da hierarquia. | Sala de bombas |  |
| Tipo do local | Escolha Site, Prédio, Andar, Área/Sala ou Zona conforme a organização. O tipo não concede permissões nem cria automação. | Área/Sala |  |
| Local pai | Onde o local se encaixa. Escolha um local existente e evite relações circulares; sem pai representa raiz quando permitido. | Prédio 1 → Andar 1 → Sala de bombas |  |
| Local do dispositivo | Associa a instância ao local existente. Essa associação organiza o projeto, não testa a comunicação. | Bomba 01 na Sala de bombas |  |
| Nome e caminho do dispositivo | Nome é rótulo; caminho é referência estável. Confira o candidato antes de aplicar e preserve a identidade ao reorganizar. | Conferir instância e referência na prévia |  |

### Procedimento e conferência

1. Configure a conexão e confira TAGs/capacidades fornecidas.
2. Crie a hierarquia de locais preenchendo nome, tipo e pai.
3. Associe o dispositivo ao local correto e revise sua identidade.
4. Confira o Preview e aplique apenas o candidato validado.
5. Salve/publique/ative.
6. Teste leitura e ações disponíveis numa instalação de teste. Uma capacidade ou comando ausente não surge porque a interface mostra um equipamento; confira provedor, mapeamento e permissões. Para apenas um faceplate gráfico, consulte Equipamentos e Templates.

[Abrir este guia na Ajuda instalada](/help?topic=home-building.organization)
