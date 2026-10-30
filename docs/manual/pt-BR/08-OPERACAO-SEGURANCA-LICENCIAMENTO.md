# Operação, Segurança e Licenciamento

## Runtime

Runtime executa a revisão Active sob autoridade do backend. A sessão recebe capabilities efetivas e, quando aplicável, um lease de Runtime.

Perda/expiração de autoridade interativa não transforma a sessão em autorizada por padrão.

### Parar e iniciar a aplicação em operação

Na visão geral de Engineering, o cartão **4 — Active** mostra o estado real do Runtime. Com `EngineeringModify`, use **Parar Runtime** e confirme. A ação para os drivers da aplicação, a coleta de dados e os Scripts de servidor. A revisão continua Active; o serviço de Engineering/API, a licença instalada e o login do operador são preservados. Sessões Runtime e conexões com equipamentos podem ser interrompidas e precisam reconectar quando o Runtime voltar. Parar não despublica a revisão nem revoga ou remove a licença.

Depois de uma parada completa, **Iniciar Runtime** reativa a mesma revisão Active e revalida licença e autoridade. Isso não renova nem reinicia um prazo limitado de Demo. Se a interface indicar parada incompleta, revise os componentes afetados; não inicie até resolver. Com HA habilitado, controles locais não são oferecidos porque não parariam o cluster inteiro; use a manutenção coordenada do cluster.

As capturas abaixo mostram as duas ações na visão geral; foram feitas durante a validação local integrada.

![Runtime em execução: o cartão Active oferece Parar Runtime](../assets/runtime-active-overview.png)

![Runtime parado: o cartão Active oferece Iniciar Runtime](../assets/runtime-stopped-overview.png)

Uma renovação automática de licença aceita ocorre em segundo plano e, por si só, não para o Runtime. Uma licença recusada, revogada ou expirada segue as proteções de direito do produto.

**Ajuda relacionada:** `/help?topic=engineering.lifecycle`, `/help?topic=licensing.overview`, `/help?topic=ha.overview`.

## Interactive e View Only

Interactive e View Only representam classes de sessão. View Only reduz autoridade operacional; especialmente, não concede escrita/comando mesmo que o usuário tenha permissões que seriam utilizáveis numa sessão interativa.

A concessão de assentos/capacidade vem do entitlement de licença e do servidor; sockets ou controles visuais não são a unidade de autoridade.

## Commands

Commands representam intenção do operador e passam por validação/autorização server-side. Um botão renderizado não prova que a ação será aceita.

**Ajuda relacionada:** `/help?topic=bindings-commands.overview`.

## Diagnostics e communication quality

Use diagnóstico em camadas: backend, identidade/capabilities, Active, Data Source, Driver, conexão, binding, quality/timestamp e apresentação. Execute capabilities de Engineering do Driver somente quando declaradas pelo descriptor real.

**Ajuda relacionada:** `/help?topic=diagnostics.overview`.

## Authority, Users, Roles, Capabilities e Scopes

Authority administra identidades e assignments. Roles alimentam autorização; capabilities efetivas são calculadas/aplicadas no servidor.

Não trate nome de role como prova suficiente de privilégio. O produto deve resolver as capabilities efetivas.

## Engineering Lock

Engineering Lock protege conteúdo de desenvolvimento da aplicação e é independente de:

- senha da conta do usuário;
- senha do backup de Authority;
- Licensing.

Quando a aplicação está bloqueada, editores protegidos não devem ser expostos. Funções de administração permitidas continuam sujeitas à autorização normal do backend.

Alterações do Engineering Lock fazem parte de Working e precisam seguir Save/Publish/Activate para se tornarem estado Active durável.

### Procedimento: desbloquear a aplicação atual

**Pré-requisitos:** acesso permitido à administração restrita e segredo correto do Engineering Lock.

**Passos:**
1. Abra a superfície de proteção da aplicação.
2. Informe o segredo do Engineering Lock.
3. Execute Unlock.
4. Confirme que Engineering volta a ser disponibilizado de acordo com as capabilities da sessão.

**Resultado esperado:** somente a aplicação atual é desbloqueada; a operação não autentica usuário nem altera licença.

**Erro esperado:** segredo incorreto mantém Engineering bloqueado.

## Conceitos de licença

