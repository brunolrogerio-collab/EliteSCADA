# Wave 16 — roteiro de distribuição da Alpha 0.16.1.1

Base aprovada na #300 em 392bbf953e04ef0b53967eea8fc5bae8576d8e36, integrada pela #938 em 21f067067f8cf48aeda7f89f764ec471870b5b1d.
## Decisão do proprietário — distribuição da Wave 16 e roteiro por ambiente

**Primeira entrega de instalação: Windows x64.** A Wave 16 inclui também pacote nativo Linux `.deb` compatível com Debian e Ubuntu, imagens de contêiner para `linux/amd64` e `linux/arm64` e uma imagem **Edge reduzida para `linux/arm64` com SQLite local**. A distribuição inclui o manual completo de utilização. Os instaláveis, as imagens e a documentação validados serão entregues ao **chat Codex do portal**, responsável por inseri-los no portal e atualizar o FAQ na #741. As versões Debian/Ubuntu e as arquiteturas atendidas pelo `.deb` devem ser declaradas no roteiro e confirmadas em teste. O requisito ARM64 é explícito para as duas imagens correspondentes.

### Ordem do trabalho

1. **Antes de desenvolver ou ampliar os instaladores**, entregar um roteiro técnico versionado e completo para cada ambiente abaixo. Partir de uma versão e um commit aceitos do produto. Em cada roteiro, identificar ferramentas e versões usadas no build, entradas, dependências incluídas e pré-requisitos do host, comandos de montagem, estrutura de diretórios, permissões, configuração inicial, testes, critérios de aceite, recuperação de falhas, artefatos e responsáveis pela entrega. Descrever também a execução em máquina limpa e a atualização sobre uma instalação com dados.
2. Implementar e validar primeiro o instalador Windows x64. Usar o mesmo núcleo de produto para os demais pacotes. Registrar por plataforma o que passou em hardware nativo, o que passou em VM ou contêiner e o que permanece experimental.
3. Fechar a distribuição com os pacotes verificáveis, o manual completo, a matriz de compatibilidade e os dados necessários para publicação no portal. O número da versão exibido na aplicação, no instalador, nas imagens, no manual e nos downloads deve corresponder ao candidato entregue.

### Conteúdo mínimo de cada roteiro

| Ambiente e artefato | Preparação e instalação | Verificação e recuperação |
|---|---|---|
| **Windows x64 — primeiro instalador nativo** | Publicação .NET autocontida, inventário de dependências nativas, pré-requisitos online/offline e assinatura. Especificar arquivos de programa, dados persistentes, configuração e logs, conta do serviço Windows, permissões, portas e acesso local à Engenharia/Runtime. Incluir o monitor da bandeja com saúde, uso da licença e comandos autorizados de início, parada e reinício. | Instalação limpa e partida automática após reinício do Windows; acesso pelo navegador; falha e retomada do serviço; atualização com projeto, licença e histórico existentes; reparo, reversão e desinstalação com tratamento explícito dos dados. |
| **Debian e Ubuntu — instalador nativo .deb** | Listar as versões e arquiteturas a validar em cada distribuição. Descrever dependências do pacote, usuário e permissões do serviço de sistema, unidade systemd, diretórios de binários, configuração, dados e logs. Documentar instalação por arquivo local, inclusive como obter dependências sem internet, e monitor da bandeja quando houver desktop. | Em **ambas** as distribuições declaradas, instalar, conferir dependências, habilitar o serviço, reiniciar o host e consultar saúde e licença. Testar troca de versão, falha de configuração, recuperação, remoção e expurgo; documentar precisamente o efeito sobre os dados. Verificar também operação sem interface gráfica no servidor. |
| **Contêiner Linux x86-64 — linux/amd64** | Descrever construção da imagem, serviços necessários, banco PostgreSQL/TimescaleDB local ou externo conforme o perfil, portas, volumes, segredos, limites do host e procedimento para carga offline. Identificar a imagem por versão e digest. | Em host limpo, subir a aplicação, restaurar a demo, operar Runtime e Engenharia e validar persistência ao recriar os contêineres. Exercitar atualização e retorno à versão anterior com backup compatível. Medir RAM, latência e margem de disco sob a carga publicada. |
| **Contêiner Linux ARM64 — linux/arm64** | Inventariar dependências nativas da API, drivers, scripts, mídia e banco para ARM64. Documentar como o usuário seleciona a imagem correta e como verificar arquitetura e digest. | Repetir a validação funcional e de atualização em Linux ARM64 real. Identificar separadamente qualquer teste executado por emulação. Medir capacidade com a mesma carga de referência usada em x86-64 para permitir comparação. |
| **Edge reduzido ARM64 — imagem com SQLite** | Descrever os recursos disponíveis no perfil, preparação e carga do projeto publicado, diretório gravável do SQLite, arquivos WAL, política de retenção, limites de RAM e disco, atualização, backup e restauração consistentes. Entregar identificação distinta da imagem ARM64 completa. | Testar na arquitetura alvo com tendências e alarmes ativos, reinício abrupto, recuperação do histórico e conservação de uma única ocorrência do alarme. Exercitar disco cheio e corte físico na flash do equipamento antes de declarar esse suporte. Registrar a capacidade observada em TAGs, frequência de aquisição, clientes e vídeo, junto com consumo do restante do equipamento. [Ensaios Edge/VPS](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-7072644632) |

