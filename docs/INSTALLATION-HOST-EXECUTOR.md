# Executor administrativo de rede por instalação (DEV-780)

**Estado: candidato Linux nativo opt-in, pendente de qualificação real do host.** Não substitui o instalador Windows/OCI. Referências: [#780](https://github.com/brunolrogerio-collab/EliteSCADA/issues/780), [ADR-025](ADR-025-NETWORK-PORT-CONFIGURATION.md), [adaptadores nativos](../web/scada-web/public/installation/NATIVE-PORTS.md).

## Autoridade e limites de confiança

O **administrador do host**, não o navegador, provisiona um executor separado por instalação no mesmo host Linux dos serviços nativos gerenciados. Ele usa socket Unix, proprietário/permissões do sistema e token específico da instalação. O processo Web/API **não** recebe root, systemd, execução de comandos nem socket Docker. Um principal autenticado com `SystemAdmin` pode pedir apenas operações tipadas e versionadas pelo canal local.

O registro vincula o executor a um perfil `scadivo-native-network-v1` já instalado. Scripts fixos `apply-native-network-profile.py`, `native_database_port.py` e `native_media_ports.py` ficam ao lado do `network-host-executor.py`, em diretório protegido; a requisição HTTP não determina scripts, paths, serviços, comandos, instância ou argumentos arbitrários. Os adaptadores nativos continuam responsáveis por preflight, lock compartilhado, backup/journal, restart, saúde e rollback. O executor verifica revisão da configuração com HMAC, acompanha a operação e só confirma após os adaptadores retornarem e as portas configuradas serem relidas.

**Cobertura inicial do executor:** Linux nativo com systemd, Web e API Kestrel independentes, PostgreSQL LocalManaged e MediaMTX locais quando registrados. **Windows SCM/pipe ACL e Docker host Compose ainda não têm este executor**. Use os scripts externos aprovados para esses perfis. Sem provisionamento, a API retorna HTTP 503 e não tenta aplicar nada.

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

- **Windows nativo:** requer executor próprio com named pipe local e ACL, mais validação real do SCM. Até lá usar PowerShell administrativo do host (`-Apply` e `-Recover`), com perfil protegido. A API Unix atual rejeita esse perfil.
- **Docker:** os helpers Bash/PowerShell permanecem externos ao Web/API e devem rodar no host, em pasta/perfil Compose registrado. Nenhum executor host Docker foi instalado nem socket criado nesta entrega. Web interno `80` publicado inicialmente em `18080`; API interno `5080` publicado em `15120`; PostgreSQL interno `5432` publicado em `15432`; MediaMTX permanece privado. A UI não afirma a publicação do host sem evidência.
- **Sem alteração** de portas de Drivers/datasources, protocolos industriais, descoberta multicast, firewall, TLS ou HA. A validação do CI é sintética/isolada; aceite da aplicação real exige provisionamento e verificação do coordenador no host, incluindo mídia/Runtime.