Quando não existe licença instalada, o estado é Demo. Uma licença válida é assinada assimetricamente e vinculada à máquina.

A licença controla entitlement comercial; não substitui Authority, Engineering Lock, lifecycle ou validação de package.

### Demo/no-license

<!-- HELP-REVIEW-301: Rever fluxo online e apresentação de consumo quando essas etapas forem entregues. A duração ilimitada do Demo já está descrita abaixo. -->
**Próximas atualizações:** a ativação online por conta e os indicadores de consumo ainda estão em desenvolvimento. Use as opções disponíveis na sua versão; este texto não declara o fluxo online concluído.

Sem uma licença instalada, você pode usar o Runtime em Demo sem limite de tempo:

- Engineering pode conter mais de 200 TAGs, mas o Run em Demo aceita até 200 definições de TAG;
- o Demo não tem contagem regressiva: não é necessário parar e iniciar o Run periodicamente;
- o Runtime pode voltar após um reinício sem exigir o horário de início de uma antiga sessão Demo;
- as cotas são de 2 clientes Interactive e 2 View Only, compartilhadas pelos clientes Runtime; HA e Inteligência exigem licença própria;
- a aba Licenciamento mostra **Duração do Demo: Sem limite de tempo**. Permissões de usuário e demais requisitos de operação continuam sendo verificados.

Acima de 200 TAGs, o produto recusa o Run completo e explica o limite; não escolhe silenciosamente apenas parte das TAGs. O projeto de Engineering e seus dados permanecem disponíveis.

Uma licença instalada mas inválida/tampered/wrong-hardware bloqueia Run; ela não deve ser tratada silenciosamente como ausência de licença.

## Machine binding e Request

A instalação gera um Machine Request Code versionado a partir de identidade estável da máquina. O fluxo normal não precisa expor identificadores brutos de hardware ao operador.

### Procedimento: obter o request

1. Abra Licensing.
2. Consulte o status atual.
3. Copie o Machine Request Code.
4. Envie apenas o request à autoridade de licenciamento.

**Resultado esperado:** o código pode ser usado pela ferramenta controlada de emissão sem distribuir a chave privada.

## Install / Replace / Remove

A instalação de licença valida assinatura, key-id, binding de hardware e validade antes de substituir o estado instalado.

Para substituir, instale a nova licença validada pelo fluxo normal. Uma tentativa inválida não deve substituir uma licença válida existente.

Remove exclui a licença instalada; ativações futuras voltam às regras de Demo, desde que não exista estado inválido externo bloqueando Run.

## TAG tiers

Tiers documentados: 500, 1000, 1500, 3000, 5000 e Unlimited. Uma licença válida aplica o limite de TAGs e os recursos assinados correspondentes à faixa adquirida.

## Runtime session quotas / Interactive / View Only

O status de licenciamento expõe entitlements de assentos View Only e Interactive quando presentes. A contagem e enforcement são de responsabilidade do servidor; uma conexão de transporte não deve ser confundida com uma sessão lógica licenciada.

Na página **Licenciamento**, cada limite aparece acompanhado de **Em uso**. Os TAGs correspondem às definições da aplicação em execução; Interactive e View Only contam sessões lógicas ativas, compartilhadas entre os clientes Runtime. Reconectar a mesma sessão não cria outro assento. Use **Atualizar** para consultar novamente. Se não houver Runtime ativo ou a leitura estiver temporariamente indisponível, o sistema mostra **Indisponível**, em vez de inventar uma contagem zero. Uma licença antiga pode não especificar o limite de sessões.

## Redundancy entitlement

O status de licença pode expor entitlement de HA Runtime. O entitlement habilita a superfície; não substitui configuração correta, Authority, transporte entre os nós, fencing ou verificação da operação.

## Alta disponibilidade: configurar e acompanhar

A tela **Alta disponibilidade** distingue Standalone de HA e apresenta configuração desejada, configuração em execução, prontidão, estado dos nós, peer, Active efetivo, saúde, freshness, referência de fencing e histórico das operações. HA usa dois servidores redundantes; a topologia do banco de dados é uma decisão separada e deve ser conferida em cada instalação.

### Preparar a configuração