### Manual completo, FAQ e entrega ao portal

- **Manual de utilização entregue com a versão**, mantendo português, inglês e espanhol. Cobrir escolha do perfil, instalação, primeira configuração, licenciamento, Engenharia e operação do Runtime, alarmes e histórico, monitoramento de saúde, atualização, backup, restauração, solução de problemas e desinstalação. Incluir um procedimento conferido para cada plataforma e o acesso ao manual em instalação offline.
- **FAQ do portal organizado pela decisão de quem vai instalar.** Comparar instalador nativo, contêiner completo, VPS compacto e Edge SQLite. Para cada opção, informar sistemas e arquiteturas homologados, banco suportado, recursos disponíveis, limites de licença, capacidade medida (TAGs, clientes, amostragem, retenção e vídeo quando avaliados), RAM/disco e condições do ensaio. Distinguir limite contratado, consumo observado e recomendação de reserva. Indicar lacunas de validação. Os 215,04 MB observados em Linux x86-64 são referência de laboratório para o Edge; a divulgação de capacidade ARM64 depende da medição correspondente.
- **Handoff ao chat Codex do portal.** Entregar instalador Windows, pacote(s) `.deb`, imagens completas amd64/arm64 e imagem Edge SQLite arm64, manual final, notas de versão, tabela do FAQ e um manifesto por versão. Para cada item indicar nome, sistema/arquitetura, tamanho, versão/commit, SHA-256 dos arquivos ou digest da imagem, assinatura quando aplicável, localização de obtenção e evidência de instalação/teste. O Codex do portal usa esses artefatos para cadastrar os downloads e conferir versão, links e integridade após a inserção. [Frente do portal #741](https://github.com/brunolrogerio-collab/EliteSCADA/issues/741)

O escopo acima é de planejamento e entrega da Wave 16. A direção original do serviço Windows e a separação entre binários e estado persistente continuam como base. [Direção do instalador Windows](https://github.com/brunolrogerio-collab/EliteSCADA/blob/442faf5df5e3f5fab2ede0397d813ac3540109cb/docs/WAVE16-WINDOWS-NATIVE-INSTALLER-DIRECTION.md) · [Serviços #361](https://github.com/brunolrogerio-collab/EliteSCADA/issues/361) · [Distribuição #363](https://github.com/brunolrogerio-collab/EliteSCADA/issues/363) · [Conteúdo dos instaladores #381](https://github.com/brunolrogerio-collab/EliteSCADA/issues/381)

## Decisões da Wave 16 que o pacote deve demonstrar

| Referência | Aplicação e evidência de aceite |
|---|---|
| [Fábrica de versões e perfis imutáveis](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-5901049581) | A entrada identifica SHA do produto, revisão da receita de distribuição, perfil, arquitetura e canal de confiança. A mesma receita resolve a versão de Directory.Build.props e gera payload, adaptador, inventário, SBOM e manifesto. Versionar templates, marca, configuração das ferramentas e regras de conteúdo. Manter estado e segredos fora do payload substituível. |
| [Contrato multiplataforma](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-5939854848) e [decisões complementares](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-5940033804) | Primeira qualificação em Windows 11 x64, Debian 12 e Ubuntu 24.04 x64. Debian 13, Ubuntu 22.04, LTSC e Server recebem linhas adicionais com evidência própria. Ensaiar o Desktop Host CEF com a Web canônica em servidor local, cliente remoto e quiosque. Na primeira abertura oferecer configurar este computador ou conectar a um servidor. Validar identidade TLS, compatibilidade de protocolo e recursos antes do login remoto. Fechar a janela e confirmar que o serviço continua coletando. |
| [Serviços](https://github.com/brunolrogerio-collab/EliteSCADA/issues/361), [topologia do banco](https://github.com/brunolrogerio-collab/EliteSCADA/issues/366) e [portas](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-6514444984) | Em instalação nativa nova LocalManaged, provisionar PostgreSQL dedicado na porta 15432 e configurar projeto e Historian juntos. Conferir Web 18080 e API 15120 independentes. Verificar conflito, acesso local e habilitação administrativa de HTTPS remoto. Na atualização, preservar os endpoints existentes. Confirmar retomada após indisponibilidade do banco remoto. |
| [OCI e capacidade](https://github.com/brunolrogerio-collab/EliteSCADA/issues/363) e [ensaios Edge/VPS](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-7072644632) | Registrar digest, arquitetura nativa, custo de memória da configuração final, banco usado e carga por perfil. Separar a matriz de capacidade da licença contratada. |
| [Higiene da distribuição](https://github.com/brunolrogerio-collab/EliteSCADA/issues/381), [Matter experimental](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-6154492670) e [avaliação controlada](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-5940089518) | Inspecionar os arquivos efetivamente entregues, excluir componentes experimentais Matter do pacote de avaliação e preservar avisos de terceiros. Registrar as dependências PostgreSQL/TimescaleDB e as extensões redistribuídas com sua licença. Identificar o canal privado e o estado da assinatura no manifesto. |

## Como executar uma rodada de distribuição

O responsável pelo build prepara os arquivos. O revisor de instalação recebe os mesmos arquivos que o cliente receberia. Preparar as entradas nas etapas 1 a 3. Entregar Windows x64 pela etapa 4 e os demais perfis pela etapa 5. Aplicar as etapas 6 a 8 a cada pacote candidato. Os comandos abaixo são de laboratório. Usar diretórios de saída novos e portas livres. Registrar os nomes reais de unidade, serviço, pacote e caminhos no manifesto assim que existirem.

### 1. Identificar a entrada e preparar a recuperação

1. Em checkout limpo, obter o SHA aprovado na #300 e a referência integrada pela #938. Registrar `git rev-parse HEAD`, `git status --porcelain`, o canal e a versão de `Directory.Build.props`. Se o pacote incluir alterações posteriores da Wave 16, registrar o SHA da candidata que efetivamente as contém e o diff em relação à base.
2. Separar três ambientes de laboratório por papel: compilação, máquina limpa e instalação preenchida da versão predecessora. Registrar SO, arquitetura, espaço livre, memória, topologia do banco e se é máquina física, VM ou emulador. Para ensaio offline, preparar previamente as dependências e desligar a saída para a internet mantendo o banco de laboratório acessível.
3. Verificar o SHA-256 da demo EEE + RHO simulada da #306 antes da importação. A referência da revisão 75 é `0c5481db2ffeef1fb01b83be226c7965d5aceea8821643557d2a74d9546b6020`. Guardar em local protegido a cópia da instalação preenchida e ensaiar a restauração em destino vazio.
4. Abrir a ficha de evidências descrita ao fim deste roteiro. Diante de SHA divergente ou restauração incompleta, corrigir a origem e repetir esta etapa antes de modificar a instalação preenchida.

### 1.1 Higienizar as entradas antes de compilar

1. Materializar uma árvore temporária do SHA aprovado. Separar os arquivos necessários ao build dos arquivos que o cliente receberá. Aplicar regras versionadas por linguagem e caminho tanto à compilação nativa quanto ao contexto enviado ao construtor OCI.
2. Revisar comentários e metadados de código próprio que alcançam JS, CSS, HTML, SVG, scripts, templates, documentos XML de API e exemplos enviados ao cliente. Remover anotações internas, nomes de máquinas, caminhos de desenvolvedor, referências a chamados privados e dados de teste. Corrigir qualquer segredo na origem e encaminhar a rotação quando aplicável. Registrar caminho, regra e resultado sem copiar o conteúdo sensível para os relatórios.
3. Preservar copyright, SPDX, atribuições, textos de licença, diretivas de compilação, anotações de ferramentas e instruções do operador. Usar parser ou compilador da linguagem para remover comentários dispensáveis da cópia de build. Incluir na qualificação uma diretiva, uma URL dentro de string e um aviso de licença, verificando os três após a transformação. O repositório conserva os comentários técnicos usados para manutenção.
4. Conferir a lista de entradas e o diff de preparação. A mesma entrada deve gerar a mesma seleção de arquivos e regras. Marcar a etapa como aprovada antes de invocar npm, dotnet ou o construtor de imagens. Repetir a inspeção no pacote final para capturar conteúdo gerado pelo compilador.

### 2. Compilar e montar o conteúdo comum

1. Conferir `dotnet --version`, `node --version` e `npm --version`. A base usa SDK 10.0.400 e o Dockerfile da Web usa Node 24.19.0. Registrar versões efetivas, lockfiles e identificadores das imagens utilizadas.
2. Na raiz da candidata, executar os comandos abaixo em PowerShell, verificando o código de saída de cada comando antes do próximo. Usar uma saída distinta por arquitetura. No Linux nativo, substituir win-x64 por linux-x64 no identificador e no destino.

~~~powershell
Push-Location web/scada-web
npm ci
npm run build
Pop-Location
dotnet publish src/Scada.Api/Scada.Api.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64/api
~~~

3. Reunir o publish autocontido, a saída web/scada-web/dist e os recursos da lista permitida em uma árvore de preparação versionada. Conservar o layout que o carregador .NET espera. Configurar no adaptador o serviço que fornece os arquivos Web e encaminha API e WebSocket. Abrir a tela e verificar atualização ao vivo. Conferir os recursos de Python/Pyodide, os sidecars e os drivers que serão anunciados, inclusive quando a máquina está offline.
4. Produzir inventário com caminho relativo, tamanho, arquitetura e hash. Classificar componentes próprios e de terceiros e associar cada dependência redistribuída à sua licença. Conferir a situação do driver DNP3 antes de incluí-lo na edição comercial conforme docs/LINUX-DEBIAN-DISTRIBUTION.md. Havendo componente ausente ou incompatível, corrigir a composição e gerar novo inventário.

### 2.1 Proteger a propriedade intelectual e conservar o diagnóstico

1. Preparar um piloto de ofuscação de assemblies **próprios** junto à compilação Release. Avaliar uma ferramenta de renomeação como Dotfuscator, registrar versão, termos de uso, automação offline, compatibilidade com .NET 10 e arquiteturas. A escolha final depende do ensaio. Na Web usar minificação de produção e manter mapas de código-fonte e fontes incorporadas fora da entrega pública. Arquivar símbolos, mapas de renomeação e hashes em armazenamento privado por build. [Renomeação e mapas](https://support.preemptive.com/hc/en-us/articles/31855512408721-Renaming)
2. Versionar as exclusões para contratos públicos, serialização, reflection, persistência, descoberta de drivers e extensões. Comparar a mesma revisão protegida e de referência com importação de projeto anterior, login, edição, ativação, WebSocket, scripts, drivers e leitura histórica. Repetir os ensaios de RAM e latência da EEE antes de aceitar proteção para VPS e Edge.
3. Inspecionar um assembly próprio transformado no laboratório e verificar a renomeação dos símbolos selecionados. Inspecionar as camadas OCI, o instalador extraído e os arquivos Web por mapas, fontes e símbolos privados, inclusive os incorporados. Produzir uma falha controlada e reconstruir a pilha usando o mapa privado daquela versão. Se um contrato quebrar, ajustar a exclusão específica, refazer a transformação e repetir a comparação.
4. Registrar titularidade do código e da arte, versão, hashes e proveniência assinada. Conferir avisos de terceiros após todas as transformações. Aplicar a proteção antes da assinatura dos binários e assinar o instalador depois de empacotar. Essas medidas aumentam o esforço de análise e permitem verificar a origem de uma cópia. Código distribuído continua sujeito a inspeção.

### 3. Preparar instalação, rede e identidade

1. Definir como cada perfil recebe URL do banco de projeto e do Historian, certificados, identidade persistente e material protegido. Entregar um exemplo de configuração sem segredos. Registrar o usuário de serviço, diretórios de leitura e escrita e as portas. Verificar conflito de portas antes da partida.
2. Instalar o serviço no gerenciador do sistema com partida automática. Deixar o monitor de bandeja na sessão do usuário. Para o primeiro acesso, executar o fluxo de instalação e ativação da candidata, selecionar a licença da bancada e registrar o resumo de vagas sem copiar códigos privados.
3. Em instalação nova, conferir a primeira configuração e o estado inicial. Em seguida importar a cópia da EEE no Workspace, validar, ativar e anotar a revisão do Runtime. Em atualização, comparar a revisão já ativa diretamente com o registro anterior.
4. Abrir Engenharia e Runtime no navegador do operador, observar atualização das correntes de B1 e B2 e consultar a tendência em um intervalo fixo. Simular um alarme, registrar identificador, disparo, retorno e quantidade de ocorrências. Essa ficha será usada antes e depois de cada intervenção.

### 4. Gerar a candidata Windows x64 primeiro

1. Em uma máquina Windows x64 de build, fixar no repositório a ferramenta, sua versão e a definição do instalador. O comando de publicação da etapa 2 gera a API. A definição do instalador ainda deve compor serviço, Web, bandeja, manual e dependências em um pacote repetível.
2. Configurar o diretório de programas em Program Files/SCADIVO/releases/<versão> e o estado da instância em ProgramData/SCADIVO. Registrar o executável do serviço e seu diretório de trabalho por caminhos absolutos resolvidos pelo instalador, incluindo caminhos com espaços. Gravar configuração inicial e conceder à conta do serviço acesso às chaves e ao estado necessários.
3. Montar a partir do inventário aprovado e guardar o log da ferramenta. No canal assinado, assinar primeiro os binários próprios e depois o instalador, verificar o editor e o carimbo de tempo. Calcular o hash final com Get-FileHash -Algorithm SHA256. Identificar a primeira avaliação privada sem assinatura no manifesto.
4. Entregar ao revisor o pacote, hash, modo de instalação offline, procedimento de reparo e localização do log. Executar a tabela Windows abaixo em máquina limpa. Se houver falha na inicialização, consultar o SCM, os logs do serviço e a conexão ao banco usando a identidade do serviço antes de repetir a instalação.


### 4.1 Aplicar a identidade visual SCADIVO ao instalador

1. Usar como referências versionadas o [símbolo oficial no site](../website/scadivo/site/scadivo-mark.svg), o [símbolo no produto](../web/scada-web/public/scadivo-mark.svg) e a [paleta do SCADIVO.com](../website/scadivo/site/styles.css). Na base conferida, os dois SVGs têm o mesmo SHA-256. Usar o nome público SCADIVO. Gerar ícones em tamanhos nativos e imagens a partir dos originais aprovados, com proporção preservada. Registrar origem, licença, dimensões e hash de cada derivado.
2. Criar um tema versionado único para boas-vindas, seleção de perfil, progresso, conclusão, reparo e remoção. Usar a hierarquia visual do site, fundo escuro azul e destaque turquesa onde apropriados, preservando contraste, foco de teclado e alto contraste do Windows. Aplicar o símbolo à janela, ao executável, aos atalhos, à lista de aplicativos instalados e à bandeja. Carregar as imagens localmente no pacote offline.
3. Mostrar nome, canal e versão por dados do manifesto. Testar instalação limpa, atualização e erro recuperável em PT-BR, EN e ES, com escala de 100%, 150% e 200%. Validar corte de texto, clareza dos botões, teclado, leitor de tela e legibilidade das imagens. Anexar capturas comparáveis ao site/produto, incluindo o estado de instalação concluída. Guardar o aceite visual da avaliação.
4. Na versão seguinte, executar o mesmo template com outro SHA aceito. Comparar composição, identidade dos arquivos de marca, editor exibido, instalação sobre a versão anterior e preservação de dados. Produzir duas vezes a mesma candidata e explicar diferenças de assinatura e data no manifesto. A prova de reuso requer a geração das duas versões sem editar manualmente o layout, as imagens ou as listas de arquivos.

## Qualificação do primeiro instalador Windows x64

| Rodada | Procedimento | Evidência para aceitar |
|---|---|---|
| Instalação limpa | Iniciar em Windows x64 sem SDK, Visual Studio ou Docker Desktop. Verificar o hash do instalador, instalar, configurar banco e abrir a Engenharia e o Runtime pelo endereço fornecido. Repetir com a mídia offline. | Registrar edição/build do Windows, SHA do pacote, versão exibida e primeira leitura de TAG pelo navegador. |
| Serviço e bandeja | Reiniciar o Windows sem login. Conferir a coleta pelo horário das amostras. Entrar com usuário comum, verificar saúde e vagas usadas/disponíveis. Exercitar início, parada, reinício e parada forçada com operador autorizado. | Registrar identidade da conta do serviço, permissões sobre configuração, dados e chaves, continuidade da coleta após retorno e resultado do comando por usuário sem autorização. |
| Atualização e reparo | Salvar revisão, usuários, licença, alarmes e intervalo histórico. Validar o backup em cópia. Atualizar a instalação preenchida e reparar um arquivo de programa removido na bancada. | Comparar os mesmos registros antes e depois. Comprovar preservação dos arquivos de configuração e recuperação dos binários pelo instalador. |
| Falha e remoção | Simular porta ocupada, banco indisponível e falha na troca de versão em VM descartável. Consultar diagnóstico antes de restaurar. Desinstalar e reinstalar usando os dados retidos. | Registrar causa visível, procedimento de recuperação e revisão recuperada. Se o esquema mudou, restaurar banco e binários compatíveis juntos. Exercitar expurgo somente na cópia de ensaio. |

O primeiro instalador é de avaliação privada. Para distribuição assinada, inventariar os binários próprios, assiná-los antes de montar o instalador, verificar as assinaturas e então assinar o instalador. Verificar o carimbo de tempo e calcular o SHA-256 dos bytes finais entregues ao portal.

## 5. Montar e qualificar cada ambiente

### Pacote nativo Debian e Ubuntu

1. Preparar primeiro VMs amd64 Debian 12 e Ubuntu 24.04 LTS. Qualificar depois cada distribuição adicional declarada. Registrar distribuição, versão, arquitetura e snapshot inicial. Construir com o SDK de global.json e dependências Web do lockfile. Publicar a API com `dotnet publish src/Scada.Api/Scada.Api.csproj -c Release -r linux-x64 --self-contained true`. Acrescentar ao pacote a Web compilada, os ativos offline e cada dependência nativa redistribuível. Conferir o carregamento efetivo de drivers, scripts e mídia em cada distribuição.
2. Gerar os metadados Debian com dependências resolvidas na distribuição alvo. Inspecionar arquitetura, versão, dependências e caminhos com `dpkg-deb --info` e `dpkg-deb --contents`. Instalar com APT na VM limpa e repetir em rede isolada usando um repositório local com as dependências. Registrar quais pacotes o administrador precisa fornecer.
3. Registrar a API como serviço de sistema. A conta do serviço precisa ler os binários e gravar somente nos destinos de estado autorizados. Configurar acesso protegido ao banco, iniciar o serviço e verificar revisão ativa, Engenharia, Runtime, histórico e licença após reboot sem sessão gráfica. Em desktop, verificar a bandeja com usuário comum e a autorização dos comandos administrativos.
4. Com um projeto preenchido, atualizar pelo gerenciador de pacotes. Comparar a revisão, os usuários e as amostras antes e depois. Repetir com banco indisponível durante a partida e comprovar que a recuperação usa o banco configurado. Registrar por versão do sistema a instalação offline, a atualização, a remoção com retenção e a restauração do backup.

**Sequência de montagem do .deb.** Preparar uma árvore por distribuição com DEBIAN/control, arquivos do produto e a unidade systemd. Declarar nome, versão com revisão Debian, arquitetura, dependências e descrição. Tratar a configuração editável como conffile. Os scripts de manutenção devem preservar dados numa atualização interrompida. Registrar o nome efetivo da unidade no manual. Em shell Linux, definir STAGE como a árvore preparada e DEB como o caminho absoluto do pacote de saída.

~~~sh
dpkg-deb --root-owner-group --build "$STAGE" "$DEB"
dpkg-deb --info "$DEB"
dpkg-deb --contents "$DEB"
sha256sum "$DEB"
~~~

Na VM limpa, usar `sudo apt install "$DEB"`. Conferir a arquitetura com `dpkg --print-architecture`, a partida com `systemctl is-enabled "$UNIT"` e `systemctl is-active "$UNIT"`, e o diagnóstico com `journalctl -u "$UNIT" -b`, onde UNIT é a unidade instalada. Repetir após reiniciar a VM sem login. Na instalação gráfica, conectar dois monitores com escalas diferentes, atribuir telas distintas, entrar em tela cheia estendida e sair. Registrar se cada monitor exibiu o quadro correto, se a janela voltou à tela principal e quantas vagas foram usadas. Aplicar o mesmo ensaio ao Windows.

### Imagens completas amd64 e arm64

Construir os dois Dockerfiles a partir da raiz do repositório. O arquivo docker-compose.stable.yml fornece os contextos e a topologia de referência. Na base da Wave 15 a etapa final da API usa uma imagem SDK .NET 10.0.400. Inventariar os comandos efetivamente usados em execução antes de experimentar uma base menor. Comparar inicialização, scripts, drivers, mídia e consumo na mesma carga.

1. Para cada Dockerfile, construir ambos os destinos com Buildx e exportar o índice e as imagens. Registrar digest do índice, digest de cada arquitetura, versões das imagens de banco e mídia e o SHA-256 dos arquivos para carga offline. Conferir a arquitetura de cada dependência, inclusive binários executados em processos auxiliares.
2. Em Linux amd64, carregar o conjunto em host limpo. Inicializar com PostgreSQL/TimescaleDB externo e repetir com banco em volume dedicado. Restaurar a EEE simulada e registrar identidade da instalação, revisão, leituras antigas e ocorrência de alarme. Recriar API e Web, comparar os registros e confirmar que entram leituras novas.
3. Em Linux ARM64 físico, repetir a mesma carga. Registrar modelo, kernel, arquitetura, memória, armazenamento, dispositivos e drivers exercitados. Testar também o carregamento offline, a estabilidade da licença e a troca de imagem por digest com volumes preservados. Registrar testes emulados em coluna própria.
4. Entregar o Compose com referências imutáveis, inventário de volumes, portas, requisitos do banco e procedimento de backup. Ensaiar restauração em destino vazio e retorno à versão anterior com esquema compatível antes de oferecer atualização.

**Montagem e carga offline.** No host de build, usar um builder com exportação OCI habilitada. A partir da raiz em shell Linux, criar dois arquivos distintos.

~~~sh
docker buildx build --platform linux/amd64,linux/arm64 -f docker/stable/Dockerfile.api --output type=oci,dest=scadivo-api.oci.tar .
docker buildx build --platform linux/amd64,linux/arm64 -f docker/stable/Dockerfile.web --output type=oci,dest=scadivo-web.oci.tar .
sha256sum scadivo-api.oci.tar scadivo-web.oci.tar
~~~

Para o kit Docker offline, preparar também um conjunto por arquitetura no daemon de preparação com tags de entrega para API, Web, gateway e, no perfil local, banco. Exportar esse conjunto com `docker image save -o scadivo-offline.tar <tags-do-manifesto>`. Na máquina limpa, conferir o SHA-256, usar `docker image load -i scadivo-offline.tar` e conferir OS, arquitetura e ID com docker image inspect. O Compose de entrega deve referenciar os nomes carregados e incluir os arquivos montados, como a configuração de mídia. Subir com `docker compose up -d --no-build --pull never` no diretório desse Compose. A alternativa com banco externo deve iniciar sem o serviço de banco local ou dependência de seu healthcheck. Registrar as imagens efetivamente usadas. Se faltar imagem ou configuração, reconstruir o kit offline e repetir em daemon limpo.

### Perfil compacto para VPS

Usar cópia da revisão 75. Recuperar o override e os resultados originais conforme o [ensaio da #408](https://github.com/brunolrogerio-collab/EliteSCADA/issues/408#issuecomment-7072644632). Conferir os valores efetivos do Compose e do PostgreSQL após a partida. Os tetos ensaiados foram API 235 MB, Web 13,5 MB, gateway 31,5 MB e banco 318 MB, com 48 MiB de buffers compartilhados. Reservar orçamento separado para sistema operacional e segunda aplicação.

Executar as mesmas consultas com seis clientes, ingestão das duas bombas e um consumidor de vídeo. Medir desde a partida e repetir com cache aquecido, consultas frias, manutenção do banco e a segunda aplicação em carga simultânea. Registrar pico simultâneo do grupo, memória disponível do host, swap, encerramentos por falta de memória, latência p95 e equivalência das amostras. Publicar o perfil como opção com a carga medida. O valor de referência da demo pública é 768 MiB de buffers compartilhados.

**Como comparar as rodadas.** Restaurar o mesmo snapshot para referência e compacto, guardar o intervalo histórico fixo e conferir no banco o valor efetivo com `SHOW shared_buffers`. Iniciar o grupo de serviços em um cgroup dedicado por rodada. Com a EEE do ensaio, aquecer por 20 s, medir carga mista por 60 s e consultas longas por 120 s. Registrar memory.peak do grupo em bytes e converter para MB dividindo por 1.000.000. Contar erros HTTP e amostras e comparar o hash das respostas da janela fixa. Medir p95 de abertura, valores ao vivo e histórico separadamente. Acrescentar uma rodada com a segunda aplicação ativa e outra com ingestão e manutenção simultâneas. Se faltar memória, conservar o log de OOM e a carga observada. Repetir com o perfil anterior no laboratório para identificar a regressão.

### Edge com banco remoto e Edge ARM64 com SQLite

Para banco remoto, medir API, Web e gateway no dispositivo e registrar a latência até o PostgreSQL. Interromper o enlace em laboratório, observar a indicação de indisponibilidade e conferir a retomada e as lacunas do histórico. Informar ao instalador a dependência da rede e o consumo do servidor externo.

Para SQLite, recuperar o protótipo da #408 e preparar uma imagem com identidade própria. Montar o diretório inteiro do SQLite em armazenamento persistente para manter banco, WAL e arquivos auxiliares juntos. Criar backup consistente pela API de backup SQLite ou após parada limpa. Restaurar em volume novo e conferir integridade, revisão publicada, tendências e ocorrências de alarme.

No ARM64 alvo, manter a aplicação de automação em execução e medir a memória do equipamento e do conjunto SCADIVO. Registrar frequência de aquisição, retenção, clientes, vídeo e crescimento em disco. Em bancada descartável, interromper energia durante escrita, reiniciar, comparar amostras confirmadas e procurar ocorrências duplicadas. Repetir com volume cheio e após liberar espaço. A meta do conjunto é 220 MB com teto de 230 MB. A edição do projeto, permissões, supressão de alarmes e recursos analíticos precisam ter seu estado registrado individualmente antes da divulgação de capacidades.

**Sequência de ensaio Edge SQLite.**

1. Recuperar o checkout e as configurações do ensaio vinculado na #408. Registrar o diff de laboratório, a revisão publicada e as dependências ARM64. Produzir imagem ARM64 a partir do perfil versionado, identificar por digest e inspecionar no dispositivo a arquitetura do processo e a biblioteca SQLite carregada.
2. Montar volume exclusivo e conferir no processo escritor as opções efetivas de WAL, sincronização, cache e checkpoint. A referência usa WAL, synchronous FULL e wal_autocheckpoint 64. Guardar os valores efetivos junto à versão da biblioteca e tipo de armazenamento.
3. Com B1 e B2 coletando, registrar amostras confirmadas até um horário conhecido, uma ocorrência ativa e uma encerrada. Fazer backup consistente, restaurar em outro volume e repetir a consulta. Guardar IDs, contagens e resultado de integrity_check na cópia.
4. Em armazenamento de bancada, provocar interrupção durante escrita e durante checkpoint em rodadas distintas. Reiniciar no mesmo volume. Conferir as amostras previamente confirmadas, a ocorrência ativa única, a leitura nova e um novo ciclo de disparo/retorno. Registrar qualquer lacuna da janela de interrupção.
5. Usar volume descartável com quota para o ensaio de disco cheio. Registrar como o operador percebe a falha, recuperar espaço e conferir nova gravação. No equipamento ARM64 pretendido, repetir o ensaio de energia física, medir pico com a automação ativa e declarar as funções e carga que passaram. Vincular qualquer reprovação ao modelo, flash, imagem e cenário.

## 6. Atualizar, provocar falha e recuperar

1. **Antes da troca.** Usar a instalação preenchida. Exportar os identificadores da revisão, usuários e papéis de teste, consumo de licença, amostras em intervalo fechado e alarmes do ensaio. Registrar versão e esquema do banco. Verificar o hash da cópia anterior e restaurá-la em laboratório até que seja possível consultar esses mesmos dados.
2. **Atualizar.** Encerrar gravações de forma controlada, aplicar a candidata pelo canal em teste e capturar o resultado da migração. Reiniciar o host e repetir o percurso da etapa 3. Conferir os registros antigos, uma amostra nova e uma única ocorrência de cada transição. Anexar contagens, IDs de laboratório e intervalos com fuso.
3. **Provocar falha.** Na cópia descartável, tornar o banco indisponível durante a inicialização. Consultar o estado exibido, o diagnóstico e o destino das gravações. Restabelecer o banco e conferir que a mesma revisão retoma sem escrita local silenciosa. Em outra rodada, interromper a atualização entre preparação e ativação.
4. **Reverter.** Anotar qual versão e esquema ficaram ativos. Parar a candidata e restaurar a versão anterior. Para migração incompatível, restaurar também o backup consistente anterior, registrar o instante de recuperação e reconciliar gravações posteriores antes de reabrir o acesso. Repetir a comparação da etapa 3.
5. **Remover e reinstalar.** Usar a remoção padrão do pacote, conferir a retenção de configuração, licença e histórico, reinstalar e comparar. Executar o expurgo em cópia identificada, inventariar o que foi removido e conferir que bancos externos e outras instalações continuam acessíveis.
6. **Repetir a fabricação.** Gerar o mesmo perfil com uma segunda versão controlada, pela mesma definição. Comparar a lista de conteúdo, a origem da versão, o resultado da atualização e as diferenças esperadas de assinatura. Registrar em que operação houve intervenção manual e transformar correções recorrentes em passos versionados.


## 7. Gerar o manual e preparar o FAQ

Para cada ambiente, o responsável pelo pacote entrega o manifesto e as evidências à documentação. Um revisor segue o manual offline em uma instalação limpa em português, inglês e espanhol, até abrir o Runtime, consultar uma tendência e localizar a saúde e o consumo da licença. Registrar idioma, plataforma, versão, resultado e limitações observadas.

O responsável pelo produto reúne os pacotes homologados, imagens offline, manuais, notas de versão e tabela do FAQ. Cada linha do FAQ informa arquitetura, banco, recursos, capacidade medida, limites da licença e margem de memória do host. O responsável pelo portal recebe o conjunto e verifica download, tamanho e checksum do arquivo servido contra o manifesto aprovado.

**Procedimento editorial.** Atualizar os artigos e capturas no idioma correspondente. Na raiz do checkout de documentação, executar `python scripts/docs/export_system_help_manual.py`, revisar o diff dos capítulos em docs/manual e executar o mesmo comando com `--check`. Montar no kit offline os capítulos, índices e imagens referenciadas, respeitando a lista permitida. Abrir cada índice a partir da instalação sem acesso à internet e seguir os links para o procedimento da plataforma. Conferir instalação, ativação, operação, diagnóstico, backup e recuperação na interface da candidata. Repetir em pt-BR, en e es e registrar capítulo e imagem quebrados antes de refazer a exportação.

## 8. Conferir os arquivos finais e entregar ao portal

**Composição.** Para cada instalador, imagem e arquivo offline, o responsável pelo build gera uma lista dos arquivos efetivamente entregues e compara com a lista de conteúdo permitida. Incluir a Ajuda e o manual do usuário. Rejeitar material de coordenação, fixtures, relatórios de CI, credenciais de laboratório e arquivos de emissão de licenças. Fazer a inspeção também nas camadas da imagem final. Arquivar inventário, SBOM e licenças das dependências junto ao manifesto. [Política de conteúdo da #381](https://github.com/brunolrogerio-collab/EliteSCADA/issues/381)

**Recuperação do conjunto completo.** Na referência OCI, o Compose usa volumes distintos para o PostgreSQL, o material protegido e as chaves do host. Incluir os três no procedimento de recuperação da mesma instalação, protegendo as chaves em backup com acesso restrito. Com banco externo, coordenar a cópia do projeto e do Historian com o administrador do banco. Ensaiar em destino isolado a leitura do material protegido, a abertura da revisão ativa e a recuperação das amostras e da licença conforme a identidade autorizada. Uma troca de máquina segue o procedimento de reativação aplicável.

**Saúde observável.** O Compose de referência usa loopback por padrão e verifica a API em /health e a Web em /healthz. Conferir primeiro essas rotas e depois abrir o Runtime pelo endereço do operador, observar uma TAG atualizar, consultar tendência e testar login da Engenharia. Para instalações em rede, documentar a interface exposta, a porta e o destino TLS. Incluir vídeo apenas nos perfis que o anunciam.

**Registro por candidato.** Anexar à #408 uma linha por combinação de pacote, sistema e arquitetura com versão/SHA, digest ou checksum, equipamento real/VM/emulação, versão anterior usada na atualização, resultado de instalação offline/reboot/restauração e link das evidências. Marcar cada cenário como aprovado, falhou ou pendente. No FAQ publicar somente combinações aprovadas e informar as limitações encontradas. Para Edge e VPS, anexar ainda carga, pico de memória, janela de medição e latência p95. O proprietário recebe o resultado por plataforma e o chat do portal recebe os arquivos correspondentes ao manifesto aceito.

### Ficha de execução para anexar à #408

| Etapa e responsável | Entrada e ação | Saída para conferência | Se falhar |
|---|---|---|---|
| Produto / build | SHA, versão, perfil e dependências fixadas | Log de build, inventário e SBOM por arquitetura | Corrigir a entrada e identificar a nova rodada |
| Instalação / revisor | Máquina limpa e arquivo do manifesto | Hash, versão instalada, saúde e primeira TAG | Capturar logs, restaurar snapshot e repetir |
| Operação / revisor | Projeto ativo e amostras anteriores | Revisão, licença, histórico e alarmes antes/depois | Registrar perda, lacuna ou duplicata e a etapa exata |
| Capacidade / bancada | Carga e equipamento identificados | Pico simultâneo, p95, erros e margem do host | Reduzir a capacidade declarada ou ajustar o perfil e repetir |
| Documentação / revisor de idioma | Manual instalado offline | Idioma, capítulo, captura e percurso concluído | Corrigir o artigo ou o recurso ausente e reexportar |
| Distribuição / portal | Pacote aprovado e manifesto | Arquivo obtido pelo download com hash idêntico | Suspender o item divergente e corrigir o arquivo ou o cadastro |

Guardar a ficha com data, operador, versão do SO, hardware, versões de origem/destino, resultado e link de cada evidência. Vincular falhas à rodada que as reproduziu e conservar o resultado anterior ao registrar o reteste.
