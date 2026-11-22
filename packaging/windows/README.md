# Instalador de bancada do SCADIVO para Windows x64

Esta primeira etapa da [fábrica de versões](../../docs/WAVE16-RELEASE-ROADMAP.md) instala o conteúdo autocontido da API e a Web do mesmo commit. O assistente usa a marca do produto em PT-BR, inglês e espanhol. O pacote destina-se à bancada privada. O serviço Windows, a configuração do banco, o cliente desktop, a bandeja e a atualização de uma instalação com dados são etapas seguintes da #408.

## Gerar uma versão

Use uma máquina de build Windows x64 com Git, PowerShell 7.4 ou superior, o SDK selecionado por `global.json`, Node.js 24 com npm, Microsoft Edge e Inno Setup 6.7.3. Para o compilador, o SHA-256 do [instalador original 6.7.3](https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3) é `9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732`. A página inicial do assistente, o ícone multirresolução e o ícone de remoção derivam do SVG do produto. A compilação confere a marca com a do site na revisão da receita.

Na raiz do repositório, selecione o SHA completo do produto e uma pasta de saída nova. A receita pode estar em um commit diferente do produto. Ela lê nome, canal e versão de `Directory.Build.props`. Registre as alterações da receita no Git antes de gerar.

```powershell
$productSha = git rev-parse 'origin/main^{commit}'
$iscc = Read-Host 'Caminho completo do ISCC.exe'
$out = Join-Path $PWD "artifacts/windows-x64-$($productSha.Substring(0, 12))"
./packaging/windows/Build-Installer.ps1 -ProductCommit $productSha -CompilerPath $iscc -OutputDirectory $out
./packaging/windows/Test-Installer.ps1 -BuildDirectory $out
```

A montagem extrai os arquivos versionados para uma pasta temporária, confere as entradas, publica a API para `win-x64`, compila a Web com o lockfile e seleciona os componentes do instalador. A estação de build precisa das dependências em cache ou de acesso aos repositórios de pacotes. A instalação dos arquivos gerados é offline. Os avisos de terceiros disponíveis acompanham a montagem. A licença MPL do Pyodide vem da [revisão correspondente à dependência da Web](https://github.com/pyodide/pyodide/blob/314.0.6/LICENSE).

## Conferir a entrega

A pasta de saída contém o instalador privado sem assinatura, `manifest.json` com os commits do produto e da receita e o SHA-256 do executável, `files.json` com tamanho e SHA-256 de cada arquivo instalado e as imagens da marca usadas pelo assistente. Os arquivos de programa ficam em `SCADIVO/releases/<versão>` sob a pasta de instalação. O assistente padrão usa Program Files e requer elevação.

O ensaio local exige uma conta sem instalação anterior desse piloto e Docker com a imagem `timescale/timescaledb:2.29.2-pg18` disponível. Ele cria um banco exclusivo com senha aleatória e porta dinâmica em loopback. O contêiner é usado apenas no ensaio e removido ao final. Ele instala o próprio executável por usuário em uma pasta temporária, verifica os hashes, danifica uma cópia de teste do ícone, repete a instalação e confere o reparo. Em seguida abre uma cópia da API instalada em porta local dinâmica, com autenticação local e PostgreSQL/TimescaleDB descartável, e verifica a versão, a saúde e a entrega da página e do JavaScript da Web. A remoção deve retirar o registro e os binários e conservar o arquivo criado pelo ensaio. O resultado fica em `smoke.json` e os logs ficam no diretório temporário indicado ao final.

A homologação em Windows limpo, o início automático antes do login, o PostgreSQL/TimescaleDB, drivers com dependências nativas, os três idiomas nas diferentes escalas de tela, o manual offline completo e a recuperação de dados em atualização têm fichas próprias no roteiro. A entrega pública também depende da qualificação de proteção de código, revisão de dependências e assinatura.