1. Confirme uma licença com HA Runtime, dois servidores acessíveis e uma conta com `HighAvailabilityObserve`; para salvar ou operar também é necessário `HighAvailabilityTransfer`.
2. Em Engineering > Alta disponibilidade, selecione HA para preparar a topologia. Standalone continua em execução enquanto a preparação está pendente.
3. Informe o endpoint principal de cada servidor. Mantenha o endpoint automático salvo se a rede realmente exigir um endereço externo diferente. Configure a autenticação do peer; nunca registre ou compartilhe o segredo em documentação/chamados.
4. Revise o nó preferencial. Se a topologia usar proteção, habilite-a e informe a referência compartilhada de fencing. Failover automático depende dessa proteção.
5. Resolva os requisitos exibidos em **Prontidão para HA** e selecione **Salvar configuração desejada**.
6. Se a tela indicar restart necessário, a configuração desejada ainda não está em execução. Planeje o restart conforme a operação da instalação; depois, atualize a tela e confirme o estado dos dois nós, do peer, do Active efetivo e da proteção.

### Transferências e recuperação

Use **Switchover** para uma transição planejada quando este servidor for o Active efetivo; o backend ainda valida elegibilidade e pode rejeitar a solicitação. Use **Failback** apenas quando o backend permitir retornar ao nó preferencial. **Recovery** e **Forçar ressincronização** são ações de recuperação para as condições indicadas na própria tela, não atalhos para forçar um nó a ficar pronto. Leia a confirmação e confira o resultado/histórico da operação.

Antes de depender de failover automático, ensaie o cenário na topologia real e confirme as consequências para Runtime, banco de dados e comandos duplicados. Estado stale, degradado ou ambíguo exige investigação de conectividade, freshness e fencing; não force promoção para apagar o aviso.

**Ajuda relacionada:** `/help?topic=ha.overview`, `/help?topic=licensing.overview`, `/help?topic=security.users-roles-capabilities`, `/help?topic=diagnostics.overview`.

## Rede e portas do pacote Docker estável

Em **Engineering > Instalação > Rede e serviços**, um usuário com `SystemAdmin` pode preparar portas para a distribuição Docker estável. O formulário mostra a configuração declarada pelo Compose no container atual, mas não consegue confirmar sozinho se o computador host realmente publicou cada porta.

### Portas que este perfil controla

| Serviço | No host, padrão | Dentro do container | Observação |
| --- | ---: | ---: | --- |
| Web | `127.0.0.1:18080` | `80/TCP` | Navegação do Runtime e Engineering |
| API direta | `127.0.0.1:15080` | `5080/TCP` | A Web encaminha `/api` para esta API |
| PostgreSQL | `127.0.0.1:15432` | `5432/TCP` | A publicação no host permanece somente loopback |
| Media control | Não publicada | `9997/TCP` | Uso interno da rede Docker |
| HLS | Não publicada | `8888/TCP` | Uso interno da rede Docker |
| Listener de Driver (opcional) | Definido no perfil | Porta configurada no Data Source | TCP/UDP; não publicado por padrão |

As portas internas dos serviços principais deste pacote são fixas. A mídia fica isolada na rede Docker: a API usa o controle MediaMTX em `9997/TCP` e o Runtime entrega HLS em `8888/TCP` pelo relay da API com tíquetes temporários. Não publique essas duas portas diretamente no host. O mapeamento opcional de Driver publica um listener já configurado; ele não troca a porta interna do Data Source.

As portas de equipamentos são ajustadas em cada Data Source. Em um driver cliente, a porta pertence ao serviço no dispositivo remoto — por exemplo, `502/TCP` para Modbus TCP ou `102/TCP` para S7 — e não precisa ser publicada no host SCADIVO. Para um listener do Driver que roda dentro da API, use a seção opcional **Listeners de Drivers** para mapear protocolo, endereço no host, porta publicada e porta interna. A porta interna deve ser igual à porta de escuta definida no Data Source; o mapeamento publica o listener no host, mas não altera o Driver. No Docker estável, configure o **Modbus TCP Server** em `1502/TCP` (ou outra porta interna a partir de 1024) e escutando na rede do container (`0.0.0.0`), pois a API roda sem privilégios e não pode abrir portas internas abaixo de 1024. Se clientes esperam `502/TCP`, publique `502` no host apontando para `1502` no container, se permitido pelo host. O **BACnet/IP** usa `47808/UDP` por padrão; a publicação pode atender tráfego unicast, mas broadcast/discovery não é garantido na rede bridge do Docker. Consulte os guias de [conexões e protocolos](21-CONEXOES-E-PROTOCOLOS.md), [BACnet/IP](22-HOME-BUILDING-CONEXOES.md) e [Modbus TCP Server](25-TELECONTROLE-E-MODBUS-SERVER.md).

