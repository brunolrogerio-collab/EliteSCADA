# SCADIVO para Windows x64

O pacote de avaliação instala a API e a interface Web da mesma revisão, PostgreSQL 18 com TimescaleDB e o monitor de saúde. O SCADIVO e o banco são serviços automáticos do Windows. A instalação inclui o runtime .NET da API, os recursos da interface e o guia de primeiros passos.

## Gerar o executável

Use Windows x64, Git, PowerShell 7.4 ou superior, o SDK selecionado por `global.json`, Node.js com npm, Microsoft Edge e Inno Setup 6.7.3. O SHA-256 do [instalador do compilador](https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3) é `9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732`. O assistente usa a marca oficial e oferece PT-BR, inglês e espanhol. A compilação confere o SVG do produto com a marca do site na revisão da receita.

Na raiz do repositório, baixe os arquivos com os nomes especificados no manifesto. A receita confere os hashes antes de incluir as dependências. Para o redistribuível Visual C++, conserve a versão exata no cache de build, pois o link da Microsoft acompanha versões novas.

```powershell
$cache = 'C:\Build\ScadivoDependencies'
New-Item -ItemType Directory -Force $cache | Out-Null
foreach ($dependency in (Get-Content packaging/windows/dependencies.json -Raw | ConvertFrom-Json)) {
    $file = Join-Path $cache $dependency.file
    if (!(Test-Path -LiteralPath $file)) {
        Invoke-WebRequest -Uri $dependency.url -OutFile $file
    }
    if ((Get-FileHash -LiteralPath $file).Hash -ne $dependency.sha256) {
        throw "Confira a versão de $($dependency.component)."
    }
}
$productSha = git rev-parse 'origin/main^{commit}'
$iscc = Read-Host 'Caminho completo do ISCC.exe'
$out = Join-Path $PWD "artifacts/windows-x64-$($productSha.Substring(0, 12))"
./packaging/windows/Build-Installer.ps1 -ProductCommit $productSha -CompilerPath $iscc -DependencyDirectory $cache -OutputDirectory $out
```

O manifesto identifica os commits do produto e da receita, os pacotes nativos, as imagens e o SHA-256 do instalador. A receita lê a versão de `Directory.Build.props` e monta o conteúdo a partir de um commit, sem importar dados do host de compilação.

## Instalar e testar

O assistente usa as portas locais 18080 para a Web, 15120 para a API e 15432 para o PostgreSQL. Na primeira instalação, o usuário pode escolher outras portas. O diretório de programas é `%ProgramFiles%\SCADIVO`.

O desinstalador oferece duas opções. **Manter os dados** preserva o banco, a configuração e a licença em `%ProgramData%\SCADIVO Windows` para reinstalação. **Apagar os dados locais** remove essa pasta após confirmação e permite reinstalar do zero. A desinstalação silenciosa preserva os dados. Cópias e backups em outros diretórios ficam disponíveis para restauração.

Execute o ensaio em Windows 11 x64 limpo, com PowerShell elevado. Ele usa portas de teste e cria o primeiro administrador com uma senha aleatória. Confere os arquivos instalados, o início automático registrado no Windows, a disponibilidade da API nos dois endereços, a interface Web, reinício dos serviços, reparo e remoção com retenção de dados.

```powershell
./packaging/windows/Test-NativeInstaller.ps1 -BuildDirectory $out
```

O resultado fica em `smoke-native.json`. Os logs de instalação, reparo e remoção ficam na pasta da compilação. Os logs dos serviços ficam em `%ProgramData%\SCADIVO Windows\logs`. A interface da bandeja abre a página de Licenciamento, onde as permissões da conta do SCADIVO controlam a visualização do consumo. A distribuição comercial tem qualificação de assinatura, atualização entre versões e suporte de drivers próprios no [roteiro da Wave 16](../../docs/WAVE16-RELEASE-ROADMAP.md).
