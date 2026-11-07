# Executor administrativo de rede por instalação (DEV-780)

**Estado: candidatos opt-in Linux nativo, Windows nativo e Docker em host Linux, pendentes de CI e homologação do instalador/host.** O executor não se instala automaticamente, não substitui a distribuição Windows/OCI e não comprova por si só todo o Runtime. Referências: [#780](https://github.com/brunolrogerio-collab/EliteSCADA/issues/780), [ADR-025](ADR-025-NETWORK-PORT-CONFIGURATION.md), [adaptadores nativos](../web/scada-web/public/installation/NATIVE-PORTS.md).

## Autoridade e limites de confiança

O **administrador do host**, não o navegador, provisiona um executor separado por instalação no mesmo host Linux dos serviços nativos gerenciados. Ele usa socket Unix, proprietário/permissões do sistema e token específico da instalação. O processo Web/API **não** recebe root, systemd, execução de comandos nem socket Docker. Um principal autenticado com `SystemAdmin` pode pedir apenas operações tipadas e versionadas pelo canal local.

O registro vincula o executor a um perfil `scadivo-native-network-v1` já instalado. Scripts fixos `apply-native-network-profile.py`, `native_database_port.py` e `native_media_ports.py` ficam ao lado do `network-host-executor.py`, em diretório protegido; a requisição HTTP não determina scripts, paths, serviços, comandos, instância ou argumentos arbitrários. Os adaptadores nativos continuam responsáveis por preflight, lock compartilhado, backup/journal, restart, saúde e rollback. O executor verifica revisão da configuração com HMAC, acompanha a operação e só confirma após os adaptadores retornarem e as portas configuradas serem relidas.

**Cobertura proposta nesta continuação:** Linux nativo com systemd; Windows nativo com executor .NET separado, Named Pipe e ACL restrita ao SID da API; e Docker estável com executor no host **Linux**, socket Unix e Compose associado ao diretório/ID/digest registrados. Somente módulos registrados são expostos. **Docker Desktop com host Windows não dispõe do executor Docker nesta versão**; use scripts PowerShell do host. A ausência de provisionamento/token/IPC retorna HTTP 503, nunca aplicação presumida. Cobertura de CI e homologação real ainda precisam ser verificadas.

## Provisionamento Linux, somente pelo administrador

1. Provisionar e conferir o perfil nativo conforme `NATIVE-PORTS.md`, apontando para os serviços/arquivos efetivamente carregados. Garantir diretório administrativo e scripts sob propriedade root/executor, sem permissão de escrita para grupo/outros. O instalador fornece as portas padrão automaticamente: Web `18080`, API `15120`, PostgreSQL local `15432`, controle de mídia `9997` e HLS `8888`. Instalações existentes mantêm suas portas.
2. Criar token aleatório exclusivo (mínimo 32 bytes imprevisíveis) em arquivo 0600, apenas legível pelo executor. Injetar o mesmo valor na API pelo mecanismo protegido de secrets/env como `InstallationNetwork__HostExecutorToken`. Não o publicar em JavaScript, HTML, comandos, repositório, backups compartilhados ou logs.
3. Criar diretório de estado exclusivo do executor (0700) e diretório pai do socket, proprietário do executor, com modo 0750 e grupo restrito da API. Usar `socketMode: "0660"` com `socketGroup` igual ao GID real desse grupo. A API pode conectar ao socket, mas não pode executar ações gerais no host. O modo 0600 é admitido apenas quando as identidades compartilham autorização mínima comprovada, sem outorgar privilégio administrativo geral à API.
4. Criar arquivo de registro JSON protegido 0600 com valores reais. Exemplo **ilustrativo, nunca usar paths/GID sem conferir a instalação**:

```json
{
  "schema": "scadivo-network-host-executor-v1",
  "instanceId": "scadivo-instance-a",
  "profile": "native-linux",
  "socketPath": "/run/scadivo-net-a/network.sock",
  "stateDirectory": "/var/lib/scadivo-instance-a/network",
  "nativeProfilePath": "/etc/scadivo-instance-a/native-network-profile.json",
  "tokenFile": "/etc/scadivo-instance-a/network-executor.token",
  "socketMode": "0660",
  "socketGroup": 1234
}
```

5. Registrar serviço independente no host para `python3 network-host-executor.py --registration /caminho/registro-protegido.json`, com conta e ACLs de mínimos privilégios, boot, status e recuperação provisionados pelo instalador. O executor recusa lock ocupado ou socket preexistente; investigue o processo anterior antes de remover arquivos.
6. Configurar `InstallationNetwork__HostExecutorSocket` na API com caminho local acessível somente ao grupo autorizado, e seu token protegido. Não montar `/var/run/docker.sock`. Um serviço sem token ou socket válido sempre falha fechado.

O provisionamento deve assegurar o funcionamento dos adaptadores nativos sobre a identidade de serviços registrada. Cópias de JSON e scripts **não** equivalem a um serviço instalado, associado à configuração efetiva ou homologado.

## Provisionamento Windows nativo (administrador do host)

O binário independente fica em `tools/Scadivo.InstallationExecutor.Windows` (publicar `net10.0-windows` fora da pasta Web/API). O administrador registra o perfil `scadivo-native-network-v1` com `platform: "windows"` e serviços SCM realmente instalados, além dos scripts administrativos PowerShell fixos, protegidos contra escrita de usuários comuns. O executor exige configuração Kestrel com listeners independentes Web/API. Para banco/mídia, exige a indicação adicional do Python provisionado na instalação, e só oferece módulos registrados/observados.

Exemplo de registro **ilustrativo** com arquivo JSON e ACL sem herança, acessível apenas ao proprietário administrativo, SYSTEM e Administradores:

```json
{
  "schema": "scadivo-network-host-executor-v1",
  "profile": "native-windows",
  "instanceId": "scadivo-instance-a",
  "pipeName": "scadivo-net-instance-a",
  "apiSid": "S-1-5-80-000000000-000000000-000000000-000000000-000000000",
  "stateDirectory": "C:\\ProgramData\\SCADIVO\\instance-a\\network-state",
  "nativeProfilePath": "C:\\ProgramData\\SCADIVO\\instance-a\\native-network-profile.json",
  "tokenFile": "C:\\ProgramData\\SCADIVO\\instance-a\\network-executor.token",
  "adapterDirectory": "C:\\ProgramData\\SCADIVO\\instance-a\\adapters",
  "pythonExecutable": "C:\\Program Files\\SCADIVO\\Python\\python.exe"
}
```

O SID acima é **fictício** e deve ser substituído pelo SID específico da conta/serviço da API; o executor recusa identidades genéricas como Everyone/Users/Administrators. Configure a API com `InstallationNetwork__HostExecutorPipe` e `InstallationNetwork__HostExecutorToken` (token exclusivo, secreto, imprevisível e com pelo menos 32 bytes). O Named Pipe é restrito por DACL à identidade executora, SYSTEM, administradores e SID específico da API. O serviço do executor é provisionado no host, com conta dotada somente dos direitos necessários para os SCM/arquivos **da instalação registrada**; não tornar a própria API administradora. Diretórios, arquivos de registro, token e configuração devem permanecer sob ACLs Windows protegidas; herança/permissões amplas causam falha fechada.

O administrador pode usar `dotnet Scadivo.InstallationExecutor.Windows.dll --registration <registro> --status` ou `--recover --confirm-recovery` localmente quando a Web cair. Isso não cria nem registra o serviço SCM; o instalador/administrador deve fazê-lo e homologar os acessos de cada serviço.

## Provisionamento Docker no host Linux (não no contêiner)

Para o pacote Docker estável, execute `docker-host-executor.py` **no host Linux**, em uma conta administrativa do host autorizada a operar Docker. Não dê acesso ao Docker socket, shell ou privilégios do host à API/Web. O executor disponibiliza **somente** seu próprio socket Unix de instalação à API, com token específico, modos `0600` ou `0660` com grupo administrativo da API e diretórios sob controle do host. No Compose, o instalador deve adicionar à API um bind mount **somente do socket dedicado** e disponibilizar o token por mecanismo de segredo, sem publicar o token no navegador nem no contêiner Web. O API usa `InstallationNetwork__HostExecutorSocket` e o token (ou `InstallationNetwork__HostExecutorTokenFile` em mount read-only).

O registro administrativo de execução usa `schema: "scadivo-network-host-executor-v1"`, `profile: "stable-docker-linux"`, `instanceId`, `socketPath`, `stateDirectory`, `dockerProfilePath`, `tokenFile`, `socketMode` e `socketGroup`. O `dockerProfilePath` aponta para arquivo privado:

```json
{
  "schema": "scadivo-docker-host-network-v1",
  "instanceId": "scadivo-instance-a",
  "installationDirectory": "/opt/scadivo/instance-a",
  "composeSha256": "DIGEST_SHA256_REAL_DE_64_HEX_DO_COMPOSE",
  "dockerExecutable": "/usr/bin/docker"
}
```

Para manter o IPC ativo após `docker compose up -d`, use também o override versionado
`docker/stable/compose-host-executor.override.yml`. O registro do adaptador deve incluir
`composeOverrideSha256` com o SHA-256 real desse arquivo, além do digest do Compose base.
O executor sempre invoca o **mesmo par** de arquivos na prévia, na aplicação e na recuperação,
impedindo que a troca de portas remova o volume do socket e a credencial da API.

Antes de subir o Compose, o instalador do host deve criar um diretório dedicado ao socket
(sem escrita para outros, grupo autorizado da API), um diretório separado de segredos e um
arquivo `token` 0640 lido somente pelo usuário executor e grupo da API. Registrar as variáveis
`SCADIVO_NETWORK_SOCKET_DIRECTORY`,
`SCADIVO_NETWORK_API_SECRET_DIRECTORY` e
`SCADIVO_NETWORK_API_GROUP_GID` no `.env` de **uma única instalação**.
O arquivo de token do executor no host permanece 0600; a cópia de leitura da API pode
ter 0640 e **não** deve ser a mesma cópia escrita/alterada pela API.
O socket exclusivo da instalação usa modo 0660 e o GID informado. A API opera como
usuário sem privilégio no contêiner, recebe acesso apenas ao socket e token por bind
read-only, e nunca recebe `/var/run/docker.sock`.
Para ativar o perfil, o administrador usa `docker compose -f docker-compose.stable.yml
-f docker/stable/compose-host-executor.override.yml up -d` na instalação registrada.
Não aplique esse override sem provisionar os diretórios e revisar seus ACLs/permissões.

O digest acima é **marcador**, não um valor executável. O administrador deve usar o SHA-256 real do `docker-compose.stable.yml` revisado. O executor exige `.env` com acesso 0600, diretório de instalação protegido, ID de projeto idêntico ao registro, imagem/serviços limitados aos quatro serviços reconhecidos pelo Compose, correspondência dos rótulos de ownership dos containers e inexistência de conflitos de portas publicadas. Não permite troca de ID, nomes de container, volumes, diretório, Compose ou bind a partir da API. O preflight gera configuração Compose candidata temporária e chama `docker compose config` **antes** de qualquer `up`; a aplicação mantém backup `.env` no estado protegido do executor e journal durável para recuperação. O health check consulta Web/API no host, PostgreSQL e listeners privados de mídia na rede da instalação. Isso **não comprova** reprodução real de vídeo. Uma segunda instalação mantém seu próprio socket/token/diretório, volumes e ID exclusivos.

Recuperação local:
```bash
python3 docker-host-executor.py --registration /caminho/registro-protegido.json --status
python3 docker-host-executor.py --registration /caminho/registro-protegido.json --recover --confirm-recovery
```

Um host Docker Desktop **Windows** continua usando o script administrativo PowerShell externo, pois este executor Docker é exclusivamente Linux. Executar o executor em um contêiner junto da API, ou montar `/var/run/docker.sock` nele, não é suportado.

## Fluxo de aplicação e recuperação

No menu **Engineering > Instalação > Rede e serviços > Aplicação administrativa da instalação**, a interface:
- Lê portas configuradas e o último estado durável. Isso **não** prova publicação externa ou reprodução de vídeo.
- Oferece Web/API, banco ou mídia somente quando o executor confirma que estão registrados. A prévia valida campos e conflitos, faz preflight nativo sem reiniciar, emite ID de plano com validade de dez minutos e prende o plano à revisão HMAC da configuração.
- Após confirmação explícita do novo endereço/interrupção, solicita aplicação e recebe ID de operação. `applying` **não é sucesso**. O executor registra início, chama o adaptador e só muda para `applied` após concluir a saúde do adaptador e reler as portas. Reinícios podem derrubar a sessão atual; reabra no endereço novo.
- Em falha/interrupção fica `recovery_required`, sem liberar nova alteração. O journal/backup nativo permanece a autoridade de recuperação. Estado persistente e trilha `network-audit.jsonl` (timestamp, módulo, ID e evento, sem segredos) sobrevivem ao reinício do processo/API.

Portas aceitas: Web/API `{webPort,apiPort}`, banco `{databasePort}` e mídia `{mediaControlPort,mediaPlaybackPort}`; todos inteiros 1..65535. Nenhum nome de serviço, path, comando arbitrário, firewall, Driver, protocolo ou peer HA pode ser enviado. Confirme Runtime, PostgreSQL autenticado e **reprodução de vídeo real** separadamente: um simples `/health` ou listener TCP não prova essas funções.

Quando a URL antiga não responder, use o executor **diretamente no host**, com permissão administrativa e registro protegido:

```bash
python3 network-host-executor.py --registration /caminho/registro-protegido.json --status
python3 network-host-executor.py --registration /caminho/registro-protegido.json --recover --confirm-recovery
```

A recuperação exige pendência registrada. Se executor/socket estiverem indisponíveis, inspecione o serviço e execute o adaptador nativo correspondente com `--recover` e perfil protegido, conforme `NATIVE-PORTS.md`. Não apague journal/backup para liberar aplicação. Se o adaptador já houver revertido e removido o journal, mas o executor indicar pendência, a reconciliação requer conferência administrativa de saúde/estado; não se declara sucesso automaticamente.

## Limites conhecidos / próximos contratos

- **Windows nativo:** candidato executor .NET separado com Named Pipe DACL/SID, serviço SCM e recuperação local. Requer CI do host Windows, provisionamento e homologação do serviço real; sem registro usa PowerShell externo (`-Apply`/`-Recover`).
- **Docker:** candidato de executor host **Linux** ligado ao Compose registrado. Continua opt-in e não é automaticamente instalado; Docker Desktop em Windows continua com PowerShell externo. Os helpers Bash/PowerShell ainda são alternativa administrativa no host. Web interno `80` publicado inicialmente em `18080`; API interno `5080` publicado em `15120`; PostgreSQL interno `5432` publicado em `15432`; MediaMTX permanece privado. A UI não afirma a publicação do host sem evidência.
- **Sem alteração** de portas de Drivers/datasources, protocolos industriais, descoberta multicast, firewall, TLS ou HA. A validação do CI é sintética/isolada; aceite da aplicação real exige provisionamento e verificação do coordenador no host, incluindo mídia/Runtime.
