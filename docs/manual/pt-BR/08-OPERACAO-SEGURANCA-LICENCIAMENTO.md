# Operação, Segurança e Licenciamento

## Runtime

Runtime executa a revisão Active sob autoridade do backend. A sessão recebe capabilities efetivas e, quando aplicável, um lease de Runtime.

Perda/expiração de autoridade interativa não transforma a sessão em autorizada por padrão.

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

No contrato operacional implementado:

- Engineering pode conter mais de 200 TAGs;
- Run em Demo é permitido para projetos com até 200 TAGs;
- cada Run Demo bem-sucedido inicia uma sessão contínua de até 300 minutos;
- expiração encerra o Runtime industrial de forma controlada e mantém Engineering;
- um novo Run explícito inicia uma nova sessão Demo.

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

Tiers documentados: 500, 1000, 1500, 3000, 5000 e Unlimited. Uma licença válida remove o timeout Demo e aplica o limite de TAGs correspondente.

## Runtime session quotas / Interactive / View Only

O status de licenciamento expõe entitlements de assentos View Only e Interactive quando presentes. A contagem e enforcement são de responsabilidade do servidor; uma conexão de transporte não deve ser confundida com uma sessão lógica licenciada.

## Redundancy entitlement

O status de licença também pode expor entitlement de HA Runtime. O comportamento operacional completo de redundância permanece bloqueado pelos workstreams #421/#423 e não é detalhado como concluído nesta fase.

## License Generator

O License Generator é uma ferramenta offline/controlada de autoridade de licenciamento. A chave privada de assinatura nunca deve ir para GitHub, CI normal, instalador de cliente ou Runtime.

Operadores finais do EliteSCADA usam request/install; somente pessoal autorizado de emissão deve operar o ambiente de assinatura.

**Ajuda relacionada:** `/help?topic=licensing.overview`.
