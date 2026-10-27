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

## License Generator

O License Generator é uma ferramenta offline/controlada de autoridade de licenciamento. A chave privada de assinatura nunca deve ir para GitHub, CI normal, instalador de cliente ou Runtime.

Operadores finais do EliteSCADA usam request/install; somente pessoal autorizado de emissão deve operar o ambiente de assinatura.

**Ajuda relacionada:** `/help?topic=licensing.overview`.