Portas de bancos remotos e endereços de peers HA pertencem às próprias integrações. Esta tela não abre firewall, muda serviços externos nem altera a porta configurada no Data Source.

### Trocar portas no mesmo sistema

1. Abra **Engineering > Instalação > Rede e serviços** e confira o ID, os binds e as portas atuais.
2. Defina portas inteiras de 1 a 65535, diferentes entre Web, API e banco. Web e API podem ficar em `127.0.0.1` (somente host local) ou escutar em `0.0.0.0` (todas as interfaces). O banco permanece em `127.0.0.1`.
3. Confira o novo endereço e o aviso de reinício. Se escolher, por exemplo, Web `28080`, a navegação passará para `http://localhost:28080`.
4. Se precisar publicar um listener, adicione-o na seção opcional **Listeners de Drivers**. Selecione TCP ou UDP, o endereço do host, a porta que clientes usarão e a porta interna que já está configurada no Data Source. Use `127.0.0.1` para limitar o acesso a este computador. Para Modbus TCP Server, confira que o bind interno aceita conexões pela rede do container; para BACnet, considere este mapeamento adequado a unicast, não uma solução para broadcast.
5. Baixe o script PowerShell (Windows/Docker Desktop) ou Bash (Linux), coloque-o junto de `docker-compose.stable.yml` e `.env`, revise-o e execute o comando mostrado no terminal do host. O script verifica portas TCP/UDP livres, publicações Docker existentes e `docker compose config` antes da troca. Depois guarda cópias de `.env` e do override gerenciado, aplica o perfil e verifica a saúde da Web, API e banco. Não remove volumes. Se uma verificação falhar, tenta restaurar a configuração anterior e os serviços.
6. Abra o endereço Web novo. A aba anterior pode desconectar enquanto o container é recriado.

Não cole segredos no comando: ele contém somente ID, binds, protocolos e números de porta. O `.env` e `docker-compose.network-profile.override.yml` permanecem no host; suas cópias de segurança e `network-profile-audit.log` também ficam na pasta da instalação.

### Criar uma segunda instalação

Use outra pasta de instalação, gere um `.env` independente com senhas/chaves/identidade próprias e defina nele o novo `SCADIVO_INSTALLATION_ID` antes de executar o comando. Não copie o `.env` da primeira instalação. No modo de segunda instalação, o script exige o ID já definido no novo `.env` e recusa um ID que já tenha containers ou volumes. O ID separa projeto Compose e volumes: a nova instalação começa com banco e dados próprios; ela não duplica os dados da primeira.

Se a Web/API for vinculada a `0.0.0.0`, ela poderá responder por todas as interfaces de rede. Para acesso remoto, configure TLS/reverse proxy e a política de firewall explicitamente fora do SCADIVO. Se houver HA, atualize seus peers e confira sua saúde após trocar URLs; isso não é feito automaticamente.

### Recuperar o acesso

Se a aba não abrir, use o novo endereço apresentado no perfil. Se o script registrar rollback, mantenha as cópias e confira os logs/saúde no host. Para restaurar manualmente uma configuração com listeners de Driver, restaure tanto o `.env.network-backup-*` quanto o `.network-profile-override-backup-*.yml` correspondentes; depois execute `docker compose -f docker-compose.stable.yml -f docker-compose.network-profile.override.yml up -d` na pasta desta instalação. Após aplicar o perfil, sempre inclua o arquivo override nos comandos manuais de Compose. Não use `down -v`: essa opção apaga dados persistentes.

**Ajuda relacionada:** `/help?topic=installation.network-ports`, [instalação Docker estável](../../LOCAL-DOCKER-STABLE.md).

## License Generator

O License Generator é uma ferramenta offline/controlada de autoridade de licenciamento. A chave privada de assinatura nunca deve ir para GitHub, CI normal, instalador de cliente ou Runtime.

Operadores finais do EliteSCADA usam request/install; somente pessoal autorizado de emissão deve operar o ambiente de assinatura.

**Ajuda relacionada:** `/help?topic=licensing.overview`.
