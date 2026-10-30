# Portas em uma instalação nativa

Estes comandos são destinados ao administrador do computador. Execute-os na pasta administrativa fornecida pela instalação, com o perfil protegido `native-network-profile.json`. O perfil identifica os serviços e arquivos efetivamente usados; não use nomes de serviço copiados de outra instalação. Os geradores de perfil Web/API existem neste candidato; sua ligação ao executor e o instalador completo ainda estão pendentes.

## Preparação pelo executor da instalação

Esta etapa pertence ao responsável pela distribuição, não ao usuário que preenche portas na interface. O executor deve identificar a configuração externa realmente carregada pelo serviço, fornecer os adaptadores na mesma pasta e proteger a pasta administrativa contra escrita por usuários não autorizados.

No Windows, `provision-native-network-profile.ps1` recebe `-ConfigurationPath`, `-ServiceName` e `-DestinationPath`. No Linux, `provision-native-network-profile.py` recebe `--configuration`, `--service` e `--destination`. O destino deve ficar junto da configuração. Ambos conferem a existência do serviço e os listeners Web/Api distintos; recusam sobrescrever perfil ou prosseguir com recuperação pendente. Não criam serviços nem alteram portas, e não copiam credenciais da configuração.

O executor ainda precisa registrar o caminho resultante em `InstallationNetwork:AdministrativeProfilePath`, declarar `InstallationNetwork:Profile` como `native-windows` ou `native-linux` e confirmar que o serviço carrega essa configuração. Para registrar esses dois valores na configuração declarada, use explicitamente `-Register` no Windows ou `--register` no Linux. O registro cria backup protegido e preserva as demais configurações e portas; sem essa opção, somente o perfil é criado. Nenhum serviço é reiniciado pelo gerador. Antes de recarregar o serviço, confira o arquivo e a identidade de acesso. Se houver interrupção durante o registro, preserve o perfil e restaure a configuração pelo backup protegido antes de repetir. Um nome de serviço existente não prova a associação com o arquivo. A conta que executa a API deve poder ler o perfil, sem poder alterá-lo indevidamente; a ACL deve ser ajustada pelo provisionamento conforme essa identidade real. A geração atual cobre Web/API; os campos administrativos de banco e mídia continuam dependentes do inventário do executor.

Uma instalação nova deve usar Web 18080, API 15120 e banco local gerenciado 15432 automaticamente. Os comandos abaixo alteram uma instalação existente; valores omitidos são preservados. Não alteram portas dos Drivers, firewall ou um banco remoto.

## Linux

Para incluir PostgreSQL local, o executor fornece um arquivo de registro JSON via `--database-registration` (Linux) ou `-DatabaseRegistrationPath` (Windows). Campos exatos: `topologyFile`, `configurationFile` (PostgreSQL), `psqlExecutable`, `serviceName`, `currentPort`, `username` e `databaseName`. Não inclua senha, connection string ou outros campos: o gerador os recusa. A autenticação SQL usa a conta/passfile protegido do host, conforme o adaptador existente. A topologia deve estar em LocalManaged sem operação pendente. O gerador confere arquivos, serviço e porta declarada; o adaptador confirma os valores efetivos por SQL antes de alterar. Existência de executável/serviço não comprova sua identidade ou associação com o arquivo: essa homologação continua responsabilidade do executor.

Para incluir mídia no perfil, o executor informa também `--media-configuration` e `--media-service` ao gerador Linux, ou `-MediaConfigurationPath` e `-MediaServiceName` no Windows. Os dois parâmetros são obrigatórios em conjunto. O gerador valida serviço distinto, arquivo existente e correspondência das portas controle/HLS com os endereços locais da API antes de criar o perfil. Não modifica MediaMTX, não copia credenciais e não substitui a conferência de que o serviço carrega aquele arquivo.

Python 3 e os módulos administrativos devem estar na pasta fornecida pela instalação. Primeiro confira o plano:

```sh
bash ./apply-native-network-profile.sh --profile ./native-network-profile.json --web-port 28080 --api-port 25120
bash ./apply-native-database-port.sh --profile ./native-network-profile.json --database-port 25432
bash ./apply-native-media-ports.sh --profile ./native-network-profile.json --control-port 19997 --hls-port 18888
```

Para executar o plano, repita somente o comando desejado com `--apply`. Haverá interrupção dos serviços envolvidos. Após mudar Web, reabra a interface usando a nova porta. Não execute mudanças de topologia do banco em paralelo.

## Windows

Use Windows PowerShell 5.1 e a pasta administrativa. O caminho do Python deve ser fornecido pela instalação para os adaptadores de banco/mídia; não depende de Visual Studio.

```powershell
.\apply-native-network-profile.ps1 -ProfilePath .\native-network-profile.json -WebPort 28080 -ApiPort 25120
.\apply-native-database-port.ps1 -ProfilePath .\native-network-profile.json -PythonExecutable $PythonDaInstalacao -DatabasePort 25432
.\apply-native-media-ports.ps1 -ProfilePath .\native-network-profile.json -PythonExecutable $PythonDaInstalacao -ControlPort 19997 -HlsPort 18888
```

Acrescente `-Apply` somente depois de conferir o plano. Backups são criados com acesso restrito ao administrador executor, SYSTEM e Administradores. No Linux, backups e registros de recuperação usam modo 0600.

## Após uma interrupção

Uma operação interrompida de Web/API, banco ou mídia mantém um registro junto da configuração. Preserve esse registro e os backups associados. Para tentar restaurar o estado anterior, use somente `--recover` (Linux) ou `-Recover` (Windows) no respectivo comando, com o mesmo perfil. Não combine recuperação com portas ou aplicação de novo plano.

Se a recuperação falhar, o registro permanece para diagnóstico. Não o apague para forçar outra troca. Os adaptadores também tentam restaurar automaticamente a configuração quando detectam uma falha durante a aplicação. A recuperação explícita atende a interrupções em que essa restauração não chegou a terminar.

## Conferência final

O componente `verify_native_readiness.py --origin http://127.0.0.1:PORTA_API` consulta o endpoint canônico `/api/installation/database-readiness`. Recebe somente uma credencial Bearer de sessão administrativa já existente pela variável temporária `SCADIVO_INSTALLATION_BEARER`; não cria login, não amplia permissões e não renova a sessão. O executor deve fornecê-la por seu caminho protegido, sem incluí-la no comando ou gravá-la em arquivo. Remova a variável ao terminar. Não extraia credenciais do navegador nem desative autenticação para usar o verificador; sem um caminho protegido de sessão, faça a conferência pela interface autenticada.

O componente aceita somente origem local, ignora proxies, não segue redirecionamentos e falha se a sessão for negada, o contrato da resposta estiver incorreto ou a prontidão não chegar dentro do prazo. Isso verifica banco/topologia pela API; não comprova Runtime, comandos ou reprodução de mídia. Sua ligação automática aos adaptadores ainda está pendente neste candidato.

Abra a interface no novo endereço e verifique o banco em **Instalação > Rede e serviços > Verificar banco**. Confira também o Runtime e a reprodução de mídia usados pela aplicação. Um serviço reiniciado, uma resposta `/health` ou o listener HLS aberto não comprovam todo esse funcionamento. Os adaptadores ainda não integram automaticamente essa conferência autenticada completa.
